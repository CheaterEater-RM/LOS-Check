using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LOSOverlay
{
    public class Gizmo_LOSMode : Command_Action
    {
        private static readonly Dictionary<int, LOSMode> _modeByThing = new Dictionary<int, LOSMode>();
        private static readonly Dictionary<int, Dictionary<IntVec3, CellLOSResult>> _cachedResults =
            new Dictionary<int, Dictionary<IntVec3, CellLOSResult>>();

        private readonly Thing _parent;

        public Gizmo_LOSMode(Thing parent)
        {
            _parent = parent;
            var mode = GetMode(parent);
            defaultLabel = "LOS: " + mode.ToString();
            defaultDesc = ModeDescription(mode);
            icon = TexCommand.Attack;
            Order = -95f;
            action = () => { CycleMode(parent); RefreshOverlay(); };
        }

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

        private static void CycleMode(Thing thing)
        {
            LOSMode current = GetMode(thing);
            LOSMode next;
            switch (current)
            {
                case LOSMode.Off: next = LOSMode.Static; break;
                case LOSMode.Static: next = LOSMode.Leaning; break;
                default: next = LOSMode.Off; break;
            }
            SetMode(thing, next);
        }

        private static string ModeDescription(LOSMode mode)
        {
            switch (mode)
            {
                case LOSMode.Off:
                    return "LOS overlay off. Click to enable static mode.";
                case LOSMode.Static:
                    return "Static LOS from this position.\nGreen = clear, Yellow = partial cover, Red = heavy cover.\nClick for leaning mode.";
                case LOSMode.Leaning:
                    return "LOS including lean-around-corner positions.\nGreen = clear, Yellow = partial cover, Red = heavy cover.\nClick to turn off.";
                default:
                    return "";
            }
        }

        public void RefreshOverlay()
        {
            var mode = GetMode(_parent);
            if (mode == LOSMode.Off || _parent.Map == null) { OverlayRenderer.ClearOverlay(); return; }
            var results = GetOrCreateCache(_parent);
            LOSCalculator.ComputeLOS(_parent.Position, _parent.Map, mode, GetRange(), results);
            OverlayRenderer.SetOverlayData(results, _parent.Map);
        }

        private int GetRange()
        {
            if (_parent is Pawn pawn)
            {
                var primary = pawn.equipment != null ? pawn.equipment.Primary : null;
                if (primary != null && primary.def.Verbs != null && primary.def.Verbs.Count > 0)
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
                    if (verb != null && verb.verbProps != null && verb.verbProps.range > 0f)
                        return Mathf.CeilToInt(verb.verbProps.range);
                }
                catch { }
            }
            return LOSOverlay_Mod.Settings.DefaultRange;
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

        public static void OnSelectionChanged(Thing selected)
        {
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

        public static void ClearAllCaches()
        {
            _cachedResults.Clear(); _modeByThing.Clear(); OverlayRenderer.ClearOverlay();
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            var mode = GetMode(_parent);
            defaultLabel = "LOS: " + mode.ToString();
            defaultDesc = ModeDescription(mode);
            return base.GizmoOnGUI(topLeft, maxWidth, parms);
        }
    }
}