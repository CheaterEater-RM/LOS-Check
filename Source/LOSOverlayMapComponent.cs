using Verse;

namespace LOSOverlay
{

    public class LOSOverlayMapComponent : MapComponent
    {
        private Thing _lastSelected;
        private IntVec3 _lastPosition = IntVec3.Invalid;

        // Refresh the cover-map overlay every N ticks to pick up terrain changes.
        // 300 ticks ≈ 5 real-seconds at 1× speed (60 ticks/s).
        private const int COVER_MAP_REFRESH_INTERVAL = 300;
        private int _coverMapTick;

        public LOSOverlayMapComponent(Map map) : base(map) { }

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

            OverlayRenderer.DrawCoverMap();
            OverlayRenderer.DrawOverlay();
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
                var mousePos = UnityEngine.Event.current.mousePosition;
                var rect = new UnityEngine.Rect(mousePos.x + 15, mousePos.y + 15, 300, 40);
                Widgets.Label(rect, tooltip);
            }
        }
    }
}