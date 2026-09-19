using System;
using System.Windows;
using System.Windows.Controls;

namespace CAIME.Controls
{
    /// <summary>
    /// Interaction logic for SprawlActionsControl.xaml
    /// </summary>
    public partial class SprawlActionsControl : UserControl
    {
        public SprawlActionsControlViewModel ViewModel { get; private set; }

        public SprawlActionsControl()
        {
            InitializeComponent();

            ViewModel   = new SprawlActionsControlViewModel();
            DataContext = ViewModel;

            ViewModel.ActiveSwatchChanged(LayerType.TownSprawl, -1);
        }

        private void AutoGenerate_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.AutoGenerateSprawl();
        }

        private void Validate_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Validate();
        }
    }
}
