using System.Windows.Input;
using System.Windows.Media;

namespace CAIME
{
    public abstract class PaintTool : ViewportPaintTool, IBrushTool
    {
        /// <summary>
        /// On-screen diameter, in pixels, of the circle drawn for the paint cursor at brush
        /// size 1 and 100% zoom - that is, at the fully zoomed out view a map opens with.
        /// Tweak this to make the cursor bigger or smaller. Brush size and zoom both scale
        /// it linearly from here.
        /// </summary>
        private const int CURSOR_DIAMETER = 1;

        /// <summary>
        /// Smallest and largest diameter the cursor is allowed to reach once the zoom scale is
        /// applied. The lower bound keeps it visible, the upper bound keeps it a cursor.
        /// </summary>
        private const int MIN_CURSOR_DIAMETER = 6;
        private const int MAX_CURSOR_DIAMETER = 256;

        public int BrushSize { get; private set; }

        private double viewportZoomScale;
        private int cursorDiameter;

        public PaintTool(ToolType toolType, System.Windows.Input.Key hotkey) : base(toolType, hotkey)
        {
            BrushSize = 1;
            viewportZoomScale = 1.0;
            cursorDiameter = 0;

            // Tools are constructed while the editor's XAML loads, before there is anything
            // for a cursor query to resolve against, so the first build just seeds the cursor.
            UpdateCursor(refreshActiveCursor: false);
        }

        public void SetBrushSize(int size)
        {
            BrushSize = size;
            UpdateCursor();
        }

        public int GetBrushSize()
        {
            return BrushSize;
        }

        /// <summary>
        /// Records how far the viewport is zoomed in, where 1.0 is the framing a map opens with.
        /// </summary>
        public void SetViewportZoomScale(double zoomScale)
        {
            viewportZoomScale = zoomScale;
            UpdateCursor();
        }

        /// <summary>
        /// Rebuilds the cursor for the current brush size and zoom level. Both scale the
        /// diameter linearly. The cursor is only rebuilt when the rounded on-screen diameter
        /// actually changes, so a zoom gesture does not rebuild it per frame.
        /// </summary>
        private void UpdateCursor(bool refreshActiveCursor = true)
        {
            var diameter = (int)System.Math.Round(CURSOR_DIAMETER * BrushSize * viewportZoomScale);
            diameter = MathHelper.Clamp(diameter, MIN_CURSOR_DIAMETER, MAX_CURSOR_DIAMETER);

            if (diameter == cursorDiameter)
            {
                return;
            }

            cursorDiameter = diameter;
            Cursor = CursorHelper.CreateCursor(diameter, new SolidColorBrush(Colors.Black));

            // QueryCursor is only raised again by mouse movement, so a wheel zoom or a drag of
            // the brush size slider would otherwise keep showing the previous size until the
            // mouse next moved.
            if (refreshActiveCursor)
            {
                Mouse.UpdateCursor();
            }
        }
    }

    public class BrushTool : PaintTool
    {
        public BrushTool() : base(ToolType.Brush, System.Windows.Input.Key.B)
        {}
    }

    public class EraserTool : PaintTool
    {
        public EraserTool() : base(ToolType.Eraser, System.Windows.Input.Key.E)
        {}
    }
}
