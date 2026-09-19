using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Integration
{
    /// <summary>
    /// Snapshot (golden-master) test: regenerates each map's trade_routes.ptd through the exporter's
    /// public entry point and compares it, byte-for-byte, against a committed
    /// <c>trade_routes.golden.ptd</c> stored next to that map's data in TestData. Unlike the
    /// differential test (which only checks route topology), this catches any change to spline
    /// geometry, segment ordering or encoding.
    ///
    /// <para>
    /// The goldens were produced by the exporter this one replaced, so a pass here is also the
    /// statement that the two implementations agree byte-for-byte on every map with reference data.
    /// </para>
    ///
    /// <para>
    /// The expected output is the on-disk golden, not a hardcoded value, so the test follows whatever
    /// maps are present in TestData. To create or refresh the goldens after an intended output change,
    /// run with the environment variable <c>CAIME_UPDATE_TRADE_ROUTES_GOLDEN=1</c> and commit them.
    /// </para>
    /// </summary>
    [TestClass]
    public class TradeRoutesGoldenMasterTests
    {
        private const string UpdateEnvVar = "CAIME_UPDATE_TRADE_ROUTES_GOLDEN";

        [TestMethod]
        public void TradeRoutes_OutputMatchesGoldenSnapshot()
        {
            var cases = TradeRoutesTestMaps.EnumerateWithReference();
            if (cases.Count == 0)
                Assert.Inconclusive("No TestData/campaign_maps/<map>/trade_routes.ptd paired with a template map.hex was found.");

            bool updateGoldens = Environment.GetEnvironmentVariable(UpdateEnvVar) == "1";
            var updated = new List<string>();
            var missing = new List<string>();

            foreach (var (mapName, mapHexPath, referencePtdPath) in cases)
            {
                var goldenPath = Path.Combine(Path.GetDirectoryName(referencePtdPath), "trade_routes.golden.ptd");
                var generated  = TradeRoutesPipeline.Export(mapHexPath);

                if (updateGoldens)
                {
                    File.WriteAllBytes(goldenPath, generated);
                    updated.Add(mapName);
                }
                else if (!File.Exists(goldenPath))
                {
                    missing.Add(mapName);
                }
                else
                {
                    AssertBytesEqual(mapName, File.ReadAllBytes(goldenPath), generated);
                }
            }

            if (updated.Count > 0)
                Assert.Inconclusive($"Wrote golden snapshots for: {string.Join(", ", updated)}. Commit them and re-run.");

            if (missing.Count > 0)
                Assert.Inconclusive($"No golden snapshot for: {string.Join(", ", missing)}. Create them with {UpdateEnvVar}=1.");
        }

        /// <summary>
        /// Compares the two files and, on a mismatch, names the PTD field that holds the first
        /// differing byte - which is usually enough to locate the rule that was misread.
        /// </summary>
        private static void AssertBytesEqual(string mapName, byte[] expected, byte[] actual)
        {
            int firstDifference = FirstDifference(expected, actual);
            if (firstDifference < 0 && expected.Length == actual.Length)
                return;

            var message = new StringBuilder();
            message.Append($"{mapName}: trade_routes.ptd output differs from the golden snapshot. ");

            if (expected.Length != actual.Length)
                message.Append($"Length {expected.Length} vs {actual.Length} bytes. ");

            if (firstDifference >= 0)
            {
                var layout = PtdLayout.Parse(expected);
                message.Append($"First difference at byte {firstDifference} (0x{firstDifference:X}), in {layout.Describe(firstDifference)}: ");
                message.Append($"expected {Hex(expected, firstDifference)}, got {Hex(actual, firstDifference)}. ");
            }
            else
            {
                message.Append($"The shorter file is a prefix of the longer one; it is truncated at byte {Math.Min(expected.Length, actual.Length)}. ");
            }

            message.Append($"If this change is intended, regenerate with {UpdateEnvVar}=1.");
            Assert.Fail(message.ToString());
        }

        private static int FirstDifference(byte[] a, byte[] b)
        {
            int shared = Math.Min(a.Length, b.Length);
            for (int i = 0; i < shared; ++i)
            {
                if (a[i] != b[i])
                    return i;
            }

            return -1;
        }

        /// <summary>Renders a short window of bytes at <paramref name="offset"/> for the failure message.</summary>
        private static string Hex(byte[] bytes, int offset)
        {
            int end = Math.Min(bytes.Length, offset + 8);
            return string.Join(" ", Enumerable.Range(offset, end - offset).Select(i => bytes[i].ToString("X2")));
        }
    }
}
