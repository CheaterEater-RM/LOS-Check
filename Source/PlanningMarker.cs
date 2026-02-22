using System.Collections.Generic;
using RimWorld;
using Verse;

namespace LOSOverlay
{
    public enum PlanningMarkerType { Observer, Wall, Cover, OpenSpace }

    public class PlanningMarker : ThingWithComps
    {
        public PlanningMarkerType MarkerType = PlanningMarkerType.Observer;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            var hypo = map.GetComponent<HypotheticalMapState>();
            if (hypo == null) return;
            switch (MarkerType)
            {
                case PlanningMarkerType.Observer: hypo.ObserverPositions.Add(Position); break;
                case PlanningMarkerType.Wall: hypo.AddWall(Position); break;
                case PlanningMarkerType.Cover: hypo.AddCover(Position); break;
                case PlanningMarkerType.OpenSpace: hypo.AddOpenSpace(Position); break;
            }
            hypo.MarkDirty();
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            var map = Map; var pos = Position;
            base.DeSpawn(mode);
            var hypo = map != null ? map.GetComponent<HypotheticalMapState>() : null;
            if (hypo == null) return;
            hypo.RemoveAt(pos); hypo.MarkDirty();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var g in base.GetGizmos()) yield return g;
            if (MarkerType == PlanningMarkerType.Observer) yield return new Gizmo_LOSMode(this);
            yield return new Command_Action
            {
                defaultLabel = "Remove Marker",
                defaultDesc = "Remove this planning marker.",
                icon = TexCommand.ClearPrioritizedWork,
                action = () => { if (!Destroyed) Destroy(); }
            };
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref MarkerType, "MarkerType", PlanningMarkerType.Observer);
        }

        public override string GetInspectString()
        {
            switch (MarkerType)
            {
                case PlanningMarkerType.Observer: return "LOS Observer — select to view overlay";
                case PlanningMarkerType.Wall: return "Hypothetical Wall — blocks line of sight";
                case PlanningMarkerType.Cover: return "Hypothetical Cover — provides partial cover";
                case PlanningMarkerType.OpenSpace: return "Open Space — ignores existing wall here";
                default: return base.GetInspectString();
            }
        }
    }
}