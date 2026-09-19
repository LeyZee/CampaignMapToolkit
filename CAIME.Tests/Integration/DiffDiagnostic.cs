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
    /// Diagnostic tool to examine byte-level differences in failed sections.
    /// Run these tests to see detailed hex dumps of mismatches.
    /// </summary>
    [TestClass]
    public class DiffDiagnostic
    {
        [TestMethod]
        public void Bridges_ShowDifference()
        {
            var outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Integration");
            Directory.CreateDirectory(outDir);
            using (var log = File.CreateText(Path.Combine(outDir, "bridges_diff.txt")))
            {
                var cases = PathfindingTestData.EnumerateCases();
                foreach (var test in cases)
                {
                    if (test.MapName != "bel_attila_map") continue;

                    var map = test.LoadPreparedMap();
                    var gen = new BridgesGenerator(map, debugPath: null, bWriteDebug: false);
                    gen.Generate(out var bridgesData);

                    var actual = PpdSection.Serialize(w => PathfindingExporter.WriteBridges(w, bridgesData));
                    var expected = test.ReadPpd().BridgesRaw;

                    log.WriteLine($"\n=== {test.MapName} BRIDGES ===");
                    log.WriteLine($"Expected size: {expected.Length}, Actual size: {actual.Length}");
                    ShowDifference(log, "Bridges", expected, actual, maxLines: 50);
                    log.Flush();
                }
            }
        }

        [TestMethod]
        public void Borders_ShowDifference()
        {
            var outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Integration");
            Directory.CreateDirectory(outDir);
            using (var log = File.CreateText(Path.Combine(outDir, "borders_diff.txt")))
            {
                var cases = PathfindingTestData.EnumerateCases();
                foreach (var test in cases)
                {
                    if (test.MapName != "bel_attila_map") continue;

                    var map = test.LoadPreparedMap();
                    var tileGroupIndices = PathfindingPipeline.ComputeTileGroupIndices(map);
                    var gen = new BordersGenerator(map, debugPath: null, bWriteDebug: false);
                    gen.Generate(out _, out var bordersData, map.LandRegions.Count, tileGroupIndices);

                    var actual = PpdSection.Serialize(w => PathfindingExporter.WritePassableBorderHexes(w, bordersData));
                    var expected = test.ReadPpd().PassableBorderHexesRaw;

                    log.WriteLine($"\n=== {test.MapName} PASSABLE_BORDER_HEXES ===");
                    log.WriteLine($"Expected size: {expected.Length}, Actual size: {actual.Length}");
                    ShowDifference(log, "Borders", expected, actual, maxLines: 50);
                    log.Flush();
                }
            }
        }

        private static void ShowDifference(StreamWriter log, string name, byte[] expected, byte[] actual, int maxLines = 20)
        {
            int minLen = Math.Min(expected.Length, actual.Length);
            int diffCount = 0;

            for (int i = 0; i < minLen; ++i)
            {
                if (expected[i] != actual[i])
                {
                    if (diffCount < maxLines)
                    {
                        log.WriteLine($"  [{i:D4}] Expected: 0x{expected[i]:X2}, Actual: 0x{actual[i]:X2}");
                    }
                    diffCount++;
                }
            }

            if (diffCount > maxLines)
                log.WriteLine($"  ... and {diffCount - maxLines} more differences");
            log.WriteLine($"Total mismatches: {diffCount}/{minLen} bytes");
        }
    }
}
