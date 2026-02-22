using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LOSOverlay
{
    public abstract class Designator_LOSPlanning : Designator
    {
        protected abstract PlanningMarkerType MarkerType { get; }
        protected abstract string MarkerDefName { get; }

        public Designator_LOSPlanning()
        {
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            useMouseIcon = true;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 loc)
        {
            if (!loc.InBounds(Find.CurrentMap)) return false;
            var things = loc.GetThingList(Find.CurrentMap);
            for (int i = 0; i < things.Count; i++)
                if (things[i] is PlanningMarker) return "Already has a planning marker.";

            if (MarkerType == PlanningMarkerType.OpenSpace)
            {
                var edifice = loc.GetEdifice(Find.CurrentMap);
                if (edifice == null || !LOSOverlay_Mod.CoverProvider.BlocksLOS(edifice))
                    return "No wall or obstacle here to open.";
            }
            if (MarkerType == PlanningMarkerType.Wall)
            {
                var edifice = loc.GetEdifice(Find.CurrentMap);
                if (edifice != null && LOSOverlay_Mod.CoverProvider.BlocksLOS(edifice))
                    return "Already a wall here.";
            }
            return true;
        }

        public override void DesignateSingleCell(IntVec3 loc)
        {
            var def = DefDatabase<ThingDef>.GetNamed(MarkerDefName, errorOnFail: false);
            if (def == null) { Log.Error($"[LOS Overlay] ThingDef '{MarkerDefName}' not found."); return; }
            var marker = (PlanningMarker)ThingMaker.MakeThing(def);
            marker.MarkerType = MarkerType;
            GenSpawn.Spawn(marker, loc, Find.CurrentMap);
        }

        public override void SelectedUpdate() { GenDraw.DrawNoBuildEdgeLines(); }
    }

    public class Designator_PlaceObserver : Designator_LOSPlanning
    {
        protected override PlanningMarkerType MarkerType => PlanningMarkerType.Observer;
        protected override string MarkerDefName => "LOSOverlay_ObserverMarker";
        public Designator_PlaceObserver()
        {
            defaultLabel = "LOS Observer"; defaultDesc = "Place an observer point. Select it to view LOS overlay.";
            icon = TexCommand.Attack; soundSucceeded = SoundDefOf.Designate_PlanAdd;
        }
    }

    public class Designator_PlaceWall : Designator_LOSPlanning
    {
        protected override PlanningMarkerType MarkerType => PlanningMarkerType.Wall;
        protected override string MarkerDefName => "LOSOverlay_WallMarker";
        public Designator_PlaceWall()
        {
            defaultLabel = "Plan Wall"; defaultDesc = "Place a hypothetical wall that blocks LOS.";
            icon = TexCommand.ForbidOn; soundSucceeded = SoundDefOf.Designate_PlanAdd;
        }
    }

    public class Designator_PlaceCover : Designator_LOSPlanning
    {
        protected override PlanningMarkerType MarkerType => PlanningMarkerType.Cover;
        protected override string MarkerDefName => "LOSOverlay_CoverMarker";
        public Designator_PlaceCover()
        {
            defaultLabel = "Plan Cover"; defaultDesc = "Place hypothetical cover (sandbag-equivalent).";
            icon = TexCommand.DesirePower; soundSucceeded = SoundDefOf.Designate_PlanAdd;
        }
    }

    public class Designator_PlaceOpen : Designator_LOSPlanning
    {
        protected override PlanningMarkerType MarkerType => PlanningMarkerType.OpenSpace;
        protected override string MarkerDefName => "LOSOverlay_OpenMarker";
        public Designator_PlaceOpen()
        {
            defaultLabel = "Plan Opening"; defaultDesc = "Mark an existing wall as open for LOS calculations.";
            icon = TexCommand.ClearPrioritizedWork; soundSucceeded = SoundDefOf.Designate_PlanAdd;
        }
    }

    public class Designator_ClearPlanning : Designator
    {
        public Designator_ClearPlanning()
        {
            defaultLabel = "Clear LOS Planning"; defaultDesc = "Remove all LOS planning markers.";
            icon = TexCommand.ClearPrioritizedWork; soundSucceeded = SoundDefOf.Designate_PlanRemove;
            useMouseIcon = false;
        }
        public override AcceptanceReport CanDesignateCell(IntVec3 loc) => false;
        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);
            var map = Find.CurrentMap; if (map == null) return;
            var markers = new List<Thing>();
            foreach (var t in map.listerThings.AllThings) if (t is PlanningMarker) markers.Add(t);
            foreach (var m in markers) if (!m.Destroyed) m.Destroy();
            map.GetComponent<HypotheticalMapState>()?.ClearAll();
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
        public override AcceptanceReport CanDesignateCell(IntVec3 loc) => false;
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
                    LOSOverlay_Mod.Settings.DefaultRange, results);
                OverlayRenderer.SetOverlayData(results, map);
                Messages.Message($"Combined LOS from {observers.Count} observer(s).", MessageTypeDefOf.NeutralEvent, false);
            }
            else OverlayRenderer.ClearOverlay();
        }
    }
}