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
    /// Differential test for the ROAD_SEGMENTS section of pathfinding.ppd, against real, game-ready
    /// .ppd files.
    ///
    /// <para>
    /// <see cref="PathfindingPipeline.ComputeRoads"/> runs the <see cref="RoadsGenerator"/> over the
    /// prepared template map, the result is serialized through the exporter's own writer seam
    /// (<see cref="PathfindingExporter.WriteRoads"/>), and the bytes are diffed against the
    /// ROAD_SEGMENTS section lifted out of a real .ppd.
    /// </para>
    ///
    /// <para>
    /// The comparison is byte-exact. Each road segment carries an ordered list of SEGMENT_DIRECTION
    /// region pairs and an ordered list of road hexes (each with a Q/R coordinate and an edge mask), so
    /// a byte-exact match proves: the same number of segments, the same number of direction pairs and
    /// hexes per segment, the same coordinates and masks, and the same discovery order as the native
    /// generator. Before the raw compare the per-segment headers are parsed out of both sides so a
    /// mismatch reports the offending segment / count rather than a bare byte offset.
    /// </para>
    ///
    /// <para>
    /// Reaching a byte-exact match required three fixes to the road pipeline: preserving the road edge
    /// mask exactly as authored in the source map (the exporter now calls <c>EnsureRoadEdgeMasks</c>,
    /// which keeps the loaded mask unless the layer was edited, instead of unconditionally recomputing a
    /// town-centre-filtered one); keeping the route and route-group lists sorted by region pair (matching
    /// the native lower-bound insertion); and using the invalid-region sentinel rather than 0 in the
    /// sprawl/dead-end guards, since region indices are 0-based here.
    /// </para>
    /// </summary>
    [TestClass]
    public class RoadsGeneratorIntegrationTests
    {
        [TestMethod]
        public void RoadSegmentsSection_MatchesRealPpd()
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

                var segments = PathfindingPipeline.ComputeRoads(map);

                AssertRoadsMatch(test, outDir,
                    ppd.RoadSegmentsRaw,
                    PpdSection.Serialize(w => PathfindingExporter.WriteRoads(w, map, segments)));

                tested.Add(test.MapName);
            }

            Console.WriteLine(
                $"Verified ROAD_SEGMENTS section for {tested.Count} map(s): {string.Join(", ", tested)}");
        }

        /// <summary>
        /// Dumps both sides for visual inspection, checks the segment count and the per-segment
        /// direction/hex counts first (friendlier first failures than a raw byte mismatch), then asserts
        /// size and byte-exact equality.
        /// </summary>
        private static void AssertRoadsMatch(
            PathfindingTestCase test, string outDir, byte[] expected, byte[] actual)
        {
            PpdSection.WriteRaw(outDir, test.MapName, "road_segments", expected, actual);

            var expectedSegments = ParseSegmentHeaders(expected);
            var actualSegments   = ParseSegmentHeaders(actual);

            Assert.AreEqual(expectedSegments.Count, actualSegments.Count,
                $"{test.MapName}: road segment count differs (expected {expectedSegments.Count}, " +
                $"got {actualSegments.Count}).");

            for (int i = 0; i < expectedSegments.Count; ++i)
            {
                Assert.AreEqual(expectedSegments[i].DirectionCount, actualSegments[i].DirectionCount,
                    $"{test.MapName}: segment {i} direction (region-pair) count differs " +
                    $"(expected {expectedSegments[i].DirectionCount}, got {actualSegments[i].DirectionCount}).");

                Assert.AreEqual(expectedSegments[i].HexCount, actualSegments[i].HexCount,
                    $"{test.MapName}: segment {i} road-hex count differs " +
                    $"(expected {expectedSegments[i].HexCount}, got {actualSegments[i].HexCount}).");
            }

            Assert.AreEqual(expected.Length, actual.Length,
                $"{test.MapName}: ROAD_SEGMENTS section size differs (expected {expected.Length}, got {actual.Length} bytes).");

            for (int i = 0; i < expected.Length; ++i)
            {
                if (expected[i] != actual[i])
                {
                    Assert.Fail(
                        $"{test.MapName}: ROAD_SEGMENTS bytes differ from the real pathfinding.ppd, first at byte {i} " +
                        $"(expected 0x{expected[i]:X2}, got 0x{actual[i]:X2}).");
                }
            }
        }

        private struct SegmentHeader
        {
            public int DirectionCount;
            public int HexCount;
        }

        /// <summary>
        /// Walks a serialized ROAD_SEGMENTS blob and returns the (direction count, hex count) header of
        /// each segment, matching the layout <see cref="PathfindingExporter.WriteRoads"/> writes:
        /// <c>uint16 segmentCount</c>, then per segment an <c>int32</c> direction count + that many
        /// 4-byte region pairs, then a <c>uint16</c> hex count + that many 5-byte hex/edge entries.
        /// </summary>
        private static List<SegmentHeader> ParseSegmentHeaders(byte[] section)
        {
            var headers = new List<SegmentHeader>();
            using (var ms = new MemoryStream(section, writable: false))
            using (var br = new BinaryReader(ms))
            {
                int segmentCount = br.ReadUInt16();
                for (int i = 0; i < segmentCount; ++i)
                {
                    int directions = br.ReadInt32();
                    br.BaseStream.Seek(directions * 4, SeekOrigin.Current);
                    int hexes = br.ReadUInt16();
                    br.BaseStream.Seek(hexes * 5, SeekOrigin.Current);

                    headers.Add(new SegmentHeader { DirectionCount = directions, HexCount = hexes });
                }
            }

            return headers;
        }
    }
}
