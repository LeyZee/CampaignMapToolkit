using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace CAIME.Views.Windows
{
    /// <summary>
    /// Interaction logic for ExportLayersWindow.xaml
    /// </summary>
    public partial class ExportLayersWindow : Window
    {
        public ProjectManager ProjectManager { get; set; }
        private uint exportMask;

        private readonly List<string> exportModes = new List<string>()
        {
            "To image",
            "To binary"
        };

        public ExportLayersWindow()
        {
            InitializeComponent();

            this.cbNogo.Tag          = (int)LayerType.Impassable;
            this.cbRoutes.Tag        = (int)LayerType.TradeRoutes;
            this.cbRoads.Tag         = (int)LayerType.Roads;
            this.cbSlots.Tag         = (int)LayerType.TownSlots;
            this.cbSprawl.Tag        = (int)LayerType.TownSprawl;
            this.cbBridges.Tag       = (int)LayerType.Bridges;
            this.cbBeaches.Tag       = (int)LayerType.Beaches;
            this.cbRegionBorders.Tag = (int)LayerType.RegionBorders;
            this.cbRegions.Tag       = (int)LayerType.Regions;
            this.cbAttritions.Tag    = (int)LayerType.Attritions;
            this.cbClimates.Tag      = (int)LayerType.Climates;
            this.cbRivers.Tag        = (int)LayerType.Rivers;
            this.cbGrounds.Tag       = (int)LayerType.GroundTypes;

            ExportModeComboBox.ItemsSource = exportModes;
            ExportModeComboBox.SelectedIndex = 0;
        }

        private void LayerExportCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if ((sender is CheckBox) == false)
            {
                return;
            }

            var checkBox = sender as CheckBox;
            var layerTag = checkBox.Tag.ToString();

            if (checkBox.IsChecked.HasValue)
            {
                var bit = checkBox.IsChecked.Value ? 1 : 0;

                if (int.TryParse(layerTag, out int checkBoxIndex))
                {
                    exportMask |= (uint)(bit << checkBoxIndex);
                }
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.exportMask > 0)
            {
                LayerExportMode exportMode;

                if (ExportModeComboBox.SelectedIndex == 0)
                {
                    exportMode = LayerExportMode.ToImage;
                }
                else
                {
                    exportMode = LayerExportMode.ToBinary;
                }

                ProjectManager.ExportLayers(exportMode, this.exportMask);
                var res = MessageBox.Show("Successfully exported the selected layers!");
                if (res == MessageBoxResult.OK)
                {
                    this.Close();
                    Owner.Focus();
                }
            }
            else
            {
                var msg = "No layers have been selected for the export!";
                MessageBox.Show(msg, "Warning!");
                LoggerViewModel.Log(msg, LogLevel.Info);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            var choice = MessageBox.Show("Are you sure you want to close this window?", "Confirmation", MessageBoxButton.YesNo);
            if (choice == MessageBoxResult.Yes)
            {
                this.Close();
                Owner.Focus();
            }
        }
    }
}
