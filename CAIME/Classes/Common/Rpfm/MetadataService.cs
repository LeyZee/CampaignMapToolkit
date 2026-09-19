using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CAIME.Rpfm
{
    /// <summary>
    /// Reads and writes <c>caime_metadata.json</c> beside a project's map.hex. All writes are
    /// read-modify-write: a caller that updates one field (e.g. the RPFM pack path) never clobbers
    /// unrelated fields (e.g. the map_data config path written by the Map Data Editor). This is
    /// what keeps the metadata file decoupled from any single feature that touches it.
    /// </summary>
    public static class MetadataService
    {
        /// <summary>Full path to the metadata file for a project located at <paramref name="projectPath"/>.</summary>
        public static string GetMetadataPath(string projectPath)
        {
            return Path.Combine(projectPath, CaimeMetadata.FILENAME);
        }

        /// <summary>
        /// Loads the metadata for the project, or null when the file does not exist. Throws only on
        /// genuine read/parse failures so callers can distinguish "no metadata yet" from "corrupt".
        /// </summary>
        public static CaimeMetadata Load(string projectPath)
        {
            var path = GetMetadataPath(projectPath);
            if (!File.Exists(path))
            {
                return null;
            }

            return CaimeMetadata.Load(path);
        }

        /// <summary>
        /// Applies <paramref name="mutate"/> to the existing metadata (or a fresh instance when none
        /// exists) and writes the result back. Existing fields not touched by the mutation are preserved.
        /// </summary>
        public static void Update(string projectPath, Action<CaimeMetadata> mutate)
        {
            if (mutate == null)
            {
                throw new ArgumentNullException(nameof(mutate));
            }

            var path = GetMetadataPath(projectPath);

            CaimeMetadata metadata = null;
            if (File.Exists(path))
            {
                metadata = CaimeMetadata.Load(path);
            }

            if (metadata == null)
            {
                metadata = new CaimeMetadata();
            }

            mutate(metadata);
            metadata.Save(path);
        }

        /// <summary>
        /// Returns the stored pack file paths, sorted alphabetically by pack file name (not full
        /// path), or an empty list when unset / no metadata file. This order is not a user-chosen
        /// priority - it only decides one rare tiebreak: when two of these modded packs each contain a
        /// fragment with the exact same name for the same table, the one whose pack file name sorts
        /// first wins. Which fragment wins between differently-named fragments is decided by fragment
        /// name, not pack name, and a modded pack always beats the vanilla pack on an identical
        /// fragment name whatever either is called (RpfmWorkflowSession consults vanilla last).
        /// </summary>
        public static IReadOnlyList<string> GetPackFilePaths(string projectPath)
        {
            try
            {
                var metadata = Load(projectPath);
                var paths = metadata?.PackFilePaths ?? new List<string>();
                return SortByPackName(paths);
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"MetadataService - failed to read pack paths: {ex.Message}", LogLevel.Warning);
                return new List<string>();
            }
        }

        /// <summary>
        /// Stores the pack file paths, preserving all other metadata fields. Sorted alphabetically by
        /// pack file name before writing so the on-disk order always matches what GetPackFilePaths
        /// returns and what the UI displays.
        /// </summary>
        public static void SetPackFilePaths(string projectPath, IEnumerable<string> packFilePaths)
        {
            var normalized = SortByPackName(
                (packFilePaths ?? Enumerable.Empty<string>()).Where(p => !string.IsNullOrWhiteSpace(p)));

            Update(projectPath, m => m.PackFilePaths = normalized);
        }

        private static List<string> SortByPackName(IEnumerable<string> packPaths)
        {
            return packPaths
                .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
