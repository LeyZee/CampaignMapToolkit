using System;
using System.Collections.Generic;
using System.Windows;
using CAIME.ViewModels;

namespace CAIME.Views.Windows
{
    public partial class ImportLayerWindow : Window
    {
        private static ImportLayerViewModel viewModel;

        public ImportLayerWindow(Project project, List<LayerType> layers)
        {
            InitializeComponent();

            viewModel = new ImportLayerViewModel(project, layers);
            DataContext = viewModel;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            viewModel.Browse();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            viewModel.Confirm();
            this.Close();
            Owner.Focus();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            Owner.Focus();
        }
    }
}
