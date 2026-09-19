using System;
using System.Collections.Generic;

namespace CAIME.Pathfinding.Heuristic
{
    /// <summary>
    /// Where each tile group can be left from: the hexes that stand on its rim, ordered by ascending
    /// flat hex index. A tile group walled in by impassable terrain or cliffs has none, and is never
    /// searched from.
    /// </summary>
    internal sealed class TileGroupBorders
    {
        private readonly int[][] _hexesByTileGroup;
        private readonly byte[]  _initialMediumByTileGroup;

        public TileGroupBorders(MapHexFile mapHexFile, SearchGrid grid, int tileGroupsCount)
        {
            _hexesByTileGroup         = Collect(mapHexFile, grid, tileGroupsCount);
            _initialMediumByTileGroup = FirstBorderMedium(grid, _hexesByTileGroup);
        }

        public int[] HexesOf(int tileGroupIndex)
        {
            return _hexesByTileGroup[tileGroupIndex];
        }

        /// <summary>The medium of the first border hex, which decides what the medium rule forbids.</summary>
        public uint InitialMediumOf(int tileGroupIndex)
        {
            return _initialMediumByTileGroup[tileGroupIndex];
        }

        private static int[][] Collect(MapHexFile mapHexFile, SearchGrid grid, int tileGroupsCount)
        {
            var collected = new List<int>[tileGroupsCount];
            var hexes     = mapHexFile.HexData;

            for (int hexIndex = 0; hexIndex < grid.Capacity; ++hexIndex)
            {
                if (!IsBorderHex(hexIndex, hexes, grid))
                    continue;

                int tileGroupIndex = grid.TileGroupOfHex[hexIndex];
                var borderHexes    = collected[tileGroupIndex] ?? (collected[tileGroupIndex] = new List<int>());
                borderHexes.Add(hexIndex);
            }

            var hexesByTileGroup = new int[tileGroupsCount][];
            for (int tileGroupIndex = 0; tileGroupIndex < tileGroupsCount; ++tileGroupIndex)
            {
                hexesByTileGroup[tileGroupIndex] = collected[tileGroupIndex] == null
                                                 ? Array.Empty<int>()
                                                 : collected[tileGroupIndex].ToArray();
            }

            return hexesByTileGroup;
        }

        private static bool IsBorderHex(int hexIndex, Hex[] hexes, SearchGrid grid)
        {
            var hex = hexes[hexIndex];
            if (!hex.IsPassable || hex.IsCliff)
                return false;

            int tileGroupIndex = grid.TileGroupOfHex[hexIndex];
            int stepBase       = hexIndex * grid.Stride;

            for (int direction = 0; direction < grid.Stride; ++direction)
            {
                int neighbourIndex = grid.Neighbours[stepBase + direction];
                if (neighbourIndex < 0)
                    continue;

                var neighbour = hexes[neighbourIndex];
                if (neighbour.IsPassable && !neighbour.IsCliff && grid.TileGroupOfHex[neighbourIndex] != tileGroupIndex)
                    return true;
            }

            return false;
        }

        private static byte[] FirstBorderMedium(SearchGrid grid, int[][] hexesByTileGroup)
        {
            var initialMedium = new byte[hexesByTileGroup.Length];

            for (int tileGroupIndex = 0; tileGroupIndex < hexesByTileGroup.Length; ++tileGroupIndex)
            {
                var borderHexes = hexesByTileGroup[tileGroupIndex];
                if (borderHexes.Length > 0)
                    initialMedium[tileGroupIndex] = grid.MediumOfHex[borderHexes[0]];
            }

            return initialMedium;
        }
    }
}
