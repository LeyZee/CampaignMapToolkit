using System;
using System.Collections.Generic;
using System.Linq;

namespace CAIME.Pathfinding
{
    internal class Border
    {
        public List<int> Hexes;

        public Border()
        {
            Hexes = new List<int>();
        }
    }

    internal class BordersData
    {
        public List<Hex> Hexes;

        public BordersData()
        {
            Hexes = new List<Hex>();
        }
    }

    internal class BordersGenerator
    {
        private readonly MapHexFile _mapHexFile;
        private readonly string     _debugPath;
        private readonly bool       _bWriteDebugInfo;

        // Per-hex data packed into dense arrays so the (nested) neighbour scans read from cache
        // instead of dereferencing scattered Hex class objects.
        private int[]  _nbrData;
        private byte[] _flags;
        private int[]  _regionId;

        private const byte F_BORDER        = 1 << 0;
        private const byte F_PASSABLE_LAND = 1 << 1;
        private const byte F_COAST         = 1 << 2;
        private const byte F_PASSABLE      = 1 << 3;
        private const byte F_CLIFF         = 1 << 4;

        public BordersGenerator(MapHexFile mapHexFile, string debugPath, bool bWriteDebug = false)
        {
            _mapHexFile         = mapHexFile;
            _debugPath          = debugPath;
            _bWriteDebugInfo    = bWriteDebug;
        }

        private void BuildPackedData()
        {
            int capacity   = (int)_mapHexFile.Capacity;
            int width      = (int)_mapHexFile.MapWidth;
            int height     = (int)_mapHexFile.MapHeight;
            int neighbours = HexGridUtility.NEIGHBOURS_COUNT;

            _flags    = new byte[capacity];
            _regionId = new int[capacity];
            _nbrData  = new int[capacity * neighbours];

            for (int hexIndex = 0; hexIndex < capacity; ++hexIndex)
            {
                var hex = _mapHexFile.HexData[hexIndex];

                byte f = 0;
                if (hex.IsBorder)       f |= F_BORDER;
                if (hex.IsPassableLand) f |= F_PASSABLE_LAND;
                if (hex.IsCoast)        f |= F_COAST;
                if (hex.IsPassable)     f |= F_PASSABLE;
                if (hex.IsCliff)        f |= F_CLIFF;
                _flags[hexIndex]    = f;
                _regionId[hexIndex] = hex.RegionId;

                int baseIdx = hexIndex * neighbours;
                for (ushort dir = 0; dir < neighbours; ++dir)
                    _nbrData[baseIdx + dir] = HexGridUtility.GetNeighbourIndexFast(hex, dir, width, height);
            }
        }

        private bool IsPassableLandBorder(int hexIndex)
        {
            byte f = _flags[hexIndex];
            return (f & F_BORDER) != 0 && (f & F_PASSABLE_LAND) != 0 && (f & F_COAST) == 0;
        }

        private bool HasPassableNeigbhours(int hexIndex)
        {
            byte f = _flags[hexIndex];
            if ((f & F_COAST) != 0)
                return false;

            if ((f & F_PASSABLE) != 0)
                return true;

            int baseIdx = hexIndex * HexGridUtility.NEIGHBOURS_COUNT;
            for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
            {
                int nbrIndex = _nbrData[baseIdx + dir];
                if (nbrIndex == -1)
                    continue;

                byte nf = _flags[nbrIndex];
                if ((nf & F_COAST) == 0 && (nf & F_PASSABLE_LAND) != 0)
                    return true;
            }

            return false;
        }

        private void CalculateLandBorders(Border[] borders)
        {
            // Per-region membership sets so the duplicate check is O(1) instead of List.IndexOf (O(n)).
            var seen = new HashSet<int>[borders.Length];

            for (int hexIndex = 0; hexIndex < _mapHexFile.Capacity; ++hexIndex)
            {
                if (!IsPassableLandBorder(hexIndex))
                    continue;

                int hexRegion = _regionId[hexIndex];

                // RegionId lives in the combined [land..., sea...] space and is -1 when unassigned,
                // so a land hex can legitimately carry an id that is not a land region. Such a hex
                // contributes to no land border list.
                if ((uint)hexRegion >= (uint)borders.Length)
                    continue;

                int baseIdx   = hexIndex * HexGridUtility.NEIGHBOURS_COUNT;

                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    int nbrIndex = _nbrData[baseIdx + dir];
                    if (nbrIndex == -1)
                        continue;

                    if (!IsPassableLandBorder(nbrIndex) && !HasPassableNeigbhours(nbrIndex))
                        continue;

                    if (hexRegion == _regionId[nbrIndex])
                        continue;

                    var regionSeen = seen[hexRegion] ?? (seen[hexRegion] = new HashSet<int>());
                    if (!regionSeen.Add(nbrIndex))
                        continue;

                    borders[hexRegion].Hexes.Add(nbrIndex);
                }
            }
        }

        private void CalculateAllBorders(BordersData bordersData, int[] tileGroupIndices)
        {
            int width = (int)_mapHexFile.MapWidth;

            for (int hexIndex = 0; hexIndex < _mapHexFile.Capacity; ++hexIndex)
            {
                byte f = _flags[hexIndex];
                if ((f & F_PASSABLE) == 0 || (f & F_CLIFF) != 0) // IsImpassable || IsCliff
                    continue;

                int hexTileGroup = tileGroupIndices[hexIndex];
                int baseIdx      = hexIndex * HexGridUtility.NEIGHBOURS_COUNT;

                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    int nbrIndex = _nbrData[baseIdx + dir];
                    if (nbrIndex == -1)
                        continue;

                    byte nf = _flags[nbrIndex];
                    if ((nf & F_PASSABLE) == 0 || (nf & F_CLIFF) != 0)
                        continue;

                    if (tileGroupIndices[nbrIndex] == hexTileGroup)
                        continue;

                    // First qualifying neighbour confirms hex belongs in bordersData; no need to
                    // iterate further directions. This replaces the O(n) List.IndexOf dedup check.
                    bordersData.Hexes.Add(_mapHexFile.HexData[hexIndex]);
                    break;
                }
            }

            bordersData.Hexes = bordersData.Hexes
                .OrderBy(hex => tileGroupIndices[hex.R * width + hex.Q])
                .ToList();
        }

        /// <summary>
        /// Computes only the passable land borders, skipping the (potentially expensive) all-borders pass.
        /// Use this when the caller does not need <see cref="BordersData"/> (e.g. validation).
        /// </summary>
        public void GenerateLandBorders(out Border[] borders, int landRegionsCount)
        {
            borders = new Border[landRegionsCount];

            for (int regionId = 0; regionId < landRegionsCount; ++regionId)
            {
                borders[regionId] = new Border();
            }

            BuildPackedData();
            CalculateLandBorders(borders);
        }

        /// <param name="tileGroupIndices">
        /// Per-hex tile group index (indexed by scan order, R * width + Q), as produced by
        /// <see cref="TileGroupsGenerator"/>. Required to classify the all-borders set the same way
        /// the game does. The exporter computes tile groups before borders and passes them in.
        /// </param>
        public void Generate(out Border[] borders, out BordersData bordersData, int landRegionsCount, int[] tileGroupIndices)
        {
            borders = new Border[landRegionsCount];
            bordersData = new BordersData();

            for (int regionId = 0; regionId < landRegionsCount; ++regionId)
            {
                borders[regionId] = new Border();
            }

            BuildPackedData();
            CalculateLandBorders(borders);
            CalculateAllBorders(bordersData, tileGroupIndices);

#if DEBUG
            if (_bWriteDebugInfo && _debugPath != null)
            {
                var debugBorders = new byte[_mapHexFile.Capacity];
                var debugBordersData = new byte[_mapHexFile.Capacity];
                var stride = (int)_mapHexFile.MapWidth;

                foreach (var border in borders)
                {
                    foreach (var hexIndex in border.Hexes)
                    {
                        HexGridUtility.CoordsFromIndex(hexIndex, stride, out int y, out int x);
                        debugBorders[x + stride * y] = 8;
                    }
                }

                foreach (var hex in bordersData.Hexes)
                {
                    var hexIndex = HexGridUtility.GetHexIndex(hex, stride);
                    HexGridUtility.CoordsFromIndex(hexIndex, stride, out int y, out int x);
                    debugBordersData[x + stride * y] = 8;
                }

                Utility.ArrayToBmp(_debugPath, $"debug_borders", _mapHexFile.MapWidth, _mapHexFile.MapHeight, debugBorders);
                Utility.ArrayToBmp(_debugPath, $"debug_passable_borders", _mapHexFile.MapWidth, _mapHexFile.MapHeight, debugBordersData);
            }
#endif
        }
    }
}
