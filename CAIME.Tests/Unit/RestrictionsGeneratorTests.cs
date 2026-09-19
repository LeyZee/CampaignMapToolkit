using CAIME;
using CAIME.Pathfinding;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Unit tests for <c>RestrictionsGenerator</c>.
    ///
    /// Grid layout: 1×N (width=1, height=N). Q=0 for all hexes; index = R.
    ///   dir 0 (dQ=0, dR=+1) connects R→R+1.
    ///   dir 3 (dQ=0, dR=-1) connects R→R-1.
    ///   Other directions go out of bounds.
    /// </summary>
    [TestClass]
    public class RestrictionsGeneratorTests
    {
        private static Hex MakeHex(int row, byte restrictionLvl) =>
            new Hex(0, row, row) { RestrictionLvl = restrictionLvl };

        private static Restriction[] Run(Hex[] hexData, uint width, uint height)
        {
            var map = BordersTestHarness.BuildMap(hexData, width, height);
            var gen = new RestrictionsGenerator(map, debugPath: null);
            gen.Generate(out var restrictions);
            return restrictions;
        }

        // dir 0 bitmask = 1 << 0 = 1
        private const byte Dir0Bit = 1 << 0;
        // dir 3 bitmask = 1 << 3 = 8
        private const byte Dir3Bit = 1 << 3;

        [TestMethod]
        public void LowerRestriction_AdjacentToHigher_MasksLevelsInRange()
        {
            // hex0(lvl=0) — hex1(lvl=2)
            // hex0 sees hex1 via dir 0: lvl 0 and 1 should have dir-0 bit set.
            // hex1 sees hex0 via dir 3: hex1.lvl(2) is NOT < hex0.lvl(0), so no mask.
            var restrictions = Run(
                new[] { MakeHex(0, 0), MakeHex(1, 2) },
                width: 1, height: 2);

            Assert.AreEqual(1, restrictions[0].Hexes.Count, "level 0 should have one hex entry");
            Assert.AreEqual(Dir0Bit, restrictions[0].EdgeMasks[0],
                "level 0 mask should have dir-0 bit set (towards hex1)");

            Assert.AreEqual(1, restrictions[1].Hexes.Count, "level 1 should have one hex entry");
            Assert.AreEqual(Dir0Bit, restrictions[1].EdgeMasks[0],
                "level 1 mask should also have dir-0 bit set");

            for (int lvl = 2; lvl < Hex.MAX_RESTRICTIONS_COUNT; ++lvl)
                Assert.AreEqual(0, restrictions[lvl].Hexes.Count,
                    $"level {lvl} should have no entries");
        }

        [TestMethod]
        public void EqualRestrictionLevels_NoMaskGenerated()
        {
            var restrictions = Run(
                new[] { MakeHex(0, 1), MakeHex(1, 1) },
                width: 1, height: 2);

            foreach (var r in restrictions)
                Assert.AreEqual(0, r.Hexes.Count, "equal levels → no restriction mask");
        }

        [TestMethod]
        public void HigherRestriction_AdjacentToLower_OnlyLowerHexGeneratesMask()
        {
            // hex0(lvl=2) adjacent to hex1(lvl=0):
            //   hex0 looking at hex1: 2 < 0 is false → hex0 produces no mask.
            //   hex1 looking at hex0: 0 < 2 is true → hex1 DOES produce masks for levels 0 and 1.
            // In restrictions[0]: only hex1 should appear (not hex0).
            var restrictions = Run(
                new[] { MakeHex(0, 2), MakeHex(1, 0) },
                width: 1, height: 2);

            Assert.AreEqual(1, restrictions[0].Hexes.Count, "only the lower-lvl hex generates a mask");
            // hex1 is at row 1 (R=1); dir 3 points towards hex0 at R=0
            Assert.AreEqual(1, restrictions[0].Hexes[0].R, "hex1 (lower-lvl) should be the entry");
            Assert.AreEqual(Dir3Bit, restrictions[0].EdgeMasks[0],
                "dir-3 bit (towards higher-lvl hex0) must be set");
        }

        [TestMethod]
        public void SingleHex_NoCrash()
        {
            var restrictions = Run(new[] { MakeHex(0, 1) }, width: 1, height: 1);

            foreach (var r in restrictions)
                Assert.AreEqual(0, r.Hexes.Count);
        }

        [TestMethod]
        public void BothDirections_BothMasked()
        {
            // 1×3: hex1(lvl=0) sandwiched between hex0(lvl=1) and hex2(lvl=1).
            // hex1 sees dir 3 (→hex0, lvl 1) and dir 0 (→hex2, lvl 1): both have lvl 0 < 1.
            // restrictions[0] for hex1 should have bits for both dir 0 and dir 3.
            var restrictions = Run(
                new[] { MakeHex(0, 1), MakeHex(1, 0), MakeHex(2, 1) },
                width: 1, height: 3);

            Assert.AreEqual(1, restrictions[0].Hexes.Count, "hex1 should be in level-0 restrictions");
            var mask = restrictions[0].EdgeMasks[0];
            Assert.IsTrue((mask & Dir0Bit) != 0, "dir-0 bit (towards hex2) must be set");
            Assert.IsTrue((mask & Dir3Bit) != 0, "dir-3 bit (towards hex0) must be set");
        }
    }
}
