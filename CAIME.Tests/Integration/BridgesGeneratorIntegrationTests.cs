using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CAIME;
using CAIME.Pathfinding;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Integration
{
    /// <summary>
    /// Differential test for the BRIDGES_ARRAY section of pathfinding.ppd.
    ///
    /// <para>
    /// For every map that has both a template <c>map.hex</c> and a game-ready
    /// <c>pathfinding.ppd</c>, the current <see cref="BridgesGenerator"/> regenerates the bridges
    /// data, the exporter's <see cref="PathfindingExporter.WriteBridges"/> seam serializes it, and
    /// the result is compared against the bridges section lifted out of the real .ppd.
    /// </para>
    ///
    /// <para>
    /// The comparison is order-independent at the <em>bridge</em> level.
    /// We therefore match the <em>set</em> of bridges rather than their sequence.
    /// Everything that <em>does</em> carry meaning is still asserted
    /// byte-exactly: each bridge must appear with the correct two sides, the correct side ordering
    /// (side 1 vs side 2 must not be swapped), and the correct hex order within each side.
    /// </para>
    /// </summary>
    [TestClass]
    public class BridgesGeneratorIntegrationTests
    {
        [TestMethod]
        public void BridgesSection_MatchesRealPpd()
        {
            var cases = PathfindingTestData.EnumerateCases();
            if (cases.Count == 0)
                Assert.Inconclusive("No TestData/<map>/pathfinding.ppd paired with a template map.hex was found.");

            var outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Integration");
            var tested = new List<string>();

            foreach (var test in cases)
            {
                var map = test.LoadPreparedMap();

                var gen = new BridgesGenerator(map, debugPath: null, bWriteDebug: false);
                gen.Generate(out var bridgesData);

                var actual   = PpdSection.Serialize(w => PathfindingExporter.WriteBridges(w, bridgesData));
                var expected = test.ReadPpd().BridgesRaw;

                var expectedBridges = ParseBridges(expected);
                var actualBridges   = ParseBridges(actual);

                // Spatial dumps for Photoshop inspection (side 1 = value 1, side 2 = value 2).
                int width  = (int)map.MapWidth;
                int height = (int)map.MapHeight;
                PpdSection.WriteRaw(outDir, test.MapName, "bridges",
                    RenderSides(expectedBridges, width, height),
                    RenderSides(actualBridges,   width, height));

                Assert.AreEqual(expectedBridges.Count, actualBridges.Count,
                    $"{test.MapName}: bridge count differs (expected {expectedBridges.Count}, got {actualBridges.Count}).");

                // Order-independent at the bridge level, but each bridge's two sides (and the hex
                // order within them) must match exactly — see the class summary.
                var expectedKeys = expectedBridges.Select(BridgeKey).OrderBy(s => s, StringComparer.Ordinal).ToList();
                var actualKeys   = actualBridges.Select(BridgeKey).OrderBy(s => s, StringComparer.Ordinal).ToList();

                CollectionAssert.AreEqual(expectedKeys, actualKeys,
                    $"{test.MapName}: bridge set differs from the real pathfinding.ppd " +
                    "(a side assignment, side ordering, or per-side hex order does not match).");

                tested.Add(test.MapName);
            }

            Console.WriteLine($"Verified bridges section for {tested.Count} map(s): {string.Join(", ", tested)}");
        }

        private readonly struct Coord
        {
            public readonly int Q;
            public readonly int R;
            public Coord(int q, int r) { Q = q; R = r; }
            public override string ToString() => $"{Q}:{R}";
        }

        private sealed class Bridge
        {
            public List<Coord> Side1 = new List<Coord>();
            public List<Coord> Side2 = new List<Coord>();
        }

        /// <summary>Parses a serialized BRIDGES_ARRAY section into its bridges and their two sides.</summary>
        private static List<Bridge> ParseBridges(byte[] section)
        {
            var bridges = new List<Bridge>();
            using (var ms = new MemoryStream(section))
            using (var br = new BinaryReader(ms))
            {
                int count = br.ReadUInt16();
                for (int b = 0; b < count; b++)
                {
                    var bridge = new Bridge();
                    int s1 = br.ReadUInt16();
                    for (int i = 0; i < s1; i++) bridge.Side1.Add(new Coord(br.ReadUInt16(), br.ReadUInt16()));
                    int s2 = br.ReadUInt16();
                    for (int i = 0; i < s2; i++) bridge.Side2.Add(new Coord(br.ReadUInt16(), br.ReadUInt16()));
                    bridges.Add(bridge);
                }
            }
            return bridges;
        }

        /// <summary>
        /// Canonical key preserving side identity and per-side hex order, so a swapped side or a
        /// reordered side produces a different key (and thus a test failure).
        /// </summary>
        private static string BridgeKey(Bridge b)
            => string.Join(",", b.Side1) + "|" + string.Join(",", b.Side2);

        private static byte[] RenderSides(List<Bridge> bridges, int width, int height)
        {
            var buf = new byte[width * height];
            foreach (var b in bridges)
            {
                foreach (var c in b.Side1) buf[c.R * width + c.Q] = 1;
                foreach (var c in b.Side2) buf[c.R * width + c.Q] = 2;
            }
            return buf;
        }
    }
}
