using System;
using System.Windows;
using CAIME.Painters;

namespace CAIME
{
    public class ZoomTool : ViewportTool
    {
        public int Delta { get; set; }

        public ZoomTool() : base(ToolType.Zoom, System.Windows.Input.Key.Z)
        {
            Delta = 120;
        }

        public override void OnLeftMouseDown(Point mousePos)
        {
        }

        public override void OnLeftMouseMove(ViewportToolParameters args)
        {
        }

        public override void OnLeftMouseUp(Point mousePos)
        {
        }
    }
}
