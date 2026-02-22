using RimWorld;
using Verse;

namespace LOSOverlay
{
    /// <summary>
    /// Vanilla cover provider. Matches CoverUtility.BaseBlockChance:
    /// FillCategory.Full => 0.75, otherwise => fillPercent, open doors => 0
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
            return UnityEngine.Mathf.Clamp01(rawValue);
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
    }
}