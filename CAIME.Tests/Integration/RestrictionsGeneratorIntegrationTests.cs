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
    /// Differential test for the RESTRICTION_LEVELS section of pathfinding.ppd.
    ///
    /// <para>
    /// The current <see cref="RestrictionsGenerator"/> regenerates the restriction levels from the
    /// template map, the exporter's <see cref="PathfindingExporter.WriteRestrictions"/> seam
    /// serializes them to game-ready bytes, and those bytes are diffed against the same section
    /// extracted from the real .ppd. Maps whose .ppd predates restriction support (no section) are
    /// skipped rather than failed.
    /// </para>
    /// </summary>
    [TestClass]
    public class RestrictionsGeneratorIntegrationTests
    {
        [TestMethod]
        public void RestrictionsSection_MatchesRealPpd()
        {
            var cases = PathfindingTestData.EnumerateCases();
            if (cases.Count == 0)
                Assert.Inconclusive("No TestData/<map>/pathfinding.ppd paired with a template map.hex was found.");

            var outDir  = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Integration");
            var tested  = new List<string>();
            var skipped = new List<string>();

            foreach (var test in cases)
            {
                var ppd = test.ReadPpd();
                if (!ppd.HasRestrictions)
                {
                    // This .ppd was exported without a restrictions section (older map version).
                    skipped.Add($"{test.MapName} (no restrictions section)");
                    continue;
                }

                var map = test.LoadPreparedMap();

                var gen = new RestrictionsGenerator(map, debugPath: null);
                gen.Generate(out var restrictions);

                var actual   = PpdSection.Serialize(w => PathfindingExporter.WriteRestrictions(w, restrictions));
                var expected = ppd.RestrictionsRaw;

                var width  = (int)map.MapWidth;
                var height = (int)map.MapHeight;

                var expectedBuf = new byte[width * height];
                using (var ms = new MemoryStream(expected))
                using (var br = new BinaryReader(ms))
                {
                    int levelCount = br.ReadByte();
                    for (byte value = 1; value <= levelCount; value++)
                    {
                        int hexCount = br.ReadInt32();
                        for (int i = 0; i < hexCount; i++) { int q = br.ReadUInt16(), r = br.ReadUInt16(); br.ReadByte(); expectedBuf[r * width + q] = value; }
                    }
                }

                var actualBuf = new byte[width * height];
                {
                    byte value = 1;
                    foreach (var restriction in restrictions)
                    {
                        if (restriction.Hexes.Count > 0)
                        {
                            foreach (var hex in restriction.Hexes) actualBuf[hex.R * width + hex.Q] = value;
                            value++;
                        }
                    }
                }

                PpdSection.WriteRaw(outDir, test.MapName, "restrictions", expectedBuf, actualBuf);

                Assert.AreEqual(expected.Length, actual.Length,
                    $"{test.MapName}: restrictions section size differs (expected {expected.Length}, got {actual.Length}).");
                CollectionAssert.AreEqual(expected, actual,
                    $"{test.MapName}: restrictions section bytes differ from the real pathfinding.ppd.");

                tested.Add(test.MapName);
            }

            if (tested.Count == 0)
                Assert.Inconclusive(
                    $"No map with a restrictions section was found. Skipped: {string.Join(", ", skipped)}");

            if (skipped.Count > 0)
                Console.WriteLine($"Skipped {skipped.Count} map(s): {string.Join("; ", skipped)}");

            Console.WriteLine($"Verified restrictions section for {tested.Count} map(s): {string.Join(", ", tested)}");
        }
    }
}
