using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace LOSOverlay
{
    [StaticConstructorOnStartup]
    public static class OverlayRenderer
    {
        // 5 discrete color bands — no gradients, what you configure is what you see.
        private static readonly Color COLOR_NONE      = new Color(0.0f, 0.8f, 0.0f);   // green
        private static readonly Color COLOR_LOW       = new Color(0.6f, 0.8f, 0.0f);   // yellow-green
        private static readonly Color COLOR_MODERATE  = new Color(1.0f, 0.9f, 0.0f);   // yellow
        private static readonly Color COLOR_HIGH      = new Color(1.0f, 0.5f, 0.0f);   // orange
        private static readonly Color COLOR_EXTREME   = new Color(0.9f, 0.1f, 0.1f);   // red
        private static readonly Color COLOR_NO_LOS    = new Color(0.15f, 0.15f, 0.15f); // dark grey

        private static readonly Dictionary<int, Material> _materialCache = new Dictionary<int, Material>();
        private static Dictionary<IntVec3, CellLOSResult> _currentResults = new Dictionary<IntVec3, CellLOSResult>();
        private static bool _overlayActive;
        private static Map _overlayMap;

        // ── Cover-map overlay (independent of LOS overlay) ────────────────
        private static Dictionary<IntVec3, CellLOSResult> _coverMapResults = new Dictionary<IntVec3, CellLOSResult>();
        private static bool _coverMapActive;
        private static Map _coverMapMap;

        public static bool IsActive        { get { return _overlayActive;  } }
        public static bool IsCoverMapActive { get { return _coverMapActive; } }

        public static void ClearMaterialCache()
        {
            _materialCache.Clear();
        }

        public static void SetOverlayData(Dictionary<IntVec3, CellLOSResult> results, Map map)
        {
            _currentResults = results;
            _overlayMap = map;
            _overlayActive = results != null && results.Count > 0;
        }

        public static void ClearOverlay()
        {
            _currentResults = new Dictionary<IntVec3, CellLOSResult>();
            _overlayActive = false;
            _overlayMap = null;
        }

        public static void SetCoverMapData(Dictionary<IntVec3, CellLOSResult> results, Map map)
        {
            _coverMapResults = results;
            _coverMapMap = map;
            _coverMapActive = results != null && results.Count > 0;
        }

        public static void ClearCoverMap()
        {
            _coverMapResults = new Dictionary<IntVec3, CellLOSResult>();
            _coverMapActive = false;
            _coverMapMap = null;
        }

        /// <summary>
        /// Recompute the cover-map in-place using the map that was active when
        /// it was first enabled. Called periodically to pick up terrain changes
        /// (walls built/destroyed, doors opened, etc.).
        /// </summary>
        public static void RefreshCoverMap()
        {
            if (!_coverMapActive || _coverMapMap == null) return;
            LOSCalculator.ComputeCoverMap(_coverMapMap, _coverMapResults);
        }

        /// <summary>Draw the terrain cover-map overlay (drawn before the LOS overlay so LOS sits on top).</summary>
        public static void DrawCoverMap()
        {
            if (!_coverMapActive || _coverMapMap == null) return;
            if (Find.CurrentMap != _coverMapMap) return;
            float opacity = LOSOverlay_Mod.Settings.OverlayOpacity;

            foreach (var kvp in _coverMapResults)
            {
                if (!kvp.Key.InBounds(_coverMapMap)) continue;
                // HasLOS = false means the cell is impassable (wall / full-fill):
                // render it dark gray so it's visually distinct from shootable cover.
                Color color = kvp.Value.HasLOS
                    ? GetCoverColor(kvp.Value.CoverValue)
                    : COLOR_NO_LOS;
                color.a = opacity;
                DrawCell(kvp.Key, color);
            }
        }

        public static void DrawOverlay()
        {
            if (!_overlayActive || _overlayMap == null) return;
            if (Find.CurrentMap != _overlayMap) return;
            float opacity = LOSOverlay_Mod.Settings.OverlayOpacity;

            foreach (var kvp in _currentResults)
            {
                if (!kvp.Key.InBounds(_overlayMap)) continue;
                Color color = kvp.Value.HasLOS
                    ? GetCoverColor(kvp.Value.CoverValue)
                    : COLOR_NO_LOS;
                color.a = opacity;
                DrawCell(kvp.Key, color);
            }
        }

        private static void DrawCell(IntVec3 cell, Color color)
        {
            var pos = cell.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays);
            var mat = GetCachedMaterial(color);
            Graphics.DrawMesh(MeshPool.plane10, pos, Quaternion.identity, mat, 0);
        }

        private static Material GetCachedMaterial(Color color)
        {
            int key = ((int)(color.r * 15) << 12) | ((int)(color.g * 15) << 8) |
                      ((int)(color.b * 15) << 4) | (int)(color.a * 15);
            Material mat;
            if (!_materialCache.TryGetValue(key, out mat))
            {
                mat = SolidColorMaterials.SimpleSolidColorMaterial(color, true);
                _materialCache[key] = mat;
            }
            return mat;
        }

        /// <summary>
        /// Map a raw cover value to a flat color band using configurable thresholds.
        /// No gradients — each band is a single solid color.
        ///
        /// For vanilla: rawCover is the cover percentage (0–1).
        /// For CE: rawCover is in cell-height units, converted to meters here.
        /// </summary>
        public static Color GetCoverColor(float rawCover)
        {
            float t1, t2, t3, t4;
            if (LOSOverlay_Mod.CEActive)
            {
                float meters = rawCover * CECoverProvider.CE_METERS_PER_CELL;
                t1 = LOSOverlay_Mod.Settings.CEThresh1;
                t2 = LOSOverlay_Mod.Settings.CEThresh2;
                t3 = LOSOverlay_Mod.Settings.CEThresh3;
                t4 = LOSOverlay_Mod.Settings.CEThresh4;
                return ColorFromThresholds(meters, t1, t2, t3, t4);
            }
            else
            {
                t1 = LOSOverlay_Mod.Settings.VanillaThresh1;
                t2 = LOSOverlay_Mod.Settings.VanillaThresh2;
                t3 = LOSOverlay_Mod.Settings.VanillaThresh3;
                t4 = LOSOverlay_Mod.Settings.VanillaThresh4;
                return ColorFromThresholds(rawCover, t1, t2, t3, t4);
            }
        }

        /// <summary>
        /// 5 flat color bands with hard cutoffs.
        ///   value &lt;= t1 → green   (no cover)
        ///   value &lt;= t2 → yellow-green (low)
        ///   value &lt;= t3 → yellow  (moderate)
        ///   value &lt;= t4 → orange  (high)
        ///   value &gt;  t4 → red     (extreme)
        /// </summary>
        private static Color ColorFromThresholds(float value, float t1, float t2, float t3, float t4)
        {
            if (value <= t1) return COLOR_NONE;
            if (value <= t2) return COLOR_LOW;
            if (value <= t3) return COLOR_MODERATE;
            if (value <= t4) return COLOR_HIGH;
            return COLOR_EXTREME;
        }

        public static string GetCellTooltip(IntVec3 cell)
        {
            CellLOSResult result;

            // LOS overlay takes priority — show LOS context first.
            if (_overlayActive && _currentResults.TryGetValue(cell, out result))
            {
                if (!result.HasLOS) return "No line of sight";
                if (result.CoverValue <= 0.01f) return "Clear - no cover";
                return LOSOverlay_Mod.CoverProvider.GetCoverLabel(result.CoverValue);
            }

            // Fall through to cover-map if no LOS overlay for this cell.
            // Guard map identity: _coverMapResults may hold cells from a different
            // map whose coordinates overlap with the current map.
            if (_coverMapActive && _coverMapMap == Find.CurrentMap && _coverMapResults.TryGetValue(cell, out result))
            {
                if (!result.HasLOS) return "Impassable - blocks line of sight";
                if (result.CoverValue <= 0.01f) return "Terrain cover: none";
                return "Terrain cover: " + LOSOverlay_Mod.CoverProvider.GetCoverLabel(result.CoverValue);
            }

            return null;
        }
    }
}
