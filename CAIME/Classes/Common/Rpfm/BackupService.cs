using System;
using System.Collections.Generic;
using System.IO;

namespace CAIME.Rpfm
{
    /// <summary>
    /// Backs up and restores original Assembly Kit db files while the RPFM workflow temporarily
    /// replaces them. A backup lives in its own directory and preserves the relative directory
    /// structure of every file it captures, so a restore is an exact reversal.
    /// </summary>
    public sealed class BackupService
    {
        private readonly string _dbRootPath;      // <AssemblyKit>/raw_data/db
        private readonly string _backupRootPath;  // session-private, outside the db root

        // Files captured, as db-root-relative paths, so they can be restored to the same place.
        private readonly List<string> _backedUpRelativePaths = new List<string>();

        public string BackupRootPath => _backupRootPath;

        /// <param name="dbRootPath">The Assembly Kit db folder whose files are being replaced.</param>
        /// <param name="backupRootPath">
        /// Where to hold the originals. It must be private to this session and outside
        /// <paramref name="dbRootPath"/>: two sessions sharing a backup root would each see the
        /// other's captured files, and either one's <see cref="DeleteBackupDirectory"/> would
        /// destroy the other's originals.
        /// </param>
        public BackupService(string dbRootPath, string backupRootPath)
        {
            _dbRootPath     = dbRootPath;
            _backupRootPath = backupRootPath;
        }

        /// <summary>
        /// Moves a single existing db file into the backup directory (creating it on first use),
        /// preserving its path relative to the db root. No-op when the file does not exist.
        /// </summary>
        public void Backup(string absoluteFilePath)
        {
            if (!File.Exists(absoluteFilePath))
            {
                return;
            }

            var relative      = GetRelativePath(_dbRootPath, absoluteFilePath);
            var backupTarget  = Path.Combine(_backupRootPath, relative);

            Directory.CreateDirectory(Path.GetDirectoryName(backupTarget));

            // Move (not copy) so the original slot is freed - conflict detection later relies on the
            // original file no longer being present.
            File.Move(absoluteFilePath, backupTarget);
            _backedUpRelativePaths.Add(relative);
        }

        /// <summary>
        /// Restores every backed-up file to its original location, overwriting whatever is there now.
        /// Safe to call multiple times.
        /// </summary>
        public void Restore()
        {
            RestoreDirectory(_dbRootPath, _backupRootPath);
            _backedUpRelativePaths.Clear();
        }

        /// <summary>
        /// Moves every file found under <paramref name="backupRootPath"/> back to the matching location
        /// under <paramref name="dbRootPath"/>, preserving directory structure and overwriting whatever
        /// is there. The backup directory contents are the authoritative record of what to restore, so
        /// this works without any in-memory state - it is what startup crash-recovery relies on. No-op
        /// when the backup directory does not exist.
        /// </summary>
        public static void RestoreDirectory(string dbRootPath, string backupRootPath)
        {
            if (!Directory.Exists(backupRootPath))
            {
                return;
            }

            foreach (var backupSource in Directory.GetFiles(backupRootPath, "*", SearchOption.AllDirectories))
            {
                var relative = GetRelativePath(backupRootPath, backupSource);
                var original = Path.Combine(dbRootPath, relative);

                Directory.CreateDirectory(Path.GetDirectoryName(original));

                if (File.Exists(original))
                {
                    File.Delete(original);
                }

                File.Move(backupSource, original);
            }
        }

        /// <summary>Deletes the backup directory. Call only after a successful <see cref="Restore"/>.</summary>
        public void DeleteBackupDirectory()
        {
            if (Directory.Exists(_backupRootPath))
            {
                Directory.Delete(_backupRootPath, recursive: true);
            }
        }

        // net48 has no Path.GetRelativePath.
        private static string GetRelativePath(string root, string fullPath)
        {
            var rootUri = new Uri(AppendSeparator(root));
            var fileUri = new Uri(fullPath);
            var relativeUri = rootUri.MakeRelativeUri(fileUri);
            return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        private static string AppendSeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString()) ? path : path + Path.DirectorySeparatorChar;
        }
    }
}
