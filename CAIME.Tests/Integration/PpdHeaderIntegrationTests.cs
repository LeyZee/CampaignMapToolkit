using System;
using System.Collections.Generic;
using System.IO;
using CAIME;
using CAIME.Pathfinding;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Integration
{
    /// <summary>
    /// Verification test for basic PPD file header properties: magic number, version,
    /// land regions count/array, and map dimensions.
    /// </summary>
    [TestClass]
    public class PpdHeaderIntegrationTests
    {
        [TestMethod]
        public void PpdHeader_MatchesRealPpd()
        {
            var cases = PathfindingTestData.EnumerateCases();
            if (cases.Count == 0)
                Assert.Inconclusive("No TestData/<map>/pathfinding.ppd paired with a template map.hex was found.");

            var tested = new List<string>();

            foreach (var test in cases)
            {
                var ppd = test.ReadPpd();
                var map = test.LoadPreparedMap();

                // Verify magic number
                Assert.AreEqual(PpdFile.MagicNumber, ppd.Magic,
                    $"{test.MapName}: PPD magic number mismatch (expected {PpdFile.MagicNumber:X16}, got {ppd.Magic:X16}).");

                // Verify file version
                Assert.AreEqual(PpdFile.Version, ppd.FileVersion,
                    $"{test.MapName}: PPD version mismatch (expected {PpdFile.Version}, got {ppd.FileVersion}).");

                // Verify land regions count
                var expectedRegionsCount = map.LandRegions.Count;
                Assert.AreEqual(expectedRegionsCount, ppd.LandRegionsCount,
                    $"{test.MapName}: land regions count mismatch (expected {expectedRegionsCount}, got {ppd.LandRegionsCount}).");

                // Verify land regions array names match
                CollectionAssert.AreEqual(map.LandRegions, new List<string>(ppd.LandRegionNames),
                    $"{test.MapName}: land region names differ from the real pathfinding.ppd.");

                // Verify map dimensions
                var expectedWidth = (ushort)map.MapWidth;
                var expectedHeight = (ushort)map.MapHeight;

                Assert.AreEqual(expectedWidth, ppd.Width,
                    $"{test.MapName}: map width mismatch (expected {expectedWidth}, got {ppd.Width}).");
                Assert.AreEqual(expectedHeight, ppd.Height,
                    $"{test.MapName}: map height mismatch (expected {expectedHeight}, got {ppd.Height}).");

                tested.Add(test.MapName);
            }

            if (tested.Count == 0)
                Assert.Inconclusive("No test cases were processed.");

            Console.WriteLine($"Verified PPD header for {tested.Count} map(s): {string.Join(", ", tested)}");
        }
    }
}
