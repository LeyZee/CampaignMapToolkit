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
    /// Differential test for the PASSABLE_BORDER_HEXES section of pathfinding.ppd.
    ///
    /// <para>
    /// The current <see cref="BordersGenerator"/> regenerates the all-borders data from the
    /// template map, the exporter's <see cref="PathfindingExporter.WritePassableBorderHexes"/>
    /// seam serializes it to game-ready bytes, and those bytes are diffed against the same section
    /// extracted from the real .ppd — count first, then byte-for-byte.
    /// </para>
    /// </summary>
    [TestClass]
    public class BordersGeneratorIntegrationTests
    {
        [TestMethod]
        public void PassableBorderHexesSection_MatchesRealPpd()
        {
            var cases = PathfindingTestData.EnumerateCases();
            if (cases.Count == 0)
                Assert.Inconclusive("No TestData/<map>/pathfinding.ppd paired with a template map.hex was found.");

            var outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Integration");
            var tested = new List<string>();

            foreach (var test in cases)
            {
                var map = test.LoadPreparedMap();

                var tileGroupIndices = PathfindingPipeline.ComputeTileGroupIndices(map);

                var gen = new BordersGenerator(map, debugPath: null, bWriteDebug: false);
                gen.Generate(out _, out var bordersData, map.LandRegions.Count, tileGroupIndices);

                var actual   = PpdSection.Serialize(w => PathfindingExporter.WritePassableBorderHexes(w, bordersData));
                var expected = test.ReadPpd().PassableBorderHexesRaw;

                var width  = (int)map.MapWidth;
                var height = (int)map.MapHeight;

                var expectedBuf = new byte[width * height];
                using (var ms = new MemoryStream(expected))
                using (var br = new BinaryReader(ms))
                {
                    int hexCount = br.ReadInt32();
                    for (int i = 0; i < hexCount; i++) { int q = br.ReadUInt16(), r = br.ReadUInt16(); expectedBuf[r * width + q] = 1; }
                }

                var actualBuf = new byte[width * height];
                foreach (var hex in bordersData.Hexes) actualBuf[hex.R * width + hex.Q] = 1;

                PpdSection.WriteRaw(outDir, test.MapName, "passable_border_hexes", expectedBuf, actualBuf);

                Assert.AreEqual(expected.Length, actual.Length,
                    $"{test.MapName}: passable-border-hexes section size differs (expected {expected.Length}, got {actual.Length}).");
                CollectionAssert.AreEqual(expected, actual,
                    $"{test.MapName}: passable-border-hexes section bytes differ from the real pathfinding.ppd.");

                tested.Add(test.MapName);
            }

            Console.WriteLine($"Verified passable-border-hexes section for {tested.Count} map(s): {string.Join(", ", tested)}");
        }
    }
}
