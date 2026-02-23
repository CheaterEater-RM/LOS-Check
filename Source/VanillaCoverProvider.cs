using RimWorld;
using UnityEngine;
using Verse;

namespace LOSOverlay
{
    /// <summary>
    /// Vanilla cover provider. Matches CoverUtility.BaseBlockChance:
    /// FillCategory.Full => 0.75, otherwise => fillPercent, open doors => 0
    /// Cover is computed using the angle-based adjacent-cell system from vanilla.
    /// </summary>
    public class VanillaCoverProvider : ICoverProvider
    {
        public float HypotheticalCoverValue => 0.55f;
        public float HypotheticalWallValue => 0.75f;

        public float GetCoverValue(Thing thing)
        {
            if (thing == null) return 0f;
            if (thing is Building_Door door && door.Open) return 0f;
            return GetCoverValueForDef(thing.def);
        }

        public float GetCoverValueForDef(ThingDef def)
        {
            if (def == null) return 0f;
            if (def.Fillage == FillCategory.Full) return 0.75f;
            return def.fillPercent;
        }

        public float NormalizeCoverValue(float rawValue)
        {
            return Mathf.Clamp01(rawValue);
        }

        public string GetCoverLabel(float rawValue)
        {
            return $"{rawValue:P0} cover";
        }

        public bool BlocksLOS(Thing thing)
        {
            if (thing == null) return false;
            if (thing is Building b)
            {
                if (b.def.Fillage != FillCategory.Full) return false;
                if (b is Building_Door door && door.Open) return false;
                return true;
            }
            return false;
        }

        public bool DefBlocksLOS(ThingDef def)
        {
            if (def == null) return false;
            return def.Fillage == FillCategory.Full;
        }

        /// <summary>
        /// Vanilla angle-based cover calculation. Checks 8 cells adjacent to the
        /// defender, selects the one providing the most cover based on angle to shooter.
        /// </summary>
        public float ComputeCoverBetween(IntVec3 shooterPos, IntVec3 defenderPos, Map map,
            HypotheticalMapState hypo)
        {
            float bestCover = 0f;
            float shooterAngle = (shooterPos - defenderPos).AngleFlat;

            for (int i = 0; i < 8; i++)
            {
                IntVec3 adjCell = defenderPos + GenAdj.AdjacentCells[i];
                if (!adjCell.InBounds(map)) continue;
                if (adjCell == shooterPos) continue;

                float rawCover;
                if (hypo != null)
                    rawCover = hypo.GetCoverValueAt(adjCell);
                else
                {
                    var cover = adjCell.GetCover(map);
                    if (cover == null) continue;
                    rawCover = GetCoverValue(cover);
                }
                if (rawCover <= 0f) continue;

                float coverAngle = (adjCell - defenderPos).AngleFlat;
                float angleDiff  = GenGeo.AngleDifferenceBetween(coverAngle, shooterAngle);
                if (!defenderPos.AdjacentToCardinal(adjCell)) angleDiff *= 1.75f;

                float angleMult;
                if      (angleDiff < 15f) angleMult = 1.0f;
                else if (angleDiff < 27f) angleMult = 0.8f;
                else if (angleDiff < 40f) angleMult = 0.6f;
                else if (angleDiff < 52f) angleMult = 0.4f;
                else if (angleDiff < 65f) angleMult = 0.2f;
                else continue;

                float effectiveCover = rawCover * angleMult;
                float dist = (shooterPos - adjCell).LengthHorizontal;
                if      (dist < 1.9f) effectiveCover *= 0.3333f;
                else if (dist < 2.9f) effectiveCover *= 0.66666f;

                if (effectiveCover > bestCover) bestCover = effectiveCover;
            }
            return bestCover;
        }
    }
}