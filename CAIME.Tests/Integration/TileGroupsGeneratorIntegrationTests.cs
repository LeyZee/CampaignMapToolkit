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
    /// Differential test for the TILE_GROUPS_ARRAY section of pathfinding.ppd.
    ///
    /// <para>
    /// For every map that has both a template <c>map.hex</c> and a game-ready
    /// <c>pathfinding.ppd</c>, the tile-group stage of the pipeline (bridges → HLCI areas → tile
    /// groups) is replayed by <see cref="PathfindingPipeline.ComputeTileGroups"/>, the exporter's
    /// <see cref="PathfindingExporter.WriteTileGroups"/> seam serializes the result to game-ready
    /// bytes, and those bytes are diffed against the same section lifted out of the real .ppd.
    /// </para>
    ///
    /// <para>
    /// Unlike bridges, the comparison is <em>order-dependent</em>: each hex references its tile
    /// group by index (encoded into HEX_CELL_DATA), and the heuristic cache / cumulative-border
    /// sections are indexed by tile group too, so the table's order carries meaning.
    /// </para>
    /// </summary>
    [TestClass]
    public class TileGroupsGeneratorIntegrationTests
    {
        [TestMethod]
        public void TileGroupsSection_MatchesRealPpd()
        {
            var cases = PathfindingTestData.EnumerateCases();
            if (cases.Count == 0)
                Assert.Inconclusive("No TestData/<map>/pathfinding.ppd paired with a template map.hex was found.");

            var outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Integration");
            var tested = new List<string>();

            foreach (var test in cases)
            {
                var map = test.LoadPreparedMap();

                var tileGroups = PathfindingPipeline.ComputeTileGroups(map);

                var actual   = PpdSection.Serialize(w => PathfindingExporter.WriteTileGroups(w, tileGroups));
                var expected = test.ReadPpd().TileGroupsRaw;

                // Raw section dumps (uint16 count + 4 bytes per entry) for hex-level inspection.
                PpdSection.WriteRaw(outDir, test.MapName, "tile_groups", expected, actual);

                // Count first for a friendlier first failure than a raw byte mismatch.
                int expectedCount = ReadCount(expected);
                Assert.AreEqual(expectedCount, tileGroups.Count,
                    $"{test.MapName}: tile group count differs (expected {expectedCount}, got {tileGroups.Count}).");

                Assert.AreEqual(expected.Length, actual.Length,
                    $"{test.MapName}: tile-groups section size differs (expected {expected.Length}, got {actual.Length}).");
                CollectionAssert.AreEqual(expected, actual,
                    $"{test.MapName}: tile-groups section bytes differ from the real pathfinding.ppd " +
                    "(an entry's (hlci, region) value or the table order does not match).");

                tested.Add(test.MapName);
            }

            Console.WriteLine($"Verified tile-groups section for {tested.Count} map(s): {string.Join(", ", tested)}");
        }

        /// <summary>Reads the leading <c>uint16</c> entry count from a serialized TILE_GROUPS_ARRAY section.</summary>
        private static int ReadCount(byte[] section)
        {
            using (var ms = new MemoryStream(section))
            using (var br = new BinaryReader(ms))
                return br.ReadUInt16();
        }
    }
}
