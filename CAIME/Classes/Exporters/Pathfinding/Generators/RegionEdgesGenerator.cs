using System;
using System.Collections.Generic;

namespace CAIME.Pathfinding
{
    internal class RegionPassableHexes
    {
        public List<Hex>    Hexes   { get; private set; }
        public List<byte>   HexMask { get; private set; }

        //Maps a hex to its position in Hexes so AddHex dedup is O(1) instead of List.IndexOf (O(n)).
        private readonly Dictionary<Hex, int> _indexByHex;

        public RegionPassableHexes()
        {
            Hexes = new List<Hex>();
            HexMask = new List<byte>();
            _indexByHex = new Dictionary<Hex, int>();
        }

        public void AddHex(Hex hex, ushort dir)
        {
            var invDir = HexGridUtility.InverseDir(dir);
            if (_indexByHex.TryGetValue(hex, out int index))
            {
                HexMask[index] |= (byte)(1 << invDir);
            }
            else
            {
                _indexByHex[hex] = Hexes.Count;
                Hexes.Add(hex);
                HexMask.Add((byte)(1 << invDir));
            }
        }
    }

    internal class RegionEdgesGenerator
    {
        private readonly MapHexFile _mapHexFile;
        private readonly string     _debugPath;

        public RegionEdgesGenerator(MapHexFile mapHexFile, string debugPath)
        {
            _mapHexFile = mapHexFile;
            _debugPath  = debugPath;
        }

        private bool IsPassableLandRegionEdge(Hex hex)
        {
            return hex.IsPassableLand && hex.IsCoast == false;
        }

        public void Generate(out RegionPassableHexes[] regionEdges, in Border[] passableLandBorders)
        {
            regionEdges = new RegionPassableHexes[passableLandBorders.Length];
            for (int i = 0; i < regionEdges.Length; ++i)
            {
                regionEdges[i] = new RegionPassableHexes();
            }

            int width  = (int)_mapHexFile.MapWidth;
            int height = (int)_mapHexFile.MapHeight;

            for (int regionIndex = 0; regionIndex < passableLandBorders.Length; ++regionIndex)
            {
                var border = passableLandBorders[regionIndex];

                //O(1) membership test instead of repeated List.IndexOf (O(n)) inside the loop.
                var borderSet = new HashSet<int>(border.Hexes);

                for (int i = 0; i < border.Hexes.Count; ++i)
                {
                    var hexIndex = border.Hexes[i];
                    var hex = _mapHexFile.HexData[hexIndex];

                    for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                    {
                        int nbrIndex = HexGridUtility.GetNeighbourIndexFast(hex, dir, width, height);
                        if (nbrIndex == -1)
                            continue;

                        var nbr = _mapHexFile.HexData[nbrIndex];
                        if (nbr.RegionId == regionIndex)
                            continue;

                        if (borderSet.Contains(nbrIndex))
                            continue;

                        if (IsPassableLandRegionEdge(nbr))
                        {
                            regionEdges[regionIndex].AddHex(nbr, dir);
                        }
                    }
                }
            }

#if DEBUG
            var debugData = new byte[_mapHexFile.Capacity];
            var stride = (int)_mapHexFile.MapWidth;

            for (int regionEdgeIndex = 0; regionEdgeIndex < regionEdges.Length; ++regionEdgeIndex)
            {
                for (int i = 0; i < regionEdges[regionEdgeIndex].Hexes.Count; ++i)
                {
                    var hex = regionEdges[regionEdgeIndex].Hexes[i];
                    var mask = regionEdges[regionEdgeIndex].HexMask[i];

                    var hexIndex = HexGridUtility.GetHexIndex(hex, stride);
                    HexGridUtility.CoordsFromIndex(hexIndex, stride, out int y, out int x);

                    debugData[x + stride * y] = mask;
                }
            }

            Utility.ArrayToRaw(_debugPath, $"debug_land_region_passable_edges", debugData);
#endif
        }
    }
}
