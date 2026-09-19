using System.Collections.Generic;
using CAIME.Models;

namespace CAIME.Pathfinding.Heuristic
{
    /// <summary>
    /// The database prices the best-case step costs are built from: what entering one hex of each
    /// ground type costs, and what the fastest road level costs.
    /// </summary>
    internal sealed class BestCaseCostTable
    {
        private const ushort MoveCostWithoutRoadTable = 50;

        private readonly ushort[] _costByGroundTypeIndex;

        public BestCaseCostTable(MapHexFile mapHexFile, DatabaseViewModel database)
        {
            _costByGroundTypeIndex = BuildGroundCosts(mapHexFile, database.CachedGroundTypes);
            MaxRoadMoveCost        = ReadMaxRoadMoveCost(database.CachedRoads);
        }

        public ushort MaxRoadMoveCost { get; }

        public ushort GroundCost(int groundTypeIndex)
        {
            return (uint)groundTypeIndex < (uint)_costByGroundTypeIndex.Length
                 ? _costByGroundTypeIndex[groundTypeIndex]
                 : (ushort)0;
        }

        private static ushort[] BuildGroundCosts(MapHexFile mapHexFile, List<DBGroundType> groundTypes)
        {
            var costByKey = new Dictionary<string, ushort>(groundTypes.Count);
            foreach (var groundType in groundTypes)
            {
                if (groundType.Key != null && !costByKey.ContainsKey(groundType.Key))
                    costByKey.Add(groundType.Key, (ushort)groundType.MoveCost);
            }

            var landTypes = mapHexFile.LandGroundTypes;
            var seaTypes  = mapHexFile.SeaGroundTypes;
            var costs     = new ushort[landTypes.Count + seaTypes.Count];

            for (int groundTypeIndex = 0; groundTypeIndex < landTypes.Count; ++groundTypeIndex)
                costByKey.TryGetValue(landTypes[groundTypeIndex], out costs[groundTypeIndex]);

            for (int groundTypeIndex = 0; groundTypeIndex < seaTypes.Count; ++groundTypeIndex)
                costByKey.TryGetValue(seaTypes[groundTypeIndex], out costs[landTypes.Count + groundTypeIndex]);

            return costs;
        }

        private static ushort ReadMaxRoadMoveCost(List<DBRoad> roads)
        {
            return roads.Count > 0 ? (ushort)roads[roads.Count - 1].MoveCost : MoveCostWithoutRoadTable;
        }
    }
}
