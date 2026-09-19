using System;
using System.Collections.Generic;

namespace CAIME.Pathfinding.Roads
{
    /// <summary>
    /// Labels every half-edge of the map with the set of region pairs whose routes run over it. Two
    /// half-edges carrying the same label belong to the same road segment, so these labels are what
    /// cuts the road network into segments.
    /// </summary>
    internal sealed class SharedRouteLabels
    {
        private readonly RegionPairSets _pairSets = new RegionPairSets();
        private readonly RegionsPair[]  _pairsByOrdinal;
        private readonly int[]          _pairSetOfHalfEdge;

        public SharedRouteLabels(TracedRoute[] routes, int halfEdgeCount)
        {
            _pairsByOrdinal    = DistinctPairsInAscendingOrder(routes);
            _pairSetOfHalfEdge = new int[halfEdgeCount];

            Label(routes, OrdinalOfEach(_pairsByOrdinal));
        }

        public int PairSetCount => _pairSets.Count;

        public int[] PairSetOfHalfEdge => _pairSetOfHalfEdge;

        public RouteGroup RouteGroupOf(int pairSet)
        {
            var group = new RouteGroup();
            foreach (int ordinal in _pairSets.PairsIn(pairSet))
                group.RegionPairs.Add(_pairsByOrdinal[ordinal]);

            return group;
        }

        private void Label(TracedRoute[] routes, Dictionary<RegionsPair, int> ordinals)
        {
            foreach (var route in routes)
            {
                int pair = ordinals[route.RegionsPair];
                foreach (int halfEdge in route.HalfEdges)
                    _pairSetOfHalfEdge[halfEdge] = _pairSets.Union(_pairSetOfHalfEdge[halfEdge], pair);
            }
        }

        /// <summary>
        /// Pairs are addressed by their rank in ascending order, which is what keeps every pair set
        /// sorted the way the segment's region pairs must be written.
        /// </summary>
        private static RegionsPair[] DistinctPairsInAscendingOrder(TracedRoute[] routes)
        {
            var distinct = new HashSet<RegionsPair>();
            foreach (var route in routes)
                distinct.Add(route.RegionsPair);

            var pairs = new RegionsPair[distinct.Count];
            distinct.CopyTo(pairs);
            Array.Sort(pairs);

            return pairs;
        }

        private static Dictionary<RegionsPair, int> OrdinalOfEach(RegionsPair[] pairs)
        {
            var ordinals = new Dictionary<RegionsPair, int>(pairs.Length);
            for (int ordinal = 0; ordinal < pairs.Length; ++ordinal)
                ordinals.Add(pairs[ordinal], ordinal);

            return ordinals;
        }
    }
}
