using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace CAIME.Pathfinding.Heuristic
{
    /// <summary>
    /// The best-case price of every single step on the map: one cost per hex and direction, decided by
    /// the hex being stepped onto. It is directional, and <see cref="NoStep"/> marks a step movement
    /// may not take at all.
    /// </summary>
    internal sealed class StepCostGrid
    {
        public const ushort NoStep = 65535;

        private const ushort OpenGroundStepCost   = 325;
        private const ushort MinimumRiverStepCost = 65;
        private const ushort TownSprawlStepCost   = 0;

        public StepCostGrid(MapHexFile mapHexFile, SearchGrid grid, BestCaseCostTable costTable)
        {
            Stride = grid.Stride;
            Costs  = new ushort[grid.Capacity * Stride];

            Build(mapHexFile, grid, costTable);
        }

        public int Stride { get; }

        /// <summary>Step costs, <see cref="Stride"/> per hex in direction order.</summary>
        public ushort[] Costs { get; }

        private void Build(MapHexFile mapHexFile, SearchGrid grid, BestCaseCostTable costTable)
        {
            var hexes           = mapHexFile.HexData;
            var neighbours      = grid.Neighbours;
            var groundCosts     = GroundCostPerHex(mapHexFile, costTable);
            int stride          = Stride;
            ushort roadStepCost = costTable.MaxRoadMoveCost;

            Parallel.ForEach(Partitioner.Create(0, grid.Capacity), range =>
            {
                for (int hexIndex = range.Item1; hexIndex < range.Item2; ++hexIndex)
                {
                    int stepBase = hexIndex * stride;

                    for (int direction = 0; direction < stride; ++direction)
                        Costs[stepBase + direction] = NoStep;

                    var hex = hexes[hexIndex];
                    if (!CanBeStoodOn(hex))
                        continue;

                    for (int direction = 0; direction < stride; ++direction)
                    {
                        int neighbourIndex = neighbours[stepBase + direction];
                        if (neighbourIndex < 0)
                            continue;

                        var neighbour = hexes[neighbourIndex];
                        if (!CanBeStoodOn(neighbour))
                            continue;

                        Costs[stepBase + direction] =
                            StepCostOnto(neighbour, groundCosts[hexIndex], groundCosts[neighbourIndex], roadStepCost);
                    }
                }
            });
        }

        private static bool CanBeStoodOn(Hex hex)
        {
            return hex.IsPassable && (!hex.IsCliff || hex.IsBridgeCliff);
        }

        private static ushort StepCostOnto(Hex neighbour, ushort hexGroundCost, ushort neighbourGroundCost, ushort roadStepCost)
        {
            if (neighbour.IsTownSprawl)
                return TownSprawlStepCost;

            if (neighbour.IsRoad)
                return roadStepCost;

            if (neighbour.IsRiver)
            {
                int crossing = (hexGroundCost + neighbourGroundCost + 1) / 2;
                return crossing > MinimumRiverStepCost ? (ushort)crossing : MinimumRiverStepCost;
            }

            return OpenGroundStepCost;
        }

        private static ushort[] GroundCostPerHex(MapHexFile mapHexFile, BestCaseCostTable costTable)
        {
            var hexes       = mapHexFile.HexData;
            var groundCosts = new ushort[mapHexFile.Capacity];

            for (int hexIndex = 0; hexIndex < groundCosts.Length; ++hexIndex)
                groundCosts[hexIndex] = costTable.GroundCost(hexes[hexIndex].GroundTypeIndex);

            return groundCosts;
        }
    }
}
