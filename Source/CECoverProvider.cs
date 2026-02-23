using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace LOSOverlay
{
    /// <summary>
    /// CE cover provider. CE uses a physically-based cover system:
    ///   - Cover height is determined by CollisionVertical, not a StatDef
    ///   - Partial buildings use fillPercent as their height in cell units
    ///   - Walls (FillCategory.Full) are 2.0 cell units tall
    ///   - Cover is the tallest partial-fill thing along the LOS path,
    ///     NOT an angle-based adjacent-cell check like vanilla
    ///
    /// Values here are in CE's internal cell-height units.
    /// Display uses MetersPerCellHeight (1.75) for the label.
    /// </summary>
    public class CECoverProvider : ICoverProvider
    {
        // From CombatExtended.CollisionVertical.WallCollisionHeight
        private const float CE_WALL_HEIGHT = 2.0f;
        // CE normalisation cap — same as wall height since nothing is taller
        private const float CE_MAX_COVER_HEIGHT = CE_WALL_HEIGHT;
        // CE_Utility.MetersPerCellHeight — for display and threshold comparison
        public const float CE_METERS_PER_CELL = 1.75f;

        // Hypothetical cover: typical sandbag fillPercent (0.55)
        public float HypotheticalCoverValue => 0.55f;
        public float HypotheticalWallValue => CE_WALL_HEIGHT;

        /// <summary>
        /// Get cover height for a placed thing. Mirrors CollisionVertical logic:
        ///   FillCategory.Full → WallCollisionHeight (2.0)
        ///   Open doors → 0
        ///   Partial buildings → fillPercent
        /// Plants are excluded from cover (matches CE's !cover.IsPlant() checks).
        /// </summary>
        public float GetCoverValue(Thing thing)
        {
            if (thing == null) return 0f;
            if (thing is Building_Door door && door.Open) return 0f;
            if (thing.def.category == ThingCategory.Plant) return 0f;
            if (thing.def.Fillage == FillCategory.Full) return CE_WALL_HEIGHT;
            return thing.def.fillPercent;
        }

        public float GetCoverValueForDef(ThingDef def)
        {
            if (def == null) return 0f;
            if (def.category == ThingCategory.Plant) return 0f;
            if (def.Fillage == FillCategory.Full) return CE_WALL_HEIGHT;
            return def.fillPercent;
        }

        public float NormalizeCoverValue(float rawValue)
        {
            return Mathf.Clamp01(rawValue / CE_MAX_COVER_HEIGHT);
        }

        public string GetCoverLabel(float rawValue)
        {
            float meters = rawValue * CE_METERS_PER_CELL;
            return $"{meters:F2}m cover height";
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
        /// CE cover calculation: walk the line of sight from defender to shooter,
        /// find the tallest partial cover along the path. This matches
        /// Verb_LaunchProjectileCE.GetHighestCoverAndSmokeForTarget().
        ///
        /// CE does NOT use angle-based adjacent-cell cover. Cover is purely
        /// determined by what physically stands between shooter and target
        /// on the LOS line, with the tallest partial-fill object winning.
        /// </summary>
        public float ComputeCoverBetween(IntVec3 shooterPos, IntVec3 defenderPos, Map map,
            HypotheticalMapState hypo)
        {
            float highestCover = 0f;

            // Bresenham walk from defender toward shooter (matching CE's iteration
            // order: cells[0] = target, walking toward caster).
            // We skip the defender cell and the shooter cell, and skip cells
            // adjacent to the shooter (CE skips AdjacentTo8Way(caster.Position)).
            bool sideOnEqual = defenderPos.x != shooterPos.x
                ? defenderPos.x < shooterPos.x
                : defenderPos.z < shooterPos.z;
            int dx = Math.Abs(shooterPos.x - defenderPos.x);
            int dz = Math.Abs(shooterPos.z - defenderPos.z);
            int x  = defenderPos.x;
            int z  = defenderPos.z;
            int n  = 1 + dx + dz;
            int xi = shooterPos.x > defenderPos.x ? 1 : -1;
            int zi = shooterPos.z > defenderPos.z ? 1 : -1;
            int err = dx - dz;
            dx *= 2; dz *= 2;

            // Skip the first cell (defenderPos) — start advancing immediately
            for (int step = 0; step < n - 1; step++)
            {
                if (err > 0 || (err == 0 && sideOnEqual)) { x += xi; err -= dz; }
                else                                        { z += zi; err += dx; }

                var cell = new IntVec3(x, 0, z);

                // Stop if we've reached the shooter
                if (cell == shooterPos) break;
                if (!cell.InBounds(map)) continue;

                // Skip cells adjacent to the shooter (CE does this to avoid
                // the shooter's own cover counting against the target)
                if (cell.AdjacentTo8WayOrInside(shooterPos)) continue;

                // Get cover value at this cell, respecting hypothetical state
                float cellCover = GetCellCoverHeight(cell, map, hypo);
                if (cellCover > highestCover)
                    highestCover = cellCover;
            }

            return highestCover;
        }

        /// <summary>
        /// Get the cover height at a cell, checking hypothetical state first,
        /// then the real map. Only partial-fill, non-plant things count as cover
        /// (matching CE's GetHighestCoverAndSmokeForTarget filter).
        /// </summary>
        private float GetCellCoverHeight(IntVec3 cell, Map map, HypotheticalMapState hypo)
        {
            if (hypo != null)
            {
                // Hypothetical walls block LOS entirely — they're not "cover"
                // in the sense of partial cover height. But we shouldn't reach
                // here if LOS was blocked, so treat hypo walls as max height.
                if (hypo.HypotheticalWalls.Contains(cell)) return CE_WALL_HEIGHT;
                if (hypo.HypotheticalCover.Contains(cell)) return HypotheticalCoverValue;
                if (hypo.OpenSpaces.Contains(cell)) return 0f;
            }

            // Real map: find the tallest partial cover thing in this cell
            // (matching CE: FillCategory.Partial, not a plant)
            float best = 0f;
            var thingList = cell.GetThingList(map);
            for (int i = 0; i < thingList.Count; i++)
            {
                var thing = thingList[i];
                if (thing == null) continue;
                if (thing.def.category == ThingCategory.Plant) continue;
                if (thing.def.Fillage != FillCategory.Partial) continue;
                if (thing is Building_Door door && door.Open) continue;

                float h = thing.def.fillPercent;
                if (h > best) best = h;
            }
            return best;
        }
    }
}
