using System;
using System.Windows;
using System.Windows.Controls;

namespace CAIME.Controls
{
    /// <summary>
    /// Interaction logic for BeachesActionsControl.xaml
    /// </summary>
    public partial class BeachesActionsControl : UserControl
    {
        public ActionsControlViewModel ViewModel { get; private set; }

        public BeachesActionsControl()
        {
            InitializeComponent();

            ViewModel   = new ActionsControlViewModel();
            DataContext = ViewModel;

            ViewModel.ActiveSwatchChanged(LayerType.Beaches, -1);
        }

        private void Validate_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Validate();
        }
    }
}
