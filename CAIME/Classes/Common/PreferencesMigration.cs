using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace CAIME
{
    /// <summary>
    /// Handles migration of preferences from old .ini format to new .json format
    /// </summary>
    public static class PreferencesMigration
    {
        private const string PreferencesDirectory = @"CampaignMapToolkit\Caime";
        private const string OldPreferencesFileName = "preferences.ini";
        private const string NewPreferencesFileName = "preferences.json";

        /// <summary>
        /// Migrates preferences from .ini to .json format if the old file exists and the new one doesn't
        /// </summary>
        public static void MigrateIfNeeded(string appDataDirectory)
        {
            var preferencesDir = Path.Combine(appDataDirectory, PreferencesDirectory);
            var oldPreferencesPath = Path.Combine(preferencesDir, OldPreferencesFileName);
            var newPreferencesPath = Path.Combine(preferencesDir, NewPreferencesFileName);

            if (!File.Exists(oldPreferencesPath))
            {
                return;
            }

            // Both files exist: migration already happened, clean up the old file
            if (File.Exists(newPreferencesPath))
            {
                LoggerViewModel.Log("Both preferences.ini and preferences.json found. Deleting leftover .ini file.", LogLevel.Info);
                try
                {
                    File.Delete(oldPreferencesPath);
                    LoggerViewModel.Log("Leftover preferences.ini deleted successfully.", LogLevel.Info);
                }
                catch (Exception ex)
                {
                    LoggerViewModel.Log($"Failed to delete old preferences file: {ex.Message}", LogLevel.Warning);
                }

                return;
            }

            LoggerViewModel.Log("preferences.ini found, starting migration to preferences.json.", LogLevel.Info);
            try
            {
                MigratePreferences(oldPreferencesPath, newPreferencesPath);
                File.Delete(oldPreferencesPath);
                LoggerViewModel.Log("Preferences migrated successfully and preferences.ini deleted.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"Preferences migration failed: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Migrates preferences from .ini format to .json format
        /// </summary>
        private static void MigratePreferences(string oldFilePath, string newFilePath)
        {
            // Read the old .ini file
            var iniLines = File.ReadAllLines(oldFilePath);
            var iniPreferences = new Dictionary<string, string>();

            // Parse key=value pairs
            foreach (var line in iniLines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split(new[] { '=' }, 2);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var value = parts[1].Trim();
                    iniPreferences[key] = value;
                }
            }

            // Convert to new JSON format
            var json = new JObject();

            // Map simple properties
            if (iniPreferences.TryGetValue(nameof(PreferencesViewModel.HexSpacing), out var hexSpacingStr) 
                && float.TryParse(hexSpacingStr, out float hexSpacing))
            {
                json[nameof(PreferencesViewModel.HexSpacing)] = hexSpacing;
            }

            if (iniPreferences.TryGetValue(nameof(PreferencesViewModel.IsFirstAppLaunch), out var firstLaunchStr) 
                && bool.TryParse(firstLaunchStr, out bool isFirstLaunch))
            {
                json[nameof(PreferencesViewModel.IsFirstAppLaunch)] = isFirstLaunch;
            }

            if (iniPreferences.TryGetValue(nameof(PreferencesViewModel.LastKnownVersion), out var lastVersionStr))
            {
                json[nameof(PreferencesViewModel.LastKnownVersion)] = lastVersionStr;
            }

            if (iniPreferences.TryGetValue(nameof(PreferencesViewModel.LogToFile), out var logToFileStr) 
                && bool.TryParse(logToFileStr, out bool logToFile))
            {
                json[nameof(PreferencesViewModel.LogToFile)] = logToFile;
            }

            if (iniPreferences.TryGetValue(nameof(PreferencesViewModel.IsAutoSave), out var autoSaveStr) 
                && bool.TryParse(autoSaveStr, out bool isAutoSave))
            {
                json[nameof(PreferencesViewModel.IsAutoSave)] = isAutoSave;
            }

            if (iniPreferences.TryGetValue(nameof(PreferencesViewModel.IsAutoBackup), out var autoBackupStr) 
                && bool.TryParse(autoBackupStr, out bool isAutoBackup))
            {
                json[nameof(PreferencesViewModel.IsAutoBackup)] = isAutoBackup;
            }

            // Map assembly kit paths
            var assKitPaths = new JObject();
            foreach (var kvp in iniPreferences)
            {
                if (kvp.Key.EndsWith("_AssKitPath"))
                {
                    assKitPaths[kvp.Key] = kvp.Value;
                }
            }

            if (assKitPaths.Count > 0)
            {
                json["AssemblyKitPaths"] = assKitPaths;
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(newFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write the new JSON file
            File.WriteAllText(newFilePath, json.ToString());
        }
    }
}
