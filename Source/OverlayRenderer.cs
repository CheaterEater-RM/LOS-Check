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

        public static bool IsActive => _overlayActive;

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
                    ? GetCoverColor(kvp.Value.NormalizedCover)
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
            if (!_materialCache.TryGetValue(key, out var mat))
            {
                mat = SolidColorMaterials.SimpleSolidColorMaterial(color, true);
                _materialCache[key] = mat;
            }
            return mat;
        }

        public static Color GetCoverColor(float normalized)
        {
            if (normalized <= 0.01f) return COLOR_CLEAR;
            if (normalized <= 0.30f) return Color.Lerp(COLOR_CLEAR, COLOR_LOW, normalized / 0.30f);
            if (normalized <= 0.50f) return Color.Lerp(COLOR_LOW, COLOR_MODERATE, (normalized - 0.30f) / 0.20f);
            if (normalized <= 0.74f) return Color.Lerp(COLOR_MODERATE, COLOR_GOOD, (normalized - 0.50f) / 0.24f);
            return Color.Lerp(COLOR_GOOD, COLOR_HIGH, Mathf.Clamp01((normalized - 0.74f) / 0.26f));
        }

        public static string GetCellTooltip(IntVec3 cell)
        {
            if (!_overlayActive || !_currentResults.TryGetValue(cell, out var result)) return null;
            if (!result.HasLOS) return "No line of sight";
            if (result.CoverValue <= 0.01f) return "Clear — no cover";
            return $"Cover: {LOSOverlay_Mod.CoverProvider.GetCoverLabel(result.CoverValue)}";
        }
    }
}