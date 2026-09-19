using System.Collections.Generic;

namespace CAIME.Validators
{
    internal static class BridgesValidator
    {
        public static bool Validate(Project project)
        {
            var mapHexFile = project.MapHexFile;
            var capacity   = (int)mapHexFile.Capacity;
            var failed     = false;

            //A bridge must sit on sea
            for (int hexIndex = 0; hexIndex < capacity; ++hexIndex)
            {
                var hex = mapHexFile.HexData[hexIndex];
                if (hex.IsBridge && hex.IsLand)
                {
                    LoggerViewModel.Log($"Bridges validation: Hex({hex.Q}, {hex.R}) is both a bridge and land.", LogLevel.Error);
                    failed = true;
                }

                if (hex.IsBridge && hex.IsImpassable)
                {
                    LoggerViewModel.Log($"Bridges validation: Hex({hex.Q}, {hex.R}) is both a bridge and impassable.", LogLevel.Error);
                    failed = true;
                }
            }

            //Per-bridge checks. Bridges are few and small, so a sequential flood fill is plenty fast.
            var visited = new bool[capacity];
            var queue   = new Queue<int>();

            for (int startIndex = 0; startIndex < capacity; ++startIndex)
            {
                if (visited[startIndex])
                {
                    continue;
                }

                var startHex = mapHexFile.HexData[startIndex];
                if (!(startHex.IsBridge && startHex.IsSea))
                {
                    continue;
                }

                //Flood fill this bridge (connected bridge sea hexes), collecting its adjacent land hexes
                queue.Clear();
                queue.Enqueue(startIndex);
                visited[startIndex] = true;

                var representative = startIndex;
                var adjacentLand   = new HashSet<int>();

                while (queue.Count > 0)
                {
                    var hexIndex = queue.Dequeue();
                    if (hexIndex < representative)
                    {
                        representative = hexIndex;
                    }

                    var hex = mapHexFile.HexData[hexIndex];
                    for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                    {
                        var nbrIndex = mapHexFile.GetNeighbourIndex(hex, dir);
                        if (nbrIndex == -1)
                        {
                            continue;
                        }

                        var nbr = mapHexFile.HexData[nbrIndex];
                        if (nbr.IsBridge && nbr.IsSea)
                        {
                            if (!visited[nbrIndex])
                            {
                                visited[nbrIndex] = true;
                                queue.Enqueue(nbrIndex);
                            }
                        }
                        else if (nbr.IsLand)
                        {
                            adjacentLand.Add(nbrIndex);
                        }
                    }
                }

                var repHex = mapHexFile.HexData[representative];

                //A bridge must connect two separate land sides. Group adjacent land hexes by
                //connectivity (the same way the bridge exporter does) and count the distinct sides.
                var sideCount = CountConnectedGroups(mapHexFile, adjacentLand);
                if (sideCount < 2)
                {
                    LoggerViewModel.Log($"Bridges validation: Bridge near Hex({repHex.Q}, {repHex.R}) does not connect two separate land sides (found {sideCount}); it leads nowhere.", LogLevel.Error);
                    failed = true;
                }
            }

            // TODO: Check for holes in bridge hex blobs

            return !failed;
        }

        /// <summary>
        /// Counts how many connected groups the given hexes form (each group is one bridge "side").
        /// </summary>
        private static int CountConnectedGroups(MapHexFile mapHexFile, HashSet<int> hexes)
        {
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

                        if (hexes.Contains(nbrIndex) && !visited.Contains(nbrIndex))
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
