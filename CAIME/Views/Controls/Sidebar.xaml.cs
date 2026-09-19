using System.Windows;
using System.Windows.Controls;

namespace CAIME
{
    /// <summary>
    /// Interaction logic for PropertiesControl.xaml
    /// </summary>
    public partial class Sidebar : UserControl
    {
        public readonly SidebarViewModel ViewModel = new SidebarViewModel();

        public Sidebar()
        {
            InitializeComponent();

            DataContext             = ViewModel;
            swatches.DataContext    = ViewModel.SwatchesVM;
            minimap.DataContext     = ViewModel.MinimapVM;
            layers.DataContext      = ViewModel.LayersVM;
            actions.DataContext     = ViewModel.ActionsVM;
        }

        private void Canvas_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(sender as IInputElement);
            ViewModel.MinimapVM.MoveViewFrame(pos);
        }

        private void Canvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(sender as IInputElement);
                ViewModel.MinimapVM.MoveViewFrame(pos);
            }
        }

        private void Canvas_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            ViewModel.MinimapVM.ScaleViewFrame(e.Delta);
            e.Handled = true;
        }
    }
}
