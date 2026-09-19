using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Markdig;

namespace CAIME.Windows
{
    /// <summary>
    /// A small markdown "browser" for the bundled user guides. It renders a markdown
    /// file (starting with the index, <see cref="AppWindowGuides.IndexFileName"/>) as a
    /// WPF <see cref="FlowDocument"/> via Markdig.Wpf, lets the user click links to other
    /// guides, and keeps a Back/Forward history.
    /// </summary>
    public partial class UserGuidesWindow : Window
    {
        private static readonly MarkdownPipeline Pipeline =
            Markdig.Wpf.MarkdownExtensions
                .UseSupportedExtensions(new MarkdownPipelineBuilder())
                .Build();

        // The guides render in a dark theme that matches the surrounding application.
        // The page background and body-text colour are set here; element styles that
        // would otherwise carry light-theme colours (headings, code, links, etc.) are
        // overridden in XAML (see Window.Resources in UserGuidesWindow.xaml).
        private static readonly Brush DocBackground = Freeze(Color.FromRgb(0x1E, 0x1E, 0x1E));
        private static readonly Brush DocForeground = Freeze(Color.FromRgb(0xDC, 0xDC, 0xDC));
        private static readonly FontFamily DocFont = new FontFamily("Segoe UI");
        private const double DocFontSize = 14.0;

        // Navigation history of guide file names. _index points at the current entry.
        private readonly List<string> _history = new List<string>();
        private int _index = -1;

        // Cached inner scroll viewer of the FlowDocumentScrollViewer template.
        private ScrollViewer _scrollViewer;

        public UserGuidesWindow()
        {
            InitializeComponent();

            // Intercept markdown link clicks so they navigate within this window
            // (for .md guides) instead of opening a browser.
            CommandBindings.Add(new CommandBinding(
                Markdig.Wpf.Commands.Hyperlink,
                OnHyperlinkClicked,
                (s, e) => e.CanExecute = true));

            // FlowDocumentScrollViewer scrolls only a couple of pixels per wheel notch
            // by default; drive its scroll viewer directly for a normal scroll speed.
            DocViewer.PreviewMouseWheel += OnPreviewMouseWheel;

            Loaded += (s, e) => Navigate(AppWindowGuides.IndexFileName);
        }

        private static Brush Freeze(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// Loads a guide by file name and pushes it onto the navigation history.
        /// </summary>
        private void Navigate(string fileName)
        {
            if (!LoadFile(fileName))
                return;

            // Drop any "forward" entries, then append the new page.
            if (_index < _history.Count - 1)
                _history.RemoveRange(_index + 1, _history.Count - _index - 1);

            _history.Add(fileName);
            _index = _history.Count - 1;
            UpdateNavButtons();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (_index <= 0)
                return;

            _index--;
            LoadFile(_history[_index]);
            UpdateNavButtons();
        }

        private void Forward_Click(object sender, RoutedEventArgs e)
        {
            if (_index >= _history.Count - 1)
                return;

            _index++;
            LoadFile(_history[_index]);
            UpdateNavButtons();
        }

        private void Home_Click(object sender, RoutedEventArgs e) =>
            Navigate(AppWindowGuides.IndexFileName);

        /// <summary>
        /// Reads, renders and displays the given guide file. Returns false if it could
        /// not be loaded (in which case an error page is shown but history is untouched).
        /// </summary>
        private bool LoadFile(string fileName)
        {
            try
            {
                var path = Path.Combine(AppWindowGuides.DocsDirectory, fileName);
                if (!File.Exists(path))
                {
                    ShowMessage($"Guide not found:\n{fileName}\n\nExpected at: {path}");
                    return false;
                }

                var markdown = File.ReadAllText(path);
                var document = Markdig.Wpf.Markdown.ToFlowDocument(markdown, Pipeline);
                ApplyDocumentTheme(document);

                DocViewer.Document = document;
                ScrollToTop();
                TitleText.Text = ExtractTitle(markdown, fileName);
                return true;
            }
            catch (Exception ex)
            {
                ShowMessage($"Failed to open guide '{fileName}'.\n\n{ex.Message}");
                return false;
            }
        }

        private void OnHyperlinkClicked(object sender, ExecutedRoutedEventArgs e)
        {
            e.Handled = true;
            var target = e.Parameter?.ToString();
            if (string.IsNullOrWhiteSpace(target))
                return;

            // In-page anchors (e.g. "#section") are not resolvable in a FlowDocument; ignore.
            if (target.StartsWith("#"))
                return;

            // External links open in the default browser.
            if (Uri.TryCreate(target, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp ||
                 uri.Scheme == Uri.UriSchemeHttps ||
                 uri.Scheme == Uri.UriSchemeMailto))
            {
                TryOpenExternal(target);
                return;
            }

            // Otherwise treat it as a link to another local guide. Strip any anchor and
            // reduce to a bare file name so cross-guide links resolve in the docs folder.
            var fileName = Path.GetFileName(target.Split('#')[0]);
            if (fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                Navigate(fileName);
            else
                TryOpenExternal(target);
        }

        private static void TryOpenExternal(string target)
        {
            try
            {
                Process.Start(target);
            }
            catch
            {
                // Nothing actionable if the shell refuses to open the link.
            }
        }

        /// <summary>
        /// Applies the dark page background and light body-text colour (so text does not
        /// inherit the app's light foreground and vanish), a comfortable font, and a
        /// single-column web-like layout. Per-element colours are handled by the style
        /// overrides in Window.Resources.
        /// </summary>
        private static void ApplyDocumentTheme(FlowDocument document)
        {
            document.Background = DocBackground;
            document.Foreground = DocForeground;
            document.FontFamily = DocFont;
            document.FontSize = DocFontSize;

            // FlowDocument defaults to a multi-column layout; force a single column so
            // guides read like a web page.
            document.ColumnWidth = double.PositiveInfinity;
            document.PagePadding = new Thickness(28, 20, 28, 24);
        }

        private ScrollViewer GetScrollViewer()
        {
            if (_scrollViewer == null)
            {
                DocViewer.ApplyTemplate();
                _scrollViewer = DocViewer.Template?.FindName("PART_ContentHost", DocViewer) as ScrollViewer;
            }

            return _scrollViewer;
        }

        /// <summary>Resets the scroll position to the top of the newly loaded guide.</summary>
        private void ScrollToTop() => GetScrollViewer()?.ScrollToTop();

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = GetScrollViewer();
            if (scrollViewer == null)
                return;

            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        private void UpdateNavButtons()
        {
            BackButton.IsEnabled = _index > 0;
            ForwardButton.IsEnabled = _index < _history.Count - 1;
        }

        /// <summary>Uses the first markdown H1 as the page title, falling back to the file name.</summary>
        private static string ExtractTitle(string markdown, string fileName)
        {
            var heading = markdown
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .FirstOrDefault(line => line.StartsWith("# "));

            return heading != null ? heading.Substring(2).Trim() : fileName;
        }

        private void ShowMessage(string message)
        {
            var doc = new FlowDocument(new Paragraph(new Run(message)));
            ApplyDocumentTheme(doc);
            DocViewer.Document = doc;
            TitleText.Text = "User Guides";
        }
    }
}
