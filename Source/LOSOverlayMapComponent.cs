using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LOSOverlay
{
    /// <summary>
    /// Implements the terrain cover-map as a RimWorld grid overlay using ICellBoolGiver.
    /// This integrates with the vanilla overlay system for auto-layout button placement.
    /// </summary>
    public class CoverMapOverlay : ICellBoolGiver
    {
        private Map map;
        private CellBoolDrawer drawerInt;

        public CoverMapOverlay(Map map)
        {
            this.map = map;
        }

        public CellBoolDrawer Drawer
        {
            get
            {
                if (drawerInt == null)
                {
                    // Reduced opacity: 0.20 instead of default 0.33
                    drawerInt = new CellBoolDrawer(this, map.Size.x, map.Size.z, 3600, opacity: 0.20f);
                }
                return drawerInt;
            }
        }

        public Color Color => Color.white;

        public void Update()
        {
            // Only render if overlay is active (has data) and not in screenshot mode
            if (OverlayRenderer.IsCoverMapActive && !Find.ScreenshotModeHandler.Active)
            {
                Drawer.MarkForDraw();
            }
            Drawer.CellBoolDrawerUpdate();
        }

        public bool GetCellBool(int index)
        {
            IntVec3 cell = map.cellIndices.IndexToCell(index);
            
            // Don't render fogged cells
            if (cell.Fogged(map))
                return false;

            // Render if we have cover data for this cell
            return OverlayRenderer.CoverMapResults.ContainsKey(cell);
        }

        public Color GetCellExtraColor(int index)
        {
            IntVec3 cell = map.cellIndices.IndexToCell(index);
            
            if (!OverlayRenderer.CoverMapResults.TryGetValue(cell, out var result))
                return Color.white;

            // Use the same color logic as the overlay renderer
            Color color = OverlayRenderer.GetCoverColor(result.CoverValue);
            
            // For cells with very low cover (< 0.01), reduce alpha further for near-invisibility
            if (result.CoverValue < 0.01f)
            {
                color.a = 0.1f;  // Nearly transparent for 0 cover
            }
            
            return color;
        }
    }

    public class LOSOverlayMapComponent : MapComponent
    {
        private Thing _lastSelected;
        private IntVec3 _lastPosition = IntVec3.Invalid;
        private CoverMapOverlay _coverMapOverlay;

        // Refresh the cover-map overlay every N ticks to pick up terrain changes.
        // 300 ticks ≈ 5 real-seconds at 1× speed (60 ticks/s).
        private const int COVER_MAP_REFRESH_INTERVAL = 300;
        private int _coverMapTick;

        public LOSOverlayMapComponent(Map map) : base(map)
        {
            _coverMapOverlay = new CoverMapOverlay(map);
        }

        public override void MapComponentUpdate()
        {
            base.MapComponentUpdate();
            var current = Find.Selector.SingleSelectedThing;

            if (current != _lastSelected)
            {
                _lastSelected = current;
                _lastPosition = current != null ? current.Position : IntVec3.Invalid;
                Gizmo_LOSMode.OnSelectionChanged(current);
            }
            else if (current != null && current.Position != _lastPosition)
            {
                _lastPosition = current.Position;
                Gizmo_LOSMode.OnPositionChanged(current);
            }

            // Recompute LOS whenever a planning designation was placed or removed.
            var hypo = map.GetComponent<HypotheticalMapState>();
            if (hypo != null && hypo.IsDirty)
            {
                hypo.ClearDirty();
                Gizmo_LOSMode.RefreshActiveOverlay(map, _lastSelected);
            }

            OverlayRenderer.DrawOverlay();
            
            // Update the cover map overlay (calls MarkForDraw and CellBoolDrawerUpdate)
            _coverMapOverlay.Update();
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!OverlayRenderer.IsCoverMapActive) return;
            if (++_coverMapTick >= COVER_MAP_REFRESH_INTERVAL)
            {
                _coverMapTick = 0;
                OverlayRenderer.RefreshCoverMap();
            }
        }

        public override void MapComponentOnGUI()
        {
            base.MapComponentOnGUI();
            if (!OverlayRenderer.IsActive && !OverlayRenderer.IsCoverMapActive) return;
            var mouseCell = UI.MouseCell();
            var tooltip = OverlayRenderer.GetCellTooltip(mouseCell);
            if (tooltip != null)
            {
                var mousePos = Event.current.mousePosition;
                var tooltipRect = new Rect(mousePos.x + 15, mousePos.y + 15, 300, 40);
                Widgets.Label(tooltipRect, tooltip);
            }
        }
    }
}