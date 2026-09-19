using System;
using System.Windows;
using CAIME.ViewModels;

namespace CAIME.Windows
{
    /// <summary>
    /// Interaction logic for ChangeSwatchColourWindow.xaml
    /// </summary>
    public partial class ChangeSwatchColourWindow : Window
    {
        private readonly ChangeSwatchColourViewModel _viewModel;

        public ChangeSwatchColourWindow(ChangeSwatchColourViewModel viewModel)
        {
            InitializeComponent();

            _viewModel  = viewModel;
            DataContext = viewModel;

            colourPicker.SetDefaultColour(_viewModel.DefaultColour);
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.ChangeColour(colourPicker.ViewModel.SelectedColor))
            {
                this.Close();
                Owner.Focus();
            }
            else
            {
                MessageBox.Show("The selected colour is already used for another swatch in this layer!", "Failed to replace colour");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            Owner.Focus();
        }
    }
}
