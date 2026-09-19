using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CAIME.Pathfinding.Heuristic
{
    /// <summary>
    /// Builds the HEURISTIC_CACHE section of pathfinding.ppd: for every ordered pair of tile groups, the
    /// cheapest best-case cost of walking out of one and arriving anywhere in the other. The game uses it
    /// as the admissible heuristic behind campaign pathfinding, so every number here is a deliberate
    /// underestimate of what a real route costs.
    /// </summary>
    internal sealed class HeuristicCacheGenerator
    {
        private const uint Unreachable = 0x7FFFFFFF;

        private readonly MapHexFile _mapHexFile;
        private readonly string     _debugPath;

        public HeuristicCacheGenerator(MapHexFile mapHexFile, string debugPath)
        {
            _mapHexFile = mapHexFile;
            _debugPath  = debugPath;
        }

        /// <summary>
        /// Produces the tile-group distance matrix in row-major order, so the distance from
        /// <c>source</c> to <c>destination</c> lands at <c>source * count + destination</c>. Every input
        /// is read-only; <paramref name="moveCosts"/> is taken for uniformity with the other pathfinding
        /// generators and has no effect on the result.
        /// </summary>
        public void Generate(
            DatabaseViewModel        db,
            out uint[]               heuristicCacheData,
            byte[]                   edgesData,
            in List<ushort>          moveCosts,
            in List<TileGroupItem>   tileGroupsData,
            BridgesData              bridgesData)
        {
            int tileGroupsCount = tileGroupsData.Count;

            var costTable = new BestCaseCostTable(_mapHexFile, db);
            var grid      = new SearchGrid(_mapHexFile, edgesData);
            var stepCosts = new StepCostGrid(_mapHexFile, grid, costTable);
            var borders   = new TileGroupBorders(_mapHexFile, grid, tileGroupsCount);
            var crossings = new BridgeCrossings(_mapHexFile, bridgesData);

            heuristicCacheData = UnreachableMatrix(tileGroupsCount);

            SearchEveryTileGroup(grid, stepCosts, borders, crossings, heuristicCacheData, tileGroupsCount);

            WriteEdgeCostDump(stepCosts);
        }

        private static uint[] UnreachableMatrix(int tileGroupsCount)
        {
            var matrix = new uint[tileGroupsCount * tileGroupsCount];

            for (int cell = 0; cell < matrix.Length; ++cell)
                matrix[cell] = Unreachable;

            return matrix;
        }

        /// <summary>
        /// Runs one search per source tile group. The rows are independent and their costs vary by more
        /// than an order of magnitude, so workers take the next source one at a time rather than being
        /// handed equal blocks, and one core is left to the rest of the application.
        /// </summary>
        private static void SearchEveryTileGroup(
            SearchGrid       grid,
            StepCostGrid     stepCosts,
            TileGroupBorders borders,
            BridgeCrossings  crossings,
            uint[]           matrix,
            int              tileGroupsCount)
        {
            if (tileGroupsCount == 0)
                return;

            int workerCount       = Math.Min(Math.Max(Environment.ProcessorCount - 1, 1), tileGroupsCount);
            var unreachableLabels = UnreachableLabels(grid.Capacity);
            var workers           = new Task[workerCount];
            int nextTileGroup     = -1;

            for (int worker = 0; worker < workerCount; ++worker)
            {
                workers[worker] = Task.Run(() =>
                {
                    var search = new TileGroupDistanceSearch(grid, stepCosts, borders, crossings, unreachableLabels);

                    int sourceTileGroup;
                    while ((sourceTileGroup = Interlocked.Increment(ref nextTileGroup)) < tileGroupsCount)
                        search.Run(sourceTileGroup, matrix, sourceTileGroup * tileGroupsCount);
                });
            }

            Task.WaitAll(workers);
        }

        private static uint[] UnreachableLabels(int capacity)
        {
            var labels = new uint[capacity];

            for (int hexIndex = 0; hexIndex < labels.Length; ++hexIndex)
                labels[hexIndex] = SearchLabel.Unreachable;

            return labels;
        }

        /// <summary>
        /// Dumps the up-direction step cost of every hex, so the grid can be opened as a greyscale image
        /// alongside the other pipeline diagnostics. It is not part of pathfinding.ppd.
        /// </summary>
        [Conditional("DEBUG")]
        private void WriteEdgeCostDump(StepCostGrid stepCosts)
        {
            if (string.IsNullOrEmpty(_debugPath))
                return;

            var dump = new byte[_mapHexFile.Capacity * sizeof(ushort)];

            for (int hexIndex = 0; hexIndex < _mapHexFile.Capacity; ++hexIndex)
            {
                ushort cost = stepCosts.Costs[hexIndex * stepCosts.Stride];

                dump[hexIndex * 2]     = (byte)cost;
                dump[hexIndex * 2 + 1] = (byte)(cost >> 8);
            }

            Utility.ArrayToRaw(_debugPath, "debug_heuristic_cache_costs", dump);
        }
    }
}
