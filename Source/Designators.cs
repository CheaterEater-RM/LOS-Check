using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LOSOverlay
{
    // =========================================================================
    // Wall / Cover / Open — designation-based, supports drag-to-place
    // =========================================================================

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

        public override bool DragDrawMeasurements => true;

        public override AcceptanceReport CanDesignateCell(IntVec3 loc)
        {
            if (!loc.InBounds(Find.CurrentMap)) return false;
            var existing = Find.CurrentMap.designationManager.DesignationAt(loc, TargetDesignationDef);
            if (existing != null) return false;
            return true;
        }

        public override void DesignateSingleCell(IntVec3 loc)
        {
            var map = Find.CurrentMap;
            RemoveExistingLOSDesignations(map, loc);
            map.designationManager.AddDesignation(new Designation(loc, TargetDesignationDef));
            map.GetComponent<HypotheticalMapState>().RebuildFromDesignations();
        }

        private void RemoveExistingLOSDesignations(Map map, IntVec3 loc)
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

    public class Designator_PlanWall : Designator_LOSPlanDesignation
    {
        protected override DesignationDef TargetDesignationDef
        {
            get { return LOSDesignationDefOf.LOSOverlay_PlanWall; }
        }
        public Designator_PlanWall()
        {
            defaultLabel = "Plan Wall";
            defaultDesc = "Place hypothetical walls that block LOS.\nDrag to place lines or rectangles.";
            icon = TexCommand.ForbidOn;
        }
        public override AcceptanceReport CanDesignateCell(IntVec3 loc)
        {
            var baseResult = base.CanDesignateCell(loc);
            if (!baseResult.Accepted) return baseResult;
            var edifice = loc.GetEdifice(Find.CurrentMap);
            if (edifice != null && LOSOverlay_Mod.CoverProvider.BlocksLOS(edifice))
                return "Already a wall here.";
            return true;
        }
    }

    public class Designator_PlanCover : Designator_LOSPlanDesignation
    {
        protected override DesignationDef TargetDesignationDef
        {
            get { return LOSDesignationDefOf.LOSOverlay_PlanCover; }
        }
        public Designator_PlanCover()
        {
            defaultLabel = "Plan Cover";
            defaultDesc = "Place hypothetical cover (sandbag-equivalent).\nDrag to place lines or rectangles.";
            icon = TexCommand.DesirePower;
        }
    }

    public class Designator_PlanOpen : Designator_LOSPlanDesignation
    {
        protected override DesignationDef TargetDesignationDef
        {
            get { return LOSDesignationDefOf.LOSOverlay_PlanOpen; }
        }
        public Designator_PlanOpen()
        {
            defaultLabel = "Plan Opening";
            defaultDesc = "Mark existing walls as open for LOS calculations.\nDrag to place lines.";
            icon = TexCommand.ClearPrioritizedWork;
        }
        public override AcceptanceReport CanDesignateCell(IntVec3 loc)
        {
            var baseResult = base.CanDesignateCell(loc);
            if (!baseResult.Accepted) return baseResult;
            var edifice = loc.GetEdifice(Find.CurrentMap);
            if (edifice == null || !LOSOverlay_Mod.CoverProvider.BlocksLOS(edifice))
                return "No wall or obstacle here to open.";
            return true;
        }
    }

    // =========================================================================
    // Observer — spawned Thing for selectability + gizmos
    // =========================================================================

    public class Designator_PlaceObserver : Designator
    {
        public Designator_PlaceObserver()
        {
            defaultLabel = "LOS Observer";
            defaultDesc = "Place an observer point. Select it to view LOS overlay.";
            icon = TexCommand.Attack;
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            soundSucceeded = SoundDefOf.Designate_PlanAdd;
            useMouseIcon = true;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 loc)
        {
            if (!loc.InBounds(Find.CurrentMap)) return false;
            var things = loc.GetThingList(Find.CurrentMap);
            for (int i = 0; i < things.Count; i++)
                if (things[i] is PlanningMarker) return "Already has an observer here.";
            return true;
        }

        public override void DesignateSingleCell(IntVec3 loc)
        {
            var def = DefDatabase<ThingDef>.GetNamed("LOSOverlay_ObserverMarker", errorOnFail: false);
            if (def == null) { Log.Error("[LOS Overlay] ThingDef LOSOverlay_ObserverMarker not found."); return; }
            var marker = (PlanningMarker)ThingMaker.MakeThing(def);
            GenSpawn.Spawn(marker, loc, Find.CurrentMap);
        }

        public override void SelectedUpdate() { GenDraw.DrawNoBuildEdgeLines(); }
    }

    // =========================================================================
    // Utility: eraser, clear all, combined view
    // =========================================================================

    public class Designator_RemoveLOSPlanning : Designator
    {
        public Designator_RemoveLOSPlanning()
        {
            defaultLabel = "Remove LOS Plan";
            defaultDesc = "Click or drag to remove LOS planning markers.";
            icon = TexCommand.ClearPrioritizedWork;
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            soundSucceeded = SoundDefOf.Designate_PlanRemove;
            useMouseIcon = true;
        }

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
            return false;
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
    }

    public class Designator_ClearAllPlanning : Designator
    {
        public Designator_ClearAllPlanning()
        {
            defaultLabel = "Clear All LOS";
            defaultDesc = "Remove ALL LOS planning markers from the map.";
            icon = TexCommand.ClearPrioritizedWork;
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
            Messages.Message("All LOS planning markers cleared.", MessageTypeDefOf.NeutralEvent, false);
        }
    }

    public class Designator_CombinedView : Designator
    {
        public Designator_CombinedView()
        {
            defaultLabel = "Combined LOS View";
            defaultDesc = "Show combined LOS from ALL observer markers.\nCover shown is the minimum (most exposed angle).";
            icon = TexCommand.Attack; soundSucceeded = SoundDefOf.Click; useMouseIcon = false;
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
                    Messages.Message("No observer markers placed.", MessageTypeDefOf.RejectInput, false);
                    hypo.CombinedViewActive = false; return;
                }
                var results = new Dictionary<IntVec3, CellLOSResult>();
                LOSCalculator.ComputeCombinedLOS(observers, map, LOSMode.Static,
                    LOSOverlay_Mod.Settings.DefaultRange, OverlayDirection.Offensive, results);
                OverlayRenderer.SetOverlayData(results, map);
                Messages.Message("Combined LOS from " + observers.Count + " observer(s).", MessageTypeDefOf.NeutralEvent, false);
            }
            else OverlayRenderer.ClearOverlay();
        }
    }
}