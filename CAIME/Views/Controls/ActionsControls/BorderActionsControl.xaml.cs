using System;
using System.Windows;
using System.Windows.Controls;

namespace CAIME.Controls
{
    /// <summary>
    /// Interaction logic for BorderActionsControl.xaml
    /// </summary>
    public partial class BorderActionsControl : UserControl
    {
        public BorderActionsControlViewModel ViewModel { get; private set; }

        public BorderActionsControl()
        {
            InitializeComponent();

            ViewModel   = new BorderActionsControlViewModel();
            DataContext = ViewModel;
        }

        private void AutoGenerate_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.AutoGenerateBorders();
        }
    }
}
