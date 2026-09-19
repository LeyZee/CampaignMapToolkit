using System;

namespace CAIME.Pathfinding.Heuristic
{
    /// <summary>
    /// One row of the heuristic cache: a cheapest-first flood that leaves a source tile group through all
    /// of its border hexes at once and records how cheaply it first reaches every other tile group.
    /// Rows never see one another, so a single instance can be reused for row after row.
    /// </summary>
    internal sealed class TileGroupDistanceSearch
    {
        private const uint BridgeCrossingCost = 500;

        private readonly TileGroupBorders _borders;
        private readonly int[]            _neighbours;
        private readonly ushort[]         _stepCosts;
        private readonly byte[]           _mediumOfHex;
        private readonly int[]            _tileGroupOfHex;
        private readonly int[][]          _bridgeLandings;
        private readonly int              _stride;
        private readonly uint[]           _unreachableLabels;
        private readonly uint[]           _labels;
        private readonly SearchFrontier   _frontier;

        public TileGroupDistanceSearch(
            SearchGrid       grid,
            StepCostGrid     stepCosts,
            TileGroupBorders borders,
            BridgeCrossings  crossings,
            uint[]           unreachableLabels)
        {
            _borders           = borders;
            _neighbours        = grid.Neighbours;
            _stepCosts         = stepCosts.Costs;
            _mediumOfHex       = grid.MediumOfHex;
            _tileGroupOfHex    = grid.TileGroupOfHex;
            _bridgeLandings    = crossings.OppositeSideByHex;
            _stride            = grid.Stride;
            _unreachableLabels = unreachableLabels;
            _labels            = new uint[grid.Capacity];
            _frontier          = new SearchFrontier(1024);
        }

        /// <summary>
        /// Fills <paramref name="matrix"/> from <paramref name="rowOffset"/> onwards with the distances
        /// out of <paramref name="sourceTileGroup"/>, leaving destinations it never reaches untouched.
        /// </summary>
        public void Run(int sourceTileGroup, uint[] matrix, int rowOffset)
        {
            var borderHexes = _borders.HexesOf(sourceTileGroup);
            if (borderHexes.Length == 0)
                return;

            Array.Copy(_unreachableLabels, _labels, _labels.Length);
            _frontier.Clear();

            var labels      = _labels;
            var neighbours  = _neighbours;
            var stepCosts   = _stepCosts;
            var mediumOfHex = _mediumOfHex;
            int stride      = _stride;

            foreach (int hexIndex in borderHexes)
            {
                uint label = (uint)mediumOfHex[hexIndex] << SearchLabel.MediumShift;
                labels[hexIndex] = label;
                _frontier.Add(label, hexIndex);
            }

            matrix[rowOffset + sourceTileGroup] = 0;

            uint initialMedium = _borders.InitialMediumOf(sourceTileGroup);

            while (_frontier.TryTake(out int currentHex))
            {
                uint currentLabel = labels[currentHex] & ~SearchLabel.Queued;
                labels[currentHex] = currentLabel;

                uint distance = currentLabel & SearchLabel.DistanceMask;
                uint medium   = currentLabel >> SearchLabel.MediumShift;
                int  stepBase = currentHex * stride;

                for (int direction = 0; direction < stride; ++direction)
                {
                    ushort stepCost = stepCosts[stepBase + direction];
                    if (stepCost == StepCostGrid.NoStep)
                        continue;

                    uint candidate      = distance + stepCost;
                    int  neighbourIndex = neighbours[stepBase + direction];
                    uint neighbourLabel = labels[neighbourIndex];

                    if (candidate >= (neighbourLabel & SearchLabel.DistanceMask))
                        continue;

                    // Once a route has touched land and sea it may not step back onto the medium it
                    // started from, or a land route could hop a strait and return to shortcut the coast.
                    uint neighbourMedium = mediumOfHex[neighbourIndex];
                    if (medium == TravelMedium.Both && neighbourMedium == initialMedium)
                        continue;

                    Reach(neighbourIndex, neighbourLabel, candidate, medium | neighbourMedium, matrix, rowOffset);
                }

                var landings = _bridgeLandings[currentHex];
                if (landings != null)
                    CrossBridge(landings, distance, medium, matrix, rowOffset);
            }
        }

        private void CrossBridge(int[] landings, uint distance, uint medium, uint[] matrix, int rowOffset)
        {
            uint candidate = distance + BridgeCrossingCost;

            foreach (int landingIndex in landings)
            {
                uint landingLabel = _labels[landingIndex];
                if (candidate >= (landingLabel & SearchLabel.DistanceMask))
                    continue;

                // A bridge carries the route over unchanged: neither the medium rule nor the far bank's
                // own medium applies to the crossing.
                Reach(landingIndex, landingLabel, candidate, medium, matrix, rowOffset);
            }
        }

        private void Reach(int hexIndex, uint previousLabel, uint distance, uint medium, uint[] matrix, int rowOffset)
        {
            uint label = (medium << SearchLabel.MediumShift) | distance | SearchLabel.Queued;
            _labels[hexIndex] = label;

            if ((previousLabel & SearchLabel.Queued) == 0)
                _frontier.Add(label, hexIndex);

            int destination = rowOffset + _tileGroupOfHex[hexIndex];
            if (distance < matrix[destination])
                matrix[destination] = distance;
        }
    }
}
