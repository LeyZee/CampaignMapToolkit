using System;
using System.Windows;
using System.Windows.Controls;

namespace CAIME.Controls
{
    /// <summary>
    /// Interaction logic for SwatchControl.xaml
    /// </summary>
    public partial class GroundTypeActionsControl : UserControl
    {
        public GroundTypeActionsControlViewModel ViewModel { get; private set; }

        public GroundTypeActionsControl()
        {
            InitializeComponent();

            ViewModel   = new GroundTypeActionsControlViewModel();
            DataContext = ViewModel;

            // Ground Types is the default active layer on project load, before any swatch-selection
            // event fires to tell the ViewModel that - without this it defaults to Impassable (0).
            ViewModel.ActiveSwatchChanged(LayerType.GroundTypes, -1);
        }

        private void CreateNewSwatch_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.CreateNewSwatch();
        }

        private void RenameSwatch_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.RenameSwatch();
        }

        private void RemoveSwatch_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.RemoveSwatch();
        }

        private void ChangeSwatchColour_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ChangeSwatchColour();
        }

        private void CleanupSwatches_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.CleanupSwatches();
        }

        private void AutoGenerate_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.AutoGenerateGroundType();
        }

        private void Validate_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Validate();
        }
    }
}
