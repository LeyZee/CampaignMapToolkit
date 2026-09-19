using System;
using System.Collections.Generic;
using System.IO;
using CAIME;
using CAIME.Pathfinding;

namespace CAIME.Tests.Helpers
{
    /// <summary>
    /// Pairs each game-ready <c>TestData/&lt;map&gt;/pathfinding.ppd</c> with the template
    /// <c>Templates/&lt;map&gt;/map.hex</c> that CAIME exported it from, so a differential test
    /// can re-generate a section from the template and diff it against the real .ppd.
    /// </summary>
    internal sealed class PathfindingTestCase
    {
        public string MapName            { get; }
        public string MapHexPath         { get; }
        public string PpdPath            { get; }

        /// <summary>
        /// Path to the game's <c>campaign_ground_types.xml</c> (the same table the app reads),
        /// or null when no matching <c>TestData/db/&lt;game&gt;</c> folder was found.
        /// </summary>
        public string GroundTypesXmlPath { get; }

        /// <summary>
        /// Path to the game's <c>campaign_map_roads.xml</c>, or null when no matching
        /// <c>TestData/db/&lt;game&gt;</c> folder was found. Used to populate <c>CachedRoads</c>.
        /// </summary>
        public string RoadsXmlPath { get; }

        /// <summary>
        /// The campaign this map belongs to, derived from the map folder name by dropping the
        /// trailing <c>_map</c> (e.g. <c>main_attila_map</c> → <c>main_attila</c>). Matches the
        /// <c>campaign</c> column the roads table is filtered by.
        /// </summary>
        public string CampaignName => MapName.EndsWith("_map")
            ? MapName.Substring(0, MapName.Length - "_map".Length)
            : MapName;

        public PathfindingTestCase(string mapName, string mapHexPath, string ppdPath, string groundTypesXmlPath, string roadsXmlPath)
        {
            MapName            = mapName;
            MapHexPath         = mapHexPath;
            PpdPath            = ppdPath;
            GroundTypesXmlPath = groundTypesXmlPath;
            RoadsXmlPath       = roadsXmlPath;
        }

        /// <summary>
        /// Loads the template map and runs the same preprocessing the exporter performs before
        /// invoking any generator (<see cref="PathfindingExporter"/> / ExportSequential).
        /// </summary>
        public MapHexFile LoadPreparedMap()
        {
            var map = new MapHexFile();
            if (!map.Load(MapHexPath))
                throw new InvalidOperationException($"{MapName}: MapHexFile.Load returned false for {MapHexPath}.");

            map.UpdateHexTypes();
            // Mirror the exporter: preserve the masks authored in the source map.hex rather than
            // recomputing them (a freshly loaded map has no edits, so these are no-ops here).
            map.EnsureRegionEdgeMasks();
            map.EnsureRoadEdgeMasks();
            return map;
        }

        /// <summary>Reads and fully parses the game-ready .ppd for this map.</summary>
        public PpdFile ReadPpd() => PpdFile.Read(PpdPath);

        /// <summary>
        /// Builds a <see cref="DatabaseViewModel"/> whose <c>CachedGroundTypes</c> are loaded from
        /// the game's real <c>campaign_ground_types.xml</c>, matching what the app caches at runtime.
        /// </summary>
        public DatabaseViewModel LoadDatabase()
        {
            if (GroundTypesXmlPath == null)
                throw new InvalidOperationException(
                    $"{MapName}: no campaign_ground_types.xml was found under TestData/db.");

            var db = GroundTypesDb.Load(GroundTypesXmlPath);

            // Roads are optional for callers that only need movement costs, but the heuristic cache
            // reads CachedRoads.Last().MoveCost, so load them when the table is available.
            if (RoadsXmlPath != null)
                RoadsDb.Load(db, RoadsXmlPath, CampaignName);

            return db;
        }

        public override string ToString() => MapName;
    }

    internal static class PathfindingTestData
    {
        /// <summary>
        /// Returns one case per map that has BOTH a TestData pathfinding.ppd and a matching
        /// template map.hex. Empty if the data folders cannot be located.
        /// </summary>
        public static IReadOnlyList<PathfindingTestCase> EnumerateCases()
        {
            var repoRoot = FindRepoRoot();
            if (repoRoot == null)
                return Array.Empty<PathfindingTestCase>();

            var testDataRoot  = Path.Combine(repoRoot, "TestData");
            var templatesRoot = Path.Combine(repoRoot, "Templates");
            var testMapDirect = Path.Combine(testDataRoot, "campaign_maps");
            var dbRoot        = Path.Combine(testDataRoot, "db");
            if (!Directory.Exists(testDataRoot) || !Directory.Exists(templatesRoot))
                return Array.Empty<PathfindingTestCase>();

            var cases = new List<PathfindingTestCase>();
            foreach (var mapDir in Directory.GetDirectories(testMapDirect))
            {
                var mapName = Path.GetFileName(mapDir);
                var ppdPath = Path.Combine(mapDir, "pathfinding.ppd");
                var hexPath = Path.Combine(templatesRoot, mapName, "map.hex");

                if (File.Exists(ppdPath) && File.Exists(hexPath))
                    cases.Add(new PathfindingTestCase(
                        mapName, hexPath, ppdPath,
                        ResolveDbFile(dbRoot, mapName, "campaign_ground_types.xml"),
                        ResolveDbFile(dbRoot, mapName, "campaign_map_roads.xml")));
            }

            return cases;
        }

        /// <summary>
        /// Finds <paramref name="fileName"/> in the <c>TestData/db/&lt;game&gt;</c> folder for a map, matching
        /// the game folder against a token in the map name (e.g. "main_attila_map" → "attila"). Falls back
        /// to the only db folder present when no token matches. Returns null if nothing usable is found.
        /// </summary>
        private static string ResolveDbFile(string dbRoot, string mapName, string fileName)
        {
            if (!Directory.Exists(dbRoot))
                return null;

            var gameDirs = Directory.GetDirectories(dbRoot);
            string match = null;
            foreach (var gameDir in gameDirs)
            {
                var game = Path.GetFileName(gameDir);
                if (Array.IndexOf(mapName.Split('_'), game) >= 0)
                {
                    match = gameDir;
                    break;
                }
            }

            // A single game folder is unambiguous even if the name token doesn't line up.
            if (match == null && gameDirs.Length == 1)
                match = gameDirs[0];

            if (match == null)
                return null;

            var xml = Path.Combine(match, fileName);
            return File.Exists(xml) ? xml : null;
        }

        /// <summary>Walks up from the test assembly until both TestData and Templates are found.</summary>
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
