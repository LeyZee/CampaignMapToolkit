using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using CAIME.Rpfm;
using Microsoft.Win32;

namespace CAIME.Windows
{
    /// <summary>
    /// Lets the user view and change the project-specific mod pack(s) the RPFM database source reads
    /// this campaign's own data from - distinct from the vanilla pack and RPFM path, which are
    /// per-game settings in Preferences, not per-project. The list is persisted, always sorted
    /// alphabetically by pack file name, into caime_metadata.json beside the project's map.hex via
    /// <see cref="MetadataService"/>. That order is not a user-chosen priority - every configured
    /// pack contributes to every table equally - it only decides a rare tiebreak: when two packs in
    /// this list define a fragment with the exact same name for the same table, the one whose file
    /// name sorts first wins. There is nothing to reorder, so the list is display-only besides
    /// Add/Remove.
    /// </summary>
    public partial class PackFileWindow : Window
    {
        private readonly string _projectPath;
        private readonly ObservableCollection<string> _packPaths;

        public PackFileWindow(string projectPath)
        {
            InitializeComponent();

            _projectPath = projectPath;
            _packPaths = new ObservableCollection<string>(MetadataService.GetPackFilePaths(projectPath));
            packPathsList.ItemsSource = _packPaths;
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Pack file (*.pack)|*.pack",
                Title  = "Select .pack file(s)",
                Multiselect = true,
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            foreach (var path in dialog.FileNames)
            {
                if (!_packPaths.Contains(path))
                {
                    _packPaths.Add(path);
                }
            }

            Resort();
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            foreach (var selected in packPathsList.SelectedItems.Cast<string>().ToList())
            {
                _packPaths.Remove(selected);
            }
        }

        // Keeps the displayed order matching the alphabetical-by-pack-file-name order actually used
        // for the merge tiebreak, so a freshly-added pack doesn't sit wherever Add happened to insert
        // it until the window is reopened.
        private void Resort()
        {
            var sorted = _packPaths.OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase).ToList();
            _packPaths.Clear();
            foreach (var path in sorted)
            {
                _packPaths.Add(path);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MetadataService.SetPackFilePaths(_projectPath, _packPaths);
                LoggerViewModel.Log($"RPFM pack paths saved to project metadata ({_packPaths.Count}).", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"Failed to save pack paths to metadata: {ex.Message}", LogLevel.ErrorMessageBox);
                return;
            }

            Close();
            Owner?.Focus();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
            Owner?.Focus();
        }
    }
}
