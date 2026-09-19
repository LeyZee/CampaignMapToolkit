using System.Windows;
using System.Windows.Input;
using CAIME.Painters;

namespace CAIME
{
    public enum ToolType
    {
        Pan = 0,
        Zoom,
        //Select,
        Brush,
        FloodFill,
        Eraser,
        BackImg,
        Line,
        ColorPicker,

        Total
    }

    /// <summary>
    /// Base class for a tool model
    /// </summary>
    public abstract class Tool : ObservableObject
    {
        /// <summary>
        /// Notify <see cref="ActiveToolChanged"/> subscribers that this tool's <see cref="IsActive"/> property has changed
        /// </summary>
        private bool raiseActive;

        private bool isEnabled;
        /// <summary>
        /// Is tool enabled?
        /// </summary>
        public bool IsEnabled
        {
            get
            {
                return isEnabled;
            }
            set
            {
                isEnabled = value;
                OnPropertyChanged(nameof(IsEnabled));
            }
        }

        private bool isActive;
        /// <summary>
        /// Is this tool currently active?
        /// </summary>
        public bool IsActive
        {
            get
            {
                return isActive;
            }
            set
            {
                isActive = value;
                OnPropertyChanged(nameof(IsActive));

                if (isActive && raiseActive)
                    ActiveToolChanged?.Invoke(this, null);

                raiseActive = true;
            }
        }

        private string toolTip;
        /// <summary>
        /// Text that appears when you hover over tool button
        /// </summary>
        public string ToolTip
        {
            get
            {
                return toolTip;
            }
            set
            {
                toolTip = value;
                OnPropertyChanged(nameof(ToolTip));
            }
        }
       
        /// <summary>
        /// Tool icon name
        /// </summary>
        public string IconPath { get; private set; }

        /// <summary>
        /// Tool type
        /// </summary>
        public ToolType Type { get; private set; }

        /// <summary>
        /// Tool hotkey
        /// </summary>
        public Key Hotkey { get; private set; }

        /// <summary>
        /// Event to notify that <see cref="IsActive"/> property has changed
        /// </summary>
        public event RoutedEventHandler ActiveToolChanged;

        public Tool(ToolType type, Key hotkey, bool isEnabled = true, bool isActive = false)
        {
            Type        = type;
            IconPath    = $@"Tools\{type}_icon.png";
            ToolTip     = $"{type} tool ({hotkey})";
            IsEnabled   = isEnabled;
            IsActive    = isActive;
            raiseActive = true;
            Hotkey      = hotkey;
        }

        /// <summary>
        /// Manually (in code) make this tool active (inactive)
        /// </summary>
        /// <param name="isActive">Should tool become active?</param>
        /// <param name="raiseEvent">Should notify that <see cref="IsActive"/> property changed?</param>
        public void SetActive(bool isActive, bool raiseEvent = false)
        {
            raiseActive = raiseEvent;
            IsActive = isActive;
        }
    }

    /// <summary>
    /// Base class for tools that can be used in the viewport.
    /// </summary>
    public abstract class ViewportTool : Tool
    {
        public ICommand Command { get; private set; }
        public Cursor   Cursor  { get; protected set; }

        public ViewportTool(ToolType type, Key hotkey) : base(type, hotkey)
        {

        }

        public void SetCommand(ViewportCommand command)
        {
            Command = command;
            command.Tool = this;
        }

        public abstract void OnLeftMouseDown(System.Windows.Point mousePos);
        public abstract void OnLeftMouseMove(ViewportToolParameters args);
        public abstract void OnLeftMouseUp(System.Windows.Point mousePos);
    }

    /// <summary>
    /// Base class for tools that can be used in the viewport.
    /// </summary>
    public abstract class ViewportPaintTool : ViewportTool
    {
        public IViewportPainter Painter { get; set; }

        public ViewportPaintTool(ToolType type, Key hotkey) : base(type, hotkey)
        {
        }

        public override void OnLeftMouseDown(System.Windows.Point mousePos)
        {
            var viewportCommand = Command as ViewportCommand;
            if (viewportCommand != null)
            {
                viewportCommand.Prepare(mousePos);
            }
        }

        public override void OnLeftMouseMove(ViewportToolParameters args)
        {
            var viewportCommand = Command as ViewportCommand;
            if (viewportCommand != null)
            {
                viewportCommand.Execute(args);
            }
        }

        public override void OnLeftMouseUp(System.Windows.Point mousePos)
        {
            var viewportCommand = Command as ViewportCommand;
            if (viewportCommand != null)
            {
                viewportCommand.Finish(mousePos);
            }
        }
    }

    /// <summary>
    /// Base class for tools that execute only once you click on the tool (like a button).
    /// This tool is never active due to it's one-time nature.
    /// </summary>
    public abstract class OneTimeTool : Tool
    {
        public OneTimeTool(ToolType type, Key hotkey) : base(type, hotkey)
        {

        }

        public abstract void ProcessClick(ViewportViewModel viewportVM);
    }

    public abstract class DefaultTool : Tool
    {
        public DefaultTool(ToolType type, Key hotkey) : base(type, hotkey)
        {

        }
    }

    public interface IBrushTool
    {
        void SetBrushSize(int size);
        int GetBrushSize();
        void SetViewportZoomScale(double zoomScale);
    }
}
