using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using CAIME;
using CAIME.Painters;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Covers the non-UI half of painting: <see cref="Swatch"/> application, the edit-tracking it
    /// records for the mask rebuild, the beach eligibility gate every painter shares, and
    /// <see cref="LinePainter"/>'s hex line tracing.
    ///
    /// <para>
    /// A swatch is the paint primitive - it is what actually mutates a hex, and what tells
    /// <see cref="MapHexFile"/> which hexes to rebuild masks for on the next save. Nothing here needs
    /// a viewport; the painters' own view models are only reached for the two members that do not
    /// use them, which are driven on instances created without running the constructor.
    /// </para>
    /// </summary>
    [TestClass]
    public class PaintingTests
    {
        private const uint WIDTH  = 5;
        private const uint HEIGHT = 5;

        [TestInitialize]
        public void Setup() => MapHexHarness.ResetStaticMaskState();

        [TestCleanup]
        public void Cleanup() => MapHexHarness.ResetStaticMaskState();

        [TestMethod]
        public void ValueSwatches_WriteTheirValueOntoTheHex()
        {
            var hex = NewHex();

            new GroundSwatch("forest", 3, 0).Apply(hex);
            new AttritionSwatch("attrition", 2, 0).Apply(hex);
            new ClimateSwatch("temperate", 1, 0).Apply(hex);
            new AreaOfInterestSwatch("capital", 4, 0).Apply(hex);
            new RegionSwatch("region", 7, 0).Apply(hex);
            new TownSlotSwatch(Hex.PORT_SLOT_INDEX).Apply(hex);
            new RestrictionSwatch(5).Apply(hex);

            Assert.AreEqual(3, hex.GroundTypeIndex, "GroundTypeIndex");
            Assert.AreEqual(2, hex.AttritionIndex,  "AttritionIndex");
            Assert.AreEqual(1, hex.ClimateIndex,    "ClimateIndex");
            Assert.AreEqual(4, hex.InterestIndex,   "InterestIndex");
            Assert.AreEqual(7, hex.RegionId,        "RegionId");
            Assert.AreEqual(Hex.PORT_SLOT_INDEX, hex.TownSlotIndex, "TownSlotIndex");
            Assert.AreEqual(5, hex.RestrictionLvl,  "RestrictionLvl");
        }

        // NogoSwatch is the one flag swatch stored inverted - it sets IsPassable from IsImpassable.
        [TestMethod]
        public void FlagSwatches_SetAndClearTheirFlag()
        {
            var hex = NewHex();

            new BeachSwatch(true).Apply(hex);
            Assert.IsTrue(hex.IsBeach, "BeachSwatch(true)");
            new BeachSwatch(false).Apply(hex);
            Assert.IsFalse(hex.IsBeach, "BeachSwatch(false)");

            new BridgeSwatch(true).Apply(hex);
            Assert.IsTrue(hex.IsBridge, "BridgeSwatch(true)");
            new BridgeSwatch(false).Apply(hex);
            Assert.IsFalse(hex.IsBridge, "BridgeSwatch(false)");

            new TownSprawlSwatch(true).Apply(hex);
            Assert.IsTrue(hex.IsTownSprawl, "TownSprawlSwatch(true)");

            new NogoSwatch(true).Apply(hex);
            Assert.IsTrue(hex.IsImpassable, "NogoSwatch(true) must make the hex impassable.");
            new NogoSwatch(false).Apply(hex);
            Assert.IsTrue(hex.IsPassable, "NogoSwatch(false) must make the hex passable.");
        }

        // Painting a road stamps a paint sequence so triangle resolution can follow paint order, and
        // records the hex for an incremental mask rebuild. Both are what Save later consumes.
        [TestMethod]
        public void RoadSwatch_Painting_StampsPaintOrderAndRecordsTheHexAsDirty()
        {
            var first  = NewHex(index: 4);
            var second = NewHex(index: 9);

            new RoadSwatch(true).Apply(first);
            new RoadSwatch(true).Apply(second);

            Assert.IsTrue(first.IsRoad,  "The first hex should be a road.");
            Assert.IsTrue(second.IsRoad, "The second hex should be a road.");

            Assert.AreEqual(1, first.RoadPaintSeq,  "The first painted hex takes sequence 1.");
            Assert.AreEqual(2, second.RoadPaintSeq, "The second painted hex takes the next sequence.");
            Assert.AreEqual(2, MapHexFile.RoadPaintCounter, "The shared counter must advance once per hex.");

            CollectionAssert.AreEquivalent(new[] { 4, 9 }, new List<int>(MapHexFile.RoadDirtyHexes),
                "Both painted hexes must be queued for an incremental mask rebuild.");
        }

        // Erasing clears the mask immediately: the hex has no edges left to describe, and leaving a
        // stale mask would make it reload as a road (IsRoad is derived from "mask > 0").
        [TestMethod]
        public void RoadSwatch_Erasing_ClearsTheMaskAndStillMarksTheHexDirty()
        {
            var hex = NewHex(index: 4);
            hex.RoadEdgeMask = 0b00101010;

            new RoadSwatch(false).Apply(hex);

            Assert.IsFalse(hex.IsRoad, "The hex should no longer be a road.");
            Assert.AreEqual(0, hex.RoadEdgeMask, "Erasing must clear the edge mask.");
            Assert.AreEqual(0, hex.RoadPaintSeq, "Erasing must not stamp a paint sequence.");
            Assert.AreEqual(0, MapHexFile.RoadPaintCounter, "Erasing must not advance the paint counter.");
            CollectionAssert.Contains(new List<int>(MapHexFile.RoadDirtyHexes), 4,
                "An erased hex still needs its neighbours' masks rebuilt.");
        }

        [TestMethod]
        public void RiverSwatch_MirrorsTheRoadSwatchsPaintOrderAndDirtyTracking()
        {
            var painted = NewHex(index: 4);
            new RiverSwatch(true).Apply(painted);

            Assert.IsTrue(painted.IsRiver, "The hex should be a river.");
            Assert.AreEqual(1, painted.RiverPaintSeq, "River paint sequence");
            Assert.AreEqual(1, MapHexFile.RiverPaintCounter, "River paint counter");
            CollectionAssert.Contains(new List<int>(MapHexFile.RiverDirtyHexes), 4, "River dirty set");

            var erased = NewHex(index: 9);
            erased.RiverEdgeMask = 0b00010101;
            new RiverSwatch(false).Apply(erased);

            Assert.IsFalse(erased.IsRiver, "The hex should no longer be a river.");
            Assert.AreEqual(0, erased.RiverEdgeMask, "Erasing must clear the river edge mask.");
        }

        [TestMethod]
        public void TradeRouteSwatch_TracksDirtyHexesAndClearsTheMaskOnErase()
        {
            var painted = NewHex(index: 4);
            new TradeRouteSwatch(true).Apply(painted);

            Assert.IsTrue(painted.IsTradeRoute, "The hex should be a trade route.");
            CollectionAssert.Contains(new List<int>(MapHexFile.TradeDirtyHexes), 4, "Trade dirty set");

            var erased = NewHex(index: 9);
            erased.TradeRouteMask = 0b00110011;
            new TradeRouteSwatch(false).Apply(erased);

            Assert.AreEqual(0, erased.TradeRouteMask, "Erasing must clear the trade route mask.");
        }

        // Region edge masks depend on both RegionId and IsBorder, so either swatch has to queue a
        // rebuild - painting a region without one leaves borders describing the previous layout.
        [TestMethod]
        public void RegionAndBorderSwatches_BothQueueARegionMaskRebuild()
        {
            var regionHex = NewHex(index: 4);
            new RegionSwatch("region", 2, 0).Apply(regionHex);

            var borderHex = NewHex(index: 9);
            borderHex.RegionEdgeMask = 0b00001111;
            new RegionBorderSwatch(false).Apply(borderHex);

            Assert.IsFalse(borderHex.IsBorder, "The hex should no longer be a border.");
            Assert.AreEqual(0, borderHex.RegionEdgeMask, "Erasing a border must clear its edge mask.");

            CollectionAssert.AreEquivalent(new[] { 4, 9 }, new List<int>(MapHexFile.RegionDirtyHexes),
                "Both a region change and a border change must queue a region mask rebuild.");
        }

        // The end-to-end paint path with no UI: apply swatches, then let the save-time refresh run.
        [TestMethod]
        public void PaintingARoadStroke_ThenRefreshing_BuildsTheStrokesEdgeMasks()
        {
            var map    = MapHexHarness.BuildGrid(WIDTH, HEIGHT);
            var swatch = new RoadSwatch(true);

            foreach (var index in new[] { 0, 5, 10 })
                swatch.Apply(map.HexData[index]);

            map.EnsureRoadEdgeMasks();

            Assert.AreEqual(0b000001, map.HexData[0].RoadEdgeMask,  "Stroke start connects up only.");
            Assert.AreEqual(0b001001, map.HexData[5].RoadEdgeMask,  "Stroke middle connects up and down.");
            Assert.AreEqual(0b001000, map.HexData[10].RoadEdgeMask, "Stroke end connects down only.");
        }

        [TestMethod]
        public void ErasingPartOfAStroke_ThenRefreshing_RemovesOnlyThatHexesEdges()
        {
            var map = MapHexHarness.BuildGrid(WIDTH, HEIGHT);

            foreach (var index in new[] { 0, 5, 10 })
                new RoadSwatch(true).Apply(map.HexData[index]);
            map.EnsureRoadEdgeMasks();

            new RoadSwatch(false).Apply(map.HexData[5]);
            map.EnsureRoadEdgeMasks();

            Assert.AreEqual(0, map.HexData[5].RoadEdgeMask,  "The erased hex must have no edges.");
            Assert.AreEqual(0, map.HexData[0].RoadEdgeMask,  "Its neighbour must drop the edge pointing at it.");
            Assert.AreEqual(0, map.HexData[10].RoadEdgeMask, "Its neighbour must drop the edge pointing at it.");
        }

        // Beach is the one swatch painting is gated on: it only belongs on a land hex touching sea.
        [TestMethod]
        public void CanPaintHex_AllowsBeachOnlyOnCoastalLand()
        {
            var map = MapHexHarness.BuildGrid(WIDTH, HEIGHT);
            map.HexData[5].IsSea = true;    // the only sea hex, directly above hex 0

            var painter = PainterWithProject(typeof(BrushPainter), map);

            Assert.IsTrue(CanPaintHex(painter, new BeachSwatch(true), 0),
                "Land touching sea must accept a beach.");
            Assert.IsFalse(CanPaintHex(painter, new BeachSwatch(true), 20),
                "Inland hexes must not accept a beach.");
            Assert.IsFalse(CanPaintHex(painter, new BeachSwatch(true), 5),
                "A sea hex must not accept a beach.");
        }

        // Only beach painting is restricted; removing a beach and every other swatch is unrestricted.
        [TestMethod]
        public void CanPaintHex_LeavesEveryOtherSwatchUnrestricted()
        {
            var map     = MapHexHarness.BuildGrid(WIDTH, HEIGHT);
            var painter = PainterWithProject(typeof(BrushPainter), map);

            Assert.IsTrue(CanPaintHex(painter, new BeachSwatch(false), 20),
                "Removing a beach must never be gated.");
            Assert.IsTrue(CanPaintHex(painter, new RoadSwatch(true), 20), "Roads are unrestricted.");
            Assert.IsTrue(CanPaintHex(painter, new GroundSwatch("forest", 1, 0), 20), "Ground types are unrestricted.");
        }

        [TestMethod]
        public void FindPath_StraightColumn_VisitsEveryHexBetweenTheEndpoints()
        {
            var map  = MapHexHarness.BuildGrid(WIDTH, HEIGHT);
            var path = TracePath(map, map.HexData[0], map.HexData[20]);

            CollectionAssert.AreEqual(new[] { 5, 10, 15 }, path.ToArray(),
                "A vertical line must fill in the hexes between its endpoints, exclusive.");
        }

        [TestMethod]
        public void FindPath_AdjacentHexes_ProducesNoIntermediateHexes()
        {
            var map  = MapHexHarness.BuildGrid(WIDTH, HEIGHT);
            var path = TracePath(map, map.HexData[0], map.HexData[5]);

            Assert.AreEqual(0, path.Count, "Neighbouring endpoints have nothing in between.");
        }

        // The trace runs in cube coordinates; every hex it emits has to be a real, in-bounds cell and
        // each step has to be adjacent to the last, or the line paints a broken trail.
        [TestMethod]
        public void FindPath_Diagonal_ProducesAContiguousInBoundsRun()
        {
            var map  = MapHexHarness.BuildGrid(WIDTH, HEIGHT);
            var src  = map.HexData[0];
            var dst  = map.HexData[24];
            var path = TracePath(map, src, dst);

            Assert.IsTrue(path.Count > 0, "A diagonal across the grid must pass through something.");

            var previous = src;
            foreach (var index in path)
            {
                Assert.IsTrue(index >= 0 && index < map.Capacity, $"Hex index {index} is off the map.");

                var current = map.HexData[index];
                Assert.IsTrue(AreNeighbours(map, previous, current),
                    $"Hex {current.Index} is not adjacent to the previous step {previous.Index}.");
                previous = current;
            }

            Assert.IsTrue(AreNeighbours(map, previous, dst),
                "The last intermediate hex must be adjacent to the destination.");
        }

        private static Hex NewHex(int index = 0) => new Hex(0, 0, index) { IsPassable = true };

        private static bool AreNeighbours(MapHexFile map, Hex hex, Hex other)
        {
            for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
            {
                if (map.GetNeighbourIndex(hex, dir) == other.Index)
                    return true;
            }

            return false;
        }

        // The painters' constructors wire themselves to a viewport and an editor view model. The two
        // members exercised here use neither, so the instance is built without running one and given
        // just the project field they do read.
        private static object PainterWithProject(Type painterType, MapHexFile map)
        {
            var project = new Project();
            MapHexHarness.SetProperty(project, nameof(Project.MapHexFile), map);

            var painter = FormatterServices.GetUninitializedObject(painterType);
            MapHexHarness.SetPrivateField(painter, "project", project);

            return painter;
        }

        private static bool CanPaintHex(object painter, Swatch swatch, int hexIndex)
        {
            var method = painter.GetType().GetMethod(
                "CanPaintHex", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "AbstractViewportPainter.CanPaintHex was not found.");

            return (bool)method.Invoke(painter, new object[] { swatch, hexIndex });
        }

        private static List<int> TracePath(MapHexFile map, Hex src, Hex dst)
        {
            var painter = PainterWithProject(typeof(LinePainter), map);

            var path = new List<int>();
            MapHexHarness.SetPrivateField(painter, "path", path);

            var method = typeof(LinePainter).GetMethod("FindPath", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "LinePainter.FindPath was not found.");
            method.Invoke(painter, new object[] { src, dst });

            return path;
        }
    }
}
