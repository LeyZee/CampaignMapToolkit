using System;
using System.IO;
using System.Linq;
using CAIME.Rpfm;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="MetadataService"/>'s pack file path ordering. The RPFM workflow
    /// merges every configured pack's fragments for a table (see RpfmWorkflowSession) rather than
    /// picking one pack to "win" - pack order only breaks a rare tie between two packs that happen to
    /// contain a fragment with the exact same name, and that tie is always broken by comparing the
    /// packs' file names alphabetically, never their full paths or the order they were added in.
    /// </summary>
    [TestClass]
    public class MetadataServiceTests
    {
        private string _projectDir;

        [TestInitialize]
        public void Setup()
        {
            _projectDir = Path.Combine(Path.GetTempPath(), "caime_metadata_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_projectDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_projectDir))
            {
                Directory.Delete(_projectDir, recursive: true);
            }
        }

        [TestMethod]
        public void SetThenGetPackFilePaths_SortsByFileNameOnly_IgnoringDirectoryAndInputOrder()
        {
            // Directory names deliberately sort the opposite way from the file names they contain, so
            // a sort that accidentally used the full path (or simply preserved insertion order) instead
            // of just the file name would return these in the wrong order.
            var lateByDirectory  = @"C:\AAA_folder\zzz_low_priority.pack";
            var earlyByFileName  = @"C:\ZZZ_folder\!!!important.pack";

            MetadataService.SetPackFilePaths(_projectDir, new[] { lateByDirectory, earlyByFileName });

            var result = MetadataService.GetPackFilePaths(_projectDir);

            CollectionAssert.AreEqual(
                new[] { earlyByFileName, lateByDirectory }, result.ToArray(),
                "Packs must be ordered by file name alone ('!!!important.pack' before 'zzz_low_priority.pack'), " +
                "not by full path or insertion order.");
        }

        [TestMethod]
        public void GetPackFilePaths_NoMetadataFile_ReturnsEmptyList()
        {
            var result = MetadataService.GetPackFilePaths(_projectDir);

            Assert.AreEqual(0, result.Count);
        }
    }
}
