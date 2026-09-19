using System;
using System.Windows;
using System.Windows.Controls;

namespace CAIME.Controls
{
    /// <summary>
    /// Interaction logic for BridgesActionsControl.xaml
    /// </summary>
    public partial class BridgesActionsControl : UserControl
    {
        public ActionsControlViewModel ViewModel { get; private set; }

        public BridgesActionsControl()
        {
            InitializeComponent();

            ViewModel   = new ActionsControlViewModel();
            DataContext = ViewModel;

            ViewModel.ActiveSwatchChanged(LayerType.Bridges, -1);
        }

        private void Validate_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Validate();
        }
    }
}
