using CAIME.Controls;
using CAIME.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace CAIME.Views.Controls.ActionsControls
{
    /// <summary>
    /// Interaction logic for TradeRoutesActionControl.xaml
    /// </summary>
    public partial class TradeRoutesActionControl : UserControl
    {
        public TradeRoutesActionsControlViewModel ViewModel { get; private set; }

        public TradeRoutesActionControl()
        {
            InitializeComponent();

            ViewModel = new TradeRoutesActionsControlViewModel();
            DataContext = ViewModel;
        }

        private void GenerateTradeRoutesFromRoadsButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.GenerateRoutesFromRoads();
        }
    }
}
