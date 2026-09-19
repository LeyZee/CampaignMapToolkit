using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CAIME.Tools;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.Win32;

namespace CAIME.Views.Windows
{
    /// <summary>
    /// UI for <see cref="ShaderResolutionCorrector"/>: lets the user pick one or both variants of a
    /// compiled Three Kingdoms shader, review/edit the baked-in resolution, and save patched copies
    /// (with a repaired checksum) into a chosen folder, keeping each file's original name.
    /// </summary>
    public partial class ShaderResolutionCorrectorWindow : Window
    {
        private const int MinResolutionValue = 1;
        private const int MaxResolutionValue = 1_000_000;

        // Ties the shader's baked-in resolution to the open map's hex grid size (standard
        // round-half-away-from-zero), matching the campaign UI-overlay texture's expected
        // dimensions for this game. Width and height scale by slightly different ratios - see
        // LookupImageExporter's MAIN_LOOKUP_RATIO_WIDTH_3K / MAIN_LOOKUP_RATIO_HEIGHT_3K.
        private const double ShaderResolutionRatioWidth = 4.3050;
        private const double ShaderResolutionRatioHeight = 4.3077;

        private static readonly Regex DigitsOnly = new Regex("^[0-9]+$", RegexOptions.Compiled);

        private ShaderResolutionCorrector tool1;
        private ShaderResolutionCorrector tool2;
        private string inputPath1;
        private string inputPath2;
        private string outputFolder;
        private bool isProcessing;

        public ShaderResolutionCorrectorWindow(Project project)
        {
            InitializeComponent();
            PrefillNewResolutionFromMap(project);
        }

        private void PrefillNewResolutionFromMap(Project project)
        {
            if (project?.MapHexFile == null)
            {
                return;
            }

            int newWidth = (int)Math.Round(project.MapHexFile.MapWidth * ShaderResolutionRatioWidth, MidpointRounding.AwayFromZero);
            int newHeight = (int)Math.Round(project.MapHexFile.MapHeight * ShaderResolutionRatioHeight, MidpointRounding.AwayFromZero);

            NewWidthTextBox.Text = newWidth.ToString(CultureInfo.InvariantCulture);
            NewHeightTextBox.Text = newHeight.ToString(CultureInfo.InvariantCulture);
        }

        private void BrowseInput1_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select a compiled Three Kingdoms shader",
                Filter = "Compiled shader (*.fxc)|*.fxc|All files (*.*)|*.*",
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var loadedTool = new ShaderResolutionCorrector();
            try
            {
                loadedTool.Load(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Couldn't read that shader file:\n{ex.Message}",
                    "Open failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            tool1 = loadedTool;
            inputPath1 = dialog.FileName;
            Input1PathTextBox.Text = inputPath1;

            if (tool1.TryDetectResolution(out int width, out int height))
            {
                CurrentWidthTextBox.Text = width.ToString(CultureInfo.InvariantCulture);
                CurrentHeightTextBox.Text = height.ToString(CultureInfo.InvariantCulture);
                StatusText.Text = $"Detected resolution {width} x {height}. Enter the new width and height.";
            }
            else
            {
                CurrentWidthTextBox.Text = string.Empty;
                CurrentHeightTextBox.Text = string.Empty;
                StatusText.Text = "Couldn't auto-detect a resolution - enter the current and new values yourself.";
            }

            BrowseInput2Button.IsEnabled = true;
            ClearInput2Button.IsEnabled = inputPath2 != null;

            UpdateRunButtonState();
        }

        private void BrowseInput2_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select the enable_shadows variant of the shader",
                Filter = "Compiled shader (*.fxc)|*.fxc|All files (*.*)|*.*",
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            if (string.Equals(Path.GetFullPath(dialog.FileName), Path.GetFullPath(inputPath1), StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "This is the same file already selected above. Choose the other shader variant.",
                    "Duplicate file", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var loadedTool = new ShaderResolutionCorrector();
            try
            {
                loadedTool.Load(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Couldn't read that shader file:\n{ex.Message}",
                    "Open failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            tool2 = loadedTool;
            inputPath2 = dialog.FileName;
            Input2PathTextBox.Text = inputPath2;
            ClearInput2Button.IsEnabled = true;

            if (tool2.TryDetectResolution(out int width, out int height)
                && int.TryParse(CurrentWidthTextBox.Text, out int currentWidth)
                && int.TryParse(CurrentHeightTextBox.Text, out int currentHeight)
                && (width != currentWidth || height != currentHeight))
            {
                StatusText.Text = $"Note: the second file's detected resolution ({width} x {height}) " +
                    $"differs from the current fields ({currentWidth} x {currentHeight}). Double-check before running.";
            }

            UpdateRunButtonState();
        }

        private void ClearInput2_Click(object sender, RoutedEventArgs e)
        {
            tool2 = null;
            inputPath2 = null;
            Input2PathTextBox.Text = string.Empty;
            ClearInput2Button.IsEnabled = false;
            UpdateRunButtonState();
        }

        private void BrowseOutputFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new CommonOpenFileDialog())
            {
                dialog.Title = "Save patched shader(s) to";
                dialog.IsFolderPicker = true;

                if (dialog.ShowDialog() != CommonFileDialogResult.Ok)
                {
                    return;
                }

                outputFolder = dialog.FileName;
                OutputFolderTextBox.Text = outputFolder;
            }

            UpdateRunButtonState();
        }

        private void ResolutionField_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateRunButtonState();
        }

        private void IntegerTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = DigitsOnly.IsMatch(e.Text) == false;
        }

        private void IntegerTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)) == false ||
                DigitsOnly.IsMatch((string)e.DataObject.GetData(typeof(string))) == false)
            {
                e.CancelCommand();
            }
        }

        private static bool TryParseResolutionValue(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value)
                && value >= MinResolutionValue
                && value <= MaxResolutionValue;
        }

        private void UpdateRunButtonState()
        {
            bool inputsValid = tool1 != null && tool1.IsLoaded
                && string.IsNullOrEmpty(outputFolder) == false
                && TryParseResolutionValue(CurrentWidthTextBox.Text, out _)
                && TryParseResolutionValue(CurrentHeightTextBox.Text, out _)
                && TryParseResolutionValue(NewWidthTextBox.Text, out _)
                && TryParseResolutionValue(NewHeightTextBox.Text, out _);

            RunButton.IsEnabled = inputsValid && isProcessing == false;
        }

        private async void RunButton_Click(object sender, RoutedEventArgs e)
        {
            if (isProcessing)
            {
                return;
            }

            TryParseResolutionValue(CurrentWidthTextBox.Text, out int currentWidth);
            TryParseResolutionValue(CurrentHeightTextBox.Text, out int currentHeight);
            TryParseResolutionValue(NewWidthTextBox.Text, out int newWidth);
            TryParseResolutionValue(NewHeightTextBox.Text, out int newHeight);

            var jobs = new List<(ShaderResolutionCorrector tool, string inputPath, string outputPath)>
            {
                (tool1, inputPath1, Path.Combine(outputFolder, Path.GetFileName(inputPath1))),
            };
            if (tool2 != null)
            {
                jobs.Add((tool2, inputPath2, Path.Combine(outputFolder, Path.GetFileName(inputPath2))));
            }

            if (jobs.Count == 2 && string.Equals(jobs[0].outputPath, jobs[1].outputPath, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "Both shader files have the same file name, so they would overwrite each " +
                    "other in the output folder. Choose files with distinct names.",
                    "Conflicting output", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var alreadyExisting = new List<string>();
            foreach (var job in jobs)
            {
                if (File.Exists(job.outputPath))
                {
                    alreadyExisting.Add(job.outputPath);
                }
            }

            if (alreadyExisting.Count > 0)
            {
                var prompt = "The following output file(s) already exist and will be overwritten:\n\n" +
                    string.Join("\n", alreadyExisting) + "\n\nContinue?";

                if (MessageBox.Show(this, prompt, "Overwrite existing file(s)", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            SetProcessing(true);
            try
            {
                var results = await Task.Run(() =>
                {
                    var jobResults = new List<(string fileName, bool success, string message)>();
                    foreach (var job in jobs)
                    {
                        try
                        {
                            var patchedBlob = job.tool.PatchResolution(currentWidth, currentHeight, newWidth, newHeight,
                                out int widthHits, out int heightHits);
                            job.tool.Save(job.outputPath, patchedBlob);
                            jobResults.Add((Path.GetFileName(job.inputPath), true,
                                $"width {widthHits}x, height {heightHits}x -> {job.outputPath}"));
                        }
                        catch (Exception ex)
                        {
                            jobResults.Add((Path.GetFileName(job.inputPath), false, ex.Message));
                        }
                    }
                    return jobResults;
                });

                var summary = new StringBuilder();
                bool anyFailed = false;
                foreach (var result in results)
                {
                    summary.Append(result.success ? "OK  " : "FAILED  ").Append(result.fileName).Append(" - ").Append(result.message).Append('\n');
                    anyFailed |= result.success == false;
                }

                StatusText.Text = anyFailed ? "Completed with errors - see details." : "Done. Patched shader(s) saved successfully.";

                MessageBox.Show(this, summary.ToString().TrimEnd(), "Shader Resolution Corrector",
                    MessageBoxButton.OK, anyFailed ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusText.Text = "Patch failed.";
                MessageBox.Show(this, $"Failed to patch the shader(s):\n{ex.Message}",
                    "Patch failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetProcessing(false);
            }
        }

        private void SetProcessing(bool processing)
        {
            isProcessing = processing;
            ProgressBar.Visibility = processing ? Visibility.Visible : Visibility.Collapsed;
            BrowseInput1Button.IsEnabled = processing == false;
            BrowseInput2Button.IsEnabled = processing == false && tool1 != null;
            ClearInput2Button.IsEnabled = processing == false && inputPath2 != null;
            BrowseOutputFolderButton.IsEnabled = processing == false;
            CurrentWidthTextBox.IsEnabled = processing == false;
            CurrentHeightTextBox.IsEnabled = processing == false;
            NewWidthTextBox.IsEnabled = processing == false;
            NewHeightTextBox.IsEnabled = processing == false;
            UpdateRunButtonState();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (isProcessing)
            {
                e.Cancel = true;
            }
        }
    }
}
