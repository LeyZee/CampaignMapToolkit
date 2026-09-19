using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CAIME;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Integration
{
    /// <summary>
    /// Differential test for <c>TradeRoutesProcessor</c> against the real, game-ready
    /// <c>trade_routes.ptd</c> files under <c>TestData/campaign_maps/&lt;map&gt;</c>, regenerated from the
    /// matching <c>Templates/&lt;map&gt;/map.hex</c>.
    ///
    /// <para>
    /// Byte-exact output is intentionally NOT asserted: older titles like Attila contain duplicated land routes.
    /// What is well-defined and must match is the trade-route topology: the land region table, and the set of
    /// land/sea region pairs that have a route.
    /// </para>
    /// </summary>
    [TestClass]
    public class TradeRoutesProcessorIntegrationTests
    {
        [TestMethod]
        public void TradeRoutes_TopologyMatchesRealPtd()
        {
            var cases = TradeRoutesTestMaps.EnumerateWithReference();
            if (cases.Count == 0)
                Assert.Inconclusive("No TestData/campaign_maps/<map>/trade_routes.ptd paired with a template map.hex was found.");

            var tested = new List<string>();
            foreach (var (mapName, mapHexPath, referencePtdPath) in cases)
            {
                var tempPtd = Path.Combine(Path.GetTempPath(), $"caime_traderoutes_{mapName}_{Guid.NewGuid():N}.ptd");
                try
                {
                    TradeRoutesPipeline.Export(mapHexPath, tempPtd);

                    var generated = PtdFile.Read(tempPtd);
                    var reference = PtdFile.Read(referencePtdPath);

                    Assert.AreEqual(reference.Version, generated.Version, $"{mapName}: PTD version differs.");

                    CollectionAssert.AreEqual(reference.LandRegions.ToList(), generated.LandRegions.ToList(),
                        $"{mapName}: land region table differs (count {reference.LandRegions.Count} vs {generated.LandRegions.Count}).");

                    // Topology: the unique (source, destination) region pairs must match the reference,
                    // both the full adjacency set and the subset that actually resolves to a route.
                    AssertSetEquals(mapName, "land (all pairs)",       Pairs(reference.LandRoutes),          Pairs(generated.LandRoutes));
                    AssertSetEquals(mapName, "sea (all pairs)",        Pairs(reference.SeaRoutes),           Pairs(generated.SeaRoutes));
                    AssertSetEquals(mapName, "land (reachable pairs)", ReachablePairs(reference.LandRoutes), ReachablePairs(generated.LandRoutes));
                    AssertSetEquals(mapName, "sea (reachable pairs)",  ReachablePairs(reference.SeaRoutes),  ReachablePairs(generated.SeaRoutes));

                    Assert.IsTrue(generated.Splines.Count > 0, $"{mapName}: no splines were produced.");

                    tested.Add(mapName);
                }
                finally
                {
                    if (File.Exists(tempPtd)) File.Delete(tempPtd);
                }
            }

            Assert.IsTrue(tested.Count > 0, "No trade-route cases were exercised.");
        }

        private static HashSet<(int, int)> Pairs(IEnumerable<PtdRoute> routes)
            => new HashSet<(int, int)>(routes.Select(r => (r.Source, r.Destination)));

        private static HashSet<(int, int)> ReachablePairs(IEnumerable<PtdRoute> routes)
            => new HashSet<(int, int)>(routes.Where(r => r.SplineReferences.Count > 0).Select(r => (r.Source, r.Destination)));

        private static void AssertSetEquals(string mapName, string kind, HashSet<(int, int)> expected, HashSet<(int, int)> actual)
        {
            if (expected.SetEquals(actual))
                return;

            var missing = expected.Except(actual).Take(10).ToList();
            var extra   = actual.Except(expected).Take(10).ToList();
            Assert.Fail(
                $"{mapName}: {kind} route pair-set differs. " +
                $"expected {expected.Count} unique pairs, got {actual.Count}. " +
                $"missing (first {missing.Count}): {string.Join(", ", missing)}; " +
                $"extra (first {extra.Count}): {string.Join(", ", extra)}.");
        }
    }
}
