using System;
using System.Windows;
using System.Windows.Controls;
using CAIME.ViewModels;

namespace CAIME.Views
{
    /// <summary>
    /// Interaction logic for MapDataSettings.xaml
    /// </summary>
    public partial class MapDataSettings : UserControl
    {
        public MapDataSettingsViewModel ViewModel { get; private set; }

        public MapDataSettings()
        {
            InitializeComponent();

            ViewModel = new MapDataSettingsViewModel();
            DataContext = ViewModel;
        }

        private void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "Map data ESF file | map_data.esf",
            };

            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ViewModel.OpenFile(ofd.FileName);
            }
        }

        private void ApplyConfig_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "Map data config file (*.xml) | *.xml",
            };

            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ViewModel.ApplyConfig(ofd.FileName);
            }
        }

        private void CreateConfig_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new System.Windows.Forms.SaveFileDialog
            {
                Filter = "Map data config file (*.xml) | *.xml",
            };

            if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ViewModel.CreateConfig(sfd.FileName);
            }
        }
    }
}
