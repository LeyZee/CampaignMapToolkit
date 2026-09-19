using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CAIME.Windows
{
    /// <summary>
    /// Interaction logic for LoggerWindow.xaml
    /// </summary>
    public partial class LoggerWindow : Window
    {
        private bool autoScroll = true;

        public LoggerWindow()
        {
            InitializeComponent();
            DataContext = LoggerViewModel.Instance;

            ((INotifyCollectionChanged)logsContainerListBox.Items).CollectionChanged += Logs_CollectionChanged;
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            var scroll = sender as ScrollViewer;

            if (e.ExtentHeightChange == 0)
            {
                if (scroll.VerticalOffset == scroll.ScrollableHeight)
                {
                    autoScroll = true;
                }
                else
                {
                    autoScroll = false;
                }
            }

            if (autoScroll && e.ExtentHeightChange != 0)
            {
                scroll.ScrollToVerticalOffset(scroll.ExtentHeight);
            }
        }

        private void Logs_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                logsContainerListBox.ScrollIntoView(e.NewItems[0]);
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            LoggerViewModel.Clear();
        }

        private void CopySelected_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            CopyItemsToClipboard(logsContainerListBox.SelectedItems.Cast<MessageItem>());
        }

        private void CopySelected_Click(object sender, RoutedEventArgs e)
        {
            CopyItemsToClipboard(logsContainerListBox.SelectedItems.Cast<MessageItem>());
        }

        private void CopyAll_Click(object sender, RoutedEventArgs e)
        {
            CopyItemsToClipboard(LoggerViewModel.Instance.MessagesStack);
        }

        private static void CopyItemsToClipboard(IEnumerable<MessageItem> items)
        {
            var sb = new StringBuilder();
            foreach (var item in items)
            {
                sb.AppendLine($"[{item.Time}] {item.Message}");
            }

            if (sb.Length == 0)
            {
                return;
            }

            try
            {
                Clipboard.SetText(sb.ToString());
            }
            catch (Exception ex)
            {
                // The clipboard is a shared resource - another process holding its lock makes
                // SetText throw. Never take the app down over a failed copy.
                LoggerViewModel.Log($"Could not copy to the clipboard: {ex.Message}", LogLevel.Warning);
            }
        }
    }
}
