using System;
using System.Windows;
using CAIME.ViewModels;

namespace CAIME.Windows
{
    /// <summary>
    /// Interaction logic for RenameCampaignMapWindow.xaml
    /// </summary>
    public partial class RenameCampaignMapWindow : Window
    {
        private readonly RenameCampaignMapViewModel _viewModel;

        public RenameCampaignMapWindow(ProjectManager projectManager)
        {
            InitializeComponent();

            _viewModel = new RenameCampaignMapViewModel(projectManager);
            DataContext = _viewModel;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.ConfirmRenameMap())
            {
                this.Close();
                Owner.Focus();
            }
            else
            {
                LoggerViewModel.Log($"An error occured, while trying to rename {_viewModel.OldMapName} to {_viewModel.NewMapName}", LogLevel.ErrorMessageBox);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            Owner.Focus();
        }
    }
}
