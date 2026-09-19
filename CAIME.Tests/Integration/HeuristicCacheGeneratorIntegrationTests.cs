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
    /// Differential test for the three tile-group-indexed sections that share the bridges → HLCI →
    /// tile groups → borders pipeline, against real, game-ready pathfinding.ppd files:
    /// HEURISTIC_CACHE, PASSABLE_BORDER_HEXES and CUMULATIVE_BORDER_HEXES.
    ///
    /// <para>
    /// All three are produced from the same shared <c>edgesData</c> buffer and tile-group table, so
    /// <see cref="PathfindingPipeline.ComputeHeuristicAndBorders"/> replays the pipeline once (in the
    /// exporter's generator order) and the sections are diffed, serialized through the exporter's own
    /// writer seams, against the matching sections lifted out of the real .ppd.
    /// </para>
    ///
    /// <para>
    /// All three sections are byte-exact. The heuristic cache reads road costs from
    /// <c>CachedRoads.Last().MoveCost</c> and ground costs from <c>campaign_ground_types.xml</c>, so the
    /// database is loaded from the game's real db tables (<c>TestData/db/&lt;game&gt;</c>); without the real
    /// road/ground costs the matrix's distances would not line up.
    /// </para>
    /// </summary>
    [TestClass]
    public class HeuristicCacheGeneratorIntegrationTests
    {
        [TestMethod]
        public void HeuristicCacheAndBorderSections_MatchRealPpd()
        {
            var cases = PathfindingTestData.EnumerateCases();
            if (cases.Count == 0)
                Assert.Inconclusive("No TestData/<map>/pathfinding.ppd paired with a template map.hex was found.");

            var outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Integration");
            var tested = new List<string>();

            foreach (var test in cases)
            {
                if (test.GroundTypesXmlPath == null)
                    Assert.Inconclusive($"{test.MapName}: no campaign_ground_types.xml found under TestData/db.");

                var map = test.LoadPreparedMap();
                var db  = test.LoadDatabase();
                var ppd = test.ReadPpd();

                var result = PathfindingPipeline.ComputeHeuristicAndBorders(map, db);

                // The heuristic-cache and cumulative-border sizes are both driven by the tile-group count;
                // check it first for a friendlier failure than a raw byte/length mismatch downstream.
                Assert.AreEqual(ppd.TileGroupsCount, result.CumulativeBorderHexes.Length,
                    $"{test.MapName}: tile group count differs (ppd {ppd.TileGroupsCount}, " +
                    $"got {result.CumulativeBorderHexes.Length}).");

                AssertSectionMatches(test, outDir, "heuristic_cache",
                    ppd.HeuristicCacheRaw,
                    PpdSection.Serialize(w => PathfindingExporter.WriteHeuristicCache(w, result.HeuristicCache)));

                AssertSectionMatches(test, outDir, "passable_border_hexes",
                    ppd.PassableBorderHexesRaw,
                    PpdSection.Serialize(w => PathfindingExporter.WritePassableBorderHexes(w, result.Borders)));

                AssertSectionMatches(test, outDir, "cumulative_border_hexes",
                    ppd.CumulativeBorderHexesRaw,
                    PpdSection.Serialize(w => PathfindingExporter.WriteCumulativeBorderHexes(w, result.CumulativeBorderHexes)));

                tested.Add(test.MapName);
            }

            Console.WriteLine(
                $"Verified heuristic cache + passable/cumulative border sections for {tested.Count} map(s): " +
                string.Join(", ", tested));
        }

        /// <summary>
        /// Dumps both sides for visual inspection, then asserts size and byte equality, pointing at the first
        /// differing byte for a readable first failure rather than a raw collection dump. For the heuristic
        /// cache the byte offset divided by 4 is the flat (srcTileGroup * count + dstTileGroup) matrix index.
        /// </summary>
        private static void AssertSectionMatches(
            PathfindingTestCase test, string outDir, string section, byte[] expected, byte[] actual)
        {
            PpdSection.WriteRaw(outDir, test.MapName, section, expected, actual);

            Assert.AreEqual(expected.Length, actual.Length,
                $"{test.MapName}: {section} section size differs (expected {expected.Length}, got {actual.Length} bytes).");

            for (int i = 0; i < expected.Length; ++i)
            {
                if (expected[i] != actual[i])
                {
                    Assert.Fail(
                        $"{test.MapName}: {section} bytes differ from the real pathfinding.ppd, first at byte {i} " +
                        $"(expected 0x{expected[i]:X2}, got 0x{actual[i]:X2}).");
                }
            }
        }
    }
}
