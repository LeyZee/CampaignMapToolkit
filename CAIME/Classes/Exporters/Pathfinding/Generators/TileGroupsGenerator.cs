using System;
using System.Collections.Generic;

namespace CAIME.Pathfinding
{
    internal struct TileGroupItem
    {
        public ushort HighLevelConnectivityIndex;
        public ushort RegionIndex;

        public TileGroupItem(ushort hlci, ushort regionIndex)
        {
            HighLevelConnectivityIndex = hlci;
            RegionIndex                = regionIndex;
        }
    }

    internal class TileGroupsGenerator
    {
        internal readonly static int TILE_GROUPS_LOWER_BITS_OFFSET  = 6;
        internal readonly static int TILE_GROUPS_HIGHER_BITS_OFFSET = 7;
        internal readonly static int HEX_TYPES_DATA_OFFSET          = 7;

        private readonly MapHexFile _mapHexFile;
        private readonly string     _debugPath;

        /// <summary>
        /// Maps each distinct (HLCI area, region) pair to its tile group index. The pair itself
        /// is the key, so there is no hash to collide and both the existence check and the index
        /// lookup are O(1).
        /// </summary>
        private Dictionary<(ushort hlci, ushort region), int> _pairToIndex;

        public TileGroupsGenerator(MapHexFile mapHexFile, string debugPath)
        {
            _mapHexFile = mapHexFile;
            _debugPath  = debugPath;
        }

        /// <summary>
        /// Returns the tile group index for the given (hlci, regionId) pair,
        /// registering a new tile group if this pair has not been seen before.
        /// </summary>
        private int GetOrAddTileGroup(
            List<TileGroupItem> tileGroups,
            ushort hlciAreaIndex,
            ushort regionId)
        {
            var key = (hlciAreaIndex, regionId);

            if (!_pairToIndex.TryGetValue(key, out int index))
            {
                index = tileGroups.Count;
                tileGroups.Add(new TileGroupItem(hlciAreaIndex, regionId));
                _pairToIndex[key] = index;
            }

            return index;
        }

        public void Generate(
            ref byte[]           edgesData,
            out List<TileGroupItem> tileGroups,
            in  byte[]           hlciAreas,
            in  int              regionsCount,
            in  int              areasCount)
        {
            tileGroups = new List<TileGroupItem>();

            if (edgesData.Length != _mapHexFile.Capacity * 8)
            {
                // Edges data must be allocated by the caller (8 bytes per hex).
                return;
            }

            _pairToIndex = new Dictionary<(ushort, ushort), int>();

            // Pass 1: write HexType into edgesData and resolve (or register) the
            // tile group index for every hex.
            var tileGroupIndices = new int[_mapHexFile.Capacity];

            for (int hexIndex = 0; hexIndex < _mapHexFile.Capacity; ++hexIndex)
            {
                var hex = _mapHexFile.HexData[hexIndex];

                edgesData[hexIndex * 8 + HEX_TYPES_DATA_OFFSET] = (byte)((int)hex.HexType << 4);

                tileGroupIndices[hexIndex] = GetOrAddTileGroup(
                    tileGroups,
                    hlciAreas[hexIndex],
                    (ushort)(hex.RegionId + 1));
            }

#if DEBUG
            var hexTypes = new byte[_mapHexFile.Capacity];
            for (int hexIndex = 0; hexIndex < _mapHexFile.Capacity; ++hexIndex)
            {
                hexTypes[hexIndex] = (byte)(edgesData[hexIndex * 8 + HEX_TYPES_DATA_OFFSET] >> 4);
            }

            Utility.ArrayToBmp(_debugPath, "debug_hex_types", _mapHexFile.MapWidth, _mapHexFile.MapHeight, hexTypes);
#endif

            // Pass 2: encode the tile group index into edgesData.
            // The index occupies 12 bits: the lower 8 go into byte 6, the upper 4
            // go into the low nibble of byte 7 (which already holds the HexType in
            // its high nibble from pass 1).
            for (int hexIndex = 0; hexIndex < _mapHexFile.Capacity; ++hexIndex)
            {
                int index = Bits.Pack(tileGroupIndices[hexIndex], 12, "Tile group index");

                byte tileGroupIndexLower  = (byte)( index        & 0xFF);
                byte tileGroupIndexHigher = (byte)( index >> 8);

                edgesData[hexIndex * 8 + TILE_GROUPS_LOWER_BITS_OFFSET]   = tileGroupIndexLower;
                edgesData[hexIndex * 8 + TILE_GROUPS_HIGHER_BITS_OFFSET] |= tileGroupIndexHigher;
            }

#if DEBUG
            var tile_groups = new byte[_mapHexFile.Capacity * 2];
            for (int hexIndex = 0; hexIndex < _mapHexFile.Capacity; ++hexIndex)
            {
                tile_groups[hexIndex * 2 + 0] = edgesData[hexIndex * 8 + TILE_GROUPS_LOWER_BITS_OFFSET];
                tile_groups[hexIndex * 2 + 1] = (byte)(edgesData[hexIndex * 8 + TILE_GROUPS_HIGHER_BITS_OFFSET] & 0b_0000_1111);
            }

            Utility.ArrayToRaw(_debugPath, "debug_tile_groups", tile_groups);
#endif
        }

        public static int ExtractTileGroupIndex(int hexIndex, in byte[] edgesData)
        {
            var higherBits = edgesData[hexIndex * 8 + TILE_GROUPS_HIGHER_BITS_OFFSET];
            var lowerBits  = edgesData[hexIndex * 8 + TILE_GROUPS_LOWER_BITS_OFFSET];

            return ((higherBits & 0b_0000_1111) << 8) | lowerBits;
        }

        /// <summary>
        /// Extracts the per-hex tile group index for every hex (scan order, R * width + Q) from the
        /// encoded edges data. Must be called after <see cref="Generate"/> has populated the tile
        /// group bits.
        /// </summary>
        public static int[] ExtractTileGroupIndices(in byte[] edgesData)
        {
            int capacity = edgesData.Length / 8;
            var indices  = new int[capacity];
            for (int hexIndex = 0; hexIndex < capacity; ++hexIndex)
            {
                indices[hexIndex] = ExtractTileGroupIndex(hexIndex, edgesData);
            }
            return indices;
        }
    }
}
