using System;
using System.Collections.Generic;
using System.Text;
using CAIME;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Tests that road edge masks are recomputed correctly when a map is <em>edited</em> (painted /
    /// erased) — the path the byte-exact .ppd tests never reach, because a freshly loaded map has no
    /// dirty hexes so <see cref="MapHexFile.EnsureRoadEdgeMasks"/> just preserves the authored masks.
    ///
    /// <para>
    /// Painting a road runs <c>RoadSwatch.Apply</c>, which sets <c>IsRoad</c>, stamps a paint sequence
    /// (<c>Hex.RoadPaintSeq</c> via <c>MapHexFile.RoadPaintCounter</c>) and marks the hex dirty
    /// (<c>MapHexFile.RoadDirtyHexes</c>). On save/export <see cref="MapHexFile.EnsureRoadEdgeMasks"/>
    /// rebuilds only the dirty hexes and their neighbours, deliberately leaving the masks elsewhere
    /// untouched. <see cref="PaintRoad"/> / <see cref="EraseRoad"/> replicate that signal headlessly.
    /// </para>
    ///
    /// <para>
    /// NB: an incremental edit is intentionally <b>not</b> equal to a full recompute of the whole map —
    /// the incremental path exists precisely because a full recompute cannot reproduce authored masks
    /// (triangle-chord choices, town-centre edges) elsewhere on the map. So the oracle here is not
    /// "incremental == full"; it is:
    ///   • a full recompute equals an incremental one only when <i>every</i> road hex is (re)painted
    ///     (nothing authored is being preserved) — <see cref="AllPaintedComplexGrid_IncrementalEqualsFull"/>;
    ///   • after any edit the masks are structurally valid (reciprocal-consistent, road-only,
    ///     triangle-free) — <see cref="Fuzz_PaintsAndErases_ProduceStructurallyValidMasks"/>;
    ///   • masks outside the edited region are preserved byte-for-byte
    ///     — <see cref="EditedRegionOnly_RestOfMapPreserved"/>.
    /// </para>
    ///
    /// <para>
    /// Grid geometry is flat-top, <c>Directions_FlatTop[Q &amp; 1, dir]</c>, index = R*width + Q. On a 2×2
    /// grid the hexes A=(0,0) idx0, C=(1,0) idx1, B=(0,1) idx2 are mutually adjacent — a triangle whose
    /// edges are A–B (dir 0/3), A–C (dir 1/4) and B–C (dir 2/5).
    /// </para>
    /// </summary>
    [TestClass]
    public class RoadEdgeMaskPaintTests
    {
        // 2×2 triangle hex indices and the direction bit each edge occupies on each endpoint.
        private const int A = 0; // (0,0)
        private const int C = 1; // (1,0)
        private const int B = 2; // (0,1)

        private const int A_to_B = 0, B_to_A = 3;
        private const int A_to_C = 1, C_to_A = 4;
        private const int B_to_C = 2, C_to_B = 5;

        /// <summary>
        /// The mask-edit state on <see cref="MapHexFile"/> is static (swatches are shared singletons and
        /// the editor is single-document). <c>Load</c>/new-map reset it; the test harness builds maps
        /// without <c>Load</c>, so reset it here to keep tests independent and deterministic.
        /// </summary>
        [TestInitialize]
        public void ResetMaskState()
        {
            MapHexFile.RoadDirtyHexes.Clear();
            MapHexFile.RiverDirtyHexes.Clear();
            MapHexFile.RegionDirtyHexes.Clear();
            MapHexFile.TradeDirtyHexes.Clear();
            MapHexFile.RoadPaintCounter = 0;
            MapHexFile.RiverPaintCounter = 0;
            MapHexFile.RoadMasksNeedRecalculation = false;
            MapHexFile.RiverMasksNeedRecalculation = false;
            MapHexFile.RegionMasksNeedRecalculation = false;
            MapHexFile.TradeMasksNeedRecalculation = false;
        }

        // ----------------------------------------------------------------- Triangle elimination

        [TestMethod]
        public void Triangle_FullRecompute_DropsExactlyOneChord()
        {
            // Three mutually-adjacent road hexes form a triangle; the generator must keep 2 of the 3
            // edges and drop one chord, so it stays a tree (no closed loop).
            var map = BuildGrid(2, 2);
            MarkLoadedRoad(map, A);
            MarkLoadedRoad(map, B);
            MarkLoadedRoad(map, C);
            map.CalculateRoadEdgeMasks();

            Assert.AreEqual(4, Bits(map, A) + Bits(map, B) + Bits(map, C),
                "a 3-hex triangle keeps 2 of its 3 undirected edges (4 directed bits), not 3 (6 bits)");

            // All loaded (RoadPaintSeq 0) -> processed in index order -> A (index 0) is the apex.
            Assert.AreEqual(2, Bits(map, A), "the first-processed hex (apex) keeps both its edges");
            Assert.AreEqual(1, Bits(map, B), "B keeps only its edge to the apex");
            Assert.AreEqual(1, Bits(map, C), "C keeps only its edge to the apex");

            AssertConnected(map, A, A_to_B, B, B_to_A, "A<->B kept");
            AssertConnected(map, A, A_to_C, C, C_to_A, "A<->C kept");
            AssertNotConnected(map, B, B_to_C, C, C_to_B, "B<->C is the dropped chord");
        }

        [TestMethod]
        public void Triangle_PaintOrder_DeterminesWhichChordIsDropped()
        {
            // Same triangle, but painted: the apex follows paint order, so the dropped chord changes
            // with the order the hexes were painted in.

            // Paint B first -> B is the apex -> the opposite chord A<->C is dropped.
            var withBFirst = BuildGrid(2, 2);
            PaintRoad(withBFirst, B);
            PaintRoad(withBFirst, A);
            PaintRoad(withBFirst, C);
            withBFirst.CalculateRoadEdgeMasks();
            Assert.AreEqual(2, Bits(withBFirst, B), "B painted first must be the apex");
            AssertNotConnected(withBFirst, A, A_to_C, C, C_to_A, "A<->C dropped when B is the apex");
            AssertConnected(withBFirst, A, A_to_B, B, B_to_A, "A<->B kept when B is the apex");
            AssertConnected(withBFirst, B, B_to_C, C, C_to_B, "B<->C kept when B is the apex");

            // Paint C first -> C is the apex -> the opposite chord A<->B is dropped.
            var withCFirst = BuildGrid(2, 2);
            PaintRoad(withCFirst, C);
            PaintRoad(withCFirst, A);
            PaintRoad(withCFirst, B);
            withCFirst.CalculateRoadEdgeMasks();
            Assert.AreEqual(2, Bits(withCFirst, C), "C painted first must be the apex");
            AssertNotConnected(withCFirst, A, A_to_B, B, B_to_A, "A<->B dropped when C is the apex");
            AssertConnected(withCFirst, A, A_to_C, C, C_to_A, "A<->C kept when C is the apex");
            AssertConnected(withCFirst, B, B_to_C, C, C_to_B, "B<->C kept when C is the apex");
        }

        [TestMethod]
        public void PaintToCloseTriangle_DropsExactlyOneChord()
        {
            // A loaded road edge A-B exists; painting C closes a triangle. The incremental rebuild must
            // resolve it to a tree: two edges kept, one chord dropped, reciprocal-consistent.
            var map = BuildGrid(2, 2);
            MarkLoadedRoad(map, A);
            MarkLoadedRoad(map, B);
            EstablishBaseline(map);
            AssertConnected(map, A, A_to_B, B, B_to_A, "baseline A<->B edge before painting C");

            PaintRoad(map, C);
            map.EnsureRoadEdgeMasks();

            Assert.AreEqual(4, Bits(map, A) + Bits(map, B) + Bits(map, C),
                "after closing the triangle exactly one of the three chords must be dropped");
            AssertReciprocalAndRoadOnly(map, "paint C to close triangle");
            AssertTriangleFree(map, "paint C to close triangle");
        }

        // ----------------------------------------------------------------- Erase

        [TestMethod]
        public void Erase_ClearsReciprocalBitOnNeighbour()
        {
            // A 1×2 strip: two adjacent road hexes connected vertically. Erasing one must clear both its
            // own mask and the neighbour's bit pointing back at it.
            var map = BuildGrid(1, 2); // idx0=(0,0), idx1=(0,1); idx0 dir0 <-> idx1 dir3
            MarkLoadedRoad(map, 0);
            MarkLoadedRoad(map, 1);
            EstablishBaseline(map);
            Assert.AreNotEqual(0, map.HexData[1].RoadEdgeMask, "neighbour has a road edge before the erase");

            EraseRoad(map, 0);
            map.EnsureRoadEdgeMasks();

            Assert.AreEqual(0, map.HexData[0].RoadEdgeMask, "erased hex has no road edges");
            Assert.AreEqual(0, map.HexData[1].RoadEdgeMask,
                "the neighbour's reciprocal bit pointing at the erased hex must be cleared");
        }

        [TestMethod]
        public void EraseTriangleApex_ReopensDroppedChord()
        {
            // Loaded triangle A-B-C (apex A, chord B-C dropped). Erasing the apex A removes the only two
            // edges, so the previously-dropped B-C chord must reappear (the road stays connected).
            var map = BuildGrid(2, 2);
            MarkLoadedRoad(map, A);
            MarkLoadedRoad(map, B);
            MarkLoadedRoad(map, C);
            EstablishBaseline(map);
            AssertNotConnected(map, B, B_to_C, C, C_to_B, "B<->C dropped while the triangle is closed");

            EraseRoad(map, A);
            map.EnsureRoadEdgeMasks();

            Assert.AreEqual(0, map.HexData[A].RoadEdgeMask, "erased apex has no road edges");
            AssertConnected(map, B, B_to_C, C, C_to_B, "B<->C reopens once the apex is erased");
            AssertReciprocalAndRoadOnly(map, "erase triangle apex");
        }

        // ----------------------------------------------------- Equivalence (only when fully repainted)

        [TestMethod]
        public void AllPaintedComplexGrid_IncrementalEqualsFull()
        {
            // When every road hex is painted there is nothing authored to preserve, so the incremental
            // rebuild must reproduce a full recompute exactly. A fully-roaded 5×5 grid is maximally
            // connected and dense with triangles and junctions — a stiff test of the triangle resolution.
            const int width = 5, height = 5;
            var map = BuildGrid(width, height);
            for (int i = 0; i < map.Capacity; ++i)
            {
                PaintRoad(map, i);
            }

            AssertIncrementalEqualsFull(map, "fully-painted 5x5 road grid");
        }

        [TestMethod]
        public void Fuzz_AllPainted_IncrementalEqualsFull()
        {
            // Randomised fully-painted networks (connected segments, junctions and triangles arise from
            // ~55% density), every road hex painted in a random order. Incremental must equal full.
            var rng = new Random(20260620);

            for (int iteration = 0; iteration < 500; ++iteration)
            {
                ResetMaskState();
                int width = rng.Next(3, 7), height = rng.Next(3, 7);
                var map = BuildGrid(width, height);

                // Paint a random subset in a random order.
                var order = new List<int>();
                for (int i = 0; i < map.Capacity; ++i)
                {
                    if (rng.NextDouble() < 0.55) order.Add(i);
                }
                Shuffle(order, rng);
                foreach (var i in order) PaintRoad(map, i);

                AssertIncrementalEqualsFull(map, $"fuzz all-painted iter {iteration} ({width}x{height})");
            }
        }

        // ----------------------------------------------------- Structural validity of edited masks

        [TestMethod]
        public void Fuzz_PaintsAndErases_ProduceStructurallyValidMasks()
        {
            // A loaded baseline, then a round of paints and erases. Whatever chord choices the incremental
            // update makes (which may differ from a full recompute in order to preserve authored masks),
            // the result must still be a valid road graph: reciprocal-consistent, edges only between
            // adjacent road hexes, and triangle-free.
            var rng = new Random(99172);

            for (int iteration = 0; iteration < 500; ++iteration)
            {
                ResetMaskState();
                int width = rng.Next(3, 7), height = rng.Next(3, 7);
                var map = BuildGrid(width, height);

                for (int i = 0; i < map.Capacity; ++i)
                {
                    if (rng.NextDouble() < 0.45) MarkLoadedRoad(map, i);
                }
                EstablishBaseline(map);
                AssertReciprocalAndRoadOnly(map, $"baseline iter {iteration}");
                AssertTriangleFree(map, $"baseline iter {iteration}");

                for (int i = 0; i < map.Capacity; ++i)
                {
                    bool isRoad = map.HexData[i].IsRoad;
                    double roll = rng.NextDouble();
                    if (!isRoad && roll < 0.35) PaintRoad(map, i);
                    else if (isRoad && roll < 0.15) EraseRoad(map, i);
                }
                map.EnsureRoadEdgeMasks();

                var scenario = $"fuzz paint/erase iter {iteration} ({width}x{height})";
                AssertReciprocalAndRoadOnly(map, scenario);
                AssertTriangleFree(map, scenario);
            }
        }

        // ----------------------------------------------------- Preservation of untouched masks

        [TestMethod]
        public void EditRipple_StaysWithinTwoHops_FarMasksPreserved()
        {
            // The incremental update re-derives the edited hexes and their neighbours, which also rewrites
            // reciprocal bits on *those* neighbours — so an edit can ripple up to two hops. Everything
            // beyond that must keep its authored mask byte-for-byte (the "preserve authored data" promise).
            const int width = 8, height = 8;
            var map = BuildGrid(width, height);
            for (int i = 0; i < map.Capacity; ++i) MarkLoadedRoad(map, i); // fully roaded authored map
            EstablishBaseline(map);
            var authored = SnapshotRoadMasks(map);

            // Edit a small cluster in the middle.
            var edited = new[] { Index(map, 3, 3), Index(map, 4, 3), Index(map, 3, 4) };
            EraseRoad(map, edited[0]);
            PaintRoad(map, edited[1]); // already road -> re-paint (still flags dirty + bumps seq)
            EraseRoad(map, edited[2]);

            var changeable = WithinHops(map, edited, hops: 2);
            map.EnsureRoadEdgeMasks();

            int preserved = 0;
            for (int i = 0; i < map.Capacity; ++i)
            {
                if (!changeable.Contains(i))
                {
                    Assert.AreEqual(authored[i], map.HexData[i].RoadEdgeMask,
                        $"hex {i} (Q={map.HexData[i].Q},R={map.HexData[i].R}) is more than two hops from any " +
                        "edit and must keep its authored mask");
                    ++preserved;
                }
            }

            // Sanity: the edit really is local — most of an 8×8 map is untouched.
            Assert.IsTrue(preserved > map.Capacity / 2,
                $"expected the bulk of the map to be preserved, only {preserved}/{map.Capacity} were");
        }

        // ----------------------------------------------------------------- Helpers

        private static MapHexFile BuildGrid(int width, int height)
        {
            var hexData = new Hex[width * height];
            for (int r = 0; r < height; ++r)
            {
                for (int q = 0; q < width; ++q)
                {
                    int idx = (r * width) + q;
                    // Land, not road, no town slot (TownSlotIndex defaults to INVALID, so the
                    // town-centre special-casing in the mask calc never triggers here).
                    hexData[idx] = new Hex(q, r, idx);
                }
            }
            return BordersTestHarness.BuildMap(hexData, (uint)width, (uint)height);
        }

        private static int Index(MapHexFile map, int q, int r) => (r * (int)map.MapWidth) + q;

        /// <summary>A road that came from the file: present but unpainted (RoadPaintSeq stays 0, not dirty).</summary>
        private static void MarkLoadedRoad(MapHexFile map, int index)
        {
            map.HexData[index].IsRoad = true;
        }

        /// <summary>Builds the authored masks for the currently loaded roads, then clears the dirty set.</summary>
        private static void EstablishBaseline(MapHexFile map)
        {
            map.CalculateRoadEdgeMasks();
            MapHexFile.RoadDirtyHexes.Clear();
        }

        /// <summary>Replicates <c>RoadSwatch.Apply(hex)</c> for a road paint (headless, no UI swatch).</summary>
        private static void PaintRoad(MapHexFile map, int index)
        {
            var hex = map.HexData[index];
            hex.IsRoad = true;
            hex.RoadPaintSeq = ++MapHexFile.RoadPaintCounter;
            MapHexFile.RoadDirtyHexes.Add(index);
        }

        /// <summary>Replicates <c>RoadSwatch.Apply(hex)</c> for a road erase.</summary>
        private static void EraseRoad(MapHexFile map, int index)
        {
            var hex = map.HexData[index];
            hex.IsRoad = false;
            hex.RoadEdgeMask = 0;
            MapHexFile.RoadDirtyHexes.Add(index);
        }

        /// <summary>The edited hexes plus everything within <paramref name="hops"/> steps of them.</summary>
        private static HashSet<int> WithinHops(MapHexFile map, IEnumerable<int> edited, int hops)
        {
            var set = new HashSet<int>(edited);
            for (int h = 0; h < hops; ++h)
            {
                foreach (var idx in new List<int>(set))
                {
                    for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                    {
                        int nbr = map.GetNeighbourIndex(map.HexData[idx], dir);
                        if (nbr != -1) set.Add(nbr);
                    }
                }
            }
            return set;
        }

        /// <summary>Applying the pending edits incrementally must match recomputing the whole map.</summary>
        private static void AssertIncrementalEqualsFull(MapHexFile map, string scenario)
        {
            map.EnsureRoadEdgeMasks();
            var incremental = SnapshotRoadMasks(map);

            map.CalculateRoadEdgeMasks();
            var full = SnapshotRoadMasks(map);

            for (int i = 0; i < full.Length; ++i)
            {
                if (incremental[i] != full[i])
                {
                    Assert.Fail(
                        $"{scenario}: incremental road mask diverged from a full recompute.\n" +
                        $"First difference at hex {i} (Q={map.HexData[i].Q},R={map.HexData[i].R}): " +
                        $"incremental=0x{incremental[i]:X2}, full=0x{full[i]:X2}.\n" +
                        DumpRoadState(map, incremental, full));
                }
            }
        }

        /// <summary>Every set edge bit must point at an adjacent road hex that carries the reciprocal bit.</summary>
        private static void AssertReciprocalAndRoadOnly(MapHexFile map, string scenario)
        {
            for (int i = 0; i < map.Capacity; ++i)
            {
                var hex = map.HexData[i];
                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    if ((hex.RoadEdgeMask & (1 << dir)) == 0) continue;

                    Assert.IsTrue(hex.IsRoad,
                        $"{scenario}: non-road hex {i} has a road edge (mask 0x{hex.RoadEdgeMask:X2})");

                    int nbrIndex = map.GetNeighbourIndex(hex, dir);
                    Assert.AreNotEqual(-1, nbrIndex,
                        $"{scenario}: hex {i} has a road edge pointing out of bounds (dir {dir})");

                    var nbr = map.HexData[nbrIndex];
                    Assert.IsTrue(nbr.IsRoad,
                        $"{scenario}: hex {i} has a road edge to non-road hex {nbrIndex}");
                    Assert.AreNotEqual(0, nbr.RoadEdgeMask & (1 << HexGridUtility.InverseDir(dir)),
                        $"{scenario}: hex {i}<->{nbrIndex} edge is not reciprocal");
                }
            }
        }

        /// <summary>No three road hexes may be all pairwise-connected (the chord-dropping must hold everywhere).</summary>
        private static void AssertTriangleFree(MapHexFile map, string scenario)
        {
            for (int i = 0; i < map.Capacity; ++i)
            {
                var hex = map.HexData[i];

                var connected = new List<int>();
                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    if ((hex.RoadEdgeMask & (1 << dir)) != 0)
                    {
                        connected.Add(map.GetNeighbourIndex(hex, dir));
                    }
                }

                for (int a = 0; a < connected.Count; ++a)
                {
                    for (int b = a + 1; b < connected.Count; ++b)
                    {
                        Assert.IsFalse(AreConnected(map, connected[a], connected[b]),
                            $"{scenario}: hexes {i}, {connected[a]}, {connected[b]} form a closed triangle " +
                            "(all three edges present)");
                    }
                }
            }
        }

        private static bool AreConnected(MapHexFile map, int x, int y)
        {
            var hex = map.HexData[x];
            for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
            {
                if ((hex.RoadEdgeMask & (1 << dir)) != 0 && map.GetNeighbourIndex(hex, dir) == y)
                {
                    return true;
                }
            }
            return false;
        }

        private static byte[] SnapshotRoadMasks(MapHexFile map)
        {
            var snapshot = new byte[map.Capacity];
            for (int i = 0; i < map.Capacity; ++i)
            {
                snapshot[i] = map.HexData[i].RoadEdgeMask;
            }
            return snapshot;
        }

        private static void Shuffle(List<int> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; --i)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static string DumpRoadState(MapHexFile map, byte[] incremental, byte[] full)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Road hexes on the {map.MapWidth}x{map.MapHeight} grid (index Q,R seq inc/full):");
            for (int i = 0; i < map.Capacity; ++i)
            {
                var hex = map.HexData[i];
                if (hex.IsRoad)
                {
                    sb.AppendLine(
                        $"  [{i}] Q={hex.Q} R={hex.R} seq={hex.RoadPaintSeq} " +
                        $"inc=0x{incremental[i]:X2} full=0x{full[i]:X2}" +
                        (incremental[i] != full[i] ? "  <-- DIFF" : ""));
                }
            }
            return sb.ToString();
        }

        private static int Bits(MapHexFile map, int index)
        {
            return HexGridUtility.GetNumBitsSetInEdgeMask(map.HexData[index].RoadEdgeMask);
        }

        private static void AssertConnected(MapHexFile map, int hexA, int dirAtoB, int hexB, int dirBtoA, string message)
        {
            Assert.AreNotEqual(0, map.HexData[hexA].RoadEdgeMask & (1 << dirAtoB), $"{message} (forward bit)");
            Assert.AreNotEqual(0, map.HexData[hexB].RoadEdgeMask & (1 << dirBtoA), $"{message} (reciprocal bit)");
        }

        private static void AssertNotConnected(MapHexFile map, int hexA, int dirAtoB, int hexB, int dirBtoA, string message)
        {
            Assert.AreEqual(0, map.HexData[hexA].RoadEdgeMask & (1 << dirAtoB), $"{message} (forward bit)");
            Assert.AreEqual(0, map.HexData[hexB].RoadEdgeMask & (1 << dirBtoA), $"{message} (reciprocal bit)");
        }
    }
}
