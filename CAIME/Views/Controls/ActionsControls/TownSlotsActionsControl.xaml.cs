using System;
using System.Windows;
using System.Windows.Controls;

namespace CAIME.Controls
{
    /// <summary>
    /// Interaction logic for TownSlotsActionsControl.xaml
    /// </summary>
    public partial class TownSlotsActionsControl : UserControl
    {
        public ActionsControlViewModel ViewModel { get; private set; }

        public TownSlotsActionsControl()
        {
            InitializeComponent();

            ViewModel   = new ActionsControlViewModel();
            DataContext = ViewModel;

            ViewModel.ActiveSwatchChanged(LayerType.TownSlots, -1);
        }

        private void Validate_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Validate();
        }
    }
}
