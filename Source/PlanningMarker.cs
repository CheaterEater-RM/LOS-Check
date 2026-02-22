using System.Collections.Generic;
using RimWorld;
using Verse;

namespace LOSOverlay
{
    /// <summary>
    /// Observer marker only. Wall/Cover/Open are now designations.
    /// Observers remain as Things because they need selection + gizmos.
    /// </summary>
    public class PlanningMarker : ThingWithComps
    {
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            var hypo = map.GetComponent<HypotheticalMapState>();
            if (hypo != null)
            {
                hypo.ObserverPositions.Add(Position);
                hypo.MarkDirty();
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            var map = Map; var pos = Position;
            base.DeSpawn(mode);
            if (map != null)
            {
                var hypo = map.GetComponent<HypotheticalMapState>();
                if (hypo != null)
                {
                    hypo.ObserverPositions.Remove(pos);
                    hypo.MarkDirty();
                }
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var g in base.GetGizmos()) yield return g;
            yield return new Gizmo_LOSMode(this);          // right-click for direction
            yield return new Command_Action
            {
                defaultLabel = "Remove Observer",
                defaultDesc = "Remove this LOS observer.",
                icon = TexCommand.ClearPrioritizedWork,
                action = () => { if (!Destroyed) Destroy(); }
            };
        }

        public override string GetInspectString()
        {
            return "LOS Observer - select to view overlay\nLeft-click the LOS gizmo to cycle mode. Right-click for offensive/defensive view.";
        }
    }
}
