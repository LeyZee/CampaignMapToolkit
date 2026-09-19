using System.IO;
using CAIME.Rpfm;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="BackupService"/>, which holds the user's real Assembly Kit files
    /// while the RPFM workflow temporarily replaces them. A lost or misplaced backup leaves the
    /// installation permanently altered, so the move semantics are pinned exactly.
    /// </summary>
    [TestClass]
    public class RpfmBackupServiceTests
    {
        private string _root;
        private string _dbRoot;
        private string _backupRoot;

        [TestInitialize]
        public void Setup()
        {
            _root       = MapHexHarness.CreateTempDir("caime_rpfm_backup");
            _dbRoot     = Path.Combine(_root, "db");
            _backupRoot = Path.Combine(_root, "backup");
            Directory.CreateDirectory(_dbRoot);
        }

        [TestCleanup]
        public void Cleanup()
        {
            MapHexHarness.DeleteTempDir(_root);
        }

        // Backup must move, not copy: conflict detection treats any surviving file matching a table
        // name as a leftover from an interrupted run and aborts the session.
        [TestMethod]
        public void Backup_MovesTheOriginalOutOfTheDbRoot()
        {
            var original = WriteDbFile("campaign_regions.xml", "original contents");

            new BackupService(_dbRoot, _backupRoot).Backup(original);

            Assert.IsFalse(File.Exists(original), "Backup must free the original slot, not copy the file.");
            Assert.AreEqual("original contents",
                File.ReadAllText(Path.Combine(_backupRoot, "campaign_regions.xml")),
                "The captured file's contents changed.");
        }

        [TestMethod]
        public void Backup_MissingFile_IsANoOp()
        {
            new BackupService(_dbRoot, _backupRoot).Backup(Path.Combine(_dbRoot, "not_there.xml"));

            Assert.IsFalse(Directory.Exists(_backupRoot),
                "Backing up a file that does not exist must not create the backup directory.");
        }

        [TestMethod]
        public void Restore_PutsOriginalsBack_OverwritingTheGeneratedReplacements()
        {
            var regions = WriteDbFile("campaign_regions.xml", "original regions");
            var roads   = WriteDbFile("campaign_map_roads.xml", "original roads");

            var service = new BackupService(_dbRoot, _backupRoot);
            service.Backup(regions);
            service.Backup(roads);

            File.WriteAllText(regions, "generated regions");
            File.WriteAllText(roads,   "generated roads");

            service.Restore();

            Assert.AreEqual("original regions", File.ReadAllText(regions), "Generated file was not replaced.");
            Assert.AreEqual("original roads",   File.ReadAllText(roads),   "Generated file was not replaced.");
        }

        [TestMethod]
        public void BackupAndRestore_PreserveNestedDirectoryStructure()
        {
            var nestedDir = Path.Combine(_dbRoot, "nested", "deeper");
            Directory.CreateDirectory(nestedDir);
            var nested = Path.Combine(nestedDir, "table.xml");
            File.WriteAllText(nested, "nested contents");

            var service = new BackupService(_dbRoot, _backupRoot);
            service.Backup(nested);

            Assert.IsTrue(File.Exists(Path.Combine(_backupRoot, "nested", "deeper", "table.xml")),
                "The backup did not mirror the file's path relative to the db root.");

            service.Restore();

            Assert.AreEqual("nested contents", File.ReadAllText(nested),
                "The file was not restored to its original nested location.");
        }

        // Restore runs from normal cleanup, Dispose and crash recovery, any of which can follow another.
        [TestMethod]
        public void Restore_IsSafeToCallTwice()
        {
            var original = WriteDbFile("campaign_regions.xml", "original contents");

            var service = new BackupService(_dbRoot, _backupRoot);
            service.Backup(original);
            service.Restore();
            service.Restore();

            Assert.AreEqual("original contents", File.ReadAllText(original), "The restored file was disturbed.");
        }

        // The static overload restores from disk alone, which is what makes startup crash recovery
        // possible - the process that took the backup is gone by then.
        [TestMethod]
        public void RestoreDirectory_WorksWithoutTheInstanceThatTookTheBackup()
        {
            var original = WriteDbFile("campaign_regions.xml", "original contents");

            new BackupService(_dbRoot, _backupRoot).Backup(original);
            File.WriteAllText(original, "generated contents");

            BackupService.RestoreDirectory(_dbRoot, _backupRoot);

            Assert.AreEqual("original contents", File.ReadAllText(original),
                "Recovery could not restore the Assembly Kit from the backup directory alone.");
        }

        [TestMethod]
        public void RestoreDirectory_MissingBackupDirectory_IsANoOp()
        {
            BackupService.RestoreDirectory(_dbRoot, Path.Combine(_root, "never_created"));
        }

        [TestMethod]
        public void DeleteBackupDirectory_RemovesTheDirectoryTree()
        {
            var original = WriteDbFile("campaign_regions.xml", "original contents");

            var service = new BackupService(_dbRoot, _backupRoot);
            service.Backup(original);
            service.Restore();
            service.DeleteBackupDirectory();

            Assert.IsFalse(Directory.Exists(_backupRoot), "The backup directory was left behind.");
        }

        private string WriteDbFile(string fileName, string contents)
        {
            var path = Path.Combine(_dbRoot, fileName);
            File.WriteAllText(path, contents);
            return path;
        }
    }
}
