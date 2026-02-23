using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace LOSOverlay
{
    [StaticConstructorOnStartup]
    public static class OverlayRenderer
    {
        private static readonly Color COLOR_CLEAR     = new Color(0.0f, 0.8f, 0.0f);
        private static readonly Color COLOR_LOW       = new Color(0.6f, 0.8f, 0.0f);
        private static readonly Color COLOR_MODERATE  = new Color(1.0f, 0.9f, 0.0f);
        private static readonly Color COLOR_GOOD      = new Color(1.0f, 0.5f, 0.0f);
        private static readonly Color COLOR_HIGH      = new Color(0.9f, 0.1f, 0.1f);
        private static readonly Color COLOR_NO_LOS    = new Color(0.15f, 0.15f, 0.15f);

        private static readonly Dictionary<int, Material> _materialCache = new Dictionary<int, Material>();
        private static Dictionary<IntVec3, CellLOSResult> _currentResults = new Dictionary<IntVec3, CellLOSResult>();
        private static bool _overlayActive;
        private static Map _overlayMap;

        public static bool IsActive { get { return _overlayActive; } }

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
            _currentResults.Clear();
            _overlayActive = false;
            _overlayMap = null;
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
        /// Map a raw cover value to a color using the configurable thresholds.
        /// For CE, rawCover is in meters (fillPercent × 1.75).
        /// For vanilla, rawCover is the cover percentage (0–1).
        /// The thresholds are set per-mode in settings.
        /// </summary>
        public static Color GetCoverColor(float rawCover)
        {
            float t1, t2, t3, t4;
            if (LOSOverlay_Mod.CEActive)
            {
                // CE: raw value is in cell-height units (fillPercent).
                // Convert to meters for threshold comparison.
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
        /// 5-band color gradient with smooth lerping between thresholds.
        ///   value ≤ t1 → green (clear)
        ///   t1..t2     → green→yellow-green (low)
        ///   t2..t3     → yellow-green→yellow (moderate)
        ///   t3..t4     → yellow→orange (good)
        ///   value > t4 → orange→red (high)
        /// </summary>
        private static Color ColorFromThresholds(float value, float t1, float t2, float t3, float t4)
        {
            if (value <= t1) return COLOR_CLEAR;
            if (value <= t2) return Color.Lerp(COLOR_CLEAR, COLOR_LOW, (value - t1) / (t2 - t1));
            if (value <= t3) return Color.Lerp(COLOR_LOW, COLOR_MODERATE, (value - t2) / (t3 - t2));
            if (value <= t4) return Color.Lerp(COLOR_MODERATE, COLOR_GOOD, (value - t3) / (t4 - t3));
            return Color.Lerp(COLOR_GOOD, COLOR_HIGH, Mathf.Clamp01((value - t4) / (t4 * 0.35f)));
        }

        public static string GetCellTooltip(IntVec3 cell)
        {
            CellLOSResult result;
            if (!_overlayActive || !_currentResults.TryGetValue(cell, out result)) return null;
            if (!result.HasLOS) return "No line of sight";
            if (result.CoverValue <= 0.01f) return "Clear - no cover";
            string coverLabel = LOSOverlay_Mod.CoverProvider.GetCoverLabel(result.CoverValue);
            return coverLabel;
        }
    }
}
