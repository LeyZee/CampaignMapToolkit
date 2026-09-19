using CAIME;
using CAIME.Tests.Helpers;
using CAIME.TradeNetwork;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Unit tests for the connectivity the trade-route exporter derives from a painted map:
    /// settlements, sea bodies, land borders and port-to-sea-body adjacency.
    /// </summary>
    [TestClass]
    public class TradeRoutesProcessorTests
    {
        private static Hex Land(int q, int r, int index, int region = -1, sbyte slot = -1, byte trade = 0)
            => new Hex(q, r, index)
            {
                IsSea = false, IsBeach = false, IsCliff = false, IsPassable = true,
                RegionId = region, TownSlotIndex = slot, TradeRouteMask = trade,
            };

        private static Hex Sea(int q, int r, int index)
            => new Hex(q, r, index)
            {
                IsSea = true, IsBeach = false, IsCliff = false, IsPassable = true,
                RegionId = -1, TownSlotIndex = -1, TradeRouteMask = 0,
            };

        private static Hex Port(int q, int r, int index, int region)
            => new Hex(q, r, index)
            {
                IsSea = false, IsBeach = true, IsCliff = false, IsPassable = true,
                RegionId = region, TownSlotIndex = Hex.PORT_SLOT_INDEX, TradeRouteMask = 0,
            };

        private static TradeConnectivity Scan(Hex[] hexes, uint width, uint height, int regionSlots = 8)
        {
            var map  = MovementTestHarness.BuildMapFor(hexes, width, height);
            var grid = TradeGridBuilder.FromMap(map);
            return new ConnectivityScanner(grid, regionSlots).Scan();
        }

        [TestMethod]
        public void Settlements_AreRecordedForEveryRegionWithAMainSlot()
        {
            var connectivity = Scan(new[]
            {
                Land(0, 0, 0, region: 2, slot: Hex.MAIN_SLOT_INDEX),
                Land(0, 1, 1, region: 2, slot: Hex.MAIN_SLOT_INDEX),
                Land(0, 2, 2, region: 5),
            }, 1, 3);

            Assert.IsTrue(connectivity.HasSettlement(2), "region 2 has a main slot");
            Assert.IsFalse(connectivity.HasSettlement(5), "region 5 has no main slot");
        }

        [TestMethod]
        public void SeaBodies_CountDisconnectedRegions()
        {
            var connectivity = Scan(new[] { Sea(0, 0, 0), Sea(0, 1, 1), Land(0, 2, 2), Sea(0, 3, 3), Sea(0, 4, 4) }, 1, 5);

            Assert.AreEqual(2, connectivity.SeaBodyCount);
        }

        [TestMethod]
        public void SeaBodies_CountSingleConnectedRegionAsOne()
        {
            var connectivity = Scan(new[] { Sea(0, 0, 0), Sea(0, 1, 1), Sea(0, 2, 2) }, 1, 3);

            Assert.AreEqual(1, connectivity.SeaBodyCount);
        }

        [TestMethod]
        public void LandBorders_LinkNeighbouringRegionsUnderTheSmallerIndex()
        {
            var connectivity = Scan(new[]
            {
                Land(0, 0, 0, region: 0, slot: Hex.MAIN_SLOT_INDEX),
                Land(0, 1, 1, region: 1, slot: Hex.MAIN_SLOT_INDEX),
            }, 1, 2);

            CollectionAssert.AreEqual(new[] { 1 }, (System.Collections.ICollection)connectivity.LandBorders(0));
            Assert.AreEqual(0, connectivity.LandBorders(1).Count, "a border is stored once, under the smaller index");
        }

        [TestMethod]
        public void SeaAdjacency_LinksCoastalPortToItsSeaBody()
        {
            var connectivity = Scan(new[] { Port(0, 0, 0, region: 2), Sea(0, 1, 1) }, 1, 2);

            Assert.AreEqual(1, connectivity.SeaBodyCount);
            CollectionAssert.Contains((System.Collections.ICollection)connectivity.SeaBodyRegions(0), 2);
        }
    }
}
