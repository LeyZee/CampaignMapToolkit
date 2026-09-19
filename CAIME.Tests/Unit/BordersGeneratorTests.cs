using CAIME;
using CAIME.Pathfinding;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Regression and behaviour tests for <c>BordersGenerator</c>.
    ///
    /// Grid layouts used throughout:
    ///   1×N  — width=1, height=N.  Q=0 for all hexes; index = R.
    ///           dir 0 (dQ=0,dR=+1) reaches the hex above; dir 3 (dQ=0,dR=-1) below.
    ///           All other directions go out of bounds (they change Q).
    ///
    ///   3×1  — width=3, height=1.  R=0 for all hexes; index = Q.
    ///           Even-column hexes (Q&amp;1==0): dir 1 (+1,0) reaches right, dir 5 (-1,0) reaches left.
    ///           Odd-column hex  (Q&amp;1==1): dir 2 (+1,0) reaches right, dir 4 (-1,0) reaches left.
    /// </summary>
    [TestClass]
    public class BordersGeneratorTests
    {
        // -----------------------------------------------------------------------
        //  Hex factory
        // -----------------------------------------------------------------------

        private static Hex MakeHex(
            int q, int r, int index, int regionId,
            bool isBorder   = true,
            bool isPassable = true,
            bool isSea      = false,
            bool isCliff    = false,
            bool isBeach    = false)
        {
            var h = new Hex(q, r, index)
            {
                RegionId  = regionId,
                IsBorder  = isBorder,
                IsPassable = isPassable,
                IsSea     = isSea,
                IsCliff   = isCliff,
                IsBeach   = isBeach,
            };
            return h;
        }

        // Convenience: 1×N column hex (Q=0, R=row, index=row)
        private static Hex Col(int row, int regionId, bool isBorder = true, bool isPassable = true,
            bool isCliff = false, bool isBeach = false)
            => MakeHex(0, row, row, regionId, isBorder, isPassable, isCliff: isCliff, isBeach: isBeach);

        // Convenience: 3×1 row hex (Q=col, R=0, index=col)
        private static Hex Row(int col, int regionId)
            => MakeHex(col, 0, col, regionId);

        // -----------------------------------------------------------------------
        //  Test runner helpers
        // -----------------------------------------------------------------------

        private static (Border[] borders, BordersData bordersData) RunGenerate(
            Hex[] hexData, uint width, uint height, int landRegions)
        {
            var map = BordersTestHarness.BuildMap(hexData, width, height);
            var gen = new BordersGenerator(map, debugPath: null, bWriteDebug: false);

            // CalculateAllBorders now classifies by tile group (HLCI area, region). These minimal
            // maps have no HLCI stage, so model each region as its own tile group — that makes the
            // tile-group comparison reduce to a region comparison, which is what these cases exercise.
            var tileGroupIndices = new int[width * height];
            foreach (var hex in hexData)
                tileGroupIndices[hex.R * width + hex.Q] = hex.RegionId;

            gen.Generate(out var borders, out var bordersData, landRegions, tileGroupIndices);
            return (borders, bordersData);
        }

        private static Border[] RunLandBorders(
            Hex[] hexData, uint width, uint height, int landRegions)
        {
            var map = BordersTestHarness.BuildMap(hexData, width, height);
            var gen = new BordersGenerator(map, debugPath: null, bWriteDebug: false);
            gen.GenerateLandBorders(out var borders, landRegions);
            return borders;
        }

        // ====================================================================
        //  CalculateLandBorders  (via GenerateLandBorders)
        // ====================================================================

        [TestMethod]
        public void LandBorders_AdjacentDifferentRegions_RecordedInBothRegions()
        {
            // 1×2: hex0(region 0) — hex1(region 1)
            // Each region should record the other's hex index as its border entry.
            var hexes = new[] { Col(0, regionId: 0), Col(1, regionId: 1) };
            var borders = RunLandBorders(hexes, 1, 2, landRegions: 2);

            CollectionAssert.AreEquivalent(new[] { 1 }, borders[0].Hexes,
                "region 0 border should list hex1 (index 1)");
            CollectionAssert.AreEquivalent(new[] { 0 }, borders[1].Hexes,
                "region 1 border should list hex0 (index 0)");
        }

        [TestMethod]
        public void LandBorders_SameRegionNeighbors_NoBorderRecorded()
        {
            // Adjacent hexes in the same region → no cross-region border.
            var hexes = new[] { Col(0, regionId: 0), Col(1, regionId: 0) };
            var borders = RunLandBorders(hexes, 1, 2, landRegions: 1);

            Assert.AreEqual(0, borders[0].Hexes.Count);
        }

        [TestMethod]
        public void LandBorders_CoastSourceHex_ExcludedAndNeighborNotListed()
        {
            // hex0 has IsCliff=true → IsCoast=true → IsPassableLandBorder=false (not a source).
            // hex1 checks hex0 as a neighbour: IsPassableLandBorder(hex0)=false,
            // HasPassableNeighbours(hex0) also returns false (IsCoast short-circuits).
            // → hex0 is not listed in borders[1] either.
            var hexes = new[]
            {
                Col(0, regionId: 0, isCliff: true),
                Col(1, regionId: 1),
            };
            var borders = RunLandBorders(hexes, 1, 2, landRegions: 2);

            Assert.AreEqual(0, borders[0].Hexes.Count, "coast hex produces no border entries");
            Assert.AreEqual(0, borders[1].Hexes.Count, "coast neighbour is excluded via HasPassableNeighbours");
        }

        [TestMethod]
        public void LandBorders_BeachSourceHex_ExcludedAndNeighborNotListed()
        {
            // IsBeach=true → IsCoast=true, same exclusion path as cliff.
            var hexes = new[]
            {
                Col(0, regionId: 0, isBeach: true),
                Col(1, regionId: 1),
            };
            var borders = RunLandBorders(hexes, 1, 2, landRegions: 2);

            Assert.AreEqual(0, borders[0].Hexes.Count);
            Assert.AreEqual(0, borders[1].Hexes.Count);
        }

        [TestMethod]
        public void LandBorders_ImpassableNeighborWithPassableSubNeighbor_Included()
        {
            // 1×3: hex0(region 0, passable) — hex1(region 1, impassable) — hex2(region 1, passable)
            //
            // hex0 (source) checks hex1:
            //   IsPassableLandBorder(hex1) = false (impassable).
            //   HasPassableNeighbours(hex1): hex0 itself is passable land → true.
            // → hex1 (index 1) is added to borders[0].
            //
            // hex2 (source) checks hex1 (same region) → skipped.
            var hexes = new[]
            {
                Col(0, regionId: 0),
                Col(1, regionId: 1, isPassable: false),
                Col(2, regionId: 1),
            };
            var borders = RunLandBorders(hexes, 1, 3, landRegions: 2);

            CollectionAssert.AreEquivalent(new[] { 1 }, borders[0].Hexes,
                "impassable hex with passable sub-neighbour must be listed");
            Assert.AreEqual(0, borders[1].Hexes.Count,
                "impassable hex is not a source; region 1 has no cross-region border here");
        }

        [TestMethod]
        public void LandBorders_NoDuplicates_SameNeighborSeenFromMultipleSources()
        {
            // 3×1: hex0(region 0) — hex1(region 1) — hex2(region 0)
            // Both hex0 and hex2 are in region 0 and border hex1.
            // hex1's index must appear in borders[0] exactly once.
            var hexes = new[] { Row(0, regionId: 0), Row(1, regionId: 1), Row(2, regionId: 0) };
            var borders = RunLandBorders(hexes, 3, 1, landRegions: 2);

            Assert.AreEqual(1, borders[0].Hexes.Count,
                "hex1 must appear in region 0's border list exactly once despite two sources");
            Assert.AreEqual(1, borders[0].Hexes[0]);

            // hex1 (region 1) can reach both hex0 and hex2 → both recorded.
            CollectionAssert.AreEquivalent(new[] { 0, 2 }, borders[1].Hexes);
        }

        [TestMethod]
        public void LandBorders_NonBorderHex_NotASource()
        {
            // hex0 has IsBorder=false → IsPassableLandBorder=false → skipped as a source.
            // hex1 checks hex0: IsPassableLandBorder=false, but IsPassable=true →
            // HasPassableNeighbours returns true → hex0 IS listed in borders[1].
            var hexes = new[]
            {
                Col(0, regionId: 0, isBorder: false),
                Col(1, regionId: 1),
            };
            var borders = RunLandBorders(hexes, 1, 2, landRegions: 2);

            Assert.AreEqual(0, borders[0].Hexes.Count, "non-border hex produces no entries as source");
            // hex0 index appears in borders[1] because it is passable and adjacent
            CollectionAssert.AreEquivalent(new[] { 0 }, borders[1].Hexes);
        }

        // ====================================================================
        //  CalculateAllBorders  (via Generate → bordersData)
        // ====================================================================

        [TestMethod]
        public void AllBorders_TwoBorderHexesDifferentRegions_BothAddedToBordersData()
        {
            // Basic: two passable border hexes in different regions → both in bordersData.
            var hexes = new[] { Col(0, regionId: 0), Col(1, regionId: 1) };
            var (_, bordersData) = RunGenerate(hexes, 1, 2, landRegions: 2);

            Assert.AreEqual(2, bordersData.Hexes.Count);
        }

        [TestMethod]
        public void AllBorders_SameRegionNeighbors_NeitherAddedToBordersData()
        {
            var hexes = new[] { Col(0, regionId: 0), Col(1, regionId: 0) };
            var (_, bordersData) = RunGenerate(hexes, 1, 2, landRegions: 1);

            Assert.AreEqual(0, bordersData.Hexes.Count);
        }

        [TestMethod]
        public void AllBorders_ImpassableHex_ExcludedFromBordersData()
        {
            // hex0 impassable → excluded as source.
            // hex1 sees hex0: nbr.IsImpassable=true → excluded as a qualifying neighbour.
            var hexes = new[]
            {
                Col(0, regionId: 0, isPassable: false),
                Col(1, regionId: 1),
            };
            var (_, bordersData) = RunGenerate(hexes, 1, 2, landRegions: 2);

            Assert.AreEqual(0, bordersData.Hexes.Count);
        }

        [TestMethod]
        public void AllBorders_CliffHex_ExcludedFromBordersData()
        {
            // hex0 IsCliff=true → excluded as source AND as qualifying neighbour.
            var hexes = new[]
            {
                Col(0, regionId: 0, isCliff: true),
                Col(1, regionId: 1),
            };
            var (_, bordersData) = RunGenerate(hexes, 1, 2, landRegions: 2);

            Assert.AreEqual(0, bordersData.Hexes.Count);
        }

        [TestMethod]
        public void AllBorders_IsBorderFlagIgnored_ClassifiedByTileGroup()
        {
            // The game's all-borders pass has no IsBorder concept: membership is purely passable +
            // non-cliff with a neighbour in a different tile group. Two passable hexes in different
            // tile groups are both included even with IsBorder=false.
            var hexes = new[]
            {
                Col(0, regionId: 0, isBorder: false),
                Col(1, regionId: 1, isBorder: false),
            };
            var (_, bordersData) = RunGenerate(hexes, 1, 2, landRegions: 2);

            Assert.AreEqual(2, bordersData.Hexes.Count);
        }

        [TestMethod]
        public void AllBorders_NoDuplicates_HexWithMultipleQualifyingNeighbors()
        {
            // 3×1: hex0(region 0) — hex1(region 1) — hex2(region 0)
            // hex1 qualifies on both dir 4 (→hex0) and dir 2 (→hex2), but must appear only once.
            var hexes = new[] { Row(0, regionId: 0), Row(1, regionId: 1), Row(2, regionId: 0) };
            var (_, bordersData) = RunGenerate(hexes, 3, 1, landRegions: 2);

            Assert.AreEqual(3, bordersData.Hexes.Count,
                "each of the three hexes should appear exactly once");
        }
    }
}
