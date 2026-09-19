using System.Windows;
using System.Windows.Controls;

namespace CAIME
{
    /// <summary>
    /// Interaction logic for Logger.xaml
    /// </summary>
    public partial class LoggerRibbon : UserControl
    {
        private readonly Windows.LoggerWindow loggerWindow = new Windows.LoggerWindow();
        
        public LoggerRibbon()
        {
            InitializeComponent();
            DataContext = LoggerViewModel.Instance;

            loggerWindow.Closing += LoggerWindow_Closing;
        }

        private void LoggerWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            loggerWindow.Visibility = Visibility.Hidden;
        }

        private void ShowMessagesStack_Click(object sender, RoutedEventArgs e)
        {
            if (loggerWindow.IsVisible == false)
            {
                loggerWindow.Show();
            }
            else
            {
                loggerWindow.Focus();
            }
        }
    }
}
