using System;
using System.IO;
using CAIME.Pathfinding;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Integration
{
    /// <summary>
    /// Validates the movement-cost data inside real, game-ready pathfinding.ppd files.
    ///
    /// <para>
    /// The corresponding map.hex inputs are not available, so these tests assert the
    /// structural invariants that <c>MovementCostsGenerator</c> guarantees rather than
    /// re-deriving exact values: a valid header, the three reserved zero cost slots, a
    /// move-cost table that fits the 7-bit edge field, and every edge index pointing at a
    /// real table entry. Together they prove the on-disk layout matches what the generator
    /// emits.
    /// </para>
    ///
    /// <para>
    /// Data location defaults to <c>%USERPROFILE%\Desktop\campaign_maps</c> and can be
    /// overridden with the <c>CAIME_PPD_DIR</c> environment variable. If no files are found
    /// the tests are marked inconclusive rather than failing.
    /// </para>
    /// </summary>
    [TestClass]
    public class MovementCostsPpdIntegrationTests
    {
        private static string[] FindPpdFiles()
        {
            var dir = Environment.GetEnvironmentVariable("CAIME_PPD_DIR");
            if (string.IsNullOrEmpty(dir))
            {
                dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    "campaign_maps");
            }

            return Directory.Exists(dir)
                ? Directory.GetFiles(dir, "pathfinding.ppd", SearchOption.AllDirectories)
                : Array.Empty<string>();
        }

        [TestMethod]
        public void RealPpdFiles_HaveValidMovementCostData()
        {
            var files = FindPpdFiles();
            if (files.Length == 0)
                Assert.Inconclusive("No pathfinding.ppd files found. Set CAIME_PPD_DIR to enable this test.");

            foreach (var file in files)
            {
                var ppd = PpdFile.Read(file);
                var name = Path.GetFileName(Path.GetDirectoryName(file));

                // --- Header -------------------------------------------------
                Assert.AreEqual(PpdFile.MagicNumber, ppd.Magic, $"{name}: bad magic number.");
                Assert.AreEqual(PpdFile.Version, ppd.FileVersion, $"{name}: unexpected file version.");
                Assert.IsTrue(ppd.Width > 0 && ppd.Height > 0, $"{name}: invalid map dimensions.");

                // --- Move-cost table ---------------------------------------
                Assert.IsTrue(ppd.MoveCosts.Length >= 3, $"{name}: move-cost table must reserve 3 entries.");
                Assert.AreEqual(0, ppd.MoveCosts[0], $"{name}: index 0 must be the immutable true-zero cost.");
                Assert.AreEqual(0, ppd.MoveCosts[1], $"{name}: index 1 (land-beach) is stored as 0.");
                Assert.AreEqual(0, ppd.MoveCosts[2], $"{name}: index 2 (sea-beach) is stored as 0.");

                // The cost index is packed into a 7-bit field, so it can address at most 128 entries.
                Assert.IsTrue(ppd.MoveCosts.Length <= 128,
                    $"{name}: {ppd.MoveCosts.Length} costs cannot be addressed by a 7-bit index.");

                // --- Every edge references a valid cost entry --------------
                int maxIndexSeen = 0;
                bool sawNavigable = false;
                bool sawBlocked = false;

                for (int hex = 0; hex < ppd.CellCount; ++hex)
                {
                    for (int dir = 0; dir < 6; ++dir)
                    {
                        byte raw = ppd.EdgeByte(hex, dir);
                        int costIndex = raw & 0b0111_1111;
                        bool navigable = (raw & 0b1000_0000) != 0;

                        Assert.IsTrue(costIndex < ppd.MoveCosts.Length,
                            $"{name}: hex {hex} dir {dir} cost index {costIndex} is out of range " +
                            $"(table size {ppd.MoveCosts.Length}).");

                        if (costIndex > maxIndexSeen) maxIndexSeen = costIndex;
                        sawNavigable |= navigable;
                        sawBlocked   |= !navigable;
                    }
                }

                // Sanity: a real map exercises both navigability states and at least one
                // non-reserved cost entry.
                Assert.IsTrue(sawNavigable, $"{name}: expected at least one navigable edge.");
                Assert.IsTrue(sawBlocked, $"{name}: expected at least one non-navigable edge.");
                Assert.IsTrue(maxIndexSeen >= 3,
                    $"{name}: expected edges to use cost entries beyond the 3 reserved slots.");
            }
        }
    }
}
