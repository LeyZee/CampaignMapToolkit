using System;
using System.Windows;
using CAIME.ViewModels;

namespace CAIME.Windows
{
    /// <summary>
    /// Interaction logic for MapDataEditor.xaml
    /// </summary>
    public partial class MapDataEditor : Window
    {
        private readonly MapDataEditorViewModel viewModel;

        public MapDataEditor(Project project)
        {
            InitializeComponent();

            viewModel = new MapDataEditorViewModel(project);
            DataContext = viewModel;

            settings.ViewModel.OnMapDataFileOpened  += viewModel.OnMapDataFileOpened;
            settings.ViewModel.OnApplyConfig        += viewModel.OnApplyConfig;
            settings.ViewModel.OnCreateConfig       += viewModel.OnCreateConfig;
        }

        private void SaveChanges_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel.SaveChanges() == false)
            {
                return;
            }

            var res = MessageBox.Show($"The file has been successfully saved to {viewModel.Filename}.\nClose this window?", "Close Map Data Editor window?", MessageBoxButton.YesNo);
            if (res == MessageBoxResult.Yes)
            {
                Close();
                Owner.Focus();
            }
        }
    }
}
