using Verse;

namespace LOSOverlay
{

    public class LOSOverlayMapComponent : MapComponent
    {
        private Thing _lastSelected;
        private IntVec3 _lastPosition = IntVec3.Invalid;

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

            OverlayRenderer.DrawOverlay();
        }

        public override void MapComponentOnGUI()
        {
            base.MapComponentOnGUI();
            if (!OverlayRenderer.IsActive) return;
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