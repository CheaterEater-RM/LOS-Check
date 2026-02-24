using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LOSOverlay
{
    public class Gizmo_LOSMode : Command_Action
    {
        private static readonly Dictionary<int, LOSMode> _modeByThing = new Dictionary<int, LOSMode>();
        private static readonly Dictionary<int, OverlayDirection> _dirByThing = new Dictionary<int, OverlayDirection>();
        private static readonly Dictionary<int, Dictionary<IntVec3, CellLOSResult>> _cachedResults =
            new Dictionary<int, Dictionary<IntVec3, CellLOSResult>>();

        private static int _purgeCounter;
        private const int PURGE_INTERVAL = 500; // every N selection changes

        private readonly Thing _parent;

        public Gizmo_LOSMode(Thing parent)
        {
            _parent = parent;
            var mode = GetMode(parent);
            defaultLabel = "LOS: " + mode.ToString();
            defaultDesc = ModeDescription(mode, parent);
            icon = TexCommand.Attack;
            Order = -95f;
            action = () => { CycleMode(parent); RefreshOverlay(); };
        }

        // ── Right-click menu: offensive / defensive ───────────────────────
        public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions
        {
            get
            {
                var dir = GetDirection(_parent);

                // FloatMenuOption(string label, Action action, Texture2D iconTex, Color iconColor, ...)
                // Passing null action marks it as Disabled (greyed out) automatically.
                Action offAction = dir == OverlayDirection.Offensive ? null : (Action)(() =>
                {
                    SetDirection(_parent, OverlayDirection.Offensive);
                    if (GetMode(_parent) != LOSMode.Off) RefreshOverlay();
                });
                yield return new FloatMenuOption(
                    "Offensive (cover targets have FROM you)",
                    offAction,
                    (Texture2D)TexCommand.FireAtWill,
                    Color.white);

                Action defAction = dir == OverlayDirection.Defensive ? null : (Action)(() =>
                {
                    SetDirection(_parent, OverlayDirection.Defensive);
                    if (GetMode(_parent) != LOSMode.Off) RefreshOverlay();
                });
                yield return new FloatMenuOption(
                    "Defensive (cover YOU have from each threat)",
                    defAction,
                    (Texture2D)TexCommand.DesirePower,
                    Color.white);
            }
        }

        // ── Static helpers ────────────────────────────────────────────────
        public static LOSMode GetMode(Thing thing)
        {
            if (thing == null) return LOSMode.Off;
            LOSMode m;
            return _modeByThing.TryGetValue(thing.thingIDNumber, out m) ? m : LOSMode.Off;
        }

        public static void SetMode(Thing thing, LOSMode mode)
        {
            if (thing != null) _modeByThing[thing.thingIDNumber] = mode;
        }

        public static OverlayDirection GetDirection(Thing thing)
        {
            if (thing == null) return OverlayDirection.Offensive;
            OverlayDirection d;
            return _dirByThing.TryGetValue(thing.thingIDNumber, out d) ? d : OverlayDirection.Offensive;
        }

        public static void SetDirection(Thing thing, OverlayDirection dir)
        {
            if (thing != null) _dirByThing[thing.thingIDNumber] = dir;
        }

        private static bool CanLean(Thing thing) => !(thing is Building_Turret);

        private static void CycleMode(Thing thing)
        {
            LOSMode current = GetMode(thing);
            LOSMode next;
            switch (current)
            {
                case LOSMode.Off:    next = LOSMode.Static;                              break;
                case LOSMode.Static: next = CanLean(thing) ? LOSMode.Leaning : LOSMode.Off; break;
                default:             next = LOSMode.Off;                                 break;
            }
            SetMode(thing, next);
        }

        private static string ModeDescription(LOSMode mode, Thing thing = null)
        {
            switch (mode)
            {
                case LOSMode.Off:
                    return "LOS overlay off. Left-click to enable static mode.\nRight-click to change view direction.";
                case LOSMode.Static:
                    return CanLean(thing)
                        ? "Static LOS from this position.\nGreen = clear, Yellow = partial cover, Red = heavy cover.\nLeft-click for leaning mode. Right-click for view direction."
                        : "Static LOS from this position.\nGreen = clear, Yellow = partial cover, Red = heavy cover.\nLeft-click to turn off. Right-click for view direction.";
                case LOSMode.Leaning:
                    return "LOS including lean-around-corner positions.\nGreen = clear, Yellow = partial cover, Red = heavy cover.\nLeft-click to turn off. Right-click for view direction.";
                default:
                    return "";
            }
        }

        // ── Overlay refresh ───────────────────────────────────────────────
        public void RefreshOverlay()
        {
            var mode = GetMode(_parent);
            if (mode == LOSMode.Off || _parent.Map == null) { OverlayRenderer.ClearOverlay(); return; }
            var results = GetOrCreateCache(_parent);
            var dir = GetDirection(_parent);
            LOSCalculator.ComputeLOS(_parent.Position, _parent.Map, mode, GetRange(), dir, results);
            OverlayRenderer.SetOverlayData(results, _parent.Map);
        }

        private int GetRange()
        {
            var dir = GetDirection(_parent);
            int defaultRange = dir == OverlayDirection.Defensive
                ? LOSOverlay_Mod.Settings.DefaultDefensiveRange
                : LOSOverlay_Mod.Settings.DefaultRange;

            // For offensive view, prefer the weapon's actual range if available.
            if (dir == OverlayDirection.Offensive)
            {
                if (_parent is Pawn pawn)
                {
                    var primary = pawn.equipment?.Primary;
                    if (primary?.def?.Verbs != null && primary.def.Verbs.Count > 0)
                    {
                        float verbRange = primary.def.Verbs[0].range;
                        if (verbRange > 0f) return Mathf.CeilToInt(verbRange);
                    }
                }
                if (_parent is Building_Turret turret)
                {
                    try
                    {
                        var verb = turret.AttackVerb;
                        if (verb?.verbProps != null && verb.verbProps.range > 0f)
                            return Mathf.CeilToInt(verb.verbProps.range);
                    }
                    catch { }
                }
            }

            return defaultRange;
        }

        private static Dictionary<IntVec3, CellLOSResult> GetOrCreateCache(Thing thing)
        {
            Dictionary<IntVec3, CellLOSResult> cache;
            if (!_cachedResults.TryGetValue(thing.thingIDNumber, out cache))
            {
                cache = new Dictionary<IntVec3, CellLOSResult>();
                _cachedResults[thing.thingIDNumber] = cache;
            }
            return cache;
        }

        // ── Selection-change callbacks ────────────────────────────────────
        public static void OnSelectionChanged(Thing selected)
        {
            // Periodically purge cached data for things no longer in LOS mode
            if (++_purgeCounter >= PURGE_INTERVAL)
            {
                _purgeCounter = 0;
                PurgeInactiveEntries();
            }

            if (selected == null) { OverlayRenderer.ClearOverlay(); return; }
            var mode = GetMode(selected);
            if (mode != LOSMode.Off) { new Gizmo_LOSMode(selected).RefreshOverlay(); return; }

            bool autoShow =
                (selected is Pawn p && p.Drafted && LOSOverlay_Mod.Settings.ShowOnPawnSelect) ||
                (selected is Building_Turret && LOSOverlay_Mod.Settings.ShowOnTurretSelect);
            if (autoShow)
            {
                SetMode(selected, LOSMode.Static);
                new Gizmo_LOSMode(selected).RefreshOverlay();
            }
            else OverlayRenderer.ClearOverlay();
        }

        public static void OnPositionChanged(Thing selected)
        {
            if (selected == null) return;
            var mode = GetMode(selected);
            if (mode != LOSMode.Off)
                new Gizmo_LOSMode(selected).RefreshOverlay();
        }

        public static void ClearAllCaches()
        {
            _cachedResults.Clear(); _modeByThing.Clear(); _dirByThing.Clear();
            OverlayRenderer.ClearOverlay();
        }

        /// <summary>
        /// Recompute and redisplay whichever overlay is currently active:
        /// combined view (all observer markers) or the single selected thing.
        /// Called when the hypothetical map state changes (designation placed/removed).
        /// </summary>
        public static void RefreshActiveOverlay(Map map, Thing currentSelected)
        {
            var hypo = map?.GetComponent<HypotheticalMapState>();
            if (hypo == null) return;

            if (hypo.CombinedViewActive)
            {
                var observers = new List<IntVec3>(hypo.ObserverPositions);
                if (observers.Count > 0)
                {
                    var results = new Dictionary<IntVec3, CellLOSResult>();
                    LOSCalculator.ComputeCombinedLOS(observers, map, LOSMode.Static,
                        LOSOverlay_Mod.Settings.DefaultRange, OverlayDirection.Offensive, results);
                    OverlayRenderer.SetOverlayData(results, map);
                }
                else
                {
                    OverlayRenderer.ClearOverlay();
                }
                return;
            }

            if (currentSelected != null)
                OnPositionChanged(currentSelected);
        }

        /// <summary>
        /// Remove cached data for things whose LOS mode is Off.
        /// Called periodically to prevent unbounded dictionary growth from
        /// destroyed things or things the player is no longer viewing.
        /// </summary>
        private static void PurgeInactiveEntries()
        {
            var stale = new List<int>();
            foreach (var kvp in _modeByThing)
            {
                if (kvp.Value == LOSMode.Off)
                    stale.Add(kvp.Key);
            }
            foreach (int id in stale)
            {
                _modeByThing.Remove(id);
                _dirByThing.Remove(id);
                _cachedResults.Remove(id);
            }
        }

        // ── GUI ───────────────────────────────────────────────────────────
        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            var mode = GetMode(_parent);
            defaultLabel = "LOS: " + mode.ToString();
            defaultDesc = ModeDescription(mode, _parent);
            return base.GizmoOnGUI(topLeft, maxWidth, parms);
        }
    }
}
