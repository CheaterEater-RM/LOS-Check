using System;
using System.Reflection;
using RimWorld;
using Verse;

namespace LOSOverlay
{
    /// <summary>
    /// CE cover provider. Uses cover height (meters) via reflection.
    /// Fallback: fillPercent * 2.5 if CE stat unavailable.
    /// </summary>
    public class CECoverProvider : ICoverProvider
    {
        private const float CE_SANDBAG_HEIGHT = 0.75f;
        private const float CE_WALL_HEIGHT = 2.5f;
        private const float CE_MAX_COVER_HEIGHT = 2.5f;

        private static StatDef _coverHeightStat;
        private static bool _statResolved;
        private static bool _statFailed;

        public float HypotheticalCoverValue => CE_SANDBAG_HEIGHT;
        public float HypotheticalWallValue => CE_WALL_HEIGHT;

        private static void EnsureStatResolved()
        {
            if (_statResolved) return;
            _statResolved = true;
            try
            {
                _coverHeightStat = DefDatabase<StatDef>.GetNamedSilentFail("CoverHeight");
                if (_coverHeightStat != null)
                {
                    Log.Message("[LOS Overlay] CE CoverHeight stat resolved via DefDatabase.");
                    return;
                }
                var ceAssembly = FindCEAssembly();
                if (ceAssembly == null) { _statFailed = true; return; }
                var statDefOfType = ceAssembly.GetType("CombatExtended.CE_StatDefOf");
                if (statDefOfType != null)
                {
                    var field = statDefOfType.GetField("CoverHeight", BindingFlags.Public | BindingFlags.Static);
                    if (field != null)
                    {
                        _coverHeightStat = field.GetValue(null) as StatDef;
                        if (_coverHeightStat != null)
                        {
                            Log.Message("[LOS Overlay] CE CoverHeight stat resolved via CE_StatDefOf.");
                            return;
                        }
                    }
                }
                Log.Warning("[LOS Overlay] Could not resolve CE CoverHeight stat. Falling back.");
                _statFailed = true;
            }
            catch (Exception ex)
            {
                Log.Error($"[LOS Overlay] CE reflection failed: {ex.Message}");
                _statFailed = true;
            }
        }

        private static Assembly FindCEAssembly()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (assembly.GetName().Name == "CombatExtended") return assembly;
            return null;
        }

        public float GetCoverValue(Thing thing)
        {
            if (thing == null) return 0f;
            EnsureStatResolved();
            if (!_statFailed && _coverHeightStat != null)
            {
                try { return thing.GetStatValue(_coverHeightStat); }
                catch { }
            }
            if (thing is Building_Door door && door.Open) return 0f;
            return thing.def.fillPercent * CE_WALL_HEIGHT;
        }

        public float GetCoverValueForDef(ThingDef def)
        {
            if (def == null) return 0f;
            EnsureStatResolved();
            if (!_statFailed && _coverHeightStat != null)
            {
                try { return def.GetStatValueAbstract(_coverHeightStat); }
                catch { }
            }
            return def.fillPercent * CE_WALL_HEIGHT;
        }

        public float NormalizeCoverValue(float rawValue)
        {
            return UnityEngine.Mathf.Clamp01(rawValue / CE_MAX_COVER_HEIGHT);
        }

        public string GetCoverLabel(float rawValue)
        {
            return $"{rawValue:F2}m cover height";
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