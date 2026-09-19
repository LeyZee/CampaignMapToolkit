using System;
using System.Collections.Generic;
using System.Linq;
using CAIME.Pathfinding;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Integration
{
    /// <summary>
    /// Differential test for the data <see cref="MovementCostsGenerator"/> produces, against real,
    /// game-ready pathfinding.ppd files:
    ///
    /// <list type="bullet">
    /// <item><description>the MOVEMENT_COSTS_ARRAY (the deduplicated move-cost table), and</description></item>
    /// <item><description>the per-edge cost index + navigability bit packed into HEX_CELL_DATA
    /// (6 edges per hex).</description></item>
    /// </list>
    ///
    /// <para>
    /// The two are interdependent and so are asserted together: each edge encodes its cost as a 7-bit
    /// index into the move-cost table, and the table is built lazily in the generator's traversal
    /// order.
    /// </para>
    ///
    /// <para>
    /// Costs come from the game's real <c>campaign_ground_types.xml</c> (loaded from
    /// <c>TestData/db/&lt;game&gt;</c>), so the averaged edge costs match what the game baked into the .ppd.
    /// </para>
    /// </summary>
    [TestClass]
    public class MovementCostsGeneratorIntegrationTests
    {
        private const int EDGES_PER_HEX = 6; // one cost per hex edge; bytes 6..7 of a cell are not edges.

        [TestMethod]
        public void MovementCostsAndEdgeCosts_MatchRealPpd()
        {
            var cases = PathfindingTestData.EnumerateCases();
            if (cases.Count == 0)
                Assert.Inconclusive("No TestData/<map>/pathfinding.ppd paired with a template map.hex was found.");

            var tested = new List<string>();

            foreach (var test in cases)
            {
                if (test.GroundTypesXmlPath == null)
                    Assert.Inconclusive($"{test.MapName}: no campaign_ground_types.xml found under TestData/db.");

                var map = test.LoadPreparedMap();
                var db  = test.LoadDatabase();
                var ppd = test.ReadPpd();

                var (edges, moveCosts) = PathfindingPipeline.ComputeMovementCosts(map, db);

                // --- MOVEMENT_COSTS_ARRAY ----------------------------------------
                CollectionAssert.AreEqual(ppd.MoveCosts, moveCosts.ToArray(),
                    $"{test.MapName}: move-cost table differs from the real pathfinding.ppd " +
                    $"(expected {ppd.MoveCosts.Length} entries, got {moveCosts.Count}; a value or its " +
                    "discovery order does not match).");

                // --- Per-edge cost index + navigability (HEX_CELL_DATA) ----------
                Assert.AreEqual(map.Capacity, (uint)ppd.CellCount,
                    $"{test.MapName}: cell count differs (map {map.Capacity}, ppd {ppd.CellCount}).");

                for (int hex = 0; hex < ppd.CellCount; ++hex)
                {
                    for (int dir = 0; dir < EDGES_PER_HEX; ++dir)
                    {
                        byte actual   = edges[hex * 8 + dir];
                        byte expected = ppd.EdgeByte(hex, dir);

                        if (actual != expected)
                        {
                            Assert.Fail(
                                $"{test.MapName}: edge byte mismatch at hex {hex} dir {dir}. " +
                                $"expected 0x{expected:X2} (nav={(expected >> 7) & 1}, cost idx={expected & 0x7F}), " +
                                $"got 0x{actual:X2} (nav={(actual >> 7) & 1}, cost idx={actual & 0x7F}).");
                        }
                    }
                }

                tested.Add(test.MapName);
            }

            Console.WriteLine($"Verified move costs + edge costs for {tested.Count} map(s): {string.Join(", ", tested)}");
        }
    }
}
