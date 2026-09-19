using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

namespace CAIME.Windows
{
    public partial class TextViewerWindow : Window
    {
        private readonly Func<Task<string>> _contentLoader;
        private readonly Func<Task<IEnumerable<Inline>>> _inlinesLoader;

        private TextViewerWindow(string caption, string windowTitle,
            Func<Task<string>> contentLoader = null,
            Func<Task<IEnumerable<Inline>>> inlinesLoader = null)
        {
            InitializeComponent();
            Title = windowTitle;
            HeaderText.Text = caption;
            _contentLoader = contentLoader;
            _inlinesLoader = inlinesLoader;
            CloseButton.Click += (s, e) => Close();
            Loaded += async (s, e) => await LoadContentAsync();
        }

        public static TextViewerWindow FromContentLoader(string caption, Func<Task<string>> loader, string windowTitle = null) =>
            new TextViewerWindow(caption, windowTitle ?? caption, contentLoader: loader);

        public static TextViewerWindow FromFile(string caption, string filePath, string windowTitle = null) =>
            new TextViewerWindow(
                caption,
                windowTitle ?? caption,
                contentLoader: () => Task.Run(() => File.ReadAllText(filePath)));

        public static TextViewerWindow FromText(string caption, string text, string windowTitle = null) =>
            new TextViewerWindow(
                caption,
                windowTitle ?? caption,
                contentLoader: () => Task.FromResult(text));

        public static TextViewerWindow FromInlines(string caption, Func<Task<IEnumerable<Inline>>> loader, string windowTitle = null) =>
            new TextViewerWindow(caption, windowTitle ?? caption, inlinesLoader: loader);

        public TextViewerWindow WithAcceptButton(string label, Action onAccepted)
        {
            AcceptButton.Content = label;
            AcceptButton.Click += (s, e) => onAccepted();
            AcceptButton.Visibility = Visibility.Visible;
            return this;
        }

        public TextViewerWindow WithAcceptedLabel()
        {
            AcceptedLabel.Visibility = Visibility.Visible;
            return this;
        }

        private async Task LoadContentAsync()
        {
            try
            {
                if (_inlinesLoader != null)
                {
                    var inlines = await _inlinesLoader();
                    ContentText.Inlines.Clear();
                    foreach (var inline in inlines)
                        ContentText.Inlines.Add(inline);
                }
                else if (_contentLoader != null)
                {
                    var content = await _contentLoader();
                    ContentText.Text = string.IsNullOrWhiteSpace(content) ? "(No content)" : content;
                }
            }
            catch (Exception ex)
            {
                ContentText.Text = $"Failed to load content.\n{ex.Message}";
            }
        }
    }
}
