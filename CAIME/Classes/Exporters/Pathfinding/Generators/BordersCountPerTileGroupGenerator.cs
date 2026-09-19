using System;
using System.Collections.Generic;

namespace CAIME.Pathfinding
{
    internal class BordersCountPerTileGroupGenerator
    {
        private readonly MapHexFile _mapHexFile;
        private readonly string     _debugPath;

        public BordersCountPerTileGroupGenerator(MapHexFile mapHexFile, string debugPath)
        {
            _mapHexFile = mapHexFile;
            _debugPath  = debugPath;
        }

        public void Generate(out int[] cumulativeEdgesData, in byte[] edgesData, in List<TileGroupItem> tileGroupsData, in BordersData bordersData)
        {
            cumulativeEdgesData             = new int[tileGroupsData.Count];
            var bordersCountPerTileGroup    = new int[tileGroupsData.Count];
            
            foreach (var hex in bordersData.Hexes)
            {
                var hexIndex = HexGridUtility.IndexFromCoords(hex.R, hex.Q, (int)_mapHexFile.MapWidth);
                var tileGroupIndex = TileGroupsGenerator.ExtractTileGroupIndex(hexIndex, edgesData);
                ++bordersCountPerTileGroup[tileGroupIndex];
            }

            // Exclusive prefix sum: entry i is the number of border hexes in all tile groups before i.
            int runningTotal = 0;
            for (int i = 0; i < tileGroupsData.Count; ++i)
            {
                cumulativeEdgesData[i] = runningTotal;
                runningTotal += bordersCountPerTileGroup[i];
            }
        }
    }
}
