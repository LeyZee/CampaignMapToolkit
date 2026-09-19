using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CAIME.Tools;

namespace CAIME
{
    public class ToolbarViewModel : BaseViewModel
    {
        public ObservableCollection<Tool> Tools { get; private set; }
        public int ActiveToolIndex { get; private set; }

        public ToolbarViewModel()
        {
            Tools = new ObservableCollection<Tool>
            {
                new PanTool(),
                new ZoomTool(),
                new BrushTool(),
                new FloodFillTool(),
                new EraserTool(),
                new BackImageTool(this),
                new LineTool(),
                new ColorPickerTool(),
            };

            Tools[(int)ToolType.Pan].SetActive(true);
        }

        public void UpdateActiveTool(Tool tool)
        {
            var oldTool = Tools[ActiveToolIndex];
            if (oldTool is ViewportPaintTool)
            {
                (oldTool as ViewportPaintTool).Painter.OnDeactivated();
            }

            ActiveToolIndex = (int)tool.Type;

            if (tool is ViewportPaintTool)
            {
                (tool as ViewportPaintTool).Painter.OnActivated();
            }
        }

        public void SetActiveTool(ToolType type)
        {
            Tools[(int)type].SetActive(true, raiseEvent: true);
            ActiveToolIndex = (int)type;
        }

        public bool SetActiveToolHotkey(Key[] keys)
        {
            if (keys.Length != 1)
            {
                return false;
            }

            foreach (var tool in Tools)
            {
                if (tool.Hotkey == keys[0])
                {
                    SetActiveTool(tool.Type);
                    return true;
                }
            }

            return true;
        }

        public Tool GetActiveTool()
        {
            return Tools[ActiveToolIndex];
        }

        public void Reset()
        {
            Tools[(int)ToolType.Pan].SetActive(true, raiseEvent: true);
        }

        public Tool GetTool(ToolType type)
        {
            return Tools[(int)type];
        }

        /// <summary>
        /// Change brush properties
        /// </summary>
        /// <param name="brushSize"></param>
        public void ChangeBrushProperties(int brushSize)
        {
            var activeTool = GetActiveTool();
            if (activeTool is IBrushTool)
            {
                (activeTool as IBrushTool).SetBrushSize(brushSize);
            }
        }

        public int? GetBrushSize()
        {
            var activeTool = GetActiveTool();
            if (activeTool is IBrushTool)
            {
                return (activeTool as IBrushTool).GetBrushSize();
            }

            return null;
        }
    }
}
