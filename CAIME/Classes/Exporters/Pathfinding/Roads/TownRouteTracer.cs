using System;
using System.Collections.Generic;

namespace CAIME.Pathfinding.Roads
{
    /// <summary>
    /// One settlement-to-settlement connection found in the painted road network: the region pair it
    /// joins and the chain of half-edges it runs along.
    /// </summary>
    internal readonly struct TracedRoute
    {
        public readonly RegionsPair RegionsPair;
        public readonly int[] HalfEdges;

        public TracedRoute(RegionsPair regionsPair, int[] halfEdges)
        {
            RegionsPair = regionsPair;
            HalfEdges   = halfEdges;
        }
    }

    /// <summary>
    /// Walks the painted road network outwards from every town, branching at every junction, and
    /// records a route whenever a walk reaches another town or runs out of road.
    /// </summary>
    internal sealed class TownRouteTracer
    {
        private struct WalkStep
        {
            public int DepartureHexIndex;
            public int Direction;
            public int EnteredHexIndex;
            public int EnteredSprawlRegion;
            public int UnexploredDirections;
        }

        private readonly Hex[] _hexes;
        private readonly int   _mapWidth;
        private readonly int   _mapHeight;

        private readonly List<WalkStep>    _chain                = new List<WalkStep>();
        private readonly int[]             _chainHalfEdgesPerHex;
        private readonly List<TracedRoute> _registered           = new List<TracedRoute>();

        private int _originRegion;

        public TownRouteTracer(MapHexFile map)
        {
            _hexes     = map.HexData;
            _mapWidth  = (int)map.MapWidth;
            _mapHeight = (int)map.MapHeight;

            _chainHalfEdgesPerHex = new int[_hexes.Length];
        }

        public TracedRoute[] Trace()
        {
            for (int hexIndex = 0; hexIndex < _hexes.Length; ++hexIndex)
            {
                var town = _hexes[hexIndex];
                if (!town.IsRoad || town.TownSlotIndex != Hex.MAIN_SLOT_INDEX)
                    continue;

                for (int direction = 0; direction < HalfEdge.DirectionsPerHex; ++direction)
                {
                    if ((town.RoadEdgeMask & (1 << direction)) != 0)
                        WalkOutOfTown(hexIndex, direction, town.RegionId);
                }
            }

            return InRequiredOrder(_registered);
        }

        private void WalkOutOfTown(int townHexIndex, int direction, int originRegion)
        {
            _originRegion = originRegion;
            Enter(townHexIndex, direction, originRegion);

            while (_chain.Count > 0)
            {
                int top  = _chain.Count - 1;
                var step = _chain[top];

                if (step.UnexploredDirections == 0)
                {
                    --_chainHalfEdgesPerHex[step.DepartureHexIndex];
                    _chain.RemoveAt(top);
                    continue;
                }

                int branch = LowestDirectionIn(step.UnexploredDirections);
                step.UnexploredDirections &= ~(1 << branch);
                _chain[top] = step;

                Enter(step.EnteredHexIndex, branch, step.EnteredSprawlRegion);
            }
        }

        /// <summary>
        /// Extends the chain by one half-edge and decides what the walk does next: it either stops,
        /// having registered a route or not, or leaves the entered hex by each of its remaining road
        /// directions in turn.
        /// </summary>
        private void Enter(int departureHexIndex, int direction, int departureSprawlRegion)
        {
            var departure           = _hexes[departureHexIndex];
            int enteredHexIndex     = HexGridUtility.GetNeighbourIndexFast(departure, (ushort)direction, _mapWidth, _mapHeight);
            var entered             = _hexes[enteredHexIndex];
            int enteredSprawlRegion = entered.IsTownSprawl ? entered.RegionId : Hex.INVALID_REGION_INDEX;

            _chain.Add(new WalkStep
            {
                DepartureHexIndex    = departureHexIndex,
                Direction            = direction,
                EnteredHexIndex      = enteredHexIndex,
                EnteredSprawlRegion  = enteredSprawlRegion,
                UnexploredDirections = 0
            });
            ++_chainHalfEdgesPerHex[departureHexIndex];

            if (LeavesTheSprawlItWanderedInto(departureSprawlRegion, enteredSprawlRegion))
                return;

            if (entered.TownSlotIndex == Hex.MAIN_SLOT_INDEX)
            {
                Register(_originRegion, entered.RegionId);
                return;
            }

            int onwardDirections = entered.RoadEdgeMask & ~(1 << HalfEdge.Opposite(direction));
            if (onwardDirections == 0)
            {
                if (_originRegion != Hex.INVALID_REGION_INDEX)
                    Register(_originRegion, _originRegion);

                return;
            }

            if (AlreadyOnThisWalk(enteredHexIndex))
                return;

            int top  = _chain.Count - 1;
            var step = _chain[top];
            step.UnexploredDirections = onwardDirections;
            _chain[top] = step;
        }

        /// <summary>
        /// Once a walk has wandered into a settlement's sprawl other than its own it may continue only
        /// while it stays in that same sprawl, because roads fanning out inside a settlement would
        /// otherwise connect towns that no road really joins.
        /// </summary>
        private bool LeavesTheSprawlItWanderedInto(int departureSprawlRegion, int enteredSprawlRegion)
        {
            return departureSprawlRegion != Hex.INVALID_REGION_INDEX
                && departureSprawlRegion != _originRegion
                && enteredSprawlRegion   != departureSprawlRegion;
        }

        private bool AlreadyOnThisWalk(int hexIndex)
        {
            return _chainHalfEdgesPerHex[hexIndex] > 0;
        }

        private void Register(int oneRegion, int otherRegion)
        {
            int source      = Math.Min(oneRegion, otherRegion);
            int destination = Math.Max(oneRegion, otherRegion);

            if (source == destination && !ChainRunsEntirelyThroughSprawl())
                return;

            _registered.Add(new TracedRoute(new RegionsPair(source, destination), ChainHalfEdges()));
        }

        private bool ChainRunsEntirelyThroughSprawl()
        {
            foreach (var step in _chain)
            {
                if (!_hexes[step.DepartureHexIndex].IsTownSprawl)
                    return false;
            }

            return true;
        }

        private int[] ChainHalfEdges()
        {
            var halfEdges = new int[_chain.Count];
            for (int i = 0; i < halfEdges.Length; ++i)
                halfEdges[i] = HalfEdge.Of(_chain[i].DepartureHexIndex, _chain[i].Direction);

            return halfEdges;
        }

        /// <summary>
        /// Routes come out ordered by region pair, and within one pair most-recently-registered first.
        /// The reversal looks arbitrary, but it is what puts the segments in the order the game's own
        /// pathfinding.ppd has them.
        /// </summary>
        private static TracedRoute[] InRequiredOrder(List<TracedRoute> registered)
        {
            var order = new int[registered.Count];
            for (int i = 0; i < order.Length; ++i)
                order[i] = i;

            Array.Sort(order, (left, right) =>
            {
                int byRegionPair = registered[left].RegionsPair.CompareTo(registered[right].RegionsPair);
                return byRegionPair != 0 ? byRegionPair : right.CompareTo(left);
            });

            var ordered = new TracedRoute[order.Length];
            for (int i = 0; i < ordered.Length; ++i)
                ordered[i] = registered[order[i]];

            return ordered;
        }

        private static int LowestDirectionIn(int directions)
        {
            int lowest = 0;
            while ((directions & 1) == 0)
            {
                directions >>= 1;
                ++lowest;
            }

            return lowest;
        }
    }
}
