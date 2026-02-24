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
            _currentResults = new Dictionary<IntVec3, CellLOSResult>();
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
            if (!_overlayActive || !_currentResults.TryGetValue(cell, out result)) return null;
            if (!result.HasLOS) return "No line of sight";
            if (result.CoverValue <= 0.01f) return "Clear - no cover";
            string coverLabel = LOSOverlay_Mod.CoverProvider.GetCoverLabel(result.CoverValue);
            return coverLabel;
        }
    }
}
