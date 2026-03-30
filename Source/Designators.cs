using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LOSOverlay
{
    // =========================================================================
    // Wall / Cover / Open — designation-based, supports drag-to-place
    // =========================================================================

    [StaticConstructorOnStartup]
    internal static class LOSTex
    {
        public static readonly Texture2D Wall       = ContentFinder<Texture2D>.Get("UI/Designators/LOSWall");
        public static readonly Texture2D Cover      = ContentFinder<Texture2D>.Get("UI/Designators/LOSCover");
        public static readonly Texture2D Open       = ContentFinder<Texture2D>.Get("UI/Designators/LOSOpen");
        public static readonly Texture2D Observer   = ContentFinder<Texture2D>.Get("UI/Designators/LOSObserver");
        /// <summary>Optional distinct icon; falls back to the Cover icon if not provided.</summary>
        public static readonly Texture2D CoverMap   =
            ContentFinder<Texture2D>.Get("UI/Designators/LOSCoverMap", reportFailure: false) ?? Cover;
        /// <summary>Show/hide toggle — reuses the vanilla zone-visibility icon.</summary>
        public static readonly Texture2D ToggleVis  = ContentFinder<Texture2D>.Get("UI/Buttons/ShowZones");
        /// <summary>Cover overlay toggle icon for bottom-right corner.</summary>
        public static readonly Texture2D CoverOverlay = ContentFinder<Texture2D>.Get("UI/Designators/ToggleCoverOverlay", reportFailure: false) ?? CoverMap;

        // --- Gizmo LOS mode icons (6 combinations: Off/Static/Leaning x Offensive/Defensive) ---
        public static readonly Texture2D LOSMode_Off_Offensive = ContentFinder<Texture2D>.Get("UI/Designators/LOS_Off_Offensive", reportFailure: false);
        public static readonly Texture2D LOSMode_Off_Defensive = ContentFinder<Texture2D>.Get("UI/Designators/LOS_Off_Defensive", reportFailure: false);
        public static readonly Texture2D LOSMode_Static_Offensive = ContentFinder<Texture2D>.Get("UI/Designators/LOS_Static_Offensive", reportFailure: false);
        public static readonly Texture2D LOSMode_Static_Defensive = ContentFinder<Texture2D>.Get("UI/Designators/LOS_Static_Defensive", reportFailure: false);
        public static readonly Texture2D LOSMode_Leaning_Offensive = ContentFinder<Texture2D>.Get("UI/Designators/LOS_Leaning_Offensive", reportFailure: false);
        public static readonly Texture2D LOSMode_Leaning_Defensive = ContentFinder<Texture2D>.Get("UI/Designators/LOS_Leaning_Defensive", reportFailure: false);
        /// <summary>Right-click menu icon for offensive direction option.</summary>
        public static readonly Texture2D Offensive = ContentFinder<Texture2D>.Get("UI/Designators/LOS_Offensive", reportFailure: false);
        /// <summary>Right-click menu icon for defensive direction option.</summary>
        public static readonly Texture2D Defensive = ContentFinder<Texture2D>.Get("UI/Designators/LOS_Defensive", reportFailure: false);

        // --- Menu designator icons ---
        public static readonly Texture2D TogglePlanVisibility = ContentFinder<Texture2D>.Get("UI/Designators/LOS_TogglePlanVisibility", reportFailure: false);
        public static readonly Texture2D CombinedLOSView = ContentFinder<Texture2D>.Get("UI/Designators/LOS_CombinedLOSView", reportFailure: false);
        public static readonly Texture2D RemoveLOSPlan = ContentFinder<Texture2D>.Get("UI/Designators/LOS_RemoveLOSPlan", reportFailure: false);
        public static readonly Texture2D ClearAllLOS = ContentFinder<Texture2D>.Get("UI/Designators/LOS_ClearAllLOS", reportFailure: false);
    }

    public abstract class Designator_LOSPlanDesignation : Designator
    {
        protected abstract DesignationDef TargetDesignationDef { get; }

        public Designator_LOSPlanDesignation()
        {
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            soundSucceeded = SoundDefOf.Designate_PlanAdd;
            useMouseIcon = true;
        }

        // Hook into the vanilla Plans draw-style system so dragging works.
        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.Plans;

        // Show dimension numbers while dragging.
        public override bool DragDrawMeasurements => true;

        // ── Cell acceptance ───────────────────────────────────────────────
        // Only require in-bounds. No fog check — these are planning tools.
        public override AcceptanceReport CanDesignateCell(IntVec3 loc)
        {
            if (!loc.InBounds(Find.CurrentMap)) return false;
            // Already has this designation → skip silently (no message).
            if (Find.CurrentMap.designationManager.DesignationAt(loc, TargetDesignationDef) != null)
                return AcceptanceReport.WasRejected;
            // Observer marker present → protected, can't be overwritten by a designation.
            var things = Find.CurrentMap.thingGrid.ThingsListAt(loc);
            for (int i = 0; i < things.Count; i++)
                if (things[i] is PlanningMarker) return AcceptanceReport.WasRejected;
            return true;
        }

        // ── Placement ─────────────────────────────────────────────────────
        public override void DesignateSingleCell(IntVec3 loc)
        {
            var map = Find.CurrentMap;
            RemoveExistingLOSDesignations(map, loc);
            map.designationManager.AddDesignation(new Designation(loc, TargetDesignationDef));
            map.GetComponent<HypotheticalMapState>().RebuildFromDesignations();
        }

        // Override multi-cell so we can skip the tutor-system fog check that
        // vanilla DesignateMultiCell enforces, and place on any in-bounds cell.
        public override void DesignateMultiCell(IEnumerable<IntVec3> cells)
        {
            bool any = false;
            foreach (var loc in cells)
            {
                if (CanDesignateCell(loc).Accepted)
                {
                    DesignateSingleCell(loc);
                    any = true;
                }
            }
            Finalize(any);
        }

        protected void RemoveExistingLOSDesignations(Map map, IntVec3 loc)
        {
            var toRemove = new List<Designation>();
            foreach (var des in map.designationManager.AllDesignationsAt(loc))
            {
                if (des.def == LOSDesignationDefOf.LOSOverlay_PlanWall ||
                    des.def == LOSDesignationDefOf.LOSOverlay_PlanCover ||
                    des.def == LOSDesignationDefOf.LOSOverlay_PlanOpen)
                    toRemove.Add(des);
            }
            foreach (var des in toRemove) map.designationManager.RemoveDesignation(des);
        }

        public override void SelectedUpdate() { GenDraw.DrawNoBuildEdgeLines(); }
    }

    // ─────────────────────────────────────────────────────────────────────────

    public class Designator_PlanWall : Designator_LOSPlanDesignation
    {
        protected override DesignationDef TargetDesignationDef => LOSDesignationDefOf.LOSOverlay_PlanWall;

        public Designator_PlanWall()
        {
            defaultLabel = "LOSDesignator_PlanWall_Label".Translate();
            defaultDesc = "LOSDesignator_PlanWall_Desc".Translate();
            icon = LOSTex.Wall;
        }

    }

    public class Designator_PlanCover : Designator_LOSPlanDesignation
    {
        protected override DesignationDef TargetDesignationDef => LOSDesignationDefOf.LOSOverlay_PlanCover;

        public Designator_PlanCover()
        {
            defaultLabel = "LOSDesignator_PlanCover_Label".Translate();
            defaultDesc = "LOSDesignator_PlanCover_Desc".Translate();
            icon = LOSTex.Cover;
        }
    }

    public class Designator_PlanOpen : Designator_LOSPlanDesignation
    {
        protected override DesignationDef TargetDesignationDef => LOSDesignationDefOf.LOSOverlay_PlanOpen;

        public Designator_PlanOpen()
        {
            defaultLabel = "LOSDesignator_PlanOpen_Label".Translate();
            defaultDesc = "LOSDesignator_PlanOpen_Desc".Translate();
            icon = LOSTex.Open;
        }
    }

    // =========================================================================
    // Observer — spawned Thing for selectability + gizmos
    // =========================================================================

    public class Designator_PlaceObserver : Designator
    {
        public Designator_PlaceObserver()
        {
            defaultLabel = "LOSDesignator_PlaceObserver_Label".Translate();
            defaultDesc = "LOSDesignator_PlaceObserver_Desc".Translate();
            icon = LOSTex.Observer;
            soundSucceeded = SoundDefOf.Designate_PlanAdd;
            useMouseIcon = true;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 loc)
        {
            // Allow placement in fog and on walls — this is a planning tool.
            if (!loc.InBounds(Find.CurrentMap)) return false;
            var things = loc.GetThingList(Find.CurrentMap);
            for (int i = 0; i < things.Count; i++)
                if (things[i] is PlanningMarker) return AcceptanceReport.WasRejected; // silent
            return true;
        }

        public override void DesignateSingleCell(IntVec3 loc)
        {
            var map = Find.CurrentMap;
            // Clear any LOS designations underneath before spawning the observer.
            var toRemove = new List<Designation>();
            foreach (var des in map.designationManager.AllDesignationsAt(loc))
                if (des.def == LOSDesignationDefOf.LOSOverlay_PlanWall ||
                    des.def == LOSDesignationDefOf.LOSOverlay_PlanCover ||
                    des.def == LOSDesignationDefOf.LOSOverlay_PlanOpen)
                    toRemove.Add(des);
            foreach (var des in toRemove) map.designationManager.RemoveDesignation(des);

            var def = DefDatabase<ThingDef>.GetNamed("LOSOverlay_ObserverMarker", errorOnFail: false);
            if (def == null) { Log.Error("[LOS Overlay] ThingDef LOSOverlay_ObserverMarker not found."); return; }
            var marker = (PlanningMarker)ThingMaker.MakeThing(def);
            GenSpawn.Spawn(marker, loc, map, Rot4.North, WipeMode.Vanish, respawningAfterLoad: true);
        }

        public override void SelectedUpdate() { GenDraw.DrawNoBuildEdgeLines(); }
    }

    // =========================================================================
    // Eraser — drag to remove all LOS planning markers
    // =========================================================================

    public class Designator_RemoveLOSPlanning : Designator
    {
        public Designator_RemoveLOSPlanning()
        {
            defaultLabel = "LOSDesignator_RemoveLOSPlanning_Label".Translate();
            defaultDesc = "LOSDesignator_RemoveLOSPlanning_Desc".Translate();
            icon = LOSTex.RemoveLOSPlan ?? TexCommand.ClearPrioritizedWork;
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            soundSucceeded = SoundDefOf.Designate_PlanRemove;
            useMouseIcon = true;
        }

        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.RemovePlans;
        public override bool DragDrawMeasurements => true;

        public override AcceptanceReport CanDesignateCell(IntVec3 loc)
        {
            if (!loc.InBounds(Find.CurrentMap)) return false;
            foreach (var des in Find.CurrentMap.designationManager.AllDesignationsAt(loc))
            {
                if (des.def == LOSDesignationDefOf.LOSOverlay_PlanWall ||
                    des.def == LOSDesignationDefOf.LOSOverlay_PlanCover ||
                    des.def == LOSDesignationDefOf.LOSOverlay_PlanOpen)
                    return true;
            }
            var things = loc.GetThingList(Find.CurrentMap);
            for (int i = 0; i < things.Count; i++)
                if (things[i] is PlanningMarker) return true;
            return AcceptanceReport.WasRejected;
        }

        public override void DesignateSingleCell(IntVec3 loc)
        {
            var map = Find.CurrentMap;
            var toRemove = new List<Designation>();
            foreach (var des in map.designationManager.AllDesignationsAt(loc))
            {
                if (des.def == LOSDesignationDefOf.LOSOverlay_PlanWall ||
                    des.def == LOSDesignationDefOf.LOSOverlay_PlanCover ||
                    des.def == LOSDesignationDefOf.LOSOverlay_PlanOpen)
                    toRemove.Add(des);
            }
            foreach (var des in toRemove) map.designationManager.RemoveDesignation(des);

            var things = new List<Thing>();
            foreach (var t in loc.GetThingList(map))
                if (t is PlanningMarker) things.Add(t);
            foreach (var t in things) if (!t.Destroyed) t.Destroy();

            map.GetComponent<HypotheticalMapState>().RebuildFromDesignations();
        }

        public override void DesignateMultiCell(IEnumerable<IntVec3> cells)
        {
            bool any = false;
            foreach (var loc in cells)
            {
                if (CanDesignateCell(loc).Accepted)
                {
                    DesignateSingleCell(loc);
                    any = true;
                }
            }
            Finalize(any);
        }
    }

    // =========================================================================
    // Utility: clear all, combined view
    // =========================================================================

    public class Designator_ClearAllPlanning : Designator
    {
        public Designator_ClearAllPlanning()
        {
            defaultLabel = "LOSDesignator_ClearAllPlanning_Label".Translate();
            defaultDesc = "LOSDesignator_ClearAllPlanning_Desc".Translate();
            icon = LOSTex.ClearAllLOS ?? TexCommand.ClearPrioritizedWork;
            soundSucceeded = SoundDefOf.Designate_PlanRemove;
            useMouseIcon = false;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 loc) { return false; }

        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);
            var map = Find.CurrentMap; if (map == null) return;
            var markers = new List<Thing>();
            foreach (var t in map.listerThings.AllThings) if (t is PlanningMarker) markers.Add(t);
            foreach (var m in markers) if (!m.Destroyed) m.Destroy();
            map.GetComponent<HypotheticalMapState>().ClearAll();
            OverlayRenderer.ClearOverlay(); Gizmo_LOSMode.ClearAllCaches();
            Messages.Message("LOSOverlay_Message_AllCleared".Translate(), MessageTypeDefOf.NeutralEvent, false);
        }
    }

    public class Designator_CombinedView : Designator
    {
        public Designator_CombinedView()
        {
            defaultLabel = "LOSDesignator_CombinedView_Label".Translate();
            defaultDesc = "LOSDesignator_CombinedView_Desc".Translate();
            icon = LOSTex.CombinedLOSView ?? TexCommand.Attack;
            soundSucceeded = SoundDefOf.Click;
            useMouseIcon = false;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 loc) { return false; }

        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);
            var map = Find.CurrentMap; if (map == null) return;
            var hypo = map.GetComponent<HypotheticalMapState>(); if (hypo == null) return;
            hypo.CombinedViewActive = !hypo.CombinedViewActive;
            if (hypo.CombinedViewActive)
            {
                var observers = new List<IntVec3>(hypo.ObserverPositions);
                if (observers.Count == 0)
                {
                    Messages.Message("LOSOverlay_Message_NoObservers".Translate(), MessageTypeDefOf.RejectInput, false);
                    hypo.CombinedViewActive = false; return;
                }
                var results = new Dictionary<IntVec3, CellLOSResult>();
                LOSCalculator.ComputeCombinedLOS(observers, map, LOSMode.Static,
                    LOSOverlay_Mod.Settings.DefaultRange, OverlayDirection.Offensive, results);
                OverlayRenderer.SetOverlayData(results, map);
                Messages.Message("LOSOverlay_Message_CombinedLOS".Translate(observers.Count), MessageTypeDefOf.NeutralEvent, false);
            }
            else OverlayRenderer.ClearOverlay();
        }
    }

    // =========================================================================
    // Toggle planning visibility — hide without deleting
    // =========================================================================

    /// <summary>
    /// Hides or reveals all LOS planning visuals (designation icons + observer markers)
    /// without destroying them. The underlying data and LOS calculations are untouched.
    /// </summary>
    public class Designator_TogglePlanningVisibility : Designator
    {
        public Designator_TogglePlanningVisibility()
        {
            defaultLabel   = "LOSDesignator_TogglePlanningVisibility_Label".Translate();
            defaultDesc    = "LOSDesignator_TogglePlanningVisibility_Desc".Translate();
            icon           = LOSTex.TogglePlanVisibility ?? LOSTex.ToggleVis;
            soundSucceeded = SoundDefOf.Click;
            useMouseIcon   = false;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 loc) => false;

        public override void ProcessInput(Event ev)
        {
            // Do NOT call base.ProcessInput — we are a toggle, not a cell-placement tool.
            var map = Find.CurrentMap;
            if (map == null) return;
            var hypo = map.GetComponent<HypotheticalMapState>();
            if (hypo == null) return;

            hypo.PlanningHidden = !hypo.PlanningHidden;

            Messages.Message(
                hypo.PlanningHidden
                    ? "LOS planning markers hidden."
                    : "LOS planning markers visible.",
                MessageTypeDefOf.NeutralEvent, historical: false);
        }
    }

    // =========================================================================
    // Terrain Cover Map — persistent overlay showing raw terrain cover values
    // =========================================================================


    /// <summary>
    /// Toggles a persistent map-wide overlay that colours every non-fogged cell
    /// by the inherent cover value of the terrain/structure sitting on it.
    /// No shooter position or line-of-sight maths are involved; this is purely
    /// "what cover does this cell's terrain offer a defender standing here?"
    ///
    /// The overlay stays active even when the designator is no longer selected,
    /// and is toggled off by clicking the button a second time.
    /// </summary>
    public class Designator_ToggleCoverMap : Designator
    {
        // Instance field — each designator instance owns its own buffer, and it
        // carries no stale data between maps because ComputeCoverMap (and the
        // explicit Clear below) always reset it before filling.
        private readonly Dictionary<IntVec3, CellLOSResult> _cache =
            new Dictionary<IntVec3, CellLOSResult>();

        public Designator_ToggleCoverMap()
        {
            defaultLabel = "LOSDesignator_ToggleCoverMap_Label".Translate();
            defaultDesc  = "LOSDesignator_ToggleCoverMap_Desc".Translate();
            icon         = LOSTex.CoverOverlay;
            soundSucceeded = SoundDefOf.Click;
            useMouseIcon = false;
        }

        // Never enters cell-placement mode.
        public override AcceptanceReport CanDesignateCell(IntVec3 loc) { return false; }
        public override void DesignateSingleCell(IntVec3 loc) { }

        public override void ProcessInput(Event ev)
        {
            // Do NOT call base.ProcessInput — that would put the UI into
            // "awaiting cell click" mode, which we don't want for a toggle.
            if (OverlayRenderer.IsCoverMapActive)
            {
                OverlayRenderer.ClearCoverMap();
            }
            else
            {
                var map = Find.CurrentMap;
                if (map == null) return;
                _cache.Clear(); // ComputeCoverMap also clears, but be explicit.
                LOSCalculator.ComputeCoverMap(map, _cache);
                OverlayRenderer.SetCoverMapData(_cache, map);
            }
        }
    }
}
