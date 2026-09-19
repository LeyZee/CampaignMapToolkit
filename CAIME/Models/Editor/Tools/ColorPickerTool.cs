using System;
using System.Windows;
using CAIME.Painters;

namespace CAIME
{
    public class ColorPickerTool : ViewportTool
    {
        public ColorPickerTool() : base(ToolType.ColorPicker, System.Windows.Input.Key.P)
        {

        }

        public override void OnLeftMouseDown(Point mousePos)
        {
        }

        public override void OnLeftMouseMove(ViewportToolParameters args)
        {
            var viewportCommand = Command as ViewportCommand;
            if (viewportCommand != null)
            {
                viewportCommand.Execute(args);
            }
        }

        public override void OnLeftMouseUp(Point mousePos)
        {
        }
    }
}
