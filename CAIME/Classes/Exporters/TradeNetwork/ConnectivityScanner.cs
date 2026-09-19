using System;
using System.Collections.Generic;

namespace CAIME.TradeNetwork
{
    /// <summary>
    /// Derives the trade network's connectivity from the painted map in two index-order passes: one
    /// to find every region's settlement, then one that registers route endpoints, land borders, sea
    /// bodies and bridge crossings.
    /// </summary>
    internal sealed class ConnectivityScanner
    {
        private readonly TradeGrid        _grid;
        private readonly bool[]           _hasSettlement;
        private readonly List<int>[]      _endpoints;
        private readonly List<int>[]      _landBorders;
        private readonly List<List<int>>  _seaBodyRegions;

        private readonly int[]            _seaBodyOf;
        private int[]                     _floodStack = new int[1024];
        private int                       _floodStackCount;

        private readonly bool[]           _bridgeVisited;
        private readonly List<int>        _bridgeGroup   = new List<int>();
        private readonly List<int>        _bridgeRegions = new List<int>();

        // Shared on purpose. Section 5.4 of the specification requires the coastal-port walk and the
        // sea flood fill to use the same six-entry buffer: a fill triggered part-way through the walk
        // overwrites it, so the walk's remaining iterations read the neighbours of the last hex the
        // fill popped. Giving either one a buffer of its own changes which region/sea-body pairs are
        // recorded, and with them the routes in the output file.
        private readonly int[]            _neighbours = new int[TradeGrid.DirectionCount];

        public ConnectivityScanner(TradeGrid grid, int regionSlotCount)
        {
            _grid           = grid;
            _hasSettlement  = new bool[regionSlotCount];
            _endpoints      = new List<int>[regionSlotCount];
            _landBorders    = new List<int>[regionSlotCount];
            _seaBodyRegions = new List<List<int>>();
            _seaBodyOf      = new int[grid.Count];
            _bridgeVisited  = new bool[grid.Count];

            for (int slot = 0; slot < regionSlotCount; ++slot)
            {
                _endpoints[slot]   = new List<int>();
                _landBorders[slot] = new List<int>();
            }

            for (int index = 0; index < _seaBodyOf.Length; ++index)
            {
                _seaBodyOf[index] = -1;
            }
        }

        public TradeConnectivity Scan()
        {
            FindSettlements();
            ScanHexes();
            SortAdjacency();

            return new TradeConnectivity(_hasSettlement, FlattenEndpoints(), _landBorders, _seaBodyRegions);
        }

        /// <summary>
        /// Section 5.1. Land and sea status is deliberately not checked, so a main slot painted on a
        /// sea hex marks that hex's region - a sea region index - as settled.
        /// </summary>
        private void FindSettlements()
        {
            var townSlot = _grid.TownSlot;
            var region   = _grid.RegionIndex;

            for (int index = 0; index < _grid.Count; ++index)
            {
                if (townSlot[index] == TradeGrid.MainSlot)
                {
                    _hasSettlement[region[index] + 1] = true;
                }
            }
        }

        private void ScanHexes()
        {
            for (int index = 0; index < _grid.Count; ++index)
            {
                RegisterEndpoints(index);
                RegisterDomain(index);

                if (_grid.IsBridge[index] && !_bridgeVisited[index])
                {
                    RegisterBridgeGroup(index);
                }
            }
        }

        /// <summary>Section 5.2 (a).</summary>
        private void RegisterEndpoints(int index)
        {
            if (_grid.TradeMask[index] == 0)
                return;

            bool isSea       = _grid.IsSea[index];
            bool isLandStart = _grid.IsInland[index] && _grid.TownSlot[index] == TradeGrid.MainSlot;
            bool isSeaStart  = isSea && _grid.TownSlot[index] == TradeGrid.PortSlot;

            if (!isLandStart && !isSeaStart)
                return;

            int   owner    = isLandStart ? _grid.RegionIndex[index] : OwningPortRegion(index);
            sbyte excluded = isLandStart ? TradeGrid.MainSlot : TradeGrid.PortSlot;
            var   list     = _endpoints[owner + 1];

            for (int direction = 0; direction < TradeGrid.DirectionCount; ++direction)
            {
                int neighbour = _grid.Neighbour(index, direction);
                if (neighbour < 0)
                    continue;

                if (_grid.TownSlot[neighbour] == excluded)
                    continue;

                if (!_grid.TradeEdgesMeet(index, neighbour))
                    continue;

                if (!list.Contains(neighbour))
                {
                    list.Add(neighbour);
                }
            }
        }

        /// <summary>
        /// The region a sea port hex belongs to. The loop does not stop early, so when several land
        /// neighbours carry the port slot the last one in direction order wins.
        /// </summary>
        private int OwningPortRegion(int index)
        {
            int owner = TradeGrid.InvalidRegion;

            for (int direction = 0; direction < TradeGrid.DirectionCount; ++direction)
            {
                int neighbour = _grid.Neighbour(index, direction);
                if (neighbour < 0)
                    continue;

                if (_grid.TownSlot[neighbour] == TradeGrid.PortSlot && !_grid.IsSea[neighbour])
                {
                    owner = _grid.RegionIndex[neighbour];
                }
            }

            return owner;
        }

        /// <summary>Section 5.2 (b). The three branches are mutually exclusive and ordered.</summary>
        private void RegisterDomain(int index)
        {
            int region = _grid.RegionIndex[index];

            if (_grid.IsInland[index] && _hasSettlement[region + 1])
            {
                if (_grid.IsPassable[index])
                {
                    RegisterLandBorders(index, region);
                }
            }
            else if (_grid.IsSea[index])
            {
                if (_seaBodyOf[index] < 0)
                {
                    FloodFillSeaBody(index);
                }
            }
            else if (_grid.TownSlot[index] == TradeGrid.PortSlot)
            {
                RegisterSeaBorders(index, region);
            }
        }

        private void RegisterLandBorders(int index, int region)
        {
            for (int direction = 0; direction < TradeGrid.DirectionCount; ++direction)
            {
                int neighbour = _grid.Neighbour(index, direction);
                if (neighbour < 0 || _grid.IsSea[neighbour] || !_grid.IsPassable[neighbour])
                    continue;

                int neighbourRegion = _grid.RegionIndex[neighbour];
                if (neighbourRegion != region && _hasSettlement[neighbourRegion + 1])
                {
                    AddLandBorder(region, neighbourRegion);
                }
            }
        }

        /// <summary>Section 5.4.</summary>
        private void RegisterSeaBorders(int index, int region)
        {
            _grid.FillNeighbours(index, _neighbours);

            for (int direction = 0; direction < TradeGrid.DirectionCount; ++direction)
            {
                int neighbour = _neighbours[direction];
                if (neighbour < 0 || !_grid.IsSea[neighbour])
                    continue;

                if (_seaBodyOf[neighbour] < 0)
                {
                    FloodFillSeaBody(neighbour);
                }

                _seaBodyRegions[_seaBodyOf[neighbour]].Add(region);
            }
        }

        /// <summary>
        /// Section 5.3. The stack is last-in-first-out and hexes are claimed when popped, not when
        /// pushed, so a hex can sit on the stack twice. Which hex is popped last is observable
        /// through the buffer this fill shares with <see cref="RegisterSeaBorders"/>.
        /// </summary>
        private void FloodFillSeaBody(int seed)
        {
            int seaBody = _seaBodyRegions.Count;
            _seaBodyRegions.Add(new List<int>());

            _floodStackCount = 0;
            Push(seed);

            while (_floodStackCount > 0)
            {
                int hex = _floodStack[--_floodStackCount];
                _seaBodyOf[hex] = seaBody;

                _grid.FillNeighbours(hex, _neighbours);

                for (int direction = 0; direction < TradeGrid.DirectionCount; ++direction)
                {
                    int neighbour = _neighbours[direction];
                    if (neighbour >= 0 && _grid.IsSea[neighbour] && _seaBodyOf[neighbour] < 0)
                    {
                        Push(neighbour);
                    }
                }
            }
        }

        private void Push(int hex)
        {
            if (_floodStackCount == _floodStack.Length)
            {
                Array.Resize(ref _floodStack, _floodStack.Length * 2);
            }

            _floodStack[_floodStackCount++] = hex;
        }

        /// <summary>
        /// Section 5.5. The bordering regions are collected with no settlement check and no validity
        /// check, so an invalid region index reaches the border list like any other.
        /// </summary>
        private void RegisterBridgeGroup(int seed)
        {
            _bridgeGroup.Clear();
            _bridgeRegions.Clear();

            _bridgeGroup.Add(seed);
            _bridgeVisited[seed] = true;

            for (int position = 0; position < _bridgeGroup.Count; ++position)
            {
                int hex = _bridgeGroup[position];

                for (int direction = 0; direction < TradeGrid.DirectionCount; ++direction)
                {
                    int neighbour = _grid.Neighbour(hex, direction);
                    if (neighbour < 0)
                        continue;

                    if (_grid.IsBridge[neighbour] && !_bridgeVisited[neighbour])
                    {
                        _bridgeVisited[neighbour] = true;
                        _bridgeGroup.Add(neighbour);
                    }

                    if (!_grid.IsSea[neighbour])
                    {
                        int region = _grid.RegionIndex[neighbour];
                        if (!_bridgeRegions.Contains(region))
                        {
                            _bridgeRegions.Add(region);
                        }
                    }
                }
            }

            for (int i = 0; i < _bridgeRegions.Count; ++i)
            {
                for (int j = i + 1; j < _bridgeRegions.Count; ++j)
                {
                    AddLandBorder(_bridgeRegions[i], _bridgeRegions[j]);
                }
            }
        }

        private void AddLandBorder(int first, int second)
        {
            int key   = first < second ? first : second;
            int value = first < second ? second : first;

            _landBorders[key + 1].Add(value);
        }

        private void SortAdjacency()
        {
            for (int slot = 0; slot < _landBorders.Length; ++slot)
            {
                SortDistinct(_landBorders[slot]);
            }

            for (int seaBody = 0; seaBody < _seaBodyRegions.Count; ++seaBody)
            {
                SortDistinct(_seaBodyRegions[seaBody]);
            }
        }

        private int[][] FlattenEndpoints()
        {
            var flattened = new int[_endpoints.Length][];

            for (int slot = 0; slot < _endpoints.Length; ++slot)
            {
                flattened[slot] = _endpoints[slot].ToArray();
            }

            return flattened;
        }

        private static void SortDistinct(List<int> values)
        {
            if (values.Count <= 1)
                return;

            values.Sort();

            int kept = 1;
            for (int i = 1; i < values.Count; ++i)
            {
                if (values[i] != values[kept - 1])
                {
                    values[kept++] = values[i];
                }
            }

            values.RemoveRange(kept, values.Count - kept);
        }
    }
}
