using System;
using System.Windows;

namespace CAIME.Windows
{
    /// <summary>
    /// Interaction logic for RenameSwatchWindow.xaml
    /// </summary>
    public partial class RenameSwatchWindow : Window
    {
        private readonly RenameSwatchViewModel _viewModel;

        public RenameSwatchWindow(RenameSwatchViewModel viewModel)
        {
            InitializeComponent();

            _viewModel  = viewModel;
            DataContext = viewModel;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.ConfirmRenameSwatch())
            {
                LoggerViewModel.Log($"Successfully renamed {_viewModel.OldSwatchName} to {_viewModel.NewSwatchName}.", LogLevel.Info);
                this.Close();
                Owner.Focus();
            }
            else
            {
                var errorMessage = $"An error occured, while trying to rename {_viewModel.OldSwatchName} to {_viewModel.NewSwatchName}";
                LoggerViewModel.Log(errorMessage, LogLevel.Error);
                MessageBox.Show(errorMessage, "Rename failed");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            Owner.Focus();
        }
    }
}
