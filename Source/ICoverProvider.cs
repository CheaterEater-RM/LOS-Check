using Verse;

namespace LOSOverlay
{
    public interface ICoverProvider
    {
        float GetCoverValue(Thing thing);
        float GetCoverValueForDef(ThingDef def);
        float NormalizeCoverValue(float rawValue);
        string GetCoverLabel(float rawValue);
        bool BlocksLOS(Thing thing);
        bool DefBlocksLOS(ThingDef def);
        float HypotheticalCoverValue { get; }
        float HypotheticalWallValue { get; }

        /// <summary>
        /// Compute the effective cover between shooter and defender.
        /// Vanilla uses angle-based adjacent-cell logic; CE walks the LOS path.
        /// </summary>
        float ComputeCoverBetween(IntVec3 shooterPos, IntVec3 defenderPos, Map map,
            HypotheticalMapState hypo);
    }
}