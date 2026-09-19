using System;
using System.Windows;
using System.Windows.Controls;

namespace CAIME.Controls
{
    /// <summary>
    /// Interaction logic for ImpassableActionsControl.xaml
    /// </summary>
    public partial class ImpassableActionsControl : UserControl
    {
        public ImpassableActionsControlViewModel ViewModel { get; private set; }

        public ImpassableActionsControl()
        {
            InitializeComponent();

            ViewModel   = new ImpassableActionsControlViewModel();
            DataContext = ViewModel;

            ViewModel.ActiveSwatchChanged(LayerType.Impassable, -1);
        }

        private void PlugHoles_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.PlugHolesImpassable();
        }

        private void Validate_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Validate();
        }
    }
}
