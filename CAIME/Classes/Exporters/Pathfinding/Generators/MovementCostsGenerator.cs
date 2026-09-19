using System;
using System.Collections.Generic;
using System.Data;

namespace CAIME.Pathfinding
{
    internal class MovementCostsGenerator
    {
        #region Some constants to make logic clearer
        private static readonly byte TRUE_ZERO_COST_INDEX       = 0;
        private static readonly byte BEACH_TO_LAND_COST_INDEX   = TRUE_ZERO_COST_INDEX;
        private static readonly byte BEACH_TO_SEA_COST_INDEX    = TRUE_ZERO_COST_INDEX;
        private static readonly byte LAND_TO_BEACH_COST_INDEX   = 1;
        private static readonly byte SEA_TO_BEACH_COST_INDEX    = 2;
        private static readonly byte NAVIGABLE                  = 1;
        private static readonly byte NON_NAVIGABLE              = 0;
        #endregion

        private struct HexData
        {
            public byte[] Attributes;
            public HexType[] HexTypes;
            public sbyte[] GroundTypes;

            public HexData(int capacity)
            {
                Attributes = new byte[capacity];
                HexTypes = new HexType[capacity];
                GroundTypes = new sbyte[capacity];
            }

            public void PackData(Hex hex)
            {
                byte attributes = 0;

                attributes |= (byte)(hex.IsLand ? 1 : 0);
                attributes |= (byte)((hex.IsBeach ? 1 : 0) << 1);
                attributes |= (byte)((hex.IsCliff ? 1 : 0) << 2);
                attributes |= (byte)((hex.IsBridgeCliff ? 1 : 0) << 3);
                attributes |= (byte)((hex.IsRiver ? 1 : 0) << 4);
                attributes |= (byte)((hex.IsPassable ? 1 : 0) << 5);

                this.Attributes[hex.Index] = attributes;
                this.HexTypes[hex.Index] = hex.HexType;
                this.GroundTypes[hex.Index] = hex.GroundTypeIndex;
            }

            public bool IsLand(int hexIndex)
            {
                return (this.Attributes[hexIndex] & 0b00000001) != 0;
            }

            public bool IsSea(int hexIndex)
            {
                return (this.Attributes[hexIndex] & 0b00000001) == 0;
            }

            public bool IsBeach(int hexIndex)
            {
                return (this.Attributes[hexIndex] & 0b00000010) != 0;
            }

            public bool IsCliff(int hexIndex)
            {
                return (this.Attributes[hexIndex] & 0b00000100) != 0;
            }

            public bool IsBridgeCliff(int hexIndex)
            {
                return (this.Attributes[hexIndex] & 0b00001000) != 0;
            }

            public bool IsRiver(int hexIndex)
            {
                return (this.Attributes[hexIndex] & 0b00010000) != 0;
            }

            public bool IsPassable(int hexIndex)
            {
                return (this.Attributes[hexIndex] & 0b00100000) != 0;
            }
        }

        private readonly MapHexFile _mapHexFile;
        private readonly string     _debugPath;
        private HexData hexData;
        private ushort[] costs;
        private int riverCostIndex;
        private int groundTypeCount;

        /// <summary>
        /// List of costs that will be stored in processed pathfinding data.
        /// Cost index points to cost value from this list.
        /// </summary>
        private List<ushort> moveCosts;
        
        /// <summary>
        /// Reverse map from cost value to its index in <see cref="moveCosts"/> for O(1) lookup.
        /// </summary>
        private Dictionary<ushort, int> _costIndex;

        /// <param name="mapHexFile"><see cref="MapHexFile"/> instance</param>
        /// <param name="db">Database instance</param>
        /// <param name="path">Export path</param>
        public MovementCostsGenerator(MapHexFile mapHexFile, DatabaseViewModel db, string debugPath)
        {
            _mapHexFile = mapHexFile;
            _debugPath  = debugPath;

            //tableCosts  = new Dictionary<string, ushort>();
            moveCosts   = new List<ushort>()
            {
                0, // Real 0 cost (immutable value)
                0, // Beach-land cost (0 will be replaced with value from DB by the game)
                0, // Beach-sea cost (0 will be replaced with value from DB by the game)
            };
            // Cost value 0 is at index 0 (true-zero); indices 1 and 2 hold the same value but
            // are addressed directly by the LAND_TO_BEACH / SEA_TO_BEACH constants, never via lookup.
            _costIndex  = new Dictionary<ushort, int> { { 0, 0 } };

            LoadCostsFromDB(db);
        }

        /// <summary>
        /// Caches ground type costs to <see cref="tableCosts"/> to accelerate data access time.
        /// </summary>
        private void LoadCostsFromDB(DatabaseViewModel db)
        {
            var costByKey = new Dictionary<string, ushort>(db.CachedGroundTypes.Count);
            foreach (var groundType in db.CachedGroundTypes)
            {
                if (groundType.Key != null && !costByKey.ContainsKey(groundType.Key))
                    costByKey.Add(groundType.Key, (ushort)groundType.MoveCost);
            }

            costs = new ushort[_mapHexFile.LandGroundTypes.Count + _mapHexFile.SeaGroundTypes.Count + 1];
            riverCostIndex = costs.Length - 1;
            groundTypeCount = riverCostIndex;

            for (int groundTypeIndex = 0; groundTypeIndex < _mapHexFile.LandGroundTypes.Count; ++groundTypeIndex)
            {
                costs[groundTypeIndex] = LookupCost(costByKey, _mapHexFile.LandGroundTypes[groundTypeIndex]);
            }

            for (int groundTypeIndex = 0; groundTypeIndex < _mapHexFile.SeaGroundTypes.Count; ++groundTypeIndex)
            {
                costs[groundTypeIndex + _mapHexFile.LandGroundTypes.Count] = LookupCost(costByKey, _mapHexFile.SeaGroundTypes[groundTypeIndex]);
            }

            costs[riverCostIndex] = LookupCost(costByKey, "river");
        }

        private static ushort LookupCost(Dictionary<string, ushort> costByKey, string groundTypeName)
        {
            if (!costByKey.TryGetValue(groundTypeName, out ushort moveCost))
            {
                throw new InvalidOperationException(
                    $"Ground type '{groundTypeName}' has no row in campaign_ground_types, so its movement cost is unknown. " +
                    "Check that the Assembly Kit database is loaded and contains every ground type the map uses.");
            }

            return moveCost;
        }

        #region Edge cost calculation
        private byte GetCostIndex(ushort edgeCost)
        {
            if (_costIndex.TryGetValue(edgeCost, out int costIndex))
                return (byte)costIndex;

            moveCosts.Add(edgeCost);
            costIndex = moveCosts.Count - 1;
            _costIndex[edgeCost] = costIndex;

            // Bit 7 of the edge byte is the navigability flag, so only 7 bits are left for the index.
            return (byte)Bits.Pack(costIndex, 7, "Movement cost index");
        }

        /// <summary>
        /// Movement cost of the ground type painted on a hex. Unassigned and out-of-range indices
        /// are reported rather than silently reading past the end of <see cref="costs"/>.
        /// </summary>
        private ushort GroundCost(int hexIndex)
        {
            int groundTypeIndex = hexData.GroundTypes[hexIndex];

            if (groundTypeIndex == Hex.INVALID_GROUND_TYPE_INDEX)
            {
                throw new InvalidOperationException(
                    $"The hex at ({hexIndex % (int)_mapHexFile.MapWidth}, {hexIndex / (int)_mapHexFile.MapWidth}) has no ground type assigned, " +
                    "so its movement cost is undefined. Paint a ground type on every passable hex before exporting pathfinding data.");
            }

            if (groundTypeIndex >= groundTypeCount)
            {
                throw new InvalidOperationException(
                    $"The hex at ({hexIndex % (int)_mapHexFile.MapWidth}, {hexIndex / (int)_mapHexFile.MapWidth}) references ground type index {groundTypeIndex}, " +
                    $"but the map only declares {groundTypeCount} ground types.");
            }

            return costs[groundTypeIndex];
        }

        private byte GetAvgCost(int hexIndex, int nbrIndex)
        {
            int hexCost = GroundCost(hexIndex);
            int nbrCost = GroundCost(nbrIndex);

            ushort edgeCost = (ushort)((hexCost + nbrCost) / 2);
            return GetCostIndex(edgeCost);
        }

        private byte CalcRiverCost(int hexIndex, int nbrIndex)
        {
            if (hexData.HexTypes[nbrIndex] == HexType.Land)
                return TRUE_ZERO_COST_INDEX;

            return GetAvgCost(hexIndex, nbrIndex);
        }

        private byte CalcBeachCost(int hexIndex, int nbrIndex)
        {
            if (hexData.HexTypes[nbrIndex] == HexType.Land)
                return BEACH_TO_LAND_COST_INDEX;

            if (hexData.HexTypes[nbrIndex] == HexType.Sea)
                return BEACH_TO_SEA_COST_INDEX;

            return GetAvgCost(hexIndex, nbrIndex);
        }
        
        private byte CalcSeaCost(int hexIndex, int nbrIndex)
        {
            if (hexData.IsBeach(nbrIndex) && !hexData.IsRiver(nbrIndex))
                return SEA_TO_BEACH_COST_INDEX;

            return GetAvgCost(hexIndex, nbrIndex);
        }

        private byte CalcLandCost(int hexIndex, int nbrIndex)
        {
            if (hexData.IsBeach(nbrIndex))
                return LAND_TO_BEACH_COST_INDEX;

            if (hexData.IsRiver(nbrIndex))
                return GetCostIndex(costs[riverCostIndex]);

            return GetAvgCost(hexIndex, nbrIndex);
        }
        
        private byte CalculateEdgeCost(int hexIndex, int nbrIndex)
        {
            if (hexData.IsPassable(hexIndex) == false)
                return TRUE_ZERO_COST_INDEX;

            if (hexData.IsPassable(nbrIndex) == false)
            {
                return GetCostIndex(GroundCost(hexIndex));
            }

            if (!hexData.IsCliff(hexIndex) && !hexData.IsCliff(nbrIndex))
            {
                if (hexData.IsRiver(hexIndex))
                    return CalcRiverCost(hexIndex, nbrIndex);
                if (hexData.IsBeach(hexIndex))
                    return CalcBeachCost(hexIndex, nbrIndex);
                if (hexData.IsLand(hexIndex))
                    return CalcLandCost(hexIndex, nbrIndex);
                if (hexData.IsSea(hexIndex))
                    return CalcSeaCost(hexIndex, nbrIndex);
            }

            return GetAvgCost(hexIndex, nbrIndex);
        }
        #endregion

        #region Navigability calculation
        private byte CalcBridgeNavigability(int nbrIndex)
        {
            if (hexData.HexTypes[nbrIndex] == HexType.Land)
                return NAVIGABLE;

            return NON_NAVIGABLE;
        }
        
        private byte CalcLandNavigability(int nbrIndex)
        {
            if (hexData.IsBeach(nbrIndex))
                return NAVIGABLE;
            if (hexData.IsRiver(nbrIndex))
                return NAVIGABLE;
            if (hexData.IsBridgeCliff(nbrIndex))
                return NAVIGABLE;
            if (hexData.IsLand(nbrIndex))
                return NAVIGABLE;

            return NON_NAVIGABLE;
        }

        private byte CalcSeaNavigability(int nbrIndex)
        {
            if (hexData.IsBeach(nbrIndex) && !hexData.IsRiver(nbrIndex))
                return NAVIGABLE;
            if (hexData.HexTypes[nbrIndex] == HexType.Sea)
                return NAVIGABLE;

            return NON_NAVIGABLE;
        }

        private byte CalcBeachNavigability(int nbrIndex)
        {
            if (hexData.HexTypes[nbrIndex] == HexType.Land)
                return NAVIGABLE;
            if (hexData.HexTypes[nbrIndex] == HexType.Sea)
                return NAVIGABLE;

            return NON_NAVIGABLE;
        }

        private byte CalcRiverNavigability(int nbrIndex)
        {
            if (hexData.HexTypes[nbrIndex] == HexType.Land)
                return NAVIGABLE;

            return NON_NAVIGABLE;
        }
        
        private byte CalculateNavigability(int hexIndex, int nbrIndex)
        {
            if (hexData.IsPassable(hexIndex) == false || hexData.IsPassable(nbrIndex) == false)
                return NON_NAVIGABLE;
            if (hexData.IsCliff(hexIndex) && !hexData.IsBridgeCliff(hexIndex))
                return NON_NAVIGABLE;
            if (hexData.IsCliff(nbrIndex) && !hexData.IsBridgeCliff(nbrIndex))
                return NON_NAVIGABLE;

            if (hexData.IsRiver(hexIndex))
                return CalcRiverNavigability(nbrIndex);
            if (hexData.IsBeach(hexIndex))
                return CalcBeachNavigability(nbrIndex);
            if (hexData.IsBridgeCliff(hexIndex))
                return CalcBridgeNavigability(nbrIndex);
            if (hexData.IsLand(hexIndex))
                return CalcLandNavigability(nbrIndex);
            if (hexData.IsSea(hexIndex))
                return CalcSeaNavigability(nbrIndex);

            return NON_NAVIGABLE;
        }
        #endregion

        /// <summary>
        /// Calculates edge costs and navigability between a hex and it's neighbours in all 6 directions.
        /// </summary>
        /// <param name="edgesData">Edges data. Hex types must already be written into it (see <see cref="TileGroupsGenerator"/>).</param>
        public void Generate(ref byte[] edgesData)
        {
            if (edgesData.Length != _mapHexFile.Capacity * 8)
            {
                // Edges data must be allocated by the caller (8 bytes per hex).
                return;
            }

            hexData = new HexData((int)_mapHexFile.Capacity);

            int width  = (int)_mapHexFile.MapWidth;
            int height = (int)_mapHexFile.MapHeight;

            var nbrData = new int[_mapHexFile.Capacity * 6];
            for (int hexIndex = 0; hexIndex < _mapHexFile.Capacity; ++hexIndex)
            {
                var hex = _mapHexFile.HexData[hexIndex];

                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    nbrData[hexIndex * 6 + dir] = HexGridUtility.GetNeighbourIndexFast(hex, dir, width, height);
                }

                hexData.PackData(hex);
            }

            for (int hexIndex = 0; hexIndex < _mapHexFile.Capacity; ++hexIndex)
            {
                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    int nbrIndex = nbrData[hexIndex * 6 + dir];
                    if (nbrIndex == -1)
                        continue;

                    byte costIndex   = CalculateEdgeCost(hexIndex, nbrIndex);
                    byte isNavigable = CalculateNavigability(hexIndex, nbrIndex);

                    byte value = (byte)((isNavigable << 7) | costIndex);
                    edgesData[hexIndex * 8 + dir] = value;
                }
            }

#if DEBUG
            var edge_costs   = new byte[_mapHexFile.Capacity * 6];
            var is_navigable = new byte[_mapHexFile.Capacity * 6];

            for (int hexIndex = 0; hexIndex < _mapHexFile.Capacity; ++hexIndex)
            {
                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    byte value = edgesData[hexIndex * 8 + dir];

                    edge_costs[hexIndex * 6 + dir]      = (byte)(value & 0b01111111);
                    is_navigable[hexIndex * 6 + dir]    = (byte)(value & 0b10000000);
                }
            }

            Utility.ArrayToRaw(_debugPath, "debug_is_navigable", is_navigable);
            Utility.ArrayToRaw(_debugPath, "debug_edge_costs", edge_costs);
#endif
        }

        public List<ushort> GetMoveCosts() => moveCosts;
    }
}
