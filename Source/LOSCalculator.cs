using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LOSOverlay
{
    public enum LOSMode { Off, Static, Leaning }

    public struct CellLOSResult
    {
        public bool HasLOS;
        public float CoverValue;
        public float NormalizedCover;
    }

    /// <summary>
    /// Core LOS and cover computation. Cover algorithm matches vanilla CoverUtility's
    /// angle-based system with distance falloff.
    /// </summary>
    public static class LOSCalculator
    {
        private static readonly List<IntVec3> _leanSources = new List<IntVec3>();

        public static void ComputeLOS(IntVec3 observerPos, Map map, LOSMode mode, int range,
            Dictionary<IntVec3, CellLOSResult> results)
        {
            results.Clear();
            if (mode == LOSMode.Off || !observerPos.InBounds(map)) return;

            var hypoState = map.GetComponent<HypotheticalMapState>();
            var provider = LOSOverlay_Mod.CoverProvider;
            float rangeSq = range * range;

            int minX = Mathf.Max(0, observerPos.x - range);
            int maxX = Mathf.Min(map.Size.x - 1, observerPos.x + range);
            int minZ = Mathf.Max(0, observerPos.z - range);
            int maxZ = Mathf.Min(map.Size.z - 1, observerPos.z + range);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    var target = new IntVec3(x, 0, z);
                    if (target == observerPos) continue;
                    float distSq = (target - observerPos).LengthHorizontalSquared;
                    if (distSq > rangeSq) continue;
                    results[target] = CheckCell(observerPos, target, map, mode, hypoState, provider);
                }
            }
        }

        private static CellLOSResult CheckCell(IntVec3 observer, IntVec3 target, Map map,
            LOSMode mode, HypotheticalMapState hypo, ICoverProvider provider)
        {
            var result = new CellLOSResult();
            if (!IsTargetAccessible(target, map, hypo)) return result;

            Func<IntVec3, bool> validator = hypo != null ? (Func<IntVec3, bool>)hypo.LOSValidator : null;
            bool hasLOS = false;

            if (mode == LOSMode.Static)
            {
                hasLOS = DirectLOS(observer, target, map, validator);
            }
            else if (mode == LOSMode.Leaning)
            {
                hasLOS = DirectLOS(observer, target, map, validator);
                if (!hasLOS) hasLOS = LeanLOS(observer, target, map, validator);
            }

            result.HasLOS = hasLOS;
            if (hasLOS)
            {
                result.CoverValue = ComputeCover(observer, target, map, hypo, provider);
                result.NormalizedCover = provider.NormalizeCoverValue(result.CoverValue);
            }
            return result;
        }

        private static bool IsTargetAccessible(IntVec3 cell, Map map, HypotheticalMapState hypo)
        {
            if (!cell.InBounds(map)) return false;
            if (hypo != null)
            {
                if (hypo.HypotheticalWalls.Contains(cell)) return false;
                if (hypo.OpenSpaces.Contains(cell)) return true;
            }
            var edifice = cell.GetEdifice(map);
            return edifice == null || !LOSOverlay_Mod.CoverProvider.BlocksLOS(edifice);
        }

        private static bool DirectLOS(IntVec3 source, IntVec3 target, Map map, Func<IntVec3, bool> validator)
        {
            if (validator != null)
                return GenSight.LineOfSight(source, target, map, true, validator, 0, 0);
            return GenSight.LineOfSight(source, target, map);
        }

        private static bool LeanLOS(IntVec3 observer, IntVec3 target, Map map, Func<IntVec3, bool> validator)
        {
            _leanSources.Clear();
            try { ShootLeanUtility.LeanShootingSourcesFromTo(observer, target, map, _leanSources); }
            catch { return false; }

            for (int i = 0; i < _leanSources.Count; i++)
            {
                if (_leanSources[i] == observer) continue;
                if (DirectLOS(_leanSources[i], target, map, validator)) return true;
            }
            return false;
        }

        /// <summary>
        /// Cover at target from observer. Matches CoverUtility.TryFindAdjustedCoverInCell:
        /// angle-based falloff + distance reduction.
        /// </summary>
        private static float ComputeCover(IntVec3 observer, IntVec3 target, Map map,
            HypotheticalMapState hypo, ICoverProvider provider)
        {
            float bestCover = 0f;
            float shooterAngle = (observer - target).AngleFlat;

            for (int i = 0; i < 8; i++)
            {
                IntVec3 adjCell = target + GenAdj.AdjacentCells[i];
                if (!adjCell.InBounds(map)) continue;
                if (adjCell == observer) continue;

                float rawCover;
                if (hypo != null)
                {
                    rawCover = hypo.GetCoverValueAt(adjCell);
                }
                else
                {
                    var cover = adjCell.GetCover(map);
                    if (cover == null) continue;
                    rawCover = provider.GetCoverValue(cover);
                }
                if (rawCover <= 0f) continue;

                // Angle falloff (matches CoverUtility)
                float coverAngle = (adjCell - target).AngleFlat;
                float angleDiff = GenGeo.AngleDifferenceBetween(coverAngle, shooterAngle);
                if (!target.AdjacentToCardinal(adjCell)) angleDiff *= 1.75f;

                float angleMult;
                if (angleDiff < 15f) angleMult = 1.0f;
                else if (angleDiff < 27f) angleMult = 0.8f;
                else if (angleDiff < 40f) angleMult = 0.6f;
                else if (angleDiff < 52f) angleMult = 0.4f;
                else if (angleDiff < 65f) angleMult = 0.2f;
                else continue;

                float effectiveCover = rawCover * angleMult;

                // Distance reduction (matches CoverUtility)
                float dist = (observer - adjCell).LengthHorizontal;
                if (dist < 1.9f) effectiveCover *= 0.3333f;
                else if (dist < 2.9f) effectiveCover *= 0.66666f;

                if (effectiveCover > bestCover) bestCover = effectiveCover;
            }
            return bestCover;
        }

        public static void ComputeCombinedLOS(List<IntVec3> observers, Map map, LOSMode mode,
            int range, Dictionary<IntVec3, CellLOSResult> combined)
        {
            combined.Clear();
            var single = new Dictionary<IntVec3, CellLOSResult>();
            foreach (var obs in observers)
            {
                ComputeLOS(obs, map, mode, range, single);
                foreach (var kvp in single)
                {
                    if (!kvp.Value.HasLOS) continue;
                    if (combined.TryGetValue(kvp.Key, out var existing))
                    {
                        if (kvp.Value.NormalizedCover < existing.NormalizedCover)
                            combined[kvp.Key] = kvp.Value;
                    }
                    else combined[kvp.Key] = kvp.Value;
                }
            }
        }
    }
}