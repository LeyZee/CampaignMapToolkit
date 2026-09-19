using System.Collections.Generic;
using System.Linq;

namespace CAIME.Validators
{
    internal static class SprawlValidator
    {
        private const int OUTLINE_DEPTH = 2;

        public static bool Validate(Project project)
        {
            var mapHexFile = project.MapHexFile;
            var failed      = false;

            for (int hexIndex = 0; hexIndex < mapHexFile.Capacity; ++hexIndex)
            {
                var hex = mapHexFile.HexData[hexIndex];
                if (!hex.IsTownSprawl)
                {
                    continue;
                }

                if (hex.IsImpassable)
                {
                    LoggerViewModel.Log($"Town Sprawl validation: Hex({hex.Q}, {hex.R}) is sprawl but is also impassable.", LogLevel.Error);
                    failed = true;
                }
            }

            var blobs             = FindBlobs(mapHexFile);
            var hazardComponentOf = BuildHazardComponents(mapHexFile);

            foreach (var blob in blobs)
            {
                if (!ValidateRegionContainment(mapHexFile, blob))
                {
                    failed = true;
                }

                if (!HasTownSlot(mapHexFile, blob))
                {
                    var repHex = mapHexFile.HexData[blob[0]];
                    LoggerViewModel.Log($"Town Sprawl validation: Sprawl blob near Hex({repHex.Q}, {repHex.R}) contains no town slot hex (allowed in some games, but maybe a mistake).", LogLevel.Warning);
                }

                if (!ValidateHazardOutline(mapHexFile, blob, hazardComponentOf))
                {
                    failed = true;
                }
            }

            if (!ValidateOneBlobPerRegion(mapHexFile, blobs))
            {
                failed = true;
            }

            return !failed;
        }

        private static bool IsHazard(Hex hex) => hex.IsImpassable || hex.IsRiver || hex.IsCoast;

        private static List<List<int>> FindBlobs(MapHexFile mapHexFile)
        {
            var capacity = (int)mapHexFile.Capacity;
            var visited  = new bool[capacity];
            var blobs    = new List<List<int>>();
            var queue    = new Queue<int>();

            for (int startIndex = 0; startIndex < capacity; ++startIndex)
            {
                if (visited[startIndex] || !mapHexFile.HexData[startIndex].IsTownSprawl)
                {
                    continue;
                }

                var blob = new List<int>();
                queue.Clear();
                queue.Enqueue(startIndex);
                visited[startIndex] = true;

                while (queue.Count > 0)
                {
                    var hexIndex = queue.Dequeue();
                    blob.Add(hexIndex);

                    var hex = mapHexFile.HexData[hexIndex];
                    for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                    {
                        var nbrIndex = mapHexFile.GetNeighbourIndex(hex, dir);
                        if (nbrIndex == -1 || visited[nbrIndex] || !mapHexFile.HexData[nbrIndex].IsTownSprawl)
                        {
                            continue;
                        }

                        visited[nbrIndex] = true;
                        queue.Enqueue(nbrIndex);
                    }
                }

                blobs.Add(blob);
            }

            return blobs;
        }

        private static bool HasTownSlot(MapHexFile mapHexFile, List<int> blob)
        {
            return blob.Any(hexIndex => mapHexFile.HexData[hexIndex].TownSlotIndex != Hex.INVALID_SLOT_INDEX);
        }

        // The blob's "owning" region is decided from its land hexes only: port sprawl is expected
        // to spill onto an adjacent sea region's hexes, and those shouldn't skew the vote or get flagged.
        private static int MainRegion(MapHexFile mapHexFile, List<int> blob)
        {
            var regionCounts = blob
                .Select(hexIndex => mapHexFile.HexData[hexIndex])
                .Where(hex => hex.IsLand)
                .Select(hex => hex.RegionId)
                .GroupBy(regionId => regionId)
                .OrderByDescending(group => group.Count())
                .ToList();

            if (regionCounts.Count > 0)
            {
                return regionCounts.First().Key;
            }

            return blob
                .Select(hexIndex => mapHexFile.HexData[hexIndex].RegionId)
                .GroupBy(regionId => regionId)
                .OrderByDescending(group => group.Count())
                .First().Key;
        }

        private static bool ValidateRegionContainment(MapHexFile mapHexFile, List<int> blob)
        {
            var mainRegion = MainRegion(mapHexFile, blob);
            var ok         = true;

            foreach (var hexIndex in blob)
            {
                var hex = mapHexFile.HexData[hexIndex];

                //Sea hexes are exempt: port sprawl is expected to reach over an adjacent sea region.
                if (hex.IsLand && hex.RegionId != mainRegion)
                {
                    LoggerViewModel.Log($"Town Sprawl validation: Hex({hex.Q}, {hex.R}) is part of a sprawl blob but belongs to a different region than the rest of the blob.", LogLevel.Error);
                    ok = false;
                }
            }

            return ok;
        }

        private static bool ValidateOneBlobPerRegion(MapHexFile mapHexFile, List<List<int>> blobs)
        {
            var ok = true;

            foreach (var regionGroup in blobs.GroupBy(blob => MainRegion(mapHexFile, blob)))
            {
                var regionBlobs = regionGroup.ToList();
                if (regionBlobs.Count <= 1)
                {
                    continue;
                }

                foreach (var blob in regionBlobs)
                {
                    var repHex = mapHexFile.HexData[blob[0]];
                    LoggerViewModel.Log($"Town Sprawl validation: Region {regionGroup.Key} has its sprawl split across {regionBlobs.Count} separate blobs (e.g. near Hex({repHex.Q}, {repHex.R})); it should be a single blob.", LogLevel.Error);
                }

                ok = false;
            }

            return ok;
        }

        // Connected components across every impassable/river/beach/cliff hex on the map, computed once
        // and reused for every blob so "does this hazard patch touch the blob" is a component lookup.
        private static Dictionary<int, int> BuildHazardComponents(MapHexFile mapHexFile)
        {
            var capacity        = (int)mapHexFile.Capacity;
            var componentOf     = new Dictionary<int, int>();
            var visited         = new bool[capacity];
            var queue           = new Queue<int>();
            var nextComponentId = 0;

            for (int startIndex = 0; startIndex < capacity; ++startIndex)
            {
                if (visited[startIndex] || !IsHazard(mapHexFile.HexData[startIndex]))
                {
                    continue;
                }

                var componentId = nextComponentId++;
                queue.Clear();
                queue.Enqueue(startIndex);
                visited[startIndex] = true;

                while (queue.Count > 0)
                {
                    var hexIndex = queue.Dequeue();
                    componentOf[hexIndex] = componentId;

                    var hex = mapHexFile.HexData[hexIndex];
                    for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                    {
                        var nbrIndex = mapHexFile.GetNeighbourIndex(hex, dir);
                        if (nbrIndex == -1 || visited[nbrIndex] || !IsHazard(mapHexFile.HexData[nbrIndex]))
                        {
                            continue;
                        }

                        visited[nbrIndex] = true;
                        queue.Enqueue(nbrIndex);
                    }
                }
            }

            return componentOf;
        }

        // Every hex within OUTLINE_DEPTH steps of the blob, excluding the blob itself.
        private static HashSet<int> BuildOutline(MapHexFile mapHexFile, HashSet<int> blobSet)
        {
            var outline  = new HashSet<int>();
            var visited  = new HashSet<int>(blobSet);
            var frontier = new List<int>(blobSet);

            for (int ring = 0; ring < OUTLINE_DEPTH; ++ring)
            {
                var nextFrontier = new List<int>();
                foreach (var hexIndex in frontier)
                {
                    var hex = mapHexFile.HexData[hexIndex];
                    for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                    {
                        var nbrIndex = mapHexFile.GetNeighbourIndex(hex, dir);
                        if (nbrIndex == -1 || !visited.Add(nbrIndex))
                        {
                            continue;
                        }

                        outline.Add(nbrIndex);
                        nextFrontier.Add(nbrIndex);
                    }
                }

                frontier = nextFrontier;
            }

            return outline;
        }

        private static bool ValidateHazardOutline(MapHexFile mapHexFile, List<int> blob, Dictionary<int, int> hazardComponentOf)
        {
            var blobSet = new HashSet<int>(blob);
            var outline = BuildOutline(mapHexFile, blobSet);

            //Hazard components that directly border the blob (ring 1 only)
            var touchingComponents = new HashSet<int>();
            foreach (var hexIndex in blob)
            {
                var hex = mapHexFile.HexData[hexIndex];
                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    var nbrIndex = mapHexFile.GetNeighbourIndex(hex, dir);
                    if (nbrIndex != -1 && !blobSet.Contains(nbrIndex) && hazardComponentOf.TryGetValue(nbrIndex, out var componentId))
                    {
                        touchingComponents.Add(componentId);
                    }
                }
            }

            //Hazard components present anywhere in the 2-hex outline
            var outlineComponents = new Dictionary<int, int>();
            foreach (var hexIndex in outline)
            {
                if (hazardComponentOf.TryGetValue(hexIndex, out var componentId) && !outlineComponents.ContainsKey(componentId))
                {
                    outlineComponents[componentId] = hexIndex;
                }
            }

            var ok         = true;
            var blobRepHex = mapHexFile.HexData[blob[0]];

            //Any hazard patch that comes within 2 hexes of the blob without ever touching it is an error,
            //regardless of how many patches touch the blob.
            foreach (var kvp in outlineComponents)
            {
                if (!touchingComponents.Contains(kvp.Key))
                {
                    var hazardHex = mapHexFile.HexData[kvp.Value];
                    LoggerViewModel.Log($"Town Sprawl validation: Impassable/beach/river/cliff terrain near Hex({hazardHex.Q}, {hazardHex.R}) comes within 2 hexes of the sprawl blob near Hex({blobRepHex.Q}, {blobRepHex.R}) without touching it directly.", LogLevel.Error);
                    ok = false;
                }
            }

            //At most one separate hazard patch is allowed to border the blob; two or more pinches it.
            if (touchingComponents.Count > 1)
            {
                LoggerViewModel.Log($"Town Sprawl validation: Sprawl blob near Hex({blobRepHex.Q}, {blobRepHex.R}) borders {touchingComponents.Count} separate impassable/beach/river/cliff areas; only one is allowed, to avoid a bottleneck.", LogLevel.Error);
                ok = false;
            }

            return ok;
        }
    }
}
