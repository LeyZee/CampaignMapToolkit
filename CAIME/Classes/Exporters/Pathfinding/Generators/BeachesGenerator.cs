using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace CAIME.Pathfinding
{
    internal class BeachHex
    {
        public int  HexIndex;
        public byte EdgeMask;

        public BeachHex(int index, byte mask)
        {
            HexIndex = index;
            EdgeMask = mask;
        }
    }

    internal class BeachesContainer
    {
        public int AreaIndexEnter;
        public int AreaIndexLeave;

        public List<BeachHex> BeachesEnter { get; private set; }
        public List<BeachHex> BeachesLeave { get; private set; }

        public BeachesContainer(int enterIndex, int leaveIndex)
        {
            BeachesEnter    = new List<BeachHex>();
            BeachesLeave    = new List<BeachHex>();

            AreaIndexEnter  = enterIndex;
            AreaIndexLeave  = leaveIndex;
        }
    }

    internal class BeachesGenerator
    {
        private readonly MapHexFile _mapHexFile;
        private readonly string     _debugPath;

        public BeachesGenerator(MapHexFile mapHexFile, string debugPath)
        {
            _mapHexFile = mapHexFile;
            _debugPath  = debugPath;
        }

        private void PrepareNearbyHLCIs(int hexIndex, byte[] hlciData, int width, int height,
            out List<ushort> landHLCIs, out List<ushort> seaHLCIs, out bool hasBridgeNbr)
        {
            var landSet  = new HashSet<byte>();
            var seaSet   = new HashSet<byte>();
            landHLCIs    = new List<ushort>();
            seaHLCIs     = new List<ushort>();
            hasBridgeNbr = false;

            var hex = _mapHexFile.HexData[hexIndex];

            for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
            {
                int nbrIndex = HexGridUtility.GetNeighbourIndexFast(hex, dir, width, height);
                if (nbrIndex == -1)
                    continue;

                var nbr = _mapHexFile.HexData[nbrIndex];
                if (nbr.IsImpassable)
                    continue;

                var areaIndex = hlciData[nbrIndex];

                if (nbr.IsLand && !nbr.IsCoast)
                {
                    if (landSet.Add(areaIndex))
                        landHLCIs.Add(areaIndex);
                }
                else if (nbr.IsSea)
                {
                    if (seaSet.Add(areaIndex))
                        seaHLCIs.Add(areaIndex);

                    if (nbr.IsBridge)
                        hasBridgeNbr = true;
                }
            }
        }

        public void Generate(out List<BeachesContainer> beachesContainer, in byte[] hlciData, in List<HLCIPair> hlciPairs)
        {
            // Step 1. Calculate land-to-sea and sea-to-land beaches
            // Step 2. Locate each beach (must include both, land-to-sea and sea-to-land parts)
            // Step 3. Calculate edge masks per each beach hex
            // Step 4. Loop through each beach and group beaches together, depending on HLCI area they're in

            beachesContainer = new List<BeachesContainer>();

            // Pre-build O(1) lookup: (enter, leave) → index in beachesContainer.
            // Also maintain per-container dictionaries for BeachesEnter and BeachesLeave
            // so FindIndex inside the inner loop becomes a TryGetValue (O(1) vs O(n)).
            var containerLookup = new Dictionary<(int enter, int leave), int>(hlciPairs.Count);
            var enterMaps = new Dictionary<int, BeachHex>[hlciPairs.Count];
            var leaveMaps = new Dictionary<int, BeachHex>[hlciPairs.Count];

            foreach (var pair in hlciPairs)
            {
                int idx = beachesContainer.Count;
                beachesContainer.Add(new BeachesContainer(pair.AreaIndexEnter, pair.AreaIndexLeave));
                containerLookup[(pair.AreaIndexEnter, pair.AreaIndexLeave)] = idx;
                enterMaps[idx] = new Dictionary<int, BeachHex>();
                leaveMaps[idx] = new Dictionary<int, BeachHex>();
            }

            int width  = (int)_mapHexFile.MapWidth;
            int height = (int)_mapHexFile.MapHeight;

            for (int hexIndex = 0; hexIndex < _mapHexFile.Capacity; ++hexIndex)
            {
                var hex = _mapHexFile.HexData[hexIndex];
                if (!hex.IsCoast)
                    continue;

                PrepareNearbyHLCIs(hexIndex, hlciData, width, height,
                    out var landHLCIs, out var seaHLCIs, out var hasBridgeNbr);

                if (!hex.IsBeach && !hasBridgeNbr)
                    continue;

                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    int nbrIndex = HexGridUtility.GetNeighbourIndexFast(hex, dir, width, height);
                    if (nbrIndex == -1)
                        continue;

                    var nbr = _mapHexFile.HexData[nbrIndex];
                    if (nbr.IsImpassable)
                        continue;

                    var isLand = nbr.IsLand && !nbr.IsCoast;
                    var isSea  = nbr.IsSea;

                    if (isSea || (isLand && !hasBridgeNbr))
                    {
                        var nbrHLCI    = hlciData[nbrIndex];
                        var otherAreas = isLand ? seaHLCIs : landHLCIs;

                        foreach (var area in otherAreas)
                        {
                            var pairKey = isLand
                                ? ((int)nbrHLCI, (int)area)
                                : ((int)area,    (int)nbrHLCI);

                            if (!containerLookup.TryGetValue(pairKey, out int groupIndex))
                                continue;

                            var group      = beachesContainer[groupIndex];
                            var beachesEnter = isLand ? group.BeachesEnter : group.BeachesLeave;
                            var beachesLeave = isLand ? group.BeachesLeave : group.BeachesEnter;
                            var enterMap   = isLand ? enterMaps[groupIndex] : leaveMaps[groupIndex];
                            var leaveMap   = isLand ? leaveMaps[groupIndex] : enterMaps[groupIndex];

                            if (!enterMap.TryGetValue(nbrIndex, out var enterHex))
                            {
                                enterHex = new BeachHex(nbrIndex, 0);
                                beachesEnter.Add(enterHex);
                                enterMap[nbrIndex] = enterHex;
                            }
                            enterHex.EdgeMask |= (byte)(1 << HexGridUtility.InverseDir(dir));

                            if (!leaveMap.TryGetValue(hexIndex, out var leaveHex))
                            {
                                leaveHex = new BeachHex(hexIndex, 0);
                                beachesLeave.Add(leaveHex);
                                leaveMap[hexIndex] = leaveHex;
                            }
                            leaveHex.EdgeMask |= (byte)(1 << dir);
                        }
                    }
                }
            }

            beachesContainer = beachesContainer.OrderBy(x => x.AreaIndexEnter).ThenBy(x => x.AreaIndexLeave).ToList();
#if DEBUG
            var test_land = new byte[_mapHexFile.MapWidth * _mapHexFile.MapHeight];
            var test_sea = new byte[_mapHexFile.MapWidth * _mapHexFile.MapHeight];
            
            foreach (var beachesGroup in beachesContainer)
            {
                var landToSeaHexes = beachesGroup.BeachesEnter;
                var seaToLandHexes = beachesGroup.BeachesLeave;
            
                foreach (var beachHex in landToSeaHexes)
                {
                    test_land[beachHex.HexIndex] = beachHex.EdgeMask;
                }
            
                foreach (var beachHex in seaToLandHexes)
                {
                    test_sea[beachHex.HexIndex] = beachHex.EdgeMask;
                }
            }
            
            var result = test_land.Zip(test_sea, (f, s) => new[] { f, s }).SelectMany(f => f).ToArray();
            Utility.ArrayToRaw(_debugPath, "beaches_validate_test", result);
#endif
        }
    }
}
