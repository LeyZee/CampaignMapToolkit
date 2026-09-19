using System;
using System.Collections.Generic;
using System.IO;
using CAIME;
using CAIME.Pathfinding;
using CAIME.Pathfinding.Heuristic;
using CAIME.Pathfinding.Roads;

namespace CAIME.Tests.Helpers
{
    /// <summary>
    /// Reproduces the slice of the exporter pipeline that the tile-group stage depends on, so a
    /// differential test can build the same tile groups (and per-hex tile group indices) the
    /// exporter feeds into downstream generators (bridges → HLCI areas → tile groups).
    /// </summary>
    internal static class PathfindingPipeline
    {
        /// <summary>
        /// Computes the per-hex tile group index (scan order, R * width + Q) for a prepared map,
        /// matching <c>PathfindingExporter.BuildTileGroupIndices</c>.
        /// </summary>
        public static int[] ComputeTileGroupIndices(MapHexFile map)
        {
            RunTileGroups(map, out _, out var edgesData);
            return TileGroupsGenerator.ExtractTileGroupIndices(edgesData);
        }

        /// <summary>
        /// Builds the TILE_GROUPS_ARRAY (the deduplicated (hlci, region) table, in the exporter's
        /// discovery order) for a prepared map, matching what <c>PathfindingExporter</c> serializes.
        /// </summary>
        public static List<TileGroupItem> ComputeTileGroups(MapHexFile map)
        {
            RunTileGroups(map, out var tileGroups, out _);
            return tileGroups;
        }

        /// <summary>
        /// Runs <see cref="MovementCostsGenerator"/> over a prepared map, returning the raw
        /// 8-bytes-per-hex edges array (cost index + navigability live in edge bytes 0..5) and the
        /// deduplicated move-cost table, in the generator's discovery order. This matches the
        /// MOVEMENT_COSTS_ARRAY and the edge fields of HEX_CELL_DATA the exporter serializes.
        /// </summary>
        public static (byte[] edges, List<ushort> moveCosts) ComputeMovementCosts(MapHexFile map, DatabaseViewModel db)
        {
            var generator = new MovementCostsGenerator(map, db, DebugDir);

            var edges = new byte[map.Capacity * 8];
            generator.Generate(ref edges);

            return (edges, generator.GetMoveCosts());
        }

        /// <summary>
        /// Runs <see cref="RoadsGenerator"/> over a prepared map, returning the road segments in the
        /// generator's discovery order. This matches the ROAD_SEGMENTS section the exporter serializes.
        /// Roads depend only on the road/region/town-sprawl data already on the prepared map, so unlike
        /// the heuristic cache they need no database.
        /// </summary>
        public static RoadSegment[] ComputeRoads(MapHexFile map)
        {
            var roadsGenerator = new RoadsGenerator(map, debugPath: null);
            roadsGenerator.Generate(out var roadSegments);
            return roadSegments;
        }

        /// <summary>
        /// Bundles the three downstream sections that share the tile-group / borders / bridges pipeline,
        /// so a differential test can diff each against the same real .ppd in one pass.
        /// </summary>
        public sealed class HeuristicResult
        {
            /// <summary>HEURISTIC_CACHE: flattened (tile-group x tile-group) best-case distance matrix.</summary>
            public uint[] HeuristicCache;

            /// <summary>PASSABLE_BORDER_HEXES source: the all-borders hex list, ordered by tile group.</summary>
            public BordersData Borders;

            /// <summary>CUMULATIVE_BORDER_HEXES: running per-tile-group offset into the border-hex list.</summary>
            public int[] CumulativeBorderHexes;
        }

        /// <summary>
        /// Bundles the two HLCI-derived sections that share the bridges → HLCI pipeline: the beach
        /// connections (grouped per HLCI land/sea pair) and the flat HLCI connection list.
        /// </summary>
        public sealed class BeachesResult
        {
            /// <summary>BEACHES_ARRAY source: per (enter, leave) HLCI pair, the land-to-sea / sea-to-land beach hexes.</summary>
            public List<BeachesContainer> Beaches;

            /// <summary>HLCI_CONNECTIONS source: the unique land/sea HLCI area pairs sharing a coastline.</summary>
            public List<HLCIPair> HlciPairs;
        }

        /// <summary>
        /// Replays the exporter pipeline up to and including the beaches and HLCI connections, in the
        /// order <c>PathfindingExporter.ExportSequential</c> runs them (bridges → HLCI → beaches). Both
        /// sections are driven by the same HLCI area/pair data, so they are produced together.
        /// </summary>
        public static BeachesResult ComputeBeachesAndConnections(MapHexFile map)
        {
            var bridgesGenerator = new BridgesGenerator(map, debugPath: null, bWriteDebug: false);
            bridgesGenerator.Generate(out var bridgesData);

            var hlciGenerator = new HLCIGenerator(map, debugPath: null);
            hlciGenerator.Generate(out var hlciAreas, in bridgesData);
            var hlciPairs = hlciGenerator.HLCIPairs;

            var beachesGenerator = new BeachesGenerator(map, debugPath: null);
            beachesGenerator.Generate(out var beaches, in hlciAreas, in hlciPairs);

            return new BeachesResult
            {
                Beaches   = beaches,
                HlciPairs = hlciPairs,
            };
        }

        /// <summary>
        /// Replays the exporter pipeline up to and including the heuristic cache, passable border hexes
        /// and cumulative border hexes, in the exact order <c>PathfindingExporter.ExportSequential</c> runs
        /// the generators (bridges → HLCI → tile groups → borders → movement costs → heuristic cache →
        /// cumulative borders). The shared <c>edgesData</c> buffer is threaded through the same way, so the
        /// heuristic and cumulative-border generators see the fully populated edge bytes the exporter feeds them.
        /// </summary>
        public static HeuristicResult ComputeHeuristicAndBorders(MapHexFile map, DatabaseViewModel db)
        {
            var bridgesGenerator = new BridgesGenerator(map, debugPath: null, bWriteDebug: false);
            bridgesGenerator.Generate(out var bridgesData);

            var hlciGenerator = new HLCIGenerator(map, debugPath: null);
            hlciGenerator.Generate(out var hlciAreas, in bridgesData);
            int areasCount   = hlciGenerator.GetAreasCount();
            int totalRegions = map.LandRegions.Count + map.SeaRegions.Count;

            var edgesData = new byte[map.Capacity * 8];
            var tileGroupsGenerator = new TileGroupsGenerator(map, debugPath: null);
            tileGroupsGenerator.Generate(ref edgesData, out var tileGroups, in hlciAreas, in totalRegions, in areasCount);

            var tileGroupIndices = TileGroupsGenerator.ExtractTileGroupIndices(edgesData);

            var bordersGenerator = new BordersGenerator(map, debugPath: null, bWriteDebug: false);
            bordersGenerator.Generate(out _, out var bordersData, map.LandRegions.Count, tileGroupIndices);

            // Movement costs run before the heuristic and write the edge-cost bytes into edgesData; the
            // heuristic only reads the tile-group bytes from it, but match the exporter's ordering exactly.
            var movementGenerator = new MovementCostsGenerator(map, db, DebugDir);
            movementGenerator.Generate(ref edgesData);
            var moveCosts = movementGenerator.GetMoveCosts();

            var heuristicGenerator = new HeuristicCacheGenerator(map, DebugDir);
            heuristicGenerator.Generate(db, out var heuristicCache, edgesData, in moveCosts, in tileGroups, bridgesData);

            var cumulativeGenerator = new BordersCountPerTileGroupGenerator(map, debugPath: null);
            cumulativeGenerator.Generate(out var cumulativeBorderHexes, in edgesData, in tileGroups, in bordersData);

            return new HeuristicResult
            {
                HeuristicCache        = heuristicCache,
                Borders               = bordersData,
                CumulativeBorderHexes = cumulativeBorderHexes,
            };
        }

        // MovementCostsGenerator unconditionally dumps debug .raw files on DEBUG builds, so give it
        // a throwaway directory rather than polluting the working directory.
        private static readonly string DebugDir = CreateDebugDir();

        private static string CreateDebugDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "caime_pipeline_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir + Path.DirectorySeparatorChar;
        }

        private static void RunTileGroups(MapHexFile map, out List<TileGroupItem> tileGroups, out byte[] edgesData)
        {
            var bridgesGenerator = new BridgesGenerator(map, debugPath: null, bWriteDebug: false);
            bridgesGenerator.Generate(out var bridgesData);

            var hlciGenerator = new HLCIGenerator(map, debugPath: null);
            hlciGenerator.Generate(out var hlciAreas, in bridgesData);
            int areasCount   = hlciGenerator.GetAreasCount();
            int totalRegions = map.LandRegions.Count + map.SeaRegions.Count;

            edgesData = new byte[map.Capacity * 8];
            var tileGroupsGenerator = new TileGroupsGenerator(map, debugPath: null);
            tileGroupsGenerator.Generate(ref edgesData, out tileGroups, in hlciAreas, in totalRegions, in areasCount);
        }
    }
}
