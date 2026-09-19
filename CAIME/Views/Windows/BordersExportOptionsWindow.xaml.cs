using System;
using System.Windows;
using System.Windows.Input;

namespace CAIME
{
    /// <summary>
    /// Interaction logic for BordersExportOptionsWindow.xaml
    /// </summary>
    public partial class BordersExportOptionsWindow : Window
    {
        private BorderExportOptionsViewModel viewModel;
        private readonly ProjectManager _projectManager;

        public BordersExportOptionsWindow(ProjectManager projectManager)
        {
            viewModel = new BorderExportOptionsViewModel(projectManager.Project);

            InitializeComponent();

            DataContext = viewModel;
            _projectManager = projectManager;
            _projectManager.OnOpenProject += viewModel.Reset;
        }

        /// <summary>
        /// Show standard context menu on Right mouse button up
        /// </summary>
        private void Menu_MouseRightButtonUp(object sender, RoutedEventArgs e)
        {
            var point = PointToScreen(Mouse.GetPosition(this));
            SystemCommands.ShowSystemMenu(this, point);
        }

        /// <summary>
        /// Show standard context menu on Left mouse button down
        /// </summary>
        private void Menu_MouseLeftButtonDown(object sender, RoutedEventArgs e)
        {
            var point = WindowState == WindowState.Maximized ? new Point(0, 34) : new Point(Left, Top + 34);
            SystemCommands.ShowSystemMenu(this, point);
        }

        private void CloseClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CancelClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ResetClicked(object sender, RoutedEventArgs e)
        {
            viewModel.Reset(null, null);
        }

        private void ExportClicked(object sender, RoutedEventArgs e)
        {
            Close();
            _projectManager.ExportProcessedBordersData();
        }
    }
}
