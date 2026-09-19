using System;
using System.Windows;
using System.Windows.Controls;

namespace CAIME.Controls
{
    /// <summary>
    /// Interaction logic for RiversActionsControl.xaml
    /// </summary>
    public partial class RiversActionsControl : UserControl
    {
        public ActionsControlViewModel ViewModel { get; private set; }

        public RiversActionsControl()
        {
            InitializeComponent();

            ViewModel   = new ActionsControlViewModel();
            DataContext = ViewModel;

            ViewModel.ActiveSwatchChanged(LayerType.Rivers, -1);
        }

        private void Validate_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Validate();
        }
    }
}
