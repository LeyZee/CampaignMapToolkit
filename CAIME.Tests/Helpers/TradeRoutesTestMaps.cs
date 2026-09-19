using System;
using System.Collections.Generic;
using System.IO;

namespace CAIME.Tests.Helpers
{
    /// <summary>
    /// Locates the trade-route test maps: every <c>Templates/&lt;map&gt;/map.hex</c>, and the subset that
    /// also has a game-ready <c>TestData/campaign_maps/&lt;map&gt;/trade_routes.ptd</c> to diff against.
    /// </summary>
    internal static class TradeRoutesTestMaps
    {
        /// <summary>All template maps available, as (mapName, map.hex path).</summary>
        public static IReadOnlyList<(string MapName, string MapHexPath)> Enumerate()
        {
            var repoRoot = FindRepoRoot();
            var result = new List<(string, string)>();
            if (repoRoot == null)
                return result;

            var templatesRoot = Path.Combine(repoRoot, "Templates");
            if (!Directory.Exists(templatesRoot))
                return result;

            foreach (var mapDir in Directory.GetDirectories(templatesRoot))
            {
                var hexPath = Path.Combine(mapDir, "map.hex");
                if (File.Exists(hexPath))
                    result.Add((Path.GetFileName(mapDir), hexPath));
            }

            return result;
        }

        /// <summary>Template maps paired with a reference trade_routes.ptd, as (mapName, map.hex, ptd).</summary>
        public static IReadOnlyList<(string MapName, string MapHexPath, string PtdPath)> EnumerateWithReference()
        {
            var repoRoot = FindRepoRoot();
            var result = new List<(string, string, string)>();
            if (repoRoot == null)
                return result;

            var mapsRoot      = Path.Combine(repoRoot, "TestData", "campaign_maps");
            var templatesRoot = Path.Combine(repoRoot, "Templates");
            if (!Directory.Exists(mapsRoot) || !Directory.Exists(templatesRoot))
                return result;

            foreach (var mapDir in Directory.GetDirectories(mapsRoot))
            {
                var mapName = Path.GetFileName(mapDir);
                var ptdPath = Path.Combine(mapDir, "trade_routes.ptd");
                var hexPath = Path.Combine(templatesRoot, mapName, "map.hex");
                if (File.Exists(ptdPath) && File.Exists(hexPath))
                    result.Add((mapName, hexPath, ptdPath));
            }

            return result;
        }

        private static string FindRepoRoot()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 10 && dir != null; ++i)
            {
                if (Directory.Exists(Path.Combine(dir, "TestData")) &&
                    Directory.Exists(Path.Combine(dir, "Templates")))
                {
                    return dir;
                }
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }
    }
}
