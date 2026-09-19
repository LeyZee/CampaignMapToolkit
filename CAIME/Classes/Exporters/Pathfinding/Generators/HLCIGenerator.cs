using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CAIME.Pathfinding
{
    internal struct HLCIPair
    {
        public ushort AreaIndexEnter;
        public ushort AreaIndexLeave;

        public HLCIPair(ushort landIndex, ushort seaIndex)
        {
            AreaIndexEnter = landIndex;
            AreaIndexLeave = seaIndex;
        }
    }

    /// <summary>
    /// Class that generates High Level Connectivity Index areas (HLCI areas)
    /// </summary>
    internal class HLCIGenerator
    {
        // hlciData is byte[] and index 0 is reserved for IMPASSABLE, so the usable range
        // is 1..255 and no more than 255 distinct connected areas can be represented.
        private const int MAX_HLCI_AREAS = byte.MaxValue;

        private bool[] isVisited;
        private int    totalAreasCount;
        private int    scanCursor;

        private readonly MapHexFile _mapHexFile;
        private readonly string     _debugPath;

        private BridgesData bridgesData;

        // Per-hex data packed into dense arrays once, so the flood-fill / pair passes read from
        // cache and use the allocation-free neighbour lookup instead of dereferencing scattered
        // Hex class objects (and the allocating MapHexFile.GetNeighbourIndex -> Hex.Add path).
        private byte[] _flags;
        private int[]  _nbrData;

        private const byte F_PASSABLE_SEA  = 1 << 0;
        private const byte F_PASSABLE_LAND = 1 << 1;
        private const byte F_BRIDGE_CLIFF  = 1 << 2;
        private const byte F_SEA           = 1 << 3;
        private const byte F_PASSABLE      = 1 << 4;
        private const byte F_COAST         = 1 << 5;

        public List<HLCIPair> HLCIPairs { get; private set; }

        internal HLCIGenerator(MapHexFile mapHexFile, string debugPath)
        {
            _mapHexFile     = mapHexFile;
            _debugPath      = debugPath;
            totalAreasCount = 0;
        }

        /// <summary>
        /// Finds the index of a first, passable unvisited hex.
        /// <para>A hex is never un-visited, so the scan resumes from where the previous call left
        /// off instead of restarting at 0. That makes seeding all areas O(capacity) in total
        /// rather than O(areas * capacity).</para>
        /// </summary>
        /// <returns>Index of unvisited hex, or -1 if none remain.</returns>
        private int FindUnvisitedHex()
        {
            while (scanCursor < _mapHexFile.Capacity)
            {
                if ((_flags[scanCursor] & F_PASSABLE) != 0 && !IsVisited(scanCursor))
                {
                    return scanCursor;
                }

                ++scanCursor;
            }

            return -1;
        }

        /// <summary>Checks whether a hex was already visited.</summary>
        private bool IsVisited(int index)
        {
            return isVisited[index];
        }

        /// <summary>Marks a hex as visited.</summary>
        private void SetVisited(int index)
        {
            isVisited[index] = true;
        }

        /// <summary>
        /// Checks whether a hex and its neighbour are contiguous (both land or both sea).
        /// </summary>
        private bool IsContiguous(int hexIndex, int nbrIndex)
        {
            byte hf = _flags[hexIndex];
            byte nf = _flags[nbrIndex];

            var isBothSea  = (hf & F_PASSABLE_SEA) != 0 && (nf & F_PASSABLE_SEA) != 0;
            var isBothLand = ((hf & F_PASSABLE_LAND) != 0 || (hf & F_BRIDGE_CLIFF) != 0)
                          && ((nf & F_PASSABLE_LAND) != 0 || (nf & F_BRIDGE_CLIFF) != 0);

            return isBothSea || isBothLand;
        }

        /// <summary>
        /// Writes an HLCI area index into the data array for the given hex and marks it visited.
        /// </summary>
        private void SetData(byte[] data, int index, int value)
        {
            data[index] = (byte)value;
            SetVisited(index);
        }

        private void UpdateAreasCount()
        {
            ++totalAreasCount;
        }

        /// <summary>
        /// BFS flood-fill that labels all hexes in one connected area with the current area index.
        /// </summary>
        /// <param name="hlciData">Output HLCI data array.</param>
        /// <param name="startHexIndex">Index of the seed hex for this area.</param>
        private void FloodFill(byte[] hlciData, int startHexIndex)
        {
            if (startHexIndex < 0 || startHexIndex >= _mapHexFile.Capacity)
            {
                return;
            }

            UpdateAreasCount();

            var frontier = new Queue<int>();
            frontier.Enqueue(startHexIndex);
            SetData(hlciData, startHexIndex, totalAreasCount);

            while (frontier.Count > 0)
            {
                var hexIndex = frontier.Dequeue();
                int hexBase  = hexIndex * HexGridUtility.NEIGHBOURS_COUNT;

                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    int nbrIndex = _nbrData[hexBase + dir];
                    if (nbrIndex == -1)
                        continue;

                    if (!IsVisited(nbrIndex) && IsContiguous(hexIndex, nbrIndex))
                    {
                        frontier.Enqueue(nbrIndex);
                        SetData(hlciData, nbrIndex, totalAreasCount);
                    }
                }

                if ((_flags[hexIndex] & F_BRIDGE_CLIFF) != 0)
                {
                    var conIndex = BridgeUtility.GetBridgeEdgeOnAnotherSide(_mapHexFile, bridgesData, hexIndex);
                    if (!IsVisited(conIndex))
                    {
                        frontier.Enqueue(conIndex);
                        SetData(hlciData, conIndex, totalAreasCount);
                    }
                }
            }
        }

        /// <summary>
        /// Builds the list of unique land/sea HLCI area pairs that share a coastline.
        /// </summary>
        private void CalculatePairs(byte[] hlciData)
        {
            HLCIPairs = new List<HLCIPair>();

            // Keyed on the canonicalised (min, max) pair so (a,b) and (b,a) collapse to one entry.
            var seenPairs = new HashSet<(ushort, ushort)>();

            for (int hexIndex = 0; hexIndex < _mapHexFile.Capacity; ++hexIndex)
            {
                int hex_hlci_index = hlciData[hexIndex];
                if (hex_hlci_index == 0)
                    continue; // skip impassable area

                if ((_flags[hexIndex] & F_COAST) == 0)
                    continue; // non-coast hexes cannot have cross-domain neighbours

                bool hexIsSea = (_flags[hexIndex] & F_SEA) != 0;
                int  hexBase  = hexIndex * HexGridUtility.NEIGHBOURS_COUNT;

                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    int nbrIndex = _nbrData[hexBase + dir];
                    if (nbrIndex == -1)
                        continue;

                    int nbr_hlci_index = hlciData[nbrIndex];
                    if (nbr_hlci_index == 0 || hex_hlci_index == nbr_hlci_index)
                        continue;

                    bool nbrIsSea = (_flags[nbrIndex] & F_SEA) != 0;
                    if (hexIsSea != nbrIsSea)
                    {
                        // Canonical key: always store (smaller, larger) so (a,b) and
                        // (b,a) both map to the same slot.
                        ushort lo = (ushort)Math.Min(hex_hlci_index, nbr_hlci_index);
                        ushort hi = (ushort)Math.Max(hex_hlci_index, nbr_hlci_index);

                        if (seenPairs.Add((lo, hi)))
                        {
                            HLCIPairs.Add(new HLCIPair((ushort)hex_hlci_index, (ushort)nbr_hlci_index));
                        }
                    }
                }
            }
        }

        public void Generate(out byte[] hlciData, in BridgesData bridgesData)
        {
            isVisited  = new bool[_mapHexFile.Capacity];
            hlciData   = new byte[_mapHexFile.Capacity];
            scanCursor = 0;

            this.bridgesData = bridgesData;

            // Pack per-hex flags and precompute neighbour indices once. The flood-fill and pair
            // passes then read these dense arrays instead of chasing Hex object pointers and
            // allocating a Hex per neighbour lookup.
            int capacity   = (int)_mapHexFile.Capacity;
            int width      = (int)_mapHexFile.MapWidth;
            int height     = (int)_mapHexFile.MapHeight;
            int neighbours = HexGridUtility.NEIGHBOURS_COUNT;

            _flags   = new byte[capacity];
            _nbrData = new int[capacity * neighbours];

            for (int hexIndex = 0; hexIndex < capacity; ++hexIndex)
            {
                var hex = _mapHexFile.HexData[hexIndex];

                byte f = 0;
                if (hex.IsPassableSea)  f |= F_PASSABLE_SEA;
                if (hex.IsPassableLand) f |= F_PASSABLE_LAND;
                if (hex.IsBridgeCliff)  f |= F_BRIDGE_CLIFF;
                if (hex.IsSea)          f |= F_SEA;
                if (hex.IsPassable)     f |= F_PASSABLE;
                if (hex.IsCoast)        f |= F_COAST;
                _flags[hexIndex] = f;

                int baseIdx = hexIndex * neighbours;
                for (ushort dir = 0; dir < neighbours; ++dir)
                    _nbrData[baseIdx + dir] = HexGridUtility.GetNeighbourIndexFast(hex, dir, width, height);
            }

            while (true)
            {
                var unvisitedIndex = FindUnvisitedHex();
                if (unvisitedIndex == -1)
                    break;

                // Past the cap the area index would wrap in hlciData's byte and alias an earlier
                // area. Leaving the remaining hexes as area 0 (impassable) is the safer failure.
                if (totalAreasCount >= MAX_HLCI_AREAS)
                {
                    LoggerViewModel.Log(
                        $"HLCIGenerator: maximum number of HLCI areas ({MAX_HLCI_AREAS}) reached. " +
                        "Some passable regions could not be assigned an area index. " +
                        "Consider reducing the number of disconnected land or sea bodies on the map.",
                        LogLevel.Error);
                    break;
                }

                FloodFill(hlciData, unvisitedIndex);
            }

            CalculatePairs(hlciData);

#if DEBUG
            Utility.ArrayToRaw(_debugPath, "debug_hlci_areas_map", hlciData);
            Utility.ArrayToBmp(_debugPath, "debug_hlci_areas_map", _mapHexFile.MapWidth, _mapHexFile.MapHeight, hlciData);
#endif
        }

        public int GetAreasCount() => totalAreasCount;
    }
}
