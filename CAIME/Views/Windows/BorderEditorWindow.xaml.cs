using System;
using System.Windows;
using System.Windows.Input;

namespace CAIME
{
    /// <summary>
    /// Interaction logic for BorderEditorWindow.xaml
    /// </summary>
    public partial class BorderEditorWindow : Window
    {
        private readonly BorderEditorViewModel viewModel;

        public BorderEditorWindow(Project project)
        {
            InitializeComponent();
            viewModel = new BorderEditorViewModel(project);

            visualizationSP.DataContext = viewModel.borderVisualizationVM;
            visulizationImage.DataContext = viewModel.borderVisualizationVM;
            DataContext = viewModel;
        }
        private void ChangeFacingClicked(object sender, RoutedEventArgs e)
        {
            viewModel.ChangeFacing();
        }
        private void AddPartClicked(object sender, RoutedEventArgs e)
        {
            viewModel.AddPart();
        }
        private void DeletePartClicked(object sender, RoutedEventArgs e)
        {
            viewModel.DeletePart();
        }
        private void InsertPointClicked(object sender, RoutedEventArgs e)
        {
            viewModel.InsertPoint();
        }
        private void DeletePointClicked(object sender, RoutedEventArgs e)
        {
            viewModel.DeletePoint();
        }
        private void UnselectPointClicked(object sender, RoutedEventArgs e)
        {
            viewModel.UnselectPoint();
        }
        private void LoadClicked(object sender, RoutedEventArgs e)
        {
            viewModel.Load();
        }
        private void SaveClicked(object sender, RoutedEventArgs e)
        {
            viewModel.Save();
        }

        private void IncreaseXClicked(object sender, RoutedEventArgs e)
        {
            viewModel.ChangeCoord("x", 1);
        }
        private void DecreaseXClicked(object sender, RoutedEventArgs e)
        {
            viewModel.ChangeCoord("x", -1);
        }
        private void IncreaseYClicked(object sender, RoutedEventArgs e)
        {
            viewModel.ChangeCoord("y", 1);
        }
        private void DecreaseYClicked(object sender, RoutedEventArgs e)
        {
            viewModel.ChangeCoord("y", -1);
        }

        //Visualization
        private void ChangeBorderColour(object sender, RoutedEventArgs e)
        {
            viewModel.borderVisualizationVM.ChangeBorderColour();
        }
        private void ChangeBorderPColour(object sender, RoutedEventArgs e)
        {
            viewModel.borderVisualizationVM.ChangeBorderPColour();
        }
        private void ChangeOtherPartColour(object sender, RoutedEventArgs e)
        {
            viewModel.borderVisualizationVM.ChangeOtherPartColour();
        }
        private void ChangeComplementaryColour(object sender, RoutedEventArgs e)
        {
            viewModel.borderVisualizationVM.ChangeComplementaryColour();
        }
        private void ChangeRegFromColour(object sender, RoutedEventArgs e)
        {
            viewModel.borderVisualizationVM.ChangeRegFromColour();
        }
        private void ChangeRegToColour(object sender, RoutedEventArgs e)
        {
            viewModel.borderVisualizationVM.ChangeRegToColour();
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

        /// <summary>
        /// Minimise click handler
        /// </summary>
        private void MinimiseClicked(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// Maximise click handler
        /// </summary>
        private void MaximiseClicked(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        /// <summary>
        /// Close click handler
        /// </summary>
        private void CloseClicked(object sender, RoutedEventArgs e)
        {
            viewModel.borderVisualizationVM.SetActive(false);
            Close();
        }
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            viewModel.borderVisualizationVM.SetActive(false);
        }

        private void SelectFromClicked(object sender, RoutedEventArgs e)
        {
            viewModel.SelectFrom();
        }
        private void ClearFromClicked(object sender, RoutedEventArgs e)
        {
            viewModel.ClearFrom();
        }
        private void SelectToClicked(object sender, RoutedEventArgs e)
        {
            viewModel.SelectTo();
        }
        private void ClearToClicked(object sender, RoutedEventArgs e)
        {
            viewModel.ClearTo();
        }
        private void RefreshVisClicked(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("TODO (is it neccessary?)");
        }
        private void ShowManualClicked(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("TODO");
        }

        private void InvertClicked(object sender, RoutedEventArgs e)
        {
            viewModel.Invert();
        }


    }
}
