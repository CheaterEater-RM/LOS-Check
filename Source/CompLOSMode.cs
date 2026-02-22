using System.Collections.Generic;
using RimWorld;
using Verse;

namespace LOSOverlay
{
    /// <summary>
    /// Added to Building_Turret ThingDefs via XML injection.
    /// Provides the LOS gizmo without needing to patch GetGizmos on a class
    /// that no longer overrides it in RimWorld 1.6.
    /// </summary>
    public class CompLOSMode : ThingComp
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent is Building_Turret turret && turret.Faction == Faction.OfPlayer)
                yield return new Gizmo_LOSMode(parent);
        }
    }

    public class CompProperties_LOSMode : CompProperties
    {
        public CompProperties_LOSMode() { compClass = typeof(CompLOSMode); }
    }
}
