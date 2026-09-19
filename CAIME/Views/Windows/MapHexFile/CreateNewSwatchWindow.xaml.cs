using System;
using System.Windows;

namespace CAIME.Windows
{
    /// <summary>
    /// Interaction logic for CreateNewSwatchWindow.xaml
    /// </summary>
    public partial class CreateNewSwatchWindow : Window
    {
        private readonly CreateSwatchViewModel _viewModel;

        public CreateNewSwatchWindow(CreateSwatchViewModel viewModel)
        {
            InitializeComponent();

            _viewModel  = viewModel;
            DataContext = viewModel;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            //var colour = colourPicker.ViewModel.SelectedColor;
            var colour = System.Windows.Media.Colors.Black;

            var result = _viewModel.ConfirmCreateNewSwatch(colour);
            if (result == CreateSwatchViewModel.CreateResult.Success)
            {
                LoggerViewModel.Log($"Successfully created {_viewModel.NewSwatchName}.", LogLevel.Info);
                this.Close();
                Owner.Focus();
            }
            else
            if (result == CreateSwatchViewModel.CreateResult.CreateFailed)
            {
                var errorMessage = $"An error occured, while trying to create ({_viewModel.NewSwatchName}) swatch.";
                LoggerViewModel.Log(errorMessage, LogLevel.Error);
                MessageBox.Show(errorMessage, "Create failed");
            }
            else
            if (result == CreateSwatchViewModel.CreateResult.ColourExists)
            {
                var errorMessage = $"The selected colour is already used for another swatch!";
                LoggerViewModel.Log(errorMessage, LogLevel.Error);
                MessageBox.Show(errorMessage, "Create failed");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            Owner.Focus();
        }

        private void RandomColourButton_Click(object sender, RoutedEventArgs e)
        {
            // _viewModel.PickRandomColour(colourPicker.ViewModel);
        }
    }
}
