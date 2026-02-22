using Verse;

namespace LOSOverlay
{
    public class LOSOverlayMapComponent : MapComponent
    {
        private Thing _lastSelected;

        public LOSOverlayMapComponent(Map map) : base(map) { }

        public override void MapComponentUpdate()
        {
            base.MapComponentUpdate();
            var current = Find.Selector.SingleSelectedThing;
            if (current != _lastSelected)
            {
                _lastSelected = current;
                Gizmo_LOSMode.OnSelectionChanged(current);
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
                var rect = new UnityEngine.Rect(mousePos.x + 15, mousePos.y + 15, 250, 30);
                Widgets.Label(rect, tooltip);
            }
        }
    }
}