using System;
using System.Windows;
using System.Windows.Controls;

namespace CAIME.Controls
{
    /// <summary>
    /// Interaction logic for RoadsActionsControl.xaml
    /// </summary>
    public partial class RoadsActionsControl : UserControl
    {
        public ActionsControlViewModel ViewModel { get; private set; }

        public RoadsActionsControl()
        {
            InitializeComponent();

            ViewModel   = new ActionsControlViewModel();
            DataContext = ViewModel;

            ViewModel.ActiveSwatchChanged(LayerType.Roads, -1);
        }

        private void Validate_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Validate();
        }
    }
}
