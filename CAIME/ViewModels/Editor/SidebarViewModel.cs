using CAIME.ViewModels;

namespace CAIME
{
    public class SidebarViewModel : BaseViewModel
    {
        public ToolbarViewModel             ToolbarVM;
        public readonly SwatchesViewModel   SwatchesVM;
        public readonly MinimapViewModel    MinimapVM;
        public readonly LayersViewModel     LayersVM;
        public readonly ActionsViewModel    ActionsVM;

        public SidebarViewModel()
        {
            SwatchesVM  = new SwatchesViewModel();
            MinimapVM   = new MinimapViewModel();
            LayersVM    = new LayersViewModel();
            ActionsVM   = new ActionsViewModel();
        }

        public void SetViewportViewModel(ViewportViewModel viewportVM)
        {
            MinimapVM.SetViewportViewModel(viewportVM);
        }
    }
}
