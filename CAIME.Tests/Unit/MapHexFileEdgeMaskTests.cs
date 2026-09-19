using System.IO;
using CAIME;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Covers <see cref="MapHexFile"/>'s edge-mask machinery: the full recalculation passes, the
    /// incremental refresh driven by the dirty-hex sets, and the authored-mask preservation the two
    /// are built around. Masks are authored data that recomputation cannot always reproduce, so a
    /// load-then-save must return them untouched while a real edit rebuilds only what it affected.
    ///
    /// <para>
    /// Geometry: flat-top grid, direction 0 = (0,+1) and direction 3 = (0,-1) for both column
    /// parities, so hexes in one column are always neighbours via 0/3. On a 5-wide grid hexes 0, 1
    /// and 5 are mutually adjacent and serve as the triangle fixture.
    /// </para>
    /// </summary>
    [TestClass]
    public class MapHexFileEdgeMaskTests
    {
        private const uint WIDTH  = 5;
        private const uint HEIGHT = 5;

        private const int HEX_0_0 = 0;
        private const int HEX_1_0 = 1;
        private const int HEX_0_1 = 5;
        private const int HEX_1_1 = 6;
        private const int HEX_0_2 = 10;
        private const int HEX_0_3 = 15;
        private const int HEX_0_4 = 20;
        private const int FAR     = 24;

        [TestInitialize]
        public void Setup() => MapHexHarness.ResetStaticMaskState();

        [TestCleanup]
        public void Cleanup() => MapHexHarness.ResetStaticMaskState();

        [TestMethod]
        public void CalculateRoadEdgeMasks_StraightChain_LinksEachHexToItsNeighbourBothWays()
        {
            var map = MapHexHarness.BuildGrid(WIDTH, HEIGHT);
            MarkRoad(map, HEX_0_0, HEX_0_1, HEX_0_2);

            map.CalculateRoadEdgeMasks();

            Assert.AreEqual(Bit(0),          map.HexData[HEX_0_0].RoadEdgeMask, "Chain start");
            Assert.AreEqual(Bit(0) | Bit(3), map.HexData[HEX_0_1].RoadEdgeMask, "Chain middle");
            Assert.AreEqual(Bit(3),          map.HexData[HEX_0_2].RoadEdgeMask, "Chain end");
        }

        // The game data never closes a triangle: exactly one chord is dropped, leaving 2 edges = 4 bits.
        [TestMethod]
        public void CalculateRoadEdgeMasks_Triangle_DropsExactlyOneChord()
        {
            var map = MapHexHarness.BuildGrid(WIDTH, HEIGHT);
            MarkRoad(map, HEX_0_0, HEX_1_0, HEX_0_1);

            map.CalculateRoadEdgeMasks();

            var totalBits = HexGridUtility.GetNumBitsSetInEdgeMask(map.HexData[HEX_0_0].RoadEdgeMask)
                          + HexGridUtility.GetNumBitsSetInEdgeMask(map.HexData[HEX_1_0].RoadEdgeMask)
                          + HexGridUtility.GetNumBitsSetInEdgeMask(map.HexData[HEX_0_1].RoadEdgeMask);

            Assert.AreEqual(4, totalBits, "A triangle must keep two of its three edges.");
        }

        // Which chord is dropped follows paint order - the behaviour the original game data exhibits,
        // so it has to be reproducible from the recorded paint sequence alone.
        [TestMethod]
        public void CalculateRoadEdgeMasks_Triangle_ChordDroppedFollowsPaintOrder()
        {
            var defaultOrder = MapHexHarness.BuildGrid(WIDTH, HEIGHT);
            MarkRoad(defaultOrder, HEX_0_0, HEX_1_0, HEX_0_1);
            defaultOrder.CalculateRoadEdgeMasks();

            Assert.IsFalse(AreConnected(defaultOrder, HEX_1_0, HEX_0_1, isRoad: true),
                "In index order the 1 <-> 5 chord is the one dropped.");
            Assert.IsTrue(AreConnected(defaultOrder, HEX_0_0, HEX_0_1, isRoad: true), "0 <-> 5 must survive.");
            Assert.IsTrue(AreConnected(defaultOrder, HEX_0_0, HEX_1_0, isRoad: true), "0 <-> 1 must survive.");

            var paintedLast = MapHexHarness.BuildGrid(WIDTH, HEIGHT);
            MarkRoad(paintedLast, HEX_0_0, HEX_1_0, HEX_0_1);
            paintedLast.HexData[HEX_0_0].RoadPaintSeq = 3;

            paintedLast.CalculateRoadEdgeMasks();

            Assert.IsFalse(AreConnected(paintedLast, HEX_0_0, HEX_0_1, isRoad: true),
                "Painting hex 0 last must move the dropped chord onto one of its own edges.");
            Assert.IsTrue(AreConnected(paintedLast, HEX_1_0, HEX_0_1, isRoad: true),
                "The chord dropped in index order must now survive.");
        }

        // A dead-end continuing over a bridge gains the opposite edge, so the road reads as passing
        // through rather than stopping.
        [TestMethod]
        public void CalculateRoadEdgeMasks_DeadendBesideABridge_GainsTheOppositeEdge()
        {
            var map = MapHexHarness.BuildGrid(WIDTH, HEIGHT);
            MarkRoad(map, HEX_0_0, HEX_0_1);
            map.HexData[HEX_1_1].IsBridge = true;

            map.CalculateRoadEdgeMasks();

            Assert.AreEqual(Bit(3) | Bit(0), map.HexData[HEX_0_1].RoadEdgeMask,
                "A single-edge road hex bordering a bridge must gain the inverse edge.");
            Assert.AreEqual(Bit(0), map.HexData[HEX_0_0].RoadEdgeMask,
                "A dead-end with no bridge neighbour must not gain an edge.");
        }

        [TestMethod]
        public void CalculateRiverEdgeMasks_LinksNeighbours_AndIgnoresBridges()
        {
            var map = MapHexHarness.BuildGrid(WIDTH, HEIGHT);
            map.HexData[HEX_0_0].IsRiver  = true;
            map.HexData[HEX_0_1].IsRiver  = true;
            map.HexData[HEX_1_1].IsBridge = true;

            map.CalculateRiverEdgeMasks();

            Assert.AreEqual(Bit(0), map.HexData[HEX_0_0].RiverEdgeMask, "River chain start");
            Assert.AreEqual(Bit(3), map.HexData[HEX_0_1].RiverEdgeMask,
                "Rivers must not pick up the bridge dead-end rule roads have.");
        }

        [TestMethod]
        public void CalculateRegionEdgeMasks_MarksOnlyBordersBetweenDifferentRegions()
        {
            var map = MapHexHarness.BuildGrid(WIDTH, HEIGHT);

            SetBorder(map, HEX_0_0, regionId: 0);
            SetBorder(map, HEX_0_1, regionId: 1);

            map.HexData[HEX_0_2].RegionId = 2;      // differently regioned, but not a border hex

            SetBorder(map, HEX_0_3, regionId: 0);   // adjacent pair sharing a region
            SetBorder(map, HEX_0_4, regionId: 0);

            map.CalculateRegionEdgeMasks();

            Assert.AreEqual(Bit(0), map.HexData[HEX_0_0].RegionEdgeMask, "Boundary, seen from below");
            Assert.AreEqual(Bit(3), map.HexData[HEX_0_1].RegionEdgeMask, "Boundary, seen from above");
            Assert.AreEqual(0, map.HexData[HEX_0_2].RegionEdgeMask, "A non-border hex never gets a mask.");
            Assert.AreEqual(0, map.HexData[HEX_0_3].RegionEdgeMask, "Same region is not a boundary.");
            Assert.AreEqual(0, map.HexData[HEX_0_4].RegionEdgeMask, "Same region is not a boundary.");
        }

        [TestMethod]
        public void CalculateTradeRouteEdgeMasks_LinksEveryAdjacentPair_IncludingTriangles()
        {
            var map = MapHexHarness.BuildGrid(WIDTH, HEIGHT);
            map.HexData[HEX_0_0].IsTradeRoute = true;
            map.HexData[HEX_1_0].IsTradeRoute = true;
            map.HexData[HEX_0_1].IsTradeRoute = true;

            map.CalculateTradeRouteEdgeMasks();

            var totalBits = HexGridUtility.GetNumBitsSetInEdgeMask(map.HexData[HEX_0_0].TradeRouteMask)
                          + HexGridUtility.GetNumBitsSetInEdgeMask(map.HexData[HEX_1_0].TradeRouteMask)
                          + HexGridUtility.GetNumBitsSetInEdgeMask(map.HexData[HEX_0_1].TradeRouteMask);

            Assert.AreEqual(6, totalBits,
                "Trade routes keep all three triangle edges - only roads and rivers drop a chord.");
        }

        // With nothing edited, Ensure* must be a no-op. The fixture uses masks a rebuild would clear.
        [TestMethod]
        public void EnsureMasks_WithNoEdits_LeavesAuthoredMasksUntouched()
        {
            var map = MapHexHarness.BuildGrid(WIDTH, HEIGHT);

            map.HexData[FAR].IsRoad         = true;
            map.HexData[FAR].RoadEdgeMask   = Bit(2);
            map.HexData[FAR].IsRiver        = true;
            map.HexData[FAR].RiverEdgeMask  = Bit(4);
            map.HexData[FAR].IsTradeRoute   = true;
            map.HexData[FAR].TradeRouteMask = Bit(1);
            map.HexData[FAR].IsBorder       = true;
            map.HexData[FAR].RegionEdgeMask = Bit(5);

            map.EnsureRoadEdgeMasks();
            map.EnsureRiverEdgeMasks();
            map.EnsureRegionEdgeMasks();
            map.EnsureTradeRouteEdgeMasks();

            Assert.AreEqual(Bit(2), map.HexData[FAR].RoadEdgeMask,   "Authored road mask was rewritten.");
            Assert.AreEqual(Bit(4), map.HexData[FAR].RiverEdgeMask,  "Authored river mask was rewritten.");
            Assert.AreEqual(Bit(1), map.HexData[FAR].TradeRouteMask, "Authored trade route mask was rewritten.");
            Assert.AreEqual(Bit(5), map.HexData[FAR].RegionEdgeMask, "Authored region mask was rewritten.");
        }

        [TestMethod]
        public void EnsureRoadEdgeMasks_WhenFullRecalculationRequested_RebuildsTheWholeMap()
        {
            var map = MapHexHarness.BuildGrid(WIDTH, HEIGHT);
            MarkRoad(map, HEX_0_0, HEX_0_1);
            map.HexData[FAR].IsRoad       = true;
            map.HexData[FAR].RoadEdgeMask = Bit(2);

            MapHexFile.RoadMasksNeedRecalculation = true;
            map.EnsureRoadEdgeMasks();

            Assert.AreEqual(0, map.HexData[FAR].RoadEdgeMask,
                "A full rebuild must clear a mask pointing at a non-road neighbour.");
            Assert.AreEqual(Bit(0), map.HexData[HEX_0_0].RoadEdgeMask, "The real chain must be rebuilt.");
            Assert.IsFalse(MapHexFile.RoadMasksNeedRecalculation, "The request flag must be consumed.");
        }

        [TestMethod]
        public void EnsureRoadEdgeMasks_WithDirtyHexes_RebuildsOnlyTheEditedNeighbourhood()
        {
            var map = MapHexHarness.BuildGrid(WIDTH, HEIGHT);
            MarkRoad(map, HEX_0_0, HEX_0_1);
            map.HexData[FAR].IsRoad       = true;
            map.HexData[FAR].RoadEdgeMask = Bit(2);

            MapHexFile.RoadDirtyHexes.Add(HEX_0_0);
            map.EnsureRoadEdgeMasks();

            Assert.AreEqual(Bit(0), map.HexData[HEX_0_0].RoadEdgeMask, "The edited hex must be rebuilt.");
            Assert.AreEqual(Bit(3), map.HexData[HEX_0_1].RoadEdgeMask, "Its neighbour must be rebuilt too.");
            Assert.AreEqual(Bit(2), map.HexData[FAR].RoadEdgeMask,
                "An incremental rebuild must not touch authored masks outside the edited neighbourhood.");
            Assert.AreEqual(0, MapHexFile.RoadDirtyHexes.Count, "The dirty set must be consumed.");
        }

        [TestMethod]
        public void EnsureRoadEdgeMasks_ErasingAHex_ClearsTheNeighboursReciprocalBit()
        {
            var map = MapHexHarness.BuildGrid(WIDTH, HEIGHT);
            MarkRoad(map, HEX_0_0, HEX_0_1, HEX_0_2);
            map.CalculateRoadEdgeMasks();
            Assert.AreEqual(Bit(0) | Bit(3), map.HexData[HEX_0_1].RoadEdgeMask, "Fixture precondition");

            map.HexData[HEX_0_1].IsRoad       = false;
            map.HexData[HEX_0_1].RoadEdgeMask = 0;
            MapHexFile.RoadDirtyHexes.Add(HEX_0_1);

            map.EnsureRoadEdgeMasks();

            Assert.AreEqual(0, map.HexData[HEX_0_1].RoadEdgeMask, "The erased hex must have no edges.");
            Assert.AreEqual(0, map.HexData[HEX_0_0].RoadEdgeMask,
                "The hex below must drop the edge that pointed at the erased road.");
            Assert.AreEqual(0, map.HexData[HEX_0_2].RoadEdgeMask,
                "The hex above must drop the edge that pointed at the erased road.");
        }

        [TestMethod]
        public void EnsureRoadEdgeMasks_FullRecalculationTakesPriorityOverDirtyHexes()
        {
            var map = MapHexHarness.BuildGrid(WIDTH, HEIGHT);
            MarkRoad(map, HEX_0_0, HEX_0_1);

            MapHexFile.RoadMasksNeedRecalculation = true;
            MapHexFile.RoadDirtyHexes.Add(HEX_0_0);

            map.EnsureRoadEdgeMasks();

            Assert.IsFalse(MapHexFile.RoadMasksNeedRecalculation, "The recalculation flag must be consumed.");
            Assert.AreEqual(0, MapHexFile.RoadDirtyHexes.Count,
                "Dirty hexes must be cleared by a full rebuild, not left for a redundant second pass.");
        }

        // A dirty index recorded before a resize no longer addresses a hex.
        [TestMethod]
        public void EnsureRoadEdgeMasks_OutOfRangeDirtyIndex_IsIgnored()
        {
            var map = MapHexHarness.BuildGrid(WIDTH, HEIGHT);
            MarkRoad(map, HEX_0_0, HEX_0_1);

            MapHexFile.RoadDirtyHexes.Add(-1);
            MapHexFile.RoadDirtyHexes.Add((int)(WIDTH * HEIGHT) + 10);
            MapHexFile.RoadDirtyHexes.Add(HEX_0_0);

            map.EnsureRoadEdgeMasks();

            Assert.AreEqual(Bit(0), map.HexData[HEX_0_0].RoadEdgeMask,
                "The valid dirty hex must still have been rebuilt.");
        }

        [TestMethod]
        public void EnsureMasks_EachLayersDirtySetIsIndependent()
        {
            var map = MapHexHarness.BuildGrid(WIDTH, HEIGHT);

            map.HexData[FAR].IsRoad        = true;
            map.HexData[FAR].RoadEdgeMask  = Bit(2);
            map.HexData[FAR].IsRiver       = true;
            map.HexData[FAR].RiverEdgeMask = Bit(4);

            MapHexFile.RiverMasksNeedRecalculation = true;

            map.EnsureRoadEdgeMasks();
            map.EnsureRiverEdgeMasks();

            Assert.AreEqual(Bit(2), map.HexData[FAR].RoadEdgeMask,
                "Rebuilding the river layer must not disturb the road layer.");
            Assert.AreEqual(0, map.HexData[FAR].RiverEdgeMask, "The river layer should have been rebuilt.");
        }

        // The edit-tracking state is static and outlives any single map. A stale flag would make the
        // next save rebuild masks the freshly loaded file authored.
        [TestMethod]
        public void Load_ResetsTheProcessWideMaskEditState()
        {
            var dir = MapHexHarness.CreateTempDir("caime_maskstate");
            try
            {
                MapHexFile.CreateMapHex(dir, "map", "warhammer3", "test_campaign", 4, 4, 0x14);

                MapHexFile.RoadMasksNeedRecalculation   = true;
                MapHexFile.RiverMasksNeedRecalculation  = true;
                MapHexFile.RegionMasksNeedRecalculation = true;
                MapHexFile.TradeMasksNeedRecalculation  = true;
                MapHexFile.RoadDirtyHexes.Add(3);
                MapHexFile.RiverDirtyHexes.Add(3);
                MapHexFile.RegionDirtyHexes.Add(3);
                MapHexFile.TradeDirtyHexes.Add(3);
                MapHexFile.RoadPaintCounter  = 7;
                MapHexFile.RiverPaintCounter = 9;

                Assert.IsTrue(new MapHexFile().Load(Path.Combine(dir, "map.hex")), "Load failed.");

                Assert.IsFalse(MapHexFile.RoadMasksNeedRecalculation,   "Road recalculation flag survived a load.");
                Assert.IsFalse(MapHexFile.RiverMasksNeedRecalculation,  "River recalculation flag survived a load.");
                Assert.IsFalse(MapHexFile.RegionMasksNeedRecalculation, "Region recalculation flag survived a load.");
                Assert.IsFalse(MapHexFile.TradeMasksNeedRecalculation,  "Trade recalculation flag survived a load.");

                Assert.AreEqual(0, MapHexFile.RoadDirtyHexes.Count,   "Road dirty set survived a load.");
                Assert.AreEqual(0, MapHexFile.RiverDirtyHexes.Count,  "River dirty set survived a load.");
                Assert.AreEqual(0, MapHexFile.RegionDirtyHexes.Count, "Region dirty set survived a load.");
                Assert.AreEqual(0, MapHexFile.TradeDirtyHexes.Count,  "Trade dirty set survived a load.");

                Assert.AreEqual(0, MapHexFile.RoadPaintCounter,  "Road paint counter survived a load.");
                Assert.AreEqual(0, MapHexFile.RiverPaintCounter, "River paint counter survived a load.");
            }
            finally
            {
                MapHexHarness.DeleteTempDir(dir);
            }
        }

        [TestMethod]
        public void Save_AppliesPendingDirtyHexes_SoAPaintedRoadReachesTheFile()
        {
            var dir = MapHexHarness.CreateTempDir("caime_masksave");
            try
            {
                MapHexFile.CreateMapHex(dir, "map", "warhammer3", "test_campaign", WIDTH, HEIGHT, 0x14);

                var map = new MapHexFile();
                Assert.IsTrue(map.Load(Path.Combine(dir, "map.hex")), "Load failed.");

                // What RoadSwatch.Apply does for a two-hex stroke.
                foreach (var index in new[] { HEX_0_0, HEX_0_1 })
                {
                    map.HexData[index].IsRoad = true;
                    MapHexFile.RoadDirtyHexes.Add(index);
                }

                Assert.IsTrue(map.Save(dir, "painted"), "Save returned false.");

                var reloaded = new MapHexFile();
                Assert.IsTrue(reloaded.Load(Path.Combine(dir, "painted.hex")), "Reload failed.");

                Assert.AreEqual(Bit(0), reloaded.HexData[HEX_0_0].RoadEdgeMask,
                    "Save must build the mask for a painted road before writing it.");
                Assert.AreEqual(Bit(3), reloaded.HexData[HEX_0_1].RoadEdgeMask,
                    "Save must build the mask for a painted road before writing it.");
                Assert.IsTrue(reloaded.HexData[HEX_0_0].IsRoad, "The painted road did not survive the save.");
            }
            finally
            {
                MapHexHarness.DeleteTempDir(dir);
            }
        }

        private static byte Bit(int dir) => (byte)(1 << dir);

        private static void MarkRoad(MapHexFile map, params int[] hexIndices)
        {
            foreach (var index in hexIndices)
                map.HexData[index].IsRoad = true;
        }

        private static void SetBorder(MapHexFile map, int hexIndex, int regionId)
        {
            map.HexData[hexIndex].IsBorder = true;
            map.HexData[hexIndex].RegionId = regionId;
        }

        private static bool AreConnected(MapHexFile map, int hexIndex, int otherIndex, bool isRoad)
        {
            var hex = map.HexData[hexIndex];

            for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
            {
                if (map.GetNeighbourIndex(hex, dir) != otherIndex)
                    continue;

                var hexMask   = isRoad ? hex.RoadEdgeMask : hex.RiverEdgeMask;
                var otherMask = isRoad ? map.HexData[otherIndex].RoadEdgeMask : map.HexData[otherIndex].RiverEdgeMask;

                return (hexMask   & (1 << dir)) != 0
                    && (otherMask & (1 << HexGridUtility.InverseDir(dir))) != 0;
            }

            return false;
        }
    }
}
