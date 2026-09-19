using System;
using System.Windows;
using System.Windows.Controls;

namespace CAIME.Controls
{
    /// <summary>
    /// Interaction logic for SwatchControl.xaml
    /// </summary>
    public partial class ActionsControl : UserControl
    {
        public ActionsControlViewModel ViewModel { get; private set; }

        public ActionsControl()
        {
            InitializeComponent();

            ViewModel   = new ActionsControlViewModel();
            DataContext = ViewModel;
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

        private void Validate_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Validate();
        }
    }
}
