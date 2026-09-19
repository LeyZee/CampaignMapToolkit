using System;
using System.Windows;
using System.Windows.Input;

namespace CAIME
{
    /// <summary>
    /// Interaction logic for RegionSelectionWindow.xaml
    /// </summary>
    public partial class RegionSelectionWindow : Window
    {
        private RegionSelectionViewModel viewModel;
        public RegionSelectionViewModel ViewModel
        {
            get => viewModel;
            set
            {
                viewModel = value;
                DataContext = viewModel;
                viewModel.Visibility = Visibility.Hidden;
            }
        }

        public RegionSelectionWindow()
        {
            InitializeComponent();
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            ViewModel.Visibility = Visibility.Hidden;
        }

        private void RegionDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ViewModel.Confirmed();
        }

        private void ConfirmClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.Confirmed();
        }
    }
}
