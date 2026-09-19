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
    /// Differential test for the two HLCI-derived sections of pathfinding.ppd that share the
    /// bridges → HLCI pipeline: BEACHES_ARRAY and HLCI_CONNECTIONS.
    ///
    /// <para>
    /// Both sections are built from the same HLCI area/pair data — the beaches are grouped by the
    /// (land, sea) HLCI pairs the HLCI generator discovers, and HLCI_CONNECTIONS is exactly that
    /// pair list. <see cref="PathfindingPipeline.ComputeBeachesAndConnections"/> therefore replays
    /// the pipeline once and both sections are serialized through the exporter's own writer seams
    /// (<see cref="PathfindingExporter.WriteBeaches"/> / <see cref="PathfindingExporter.WriteHlciConnections"/>)
    /// and diffed against the matching sections lifted out of a real, game-ready .ppd.
    /// </para>
    ///
    /// <para>
    /// The comparison is byte-exact. Sections are reproduced byte-for-byte on every test map
    /// — including each beach connection's hex order and
    /// edge masks and the HLCI pair ordering — so the connection / pair / per-side hex order all carry
    /// meaning and a byte-exact match also proves the generators' discovery order matches the native one.
    /// </para>
    /// </summary>
    [TestClass]
    public class BeachesGeneratorIntegrationTests
    {
        [TestMethod]
        public void BeachesAndHlciConnectionSections_MatchRealPpd()
        {
            var cases = PathfindingTestData.EnumerateCases();
            if (cases.Count == 0)
                Assert.Inconclusive("No TestData/<map>/pathfinding.ppd paired with a template map.hex was found.");

            var outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Integration");
            var tested = new List<string>();

            foreach (var test in cases)
            {
                var map = test.LoadPreparedMap();
                var ppd = test.ReadPpd();

                var result = PathfindingPipeline.ComputeBeachesAndConnections(map);

                AssertSectionMatches(test, outDir, "hlci_connections",
                    ppd.HlciConnectionsRaw,
                    PpdSection.Serialize(w => PathfindingExporter.WriteHlciConnections(w, result.HlciPairs)),
                    expectedCount: ReadUInt16(ppd.HlciConnectionsRaw), actualCount: result.HlciPairs.Count,
                    countLabel: "HLCI connection");

                AssertSectionMatches(test, outDir, "beaches",
                    ppd.BeachesRaw,
                    PpdSection.Serialize(w => PathfindingExporter.WriteBeaches(w, map, result.Beaches)),
                    expectedCount: ReadUInt16(ppd.BeachesRaw), actualCount: result.Beaches.Count,
                    countLabel: "beach connection");

                tested.Add(test.MapName);
            }

            Console.WriteLine(
                $"Verified beaches + HLCI connection sections for {tested.Count} map(s): {string.Join(", ", tested)}");
        }

        /// <summary>
        /// Dumps both sides for visual inspection, checks the leading connection count first (a friendlier
        /// first failure than a raw byte mismatch), then asserts size and byte-exact equality.
        /// </summary>
        private static void AssertSectionMatches(
            PathfindingTestCase test, string outDir, string section, byte[] expected, byte[] actual,
            int expectedCount, int actualCount, string countLabel)
        {
            PpdSection.WriteRaw(outDir, test.MapName, section, expected, actual);

            Assert.AreEqual(expectedCount, actualCount,
                $"{test.MapName}: {countLabel} count differs (expected {expectedCount}, got {actualCount}).");

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

        /// <summary>Reads the leading <c>uint16</c> connection count from a serialized section.</summary>
        private static int ReadUInt16(byte[] section) => section[0] | (section[1] << 8);
    }
}
