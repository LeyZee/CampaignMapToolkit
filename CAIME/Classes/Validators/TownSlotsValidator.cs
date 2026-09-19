using System.Collections.Generic;

namespace CAIME.Validators
{
    internal static class TownSlotsValidator
    {
        public static bool Validate(Project project)
        {
            var mapHexFile       = project.MapHexFile;
            var capacity         = (int)mapHexFile.Capacity;
            var landRegionsCount = mapHexFile.LandRegions.Count;
            var failed           = false;

            //Some games allow smaller / non-uniform slots, so they are exempt from the size checks.
            var slotSizeExempt = mapHexFile.GameName == "warhammer3"
                              || mapHexFile.GameName == "warhammer2"
                              || mapHexFile.GameName == "warhammer"
                              || mapHexFile.GameName == "three_kingdoms"
                              || mapHexFile.GameName == "troy";

            var slotCounts    = new Dictionary<int, Dictionary<int, int>>();
            var validSlots    = new Dictionary<int, Dictionary<int, bool>>();
            var validPortSlot = new Dictionary<int, bool>();
            var slotHexes     = new Dictionary<int, Dictionary<int, List<int>>>();

            //Per land-region facts used by the region-level checks below.
            var regionExists          = new bool[landRegionsCount];
            var regionHasPassable     = new bool[landRegionsCount];
            var regionHasSeaAdjacency = new bool[landRegionsCount];
            var regionHasPortSlot     = new bool[landRegionsCount];

            for (int hexIndex = 0; hexIndex < capacity; ++hexIndex)
            {
                var hex = mapHexFile.HexData[hexIndex];

                //Track land-region facts (a coast hex implies the region borders the sea)
                if (hex.RegionId >= 0 && hex.RegionId < landRegionsCount)
                {
                    regionExists[hex.RegionId] = true;
                    if (hex.IsPassable)
                    {
                        regionHasPassable[hex.RegionId] = true;
                    }
                    if (hex.IsCoast)
                    {
                        regionHasSeaAdjacency[hex.RegionId] = true;
                    }
                }

                //Town slot index out of range
                if (hex.TownSlotIndex != Hex.INVALID_SLOT_INDEX && hex.TownSlotIndex >= Hex.MAX_SLOTS_COUNT)
                {
                    LoggerViewModel.Log($"Town Slot validation: Hex({hex.Q}, {hex.R}) has invalid town slot index {hex.TownSlotIndex}. Valid range is 0-{Hex.MAX_SLOTS_COUNT - 1}.", LogLevel.Error);
                    failed = true;
                }

                //Town slot outside town sprawl
                if (!hex.IsTownSprawl && (hex.TownSlotIndex != Hex.INVALID_SLOT_INDEX))
                {
                    LoggerViewModel.Log($"Town Slot validation: Hex({hex.Q}, {hex.R}) Town Slot outside Town Sprawl (use Town Sprawl auto-generate to fix!).", LogLevel.Warning);
                    failed = true;
                }

                //Town slot impassable
                if (hex.TownSlotIndex != Hex.INVALID_SLOT_INDEX && hex.IsImpassable)
                {
                    LoggerViewModel.Log($"Town Slot validation: Hex({hex.Q}, {hex.R}) Town Slot is impassable (allowed, but maybe a mistake).", LogLevel.Info);
                }

                //Non-port town slot in sea
                if (hex.TownSlotIndex != Hex.PORT_SLOT_INDEX && hex.TownSlotIndex != Hex.INVALID_SLOT_INDEX && hex.IsSea)
                {
                    LoggerViewModel.Log($"Town Slot validation: Hex({hex.Q}, {hex.R}) Non-port Town Slot is on sea hex.", LogLevel.Warning);
                    failed = true;
                }

                //Sprawl of one regionID next to sprawl of another regionID (land only; sprawl may spill into a sea region for ports)
                if (hex.IsTownSprawl && hex.IsLand)
                {
                    var overlapping = false;
                    for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                    {
                        var nbrIndex = mapHexFile.GetNeighbourIndex(hex, dir);
                        if (nbrIndex == -1)
                        {
                            continue;
                        }

                        var nbr = mapHexFile.HexData[nbrIndex];
                        if (nbr.IsTownSprawl && nbr.IsLand && nbr.RegionId != hex.RegionId)
                        {
                            overlapping = true;
                        }
                    }
                    if (overlapping)
                    {
                        LoggerViewModel.Log($"Town Slot validation: Hex({hex.Q}, {hex.R}) Town Sprawl overlaps region border.", LogLevel.Warning);
                        failed = true;
                    }
                }

                //Per-slot bookkeeping for land slot hexes:
                // - count slot size within each region
                // - record the hexes of each slot (for contiguity / duplicate-cluster checks)
                // - check that the slot has a hex surrounded by 6 same-slot land hexes (and a sea hex for ports)
                if (hex.TownSlotIndex != Hex.INVALID_SLOT_INDEX && hex.IsLand)
                {
                    if (validSlots.ContainsKey(hex.RegionId) == false)
                    {
                        slotCounts.Add(hex.RegionId, new Dictionary<int, int>());
                        validSlots.Add(hex.RegionId, new Dictionary<int, bool>());
                        slotHexes.Add(hex.RegionId, new Dictionary<int, List<int>>());
                    }

                    if (validSlots[hex.RegionId].ContainsKey(hex.TownSlotIndex) == false)
                    {
                        slotCounts[hex.RegionId].Add(hex.TownSlotIndex, 0);
                        validSlots[hex.RegionId].Add(hex.TownSlotIndex, false);
                        slotHexes[hex.RegionId].Add(hex.TownSlotIndex, new List<int>());
                    }

                    slotCounts[hex.RegionId][hex.TownSlotIndex] += 1;
                    slotHexes[hex.RegionId][hex.TownSlotIndex].Add(hexIndex);

                    if (hex.TownSlotIndex == Hex.PORT_SLOT_INDEX)
                    {
                        if (hex.RegionId < landRegionsCount)
                        {
                            regionHasPortSlot[hex.RegionId] = true;
                        }
                        if (validPortSlot.ContainsKey(hex.RegionId) == false)
                        {
                            validPortSlot.Add(hex.RegionId, false);
                        }
                    }

                    var isValid = true;
                    for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                    {
                        var nbrIndex = mapHexFile.GetNeighbourIndex(hex, dir);
                        if (nbrIndex == -1)
                        {
                            //A slot hex touching the map edge cannot be fully surrounded by same-slot hexes
                            isValid = false;
                            continue;
                        }

                        var nbr = mapHexFile.HexData[nbrIndex];

                        if (hex.TownSlotIndex == nbr.TownSlotIndex)
                        {
                            if (!nbr.IsLand || nbr.RegionId != hex.RegionId)
                            {
                                if (nbr.IsSea && hex.TownSlotIndex == Hex.PORT_SLOT_INDEX)
                                {
                                    //extra logic for ports, to check for a hex in the sea
                                    validPortSlot[hex.RegionId] = true;
                                }
                                else
                                {
                                    isValid = false;
                                }
                            }
                        }
                        else
                        {
                            isValid = false;
                        }
                    }

                    validSlots[hex.RegionId][hex.TownSlotIndex] |= isValid;
                }
            }

            //Main slot needs to be either 19 for an in-land settlement, or 16 for a port (WH only).
            //I think only relevant for WH.
            bool checkMainSlotSize = mapHexFile.GameName == "warhammer3"
                                  || mapHexFile.GameName == "warhammer2"
                                  || mapHexFile.GameName == "warhammer";

            if (checkMainSlotSize)
            {
                foreach (int regionId in slotCounts.Keys)
                {
                    //A region can hold slot hexes without a main slot at all - only a port, say, or
                    //slots 2..11 on land while the main slot hexes are sea and were skipped above.
                    if (!slotCounts[regionId].TryGetValue(Hex.MAIN_SLOT_INDEX, out int mainSlotCount))
                    {
                        continue;
                    }

                    if (mainSlotCount != 19 && mainSlotCount != 16)
                    {
                        LoggerViewModel.Log($"Town Slot validation: {mapHexFile.GetRegionName(regionId)} Town Slot {Hex.MAIN_SLOT_INDEX} (the main slot) is the wrong size: {mainSlotCount} hexes, expected 19 (inland) or 16 (port).", LogLevel.Warning);
                        failed = true;
                    }
                }
            }

            foreach (int regionId in validSlots.Keys)
            {
                foreach (int slotIndex in validSlots[regionId].Keys)
                {
                    if (validSlots[regionId][slotIndex] == false)
                    {
                        //Not really relevant for WH3, possibly others
                        if (!slotSizeExempt)
                        {
                            LoggerViewModel.Log($"Town Slot validation: {mapHexFile.GetRegionName(regionId)} Town Slot {slotIndex} needs at least one land hex with 6 adjacent same-slot hexes.", LogLevel.Warning);
                            failed = true;
                        }
                    }
                }
            }

            foreach (int regionId in validPortSlot.Keys)
            {
                if (validPortSlot[regionId] == false)
                {
                    LoggerViewModel.Log($"Town Slot validation: {mapHexFile.GetRegionName(regionId)} Port Slot needs at least one hex in a sea region.", LogLevel.Warning);
                    failed = true;
                }
            }

            //Slot size (minimum 7 = a hex plus its 6 neighbours) and duplicate / disconnected slot clusters.
            foreach (int regionId in slotHexes.Keys)
            {
                foreach (var slot in slotHexes[regionId])
                {
                    var slotIndex = slot.Key;
                    var hexes     = slot.Value;

                    //Duplicate town slot: the same slot index appearing as multiple disconnected clusters in one
                    //region (e.g. two separate settlements both claiming the main slot). Ports are skipped as their
                    //land hexes can legitimately be split by the water.
                    if (slotIndex != Hex.PORT_SLOT_INDEX)
                    {
                        var clusters = CountConnectedGroups(mapHexFile, hexes);
                        if (clusters > 1)
                        {
                            LoggerViewModel.Log($"Town Slot validation: {mapHexFile.GetRegionName(regionId)} Town Slot {slotIndex} is split into {clusters} disconnected clusters (duplicate town slot).", LogLevel.Warning);
                            failed = true;
                        }

                        //Minimum slot size
                        if (!slotSizeExempt && hexes.Count < 7)
                        {
                            LoggerViewModel.Log($"Town Slot validation: {mapHexFile.GetRegionName(regionId)} Town Slot {slotIndex} has only {hexes.Count} hex(es); a town slot needs at least 7.", LogLevel.Warning);
                            failed = true;
                        }
                    }
                }
            }

            //Port slot present but the region is not adjacent to any sea.
            for (int regionId = 0; regionId < landRegionsCount; ++regionId)
            {
                if (regionHasPortSlot[regionId] && !regionHasSeaAdjacency[regionId])
                {
                    LoggerViewModel.Log($"Town Slot validation: {mapHexFile.GetRegionName(regionId)} has a port slot but is not adjacent to any sea.", LogLevel.Warning);
                    failed = true;
                }
            }

            //A region with no town slots should be fully impassable; otherwise it is likely a mistake.
            for (int regionId = 0; regionId < landRegionsCount; ++regionId)
            {
                if (!regionExists[regionId] || validSlots.ContainsKey(regionId))
                {
                    continue;
                }

                if (regionHasPassable[regionId])
                {
                    LoggerViewModel.Log($"Town Slot validation: {mapHexFile.GetRegionName(regionId)} has no Town Slots but is not fully impassable.", LogLevel.Warning);
                    failed = true;
                }
                else
                {
                    LoggerViewModel.Log($"Town Slot validation: {mapHexFile.GetRegionName(regionId)} has no Town Slots (fully impassable; could this be a wasteland?).", LogLevel.Info);
                }
            }

            return !failed;
        }

        /// <summary>
        /// Counts how many connected groups the given hexes form using hex adjacency.
        /// </summary>
        private static int CountConnectedGroups(MapHexFile mapHexFile, List<int> hexes)
        {
            if (hexes.Count <= 1)
            {
                return hexes.Count;
            }

            var members = new HashSet<int>(hexes);
            var visited = new HashSet<int>();
            var queue   = new Queue<int>();
            var groups  = 0;

            foreach (var startIndex in hexes)
            {
                if (visited.Contains(startIndex))
                {
                    continue;
                }

                ++groups;
                queue.Clear();
                queue.Enqueue(startIndex);
                visited.Add(startIndex);

                while (queue.Count > 0)
                {
                    var hexIndex = queue.Dequeue();
                    var hex      = mapHexFile.HexData[hexIndex];
                    for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                    {
                        var nbrIndex = mapHexFile.GetNeighbourIndex(hex, dir);
                        if (nbrIndex == -1)
                        {
                            continue;
                        }

                        if (members.Contains(nbrIndex) && !visited.Contains(nbrIndex))
                        {
                            visited.Add(nbrIndex);
                            queue.Enqueue(nbrIndex);
                        }
                    }
                }
            }

            return groups;
        }
    }
}
