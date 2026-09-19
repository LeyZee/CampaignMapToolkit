using System.Windows;
using System.Windows.Controls;

namespace CAIME
{
    /// <summary>
    /// Interaction logic for Toolbar.xaml
    /// </summary>
    public partial class Toolbar : UserControl
    {
        public readonly ToolbarViewModel ViewModel = new ToolbarViewModel();

        public Toolbar()
        {
            InitializeComponent();
            DataContext = ViewModel;
        }
        /// <summary>
        /// Fires when user click on brush property button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BrushProperty_Click(object sender, RoutedEventArgs e)
        {
            var win = new Windows.BrushPropertyWindow(ViewModel)
            {
                Owner = Window.GetWindow(this)
            };

            win.Show();
        }
    }
}
