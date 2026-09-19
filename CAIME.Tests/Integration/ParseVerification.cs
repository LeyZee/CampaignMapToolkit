using System;
using System.IO;
using CAIME.Pathfinding;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Integration
{
    /// <summary>
    /// Verify that PpdFile parser is reading section boundaries correctly.
    /// </summary>
    [TestClass]
    public class ParseVerification
    {
        [TestMethod]
        public void VerifyPpdParsing()
        {
            var cases = PathfindingTestData.EnumerateCases();
            foreach (var test in cases)
            {
                if (test.MapName != "bel_attila_map") continue;

                var map = test.LoadPreparedMap();
                var ppd = test.ReadPpd();

                using (var log = File.CreateText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Integration", "parse_verify.txt")))
                {
                    log.WriteLine($"=== {test.MapName} PPD PARSE VERIFICATION ===\n");

                    // Header
                    log.WriteLine($"Magic: 0x{ppd.Magic:X16} (expected 0x{PpdFile.MagicNumber:X16})");
                    log.WriteLine($"Version: {ppd.FileVersion} (expected {PpdFile.Version})");
                    log.WriteLine($"Land Regions: {ppd.LandRegionsCount}");

                    // Map dimensions
                    log.WriteLine($"\nMap Size: {ppd.Width} x {ppd.Height} = {ppd.CellCount} cells");
                    log.WriteLine($"Map.Capacity: {map.Capacity} cells");
                    log.WriteLine($"Map.MapWidth: {map.MapWidth}, Map.MapHeight: {map.MapHeight}");
                    log.WriteLine($"Dimensions match: {ppd.Width == map.MapWidth && ppd.Height == map.MapHeight}");

                    // Tile groups
                    log.WriteLine($"\nTile Groups: {ppd.TileGroupsCount}");

                    // Section sizes
                    log.WriteLine($"\nSection Raw Sizes:");
                    log.WriteLine($"  BridgesRaw: {ppd.BridgesRaw?.Length ?? 0} bytes");
                    log.WriteLine($"  PassableBorderHexesRaw: {ppd.PassableBorderHexesRaw?.Length ?? 0} bytes");
                    log.WriteLine($"  RestrictionsRaw: {ppd.RestrictionsRaw?.Length ?? 0} bytes");

                    // Parse Bridges raw section to check it's valid
                    if (ppd.BridgesRaw != null && ppd.BridgesRaw.Length >= 2)
                    {
                        int bridgeCount = ppd.BridgesRaw[0] | (ppd.BridgesRaw[1] << 8);
                        log.WriteLine($"\nBridges: parsed count = {bridgeCount}");
                    }

                    // Parse PassableBorderHexes raw section
                    if (ppd.PassableBorderHexesRaw != null && ppd.PassableBorderHexesRaw.Length >= 4)
                    {
                        int borderCount = ppd.PassableBorderHexesRaw[0] | (ppd.PassableBorderHexesRaw[1] << 8) |
                                         (ppd.PassableBorderHexesRaw[2] << 16) | (ppd.PassableBorderHexesRaw[3] << 24);
                        log.WriteLine($"\nPassable Border Hexes: parsed count = {borderCount}");
                        log.WriteLine($"  Expected data size: 4 + {borderCount} * 4 = {4 + borderCount * 4} bytes");
                        log.WriteLine($"  Actual raw size: {ppd.PassableBorderHexesRaw.Length} bytes");
                    }

                    log.Flush();
                }
            }
        }
    }
}
