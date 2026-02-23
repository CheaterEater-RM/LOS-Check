using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LOSOverlay
{
    public enum LOSMode { Off, Static, Leaning }
    public enum OverlayDirection { Offensive, Defensive }

    public struct CellLOSResult
    {
        public bool HasLOS;
        public float CoverValue;
    }

    public static class LOSCalculator
    {
        private static readonly List<IntVec3> _leanSources = new List<IntVec3>();
        private static readonly List<IntVec3> _validLeanSources = new List<IntVec3>();

        public static void ComputeLOS(IntVec3 observerPos, Map map, LOSMode mode, int range,
            OverlayDirection direction, Dictionary<IntVec3, CellLOSResult> results)
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
                    results[target] = CheckCell(observerPos, target, map, mode, direction, hypoState, provider);
                }
            }
        }

        private static CellLOSResult CheckCell(IntVec3 observer, IntVec3 target, Map map,
            LOSMode mode, OverlayDirection direction,
            HypotheticalMapState hypo, ICoverProvider provider)
        {
            var result = new CellLOSResult();
            if (!IsTargetAccessible(target, map, hypo)) return result;

            if (mode == LOSMode.Static)
            {
                result.HasLOS = HypoLOS(observer, target, map, hypo);
                if (result.HasLOS)
                {
                    result.CoverValue = direction == OverlayDirection.Offensive
                        ? provider.ComputeCoverBetween(observer, target, map, hypo)
                        : provider.ComputeCoverBetween(target, observer, map, hypo);
                }
            }
            else // Leaning — compute cover from the actual shooting position(s)
            {
                // Gather all valid shooting positions: direct + lean sources with LOS
                _validLeanSources.Clear();

                if (HypoLOS(observer, target, map, hypo))
                    _validLeanSources.Add(observer);

                // Always check lean sources too — even with direct LOS, a lean
                // position may give better cover, and the game will prefer it.
                _leanSources.Clear();
                GetHypoLeanSources(observer, target, map, hypo, _leanSources);
                for (int i = 0; i < _leanSources.Count; i++)
                {
                    var src = _leanSources[i];
                    if (src == observer) continue; // already checked above
                    if (HypoLOS(src, target, map, hypo))
                        _validLeanSources.Add(src);
                }

                if (_validLeanSources.Count == 0)
                    return result;

                result.HasLOS = true;

                // Pick the best shooting position for cover:
                //   Offensive: minimize target cover (best shot for us)
                //   Defensive: maximize our cover (best protection for us)
                float bestCover = direction == OverlayDirection.Offensive
                    ? float.MaxValue : float.MinValue;

                for (int i = 0; i < _validLeanSources.Count; i++)
                {
                    var src = _validLeanSources[i];
                    float cover = direction == OverlayDirection.Offensive
                        ? provider.ComputeCoverBetween(src, target, map, hypo)
                        : provider.ComputeCoverBetween(target, src, map, hypo);

                    bool isBetter = direction == OverlayDirection.Offensive
                        ? cover < bestCover
                        : cover > bestCover;

                    if (isBetter)
                        bestCover = cover;
                }

                result.CoverValue = bestCover == float.MaxValue || bestCover == float.MinValue
                    ? 0f : bestCover;
            }
            return result;
        }

        /// <summary>
        /// LOS raycast that respects hypothetical state.
        /// We cannot use GenSight.LineOfSight with a validator because it calls
        /// CanBeSeenOverFast() BEFORE our validator — real walls always block
        /// regardless of OpenSpaces. We must own the entire ray loop.
        /// </summary>
        private static bool HypoLOS(IntVec3 source, IntVec3 target, Map map, HypotheticalMapState hypo)
        {
            if (!source.InBounds(map) || !target.InBounds(map)) return false;

            // Bresenham walk — skip source cell, check every cell through to target.
            bool sideOnEqual = source.x != target.x ? source.x < target.x : source.z < target.z;
            int dx = Math.Abs(target.x - source.x);
            int dz = Math.Abs(target.z - source.z);
            int x  = source.x;
            int z  = source.z;
            int n  = 1 + dx + dz;
            int xi = target.x > source.x ? 1 : -1;
            int zi = target.z > source.z ? 1 : -1;
            int err = dx - dz;
            dx *= 2; dz *= 2;

            for (; n > 1; n--)
            {
                // Advance first so we skip source
                if (err > 0 || (err == 0 && sideOnEqual)) { x += xi; err -= dz; }
                else                                        { z += zi; err += dx; }

                var cell = new IntVec3(x, 0, z);

                // Target cell is always reachable (IsTargetAccessible already checked it).
                if (cell == target) return true;

                // Designations override everything beneath them (checked in priority order).
                if (hypo != null)
                {
                    if (hypo.HypotheticalWalls.Contains(cell)) return false;  // wall  → blocks
                    if (hypo.HypotheticalCover.Contains(cell)) continue;      // cover → passable
                    if (hypo.OpenSpaces.Contains(cell))        continue;      // open  → passable
                }

                // No designation: fog = unknown = blocks.
                if (cell.Fogged(map)) return false;

                // No designation, not fogged: use real map.
                if (!cell.CanBeSeenOverFast(map)) return false;
            }
            return true;
        }

        /// <summary>
        /// Replicates ShootLeanUtility.LeanShootingSourcesFromTo but using
        /// HypoBlocks() instead of CanBeSeenOver(map) so hypothetical walls
        /// and open spaces are respected.
        /// </summary>
        private static void GetHypoLeanSources(IntVec3 observer, IntVec3 target,
            Map map, HypotheticalMapState hypo, List<IntVec3> list)
        {
            list.Clear();
            float angle = (target - observer).AngleFlat;
            bool east  = angle > 270f || angle < 90f;
            bool west  = angle > 90f  && angle < 270f;
            bool south = angle > 180f;
            bool north = angle < 180f;

            // Build blocked bits for the 8 neighbours (indices match GenAdj.AdjacentCells)
            bool[] blocked = new bool[8];
            for (int i = 0; i < 8; i++)
                blocked[i] = HypoBlocks(observer + GenAdj.AdjacentCells[i], map, hypo);

            // GenAdj cardinal indices: 0=N, 1=E, 2=S, 3=W
            // (matching vanilla ShootLeanUtility ordering)
            if (!blocked[1] && ((blocked[0] && !blocked[5] && east)  || (blocked[2] && !blocked[4] && west)))
                list.Add(observer + IntVec3.East);
            if (!blocked[3] && ((blocked[0] && !blocked[6] && east)  || (blocked[2] && !blocked[7] && west)))
                list.Add(observer + IntVec3.West);
            if (!blocked[2] && ((blocked[3] && !blocked[7] && south) || (blocked[1] && !blocked[4] && north)))
                list.Add(observer + IntVec3.South);
            if (!blocked[0] && ((blocked[3] && !blocked[6] && south) || (blocked[1] && !blocked[5] && north)))
                list.Add(observer + IntVec3.North);

            // Observer cell itself if it can be seen over
            if (!HypoBlocks(observer, map, hypo))
                list.Add(observer);

            // Cover-based lean sources: lean onto adjacent sandbags/rocks etc.
            // Matches vanilla exactly, but we additionally require the observer's
            // own cell to be open — a pawn inside a wall can't step to a cover
            // object outside it. This prevents phantom lean sources appearing
            // through tunnel walls that happen to have trees outside them.
            if (!HypoBlocks(observer, map, hypo))
            {
                for (int j = 0; j < 4; j++)
                {
                    if (blocked[j]) continue; // adjacent cell itself is blocked
                    bool inDirection = (j == 0 && east) || (j == 1 && north) || (j == 2 && west) || (j == 3 && south);
                    if (!inDirection) continue;
                    var adj = observer + GenAdj.AdjacentCells[j];
                    if (adj.InBounds(map) && adj.GetCover(map) != null)
                        list.Add(adj);
                }
            }
        }

        // Designation priority (highest to lowest):
        //   1. PlanWall   → always blocks, regardless of what's underneath
        //   2. PlanCover  → always passable (cover object), regardless of what's underneath
        //   3. PlanOpen   → always passable (empty space), regardless of what's underneath
        //   4. (no desig) → fog → unknown, treat as wall
        //   4. (no desig) → real map state
        private static bool HypoBlocks(IntVec3 cell, Map map, HypotheticalMapState hypo)
        {
            if (!cell.InBounds(map)) return true;
            if (hypo != null)
            {
                if (hypo.HypotheticalWalls.Contains(cell))  return true;   // wall designation → blocks
                if (hypo.HypotheticalCover.Contains(cell))  return false;  // cover designation → passable
                if (hypo.OpenSpaces.Contains(cell))         return false;  // open designation  → passable
            }
            // No designation: fog = unknown terrain = treat as wall.
            if (cell.Fogged(map)) return true;
            // No designation, not fogged: use real map.
            return !cell.CanBeSeenOverFast(map);
        }

        private static bool IsTargetAccessible(IntVec3 cell, Map map, HypotheticalMapState hypo)
        {
            if (!cell.InBounds(map)) return false;
            if (hypo != null)
            {
                if (hypo.HypotheticalWalls.Contains(cell))  return false;  // wall → not a valid target cell
                if (hypo.HypotheticalCover.Contains(cell))  return true;   // cover → passable, valid target
                if (hypo.OpenSpaces.Contains(cell))         return true;   // open  → passable, valid target
            }
            // No designation: fog = unknown = not accessible as a target.
            if (cell.Fogged(map)) return false;
            // No designation, not fogged: use real map.
            var edifice = cell.GetEdifice(map);
            return edifice == null || !LOSOverlay_Mod.CoverProvider.BlocksLOS(edifice);
        }

        // Cover computation is now delegated to ICoverProvider.ComputeCoverBetween().
        // Vanilla uses angle-based adjacent-cell logic; CE walks the LOS path.

        public static void ComputeCombinedLOS(List<IntVec3> observers, Map map, LOSMode mode,
            int range, OverlayDirection direction, Dictionary<IntVec3, CellLOSResult> combined)
        {
            combined.Clear();
            var single = new Dictionary<IntVec3, CellLOSResult>();
            foreach (var obs in observers)
            {
                ComputeLOS(obs, map, mode, range, direction, single);
                foreach (var kvp in single)
                {
                    if (!kvp.Value.HasLOS) continue;
                    CellLOSResult existing;
                    if (combined.TryGetValue(kvp.Key, out existing))
                    {
                        if (kvp.Value.CoverValue < existing.CoverValue)
                            combined[kvp.Key] = kvp.Value;
                    }
                    else combined[kvp.Key] = kvp.Value;
                }
            }
        }
    }
}
