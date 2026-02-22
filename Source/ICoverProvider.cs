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
    }
}