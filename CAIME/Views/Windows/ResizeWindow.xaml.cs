using System;
using System.Windows;

namespace CAIME.Views.Windows
{
    /// <summary>
    /// Interaction logic for ResizeWindow.xaml
    /// </summary>
    public partial class ResizeWindow : Window
    {
        private readonly ResizeViewModel resizeVM;

        public ResizeWindow(ProjectManager ProjectManager)
        {
            InitializeComponent();
            resizeVM = new ResizeViewModel(ProjectManager);
            DataContext = resizeVM;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (resizeVM.ResizeProject())
            {
                Close();
                Owner.Focus();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            Owner.Focus();
        }
    }
}
