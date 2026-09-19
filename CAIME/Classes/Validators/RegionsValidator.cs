using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CAIME.Pathfinding;

namespace CAIME.Validators
{
    internal static class RegionsValidator
    {
        public static bool Validate(Project project)
        {
            var mapHexFile       = project.MapHexFile;
            var hexData          = mapHexFile.HexData;
            var width            = (int)mapHexFile.MapWidth;
            var height           = (int)mapHexFile.MapHeight;
            var landRegionsCount = mapHexFile.LandRegions.Count;
            var regionsCount     = landRegionsCount + mapHexFile.SeaRegions.Count;
            var capacity         = (int)mapHexFile.Capacity;
            var failed           = 0;

            //Database / map.hex region parity. The two lists don't need to be in the same order -
            //only the set of region keys needs to match.
            if (regionsCount != project.Database.CachedRegions.Count)
            {
                LoggerViewModel.Log($"Regions validation: There is a mismatch between database regions (count = {project.Database.CachedRegions.Count}) and map.hex regions (count = {regionsCount}).", LogLevel.Error);
                Interlocked.Exchange(ref failed, 1);
            }

            var hexRegionKeys = new HashSet<string>();
            for (int i = 0; i < regionsCount; ++i)
            {
                hexRegionKeys.Add(mapHexFile.GetRegionName(i));
            }

            var dbRegionKeys = new HashSet<string>();
            foreach (var db_region in project.Database.CachedRegions)
            {
                dbRegionKeys.Add(db_region.Key);
            }

            foreach (var db_region in project.Database.CachedRegions)
            {
                if (hexRegionKeys.Contains(db_region.Key) == false)
                {
                    LoggerViewModel.Log($"Regions validation: Database region '{db_region.Key}' has no matching region in map.hex.", LogLevel.Error);
                    Interlocked.Exchange(ref failed, 1);
                }
            }

            for (int i = 0; i < regionsCount; ++i)
            {
                var hex_region_key = mapHexFile.GetRegionName(i);
                if (dbRegionKeys.Contains(hex_region_key) == false)
                {
                    LoggerViewModel.Log($"Regions validation: Map.hex region '{hex_region_key}' has no matching region in the database.", LogLevel.Error);
                    Interlocked.Exchange(ref failed, 1);
                }
            }

            var hexCountPerRegion    = new int[regionsCount];
            var seaRegionAdjacencies = new HashSet<int>[regionsCount];
            var hasLandNeighbor      = new bool[landRegionsCount];
            var adjacencySync        = new object();

            //Per-hex pass (parallel): no-region/isolated checks plus the per-region reductions.
            Parallel.For(0, capacity,
                () => new HashSet<int>[regionsCount],
                (hexIndex, loopState, localAdjacency) =>
                {
                    var hex = hexData[hexIndex];

                    //No region set
                    if (hex.RegionId == Hex.INVALID_REGION_INDEX)
                    {
                        LoggerViewModel.Log($"Regions validation: Hex({hex.Q}, {hex.R}) has no region set.", LogLevel.Warning);
                        Interlocked.Exchange(ref failed, 1);
                    }
                    else
                    {
                        Interlocked.Increment(ref hexCountPerRegion[hex.RegionId]);
                    }

                    //Stray region hex + record sea region adjacencies to land regions
                    var isolated = true;
                    for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                    {
                        var nbrIndex = NeighbourIndex(hex, dir, width, height);
                        if (nbrIndex == -1)
                        {
                            continue;
                        }

                        var nbr = hexData[nbrIndex];

                        if (nbr.RegionId == hex.RegionId)
                        {
                            isolated = false;
                        }

                        //Land region adjacency (beach or port slot)
                        if (hex.RegionId != Hex.INVALID_REGION_INDEX && nbr.IsLand && (nbr.IsBeach || nbr.TownSlotIndex == Hex.PORT_SLOT_INDEX))
                        {
                            var set = localAdjacency[hex.RegionId] ?? (localAdjacency[hex.RegionId] = new HashSet<int>());
                            set.Add(nbr.RegionId);
                        }

                        //Direct land-region-to-land-region adjacency, regardless of passability. Used to
                        //tell a genuine island (no land region borders it at all) apart from a region whose
                        //border to a neighbouring land region happens to be entirely impassable.
                        if (hex.RegionId >= 0 && hex.RegionId < landRegionsCount &&
                            nbr.RegionId >= 0 && nbr.RegionId < landRegionsCount &&
                            nbr.RegionId != hex.RegionId)
                        {
                            hasLandNeighbor[hex.RegionId] = true;
                        }
                    }

                    if (isolated)
                    {
                        LoggerViewModel.Log($"Regions validation: Hex({hex.Q}, {hex.R}) is an isolated region hex (allowed, but maybe a mistake).", LogLevel.Info);
                    }

                    return localAdjacency;
                },
                localAdjacency =>
                {
                    lock (adjacencySync)
                    {
                        for (int regionId = 0; regionId < regionsCount; ++regionId)
                        {
                            if (localAdjacency[regionId] == null)
                            {
                                continue;
                            }

                            var shared = seaRegionAdjacencies[regionId] ?? (seaRegionAdjacencies[regionId] = new HashSet<int>());
                            shared.UnionWith(localAdjacency[regionId]);
                        }
                    }
                });

            //More than 5 land region adjacencies = starting to get AI slow down
            //More than 9 = bad bad bad
            for (int regionId = 0; regionId < regionsCount; ++regionId)
            {
                var adjacent = seaRegionAdjacencies[regionId];
                if (adjacent == null)
                {
                    continue;
                }

                if (adjacent.Count > 9)
                {
                    LoggerViewModel.Log($"Regions validation: {mapHexFile.GetRegionName(regionId)} has {adjacent.Count} adjacent land regions, more than 9 means slowdown due to AI calculations, split it up!", LogLevel.Warning);
                    Interlocked.Exchange(ref failed, 1);
                }
                else if (adjacent.Count > 5)
                {
                    LoggerViewModel.Log($"Regions validation: {mapHexFile.GetRegionName(regionId)} has {adjacent.Count} adjacent land regions, 5 is recommended, consider splitting the region in two.", LogLevel.Info);
                }
            }

            //Empty regions
            for (int regionIndex = 0; regionIndex < regionsCount; ++regionIndex)
            {
                if (hexCountPerRegion[regionIndex] == 0)
                {
                    LoggerViewModel.Log($"Regions validation: Region {mapHexFile.GetRegionName(regionIndex)} does not have any hexes assigned. This may lead to startpos processing issues.", LogLevel.Warning);
                }
            }

            //Region contiguity: a land region split into disconnected blobs is a frequent startpos bug.
            DetectNonContiguousRegions(mapHexFile, hexData, width, height, capacity, landRegionsCount, regionsCount, ref failed);

            //Check for regions that have no passable region edges (use same logic as exporters)
            try
            {
                var bordersGenerator = new BordersGenerator(mapHexFile, null, false);
                Border[] passableLandBorders;
                bordersGenerator.GenerateLandBorders(out passableLandBorders, landRegionsCount);

                var regionEdgesGenerator = new RegionEdgesGenerator(mapHexFile, null);
                RegionPassableHexes[] regionEdges;
                regionEdgesGenerator.Generate(out regionEdges, in passableLandBorders);

                for (int regionEdgeIndex = 0; regionEdgeIndex < regionEdges.Length; ++regionEdgeIndex)
                {
                    //A region with no land neighbours at all (an island) will always have 0 passable
                    //edge hexes - that's expected, not a mistake. Only warn when the region does border
                    //another land region but the whole shared border happens to be impassable.
                    if (regionEdges[regionEdgeIndex].Hexes.Count == 0 && hasLandNeighbor[regionEdgeIndex])
                    {
                        LoggerViewModel.Log($"Regions validation: Region {mapHexFile.GetRegionName(regionEdgeIndex)} has 0 passable region edge hexes. This may be valid and intentional but double check it.", LogLevel.Info);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"Regions validation: Region validation threw an exception: {ex.Message}", LogLevel.Error);
                Interlocked.Exchange(ref failed, 1);
            }

            return failed == 0;
        }

        /// <summary>
        /// Allocation-free neighbour index lookup. Mirrors <see cref="MapHexFile.GetNeighbour"/> but avoids
        /// allocating a Hex per call, which otherwise dominates the per-hex loops and flood-fill on large maps.
        /// </summary>
        private static int NeighbourIndex(Hex hex, ushort dir, int width, int height)
        {
            var offset = Hex.Directions_FlatTop[hex.Q & 1, dir];
            int q = hex.Q + offset.Q;
            int r = hex.R + offset.R;

            if (q < 0 || r < 0 || q >= width || r >= height)
            {
                return -1;
            }

            return r * width + q;
        }

        /// <summary>
        /// Flood-fills each region and flags land regions whose hexes form more than one disconnected group.
        /// </summary>
        private static void DetectNonContiguousRegions(MapHexFile mapHexFile, Hex[] hexData, int width, int height, int capacity, int landRegionsCount, int regionsCount, ref int failed)
        {
            var componentCount   = new int[regionsCount];
            var firstHexOfRegion = new int[regionsCount];
            for (int i = 0; i < regionsCount; ++i)
            {
                firstHexOfRegion[i] = -1;
            }

            var visited = new bool[capacity];
            var queue   = new Queue<int>();

            for (int startIndex = 0; startIndex < capacity; ++startIndex)
            {
                if (visited[startIndex])
                {
                    continue;
                }

                var region = hexData[startIndex].RegionId;
                if (region == Hex.INVALID_REGION_INDEX)
                {
                    visited[startIndex] = true;
                    continue;
                }

                ++componentCount[region];
                if (firstHexOfRegion[region] == -1)
                {
                    firstHexOfRegion[region] = startIndex;
                }

                queue.Clear();
                queue.Enqueue(startIndex);
                visited[startIndex] = true;

                while (queue.Count > 0)
                {
                    var hexIndex = queue.Dequeue();
                    var hex      = hexData[hexIndex];

                    for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                    {
                        var nbrIndex = NeighbourIndex(hex, dir, width, height);
                        if (nbrIndex == -1 || visited[nbrIndex])
                        {
                            continue;
                        }

                        if (hexData[nbrIndex].RegionId == region)
                        {
                            visited[nbrIndex] = true;
                            queue.Enqueue(nbrIndex);
                        }
                    }
                }
            }

            //Only warn for land regions; sea regions are legitimately split (archipelagos, opposite coasts).
            for (int regionId = 0; regionId < landRegionsCount; ++regionId)
            {
                if (componentCount[regionId] > 1)
                {
                    LoggerViewModel.Log($"Regions validation: Region {mapHexFile.GetRegionName(regionId)} is split into {componentCount[regionId]} disconnected areas. This may lead to startpos processing issues.", LogLevel.Warning);
                    Interlocked.Exchange(ref failed, 1);
                }
            }
        }
    }
}
