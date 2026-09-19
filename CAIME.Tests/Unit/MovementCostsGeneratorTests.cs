using System;
using CAIME;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static CAIME.Tests.Helpers.MovementTestHarness;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Branch-by-branch coverage of <c>MovementCostsGenerator</c>'s edge-cost and
    /// navigability calculations, driven through the public <c>Generate</c> entry point
    /// over hand-built two-hex maps.
    ///
    /// <para>Semantic move-cost indices baked into the generator:</para>
    /// <list type="bullet">
    ///   <item>index 0 = true zero / beach-to-land / beach-to-sea</item>
    ///   <item>index 1 = land-to-beach</item>
    ///   <item>index 2 = sea-to-beach</item>
    /// </list>
    /// </summary>
    [TestClass]
    public class MovementCostsGeneratorTests
    {
        private const int TRUE_ZERO_INDEX    = 0;
        private const int LAND_TO_BEACH_INDEX = 1;
        private const int SEA_TO_BEACH_INDEX  = 2;

        // ---- hex factory ------------------------------------------------------

        private static Hex MakeHex(int q, int r, int index, sbyte ground, Action<Hex> configure = null)
        {
            var hex = new Hex(q, r, index) { GroundTypeIndex = ground };
            configure?.Invoke(hex);
            hex.UpdateHexType();
            return hex;
        }

        private static Hex A(sbyte ground, Action<Hex> configure = null) => MakeHex(0, 0, 0, ground, configure);
        private static Hex B(sbyte ground, Action<Hex> configure = null) => MakeHex(0, 1, 1, ground, configure);

        // ====================================================================
        //  Edge-cost calculation
        // ====================================================================

        [TestMethod]
        public void ImpassableHex_EdgeCostIsTrueZero()
        {
            var a = A(GROUND_GRASS, h => h.IsPassable = false);
            var b = B(GROUND_GRASS);

            var (aToB, _, costs) = RunPair(a, b);
            var edge = new Edge(aToB, costs);

            Assert.AreEqual(TRUE_ZERO_INDEX, edge.CostIndex, "Impassable source hex must yield the true-zero index.");
            Assert.AreEqual((ushort)0, edge.CostValue);
        }

        [TestMethod]
        public void ImpassableNeighbour_EdgeCostIsSourceGroundCost()
        {
            var a = A(GROUND_FOREST);                       // cost 30
            var b = B(GROUND_GRASS, h => h.IsPassable = false);

            var (aToB, _, costs) = RunPair(a, b);
            var edge = new Edge(aToB, costs);

            Assert.AreEqual(COST_FOREST, edge.CostValue, "Edge into an impassable neighbour costs the source hex's own ground cost.");
        }

        [TestMethod]
        public void CliffSource_FallsBackToAverage()
        {
            var a = A(GROUND_GRASS, h => h.IsCliff = true); // cost 10
            var b = B(GROUND_FOREST);                       // cost 30

            var (aToB, _, costs) = RunPair(a, b);
            var edge = new Edge(aToB, costs);

            Assert.AreEqual((ushort)20, edge.CostValue, "A cliff hex skips type-specific rules and uses the plain average.");
        }

        [TestMethod]
        public void CliffNeighbour_FallsBackToAverage()
        {
            var a = A(GROUND_GRASS);                        // cost 10
            var b = B(GROUND_FOREST, h => h.IsCliff = true);// cost 30

            var (aToB, _, costs) = RunPair(a, b);
            var edge = new Edge(aToB, costs);

            Assert.AreEqual((ushort)20, edge.CostValue);
        }

        [TestMethod]
        public void River_To_Land_IsTrueZero()
        {
            var a = A(GROUND_RIVER, h => h.IsRiver = true);
            var b = B(GROUND_GRASS);

            var (aToB, _, costs) = RunPair(a, b);
            var edge = new Edge(aToB, costs);

            Assert.AreEqual(TRUE_ZERO_INDEX, edge.CostIndex, "Crossing from a river onto land is free.");
        }

        [TestMethod]
        public void River_To_Sea_IsAverage()
        {
            var a = A(GROUND_RIVER, h => h.IsRiver = true); // cost 40
            var b = B(GROUND_OCEAN, h => h.IsSea = true);   // cost 20

            var (aToB, _, costs) = RunPair(a, b);
            var edge = new Edge(aToB, costs);

            Assert.AreEqual((ushort)30, edge.CostValue);
        }

        [TestMethod]
        public void River_To_Beach_IsAverage()
        {
            var a = A(GROUND_RIVER, h => h.IsRiver = true); // cost 40
            var b = B(GROUND_COAST, h => h.IsBeach = true); // cost 50

            var (aToB, _, costs) = RunPair(a, b);
            var edge = new Edge(aToB, costs);

            Assert.AreEqual((ushort)45, edge.CostValue, "A beach neighbour is not 'Land', so river falls back to the average.");
        }

        [TestMethod]
        public void Beach_To_Land_IsTrueZero()
        {
            var a = A(GROUND_GRASS, h => h.IsBeach = true);
            var b = B(GROUND_GRASS);

            var (aToB, _, costs) = RunPair(a, b);
            var edge = new Edge(aToB, costs);

            Assert.AreEqual(TRUE_ZERO_INDEX, edge.CostIndex);
        }

        [TestMethod]
        public void Beach_To_Sea_IsTrueZero()
        {
            var a = A(GROUND_GRASS, h => h.IsBeach = true);
            var b = B(GROUND_OCEAN, h => h.IsSea = true);

            var (aToB, _, costs) = RunPair(a, b);
            var edge = new Edge(aToB, costs);

            Assert.AreEqual(TRUE_ZERO_INDEX, edge.CostIndex);
        }

        [TestMethod]
        public void Beach_To_Beach_IsAverage()
        {
            var a = A(GROUND_GRASS, h => h.IsBeach = true); // cost 10
            var b = B(GROUND_COAST, h => h.IsBeach = true); // cost 50

            var (aToB, _, costs) = RunPair(a, b);
            var edge = new Edge(aToB, costs);

            Assert.AreEqual((ushort)30, edge.CostValue, "Beach-to-beach is neither land nor sea, so it averages.");
        }

        [TestMethod]
        public void Land_To_Beach_UsesLandToBeachIndex()
        {
            var a = A(GROUND_GRASS);
            var b = B(GROUND_GRASS, h => h.IsBeach = true);

            var (aToB, _, costs) = RunPair(a, b);
            var edge = new Edge(aToB, costs);

            Assert.AreEqual(LAND_TO_BEACH_INDEX, edge.CostIndex);
        }

        [TestMethod]
        public void Land_To_River_UsesRiverGroundCost()
        {
            var a = A(GROUND_GRASS);
            var b = B(GROUND_GRASS, h => h.IsRiver = true);

            var (aToB, _, costs) = RunPair(a, b);
            var edge = new Edge(aToB, costs);

            Assert.AreEqual(COST_RIVER, edge.CostValue, "Land-into-river uses the dedicated 'river' ground cost, not the neighbour's ground.");
        }

        [TestMethod]
        public void Land_To_Land_IsAverage()
        {
            var a = A(GROUND_GRASS);   // cost 10
            var b = B(GROUND_FOREST);  // cost 30

            var (aToB, _, costs) = RunPair(a, b);
            var edge = new Edge(aToB, costs);

            Assert.AreEqual((ushort)20, edge.CostValue);
        }

        [TestMethod]
        public void Sea_To_Beach_UsesSeaToBeachIndex()
        {
            var a = A(GROUND_OCEAN, h => h.IsSea = true);
            var b = B(GROUND_GRASS, h => h.IsBeach = true);

            var (aToB, _, costs) = RunPair(a, b);
            var edge = new Edge(aToB, costs);

            Assert.AreEqual(SEA_TO_BEACH_INDEX, edge.CostIndex);
        }

        [TestMethod]
        public void Sea_To_RiverBeach_IsAverage()
        {
            var a = A(GROUND_OCEAN, h => h.IsSea = true);   // cost 20
            var b = B(GROUND_COAST, h => { h.IsBeach = true; h.IsRiver = true; }); // cost 50

            var (aToB, _, costs) = RunPair(a, b);
            var edge = new Edge(aToB, costs);

            Assert.AreEqual((ushort)35, edge.CostValue, "A beach that is also a river is excluded from the sea-to-beach rule.");
        }

        [TestMethod]
        public void Sea_To_Sea_IsAverage()
        {
            var a = A(GROUND_OCEAN, h => h.IsSea = true);   // cost 20
            var b = B(GROUND_COAST, h => h.IsSea = true);   // cost 50

            var (aToB, _, costs) = RunPair(a, b);
            var edge = new Edge(aToB, costs);

            Assert.AreEqual((ushort)35, edge.CostValue);
        }

        [TestMethod]
        public void BridgeCliff_EdgeCostUsesAverage()
        {
            // A bridge cliff has IsCliff = true, so the type-specific block is skipped
            // and the cost is the plain average.
            var a = A(GROUND_GRASS, h => { h.IsCliff = true; h.IsBridgeCliff = true; }); // cost 10
            var b = B(GROUND_FOREST);                                                    // cost 30

            var (aToB, _, costs) = RunPair(a, b);
            var edge = new Edge(aToB, costs);

            Assert.AreEqual((ushort)20, edge.CostValue);
        }

        [TestMethod]
        public void AverageOfZeroCosts_MapsToTrueZeroIndex()
        {
            // Two void-cost sea hexes -> average 0 -> GetCostIndex(0) must reuse index 0.
            var a = A(GROUND_VOID, h => h.IsSea = true);
            var b = B(GROUND_VOID, h => h.IsSea = true);

            var (aToB, _, costs) = RunPair(a, b);
            var edge = new Edge(aToB, costs);

            Assert.AreEqual(TRUE_ZERO_INDEX, edge.CostIndex);
            Assert.AreEqual((ushort)0, edge.CostValue);
        }

        // ====================================================================
        //  Navigability calculation
        // ====================================================================

        [TestMethod]
        public void Navigability_ImpassableSource_IsBlocked()
        {
            var a = A(GROUND_GRASS, h => h.IsPassable = false);
            var b = B(GROUND_GRASS);

            var (aToB, _, costs) = RunPair(a, b);
            Assert.IsFalse(new Edge(aToB, costs).Navigable);
        }

        [TestMethod]
        public void Navigability_ImpassableNeighbour_IsBlocked()
        {
            var a = A(GROUND_GRASS);
            var b = B(GROUND_GRASS, h => h.IsPassable = false);

            var (aToB, _, costs) = RunPair(a, b);
            Assert.IsFalse(new Edge(aToB, costs).Navigable);
        }

        [TestMethod]
        public void Navigability_CliffSource_IsBlocked()
        {
            var a = A(GROUND_GRASS, h => h.IsCliff = true);
            var b = B(GROUND_GRASS);

            var (aToB, _, costs) = RunPair(a, b);
            Assert.IsFalse(new Edge(aToB, costs).Navigable);
        }

        [TestMethod]
        public void Navigability_CliffNeighbour_IsBlocked()
        {
            var a = A(GROUND_GRASS);
            var b = B(GROUND_GRASS, h => h.IsCliff = true);

            var (aToB, _, costs) = RunPair(a, b);
            Assert.IsFalse(new Edge(aToB, costs).Navigable);
        }

        [TestMethod]
        public void Navigability_River_To_Land_IsNavigable()
        {
            var a = A(GROUND_RIVER, h => h.IsRiver = true);
            var b = B(GROUND_GRASS);

            var (aToB, _, costs) = RunPair(a, b);
            Assert.IsTrue(new Edge(aToB, costs).Navigable);
        }

        [TestMethod]
        public void Navigability_River_To_Sea_IsBlocked()
        {
            var a = A(GROUND_RIVER, h => h.IsRiver = true);
            var b = B(GROUND_OCEAN, h => h.IsSea = true);

            var (aToB, _, costs) = RunPair(a, b);
            Assert.IsFalse(new Edge(aToB, costs).Navigable);
        }

        [TestMethod]
        public void Navigability_Beach_To_Land_IsNavigable()
        {
            var a = A(GROUND_GRASS, h => h.IsBeach = true);
            var b = B(GROUND_GRASS);

            var (aToB, _, costs) = RunPair(a, b);
            Assert.IsTrue(new Edge(aToB, costs).Navigable);
        }

        [TestMethod]
        public void Navigability_Beach_To_Sea_IsNavigable()
        {
            var a = A(GROUND_GRASS, h => h.IsBeach = true);
            var b = B(GROUND_OCEAN, h => h.IsSea = true);

            var (aToB, _, costs) = RunPair(a, b);
            Assert.IsTrue(new Edge(aToB, costs).Navigable);
        }

        [TestMethod]
        public void Navigability_Beach_To_Beach_IsBlocked()
        {
            var a = A(GROUND_GRASS, h => h.IsBeach = true);
            var b = B(GROUND_COAST, h => h.IsBeach = true);

            var (aToB, _, costs) = RunPair(a, b);
            Assert.IsFalse(new Edge(aToB, costs).Navigable);
        }

        [TestMethod]
        public void Navigability_BridgeCliff_To_Land_IsNavigable()
        {
            var a = A(GROUND_GRASS, h => { h.IsCliff = true; h.IsBridgeCliff = true; });
            var b = B(GROUND_GRASS);

            var (aToB, _, costs) = RunPair(a, b);
            Assert.IsTrue(new Edge(aToB, costs).Navigable);
        }

        [TestMethod]
        public void Navigability_BridgeCliff_To_Sea_IsBlocked()
        {
            var a = A(GROUND_GRASS, h => { h.IsCliff = true; h.IsBridgeCliff = true; });
            var b = B(GROUND_OCEAN, h => h.IsSea = true);

            var (aToB, _, costs) = RunPair(a, b);
            Assert.IsFalse(new Edge(aToB, costs).Navigable);
        }

        [TestMethod]
        public void Navigability_Land_To_BridgeCliff_IsNavigable()
        {
            var a = A(GROUND_GRASS);
            var b = B(GROUND_GRASS, h => { h.IsCliff = true; h.IsBridgeCliff = true; });

            var (aToB, _, costs) = RunPair(a, b);
            Assert.IsTrue(new Edge(aToB, costs).Navigable);
        }

        [TestMethod]
        public void Navigability_Land_To_River_IsNavigable()
        {
            var a = A(GROUND_GRASS);
            var b = B(GROUND_GRASS, h => h.IsRiver = true);

            var (aToB, _, costs) = RunPair(a, b);
            Assert.IsTrue(new Edge(aToB, costs).Navigable);
        }

        [TestMethod]
        public void Navigability_Land_To_Land_IsNavigable()
        {
            var a = A(GROUND_GRASS);
            var b = B(GROUND_FOREST);

            var (aToB, _, costs) = RunPair(a, b);
            Assert.IsTrue(new Edge(aToB, costs).Navigable);
        }

        [TestMethod]
        public void Navigability_Land_To_Sea_IsBlocked()
        {
            var a = A(GROUND_GRASS);
            var b = B(GROUND_OCEAN, h => h.IsSea = true);

            var (aToB, _, costs) = RunPair(a, b);
            Assert.IsFalse(new Edge(aToB, costs).Navigable);
        }

        [TestMethod]
        public void Navigability_Sea_To_Sea_IsNavigable()
        {
            var a = A(GROUND_OCEAN, h => h.IsSea = true);
            var b = B(GROUND_COAST, h => h.IsSea = true);

            var (aToB, _, costs) = RunPair(a, b);
            Assert.IsTrue(new Edge(aToB, costs).Navigable);
        }

        [TestMethod]
        public void Navigability_Sea_To_Beach_IsNavigable()
        {
            var a = A(GROUND_OCEAN, h => h.IsSea = true);
            var b = B(GROUND_GRASS, h => h.IsBeach = true);

            var (aToB, _, costs) = RunPair(a, b);
            Assert.IsTrue(new Edge(aToB, costs).Navigable);
        }

        [TestMethod]
        public void Navigability_Sea_To_RiverBeach_IsBlocked()
        {
            var a = A(GROUND_OCEAN, h => h.IsSea = true);
            var b = B(GROUND_COAST, h => { h.IsBeach = true; h.IsRiver = true; });

            var (aToB, _, costs) = RunPair(a, b);
            Assert.IsFalse(new Edge(aToB, costs).Navigable);
        }

        [TestMethod]
        public void Navigability_Sea_To_Land_IsBlocked()
        {
            var a = A(GROUND_OCEAN, h => h.IsSea = true);
            var b = B(GROUND_GRASS);

            var (aToB, _, costs) = RunPair(a, b);
            Assert.IsFalse(new Edge(aToB, costs).Navigable);
        }

        // ====================================================================
        //  Byte packing, geometry, out-of-bounds and the move-cost table
        // ====================================================================

        [TestMethod]
        public void EdgeByte_PacksNavigabilityIntoHighBit()
        {
            var navigable = RunPair(A(GROUND_GRASS), B(GROUND_FOREST)).aToB;            // land-land
            var blocked   = RunPair(A(GROUND_GRASS), B(GROUND_OCEAN, h => h.IsSea = true)).aToB; // land-sea

            Assert.AreEqual(0x80, navigable & 0x80, "Navigable edge must have bit 7 set.");
            Assert.AreEqual(0x00, blocked   & 0x80, "Non-navigable edge must have bit 7 clear.");
        }

        [TestMethod]
        public void Edges_AreWrittenToCorrectDirectionSlots()
        {
            // A=(0,0), B=(0,1). Direction 0 is (0,+1) so A->B is slot 0; the inverse
            // direction 3 is (0,-1) so B->A is slot (8 + 3). All other slots are
            // out of bounds and must stay zero.
            var a = A(GROUND_GRASS);
            var b = B(GROUND_GRASS, h => h.IsBeach = true);

            var (edges, costs) = Run(new[] { a, b }, 1, 2);

            Assert.AreEqual(LAND_TO_BEACH_INDEX, new Edge(edges[0], costs).CostIndex, "A->B should be in slot 0.");
            Assert.AreEqual(TRUE_ZERO_INDEX,     new Edge(edges[8 + 3], costs).CostIndex, "B->A should be in slot 8+3.");

            for (int dir = 1; dir < 6; ++dir)
                Assert.AreEqual(0, edges[dir], $"A slot {dir} is out of bounds and must be zero.");

            for (int dir = 0; dir < 6; ++dir)
            {
                if (dir == 3) continue;
                Assert.AreEqual(0, edges[8 + dir], $"B slot {dir} is out of bounds and must be zero.");
            }
        }

        [TestMethod]
        public void OutOfBoundsNeighbours_LeaveEdgesZero()
        {
            var (edges, _) = Run(new[] { A(GROUND_GRASS) }, 1, 1);

            Assert.AreEqual(8, edges.Length);
            for (int i = 0; i < edges.Length; ++i)
                Assert.AreEqual(0, edges[i], $"Slot {i} of an isolated hex must be zero.");
        }

        [TestMethod]
        public void MoveCostTable_ReservesThreeZeroEntriesAndDeduplicates()
        {
            // A 1x3 column where every in-bounds edge averages to 20.
            var a = MakeHex(0, 0, 0, GROUND_GRASS);   // cost 10
            var b = MakeHex(0, 1, 1, GROUND_FOREST);  // cost 30
            var c = MakeHex(0, 2, 2, GROUND_GRASS);   // cost 10

            var (edges, costs) = Run(new[] { a, b, c }, 1, 3);

            CollectionAssert.AreEqual(
                new ushort[] { 0, 0, 0, 20 }, costs,
                "Three reserved zero entries, then a single deduplicated '20' for all four edges.");

            // Every in-bounds edge must point at the shared '20' entry (index 3).
            Assert.AreEqual(3, new Edge(edges[0 * 8 + 0], costs).CostIndex);       // A->B
            Assert.AreEqual(3, new Edge(edges[1 * 8 + 0], costs).CostIndex);       // B->C
            Assert.AreEqual(3, new Edge(edges[1 * 8 + 3], costs).CostIndex);       // B->A
            Assert.AreEqual(3, new Edge(edges[2 * 8 + 3], costs).CostIndex);       // C->B
        }

        [TestMethod]
        public void Generate_RejectsWronglySizedEdgeBuffer()
        {
            // Generate bails out (leaving the buffer untouched) when the edges array
            // is not Capacity * 8 bytes, matching the HexTypes-generator contract.
            var a = A(GROUND_GRASS);
            var b = B(GROUND_FOREST);
            var map = BuildMapFor(new[] { a, b }, 1, 2);

            var generator = CreateGenerator(map);
            var wrongSize = new byte[3]; // should be 2 * 8 = 16
            generator.Generate(ref wrongSize);

            CollectionAssert.AreEqual(new byte[3], wrongSize, "Mismatched buffer must be left untouched.");
        }
    }
}
