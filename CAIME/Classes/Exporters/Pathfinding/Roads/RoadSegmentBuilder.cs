using System.Collections.Generic;

namespace CAIME.Pathfinding.Roads
{
    /// <summary>
    /// Turns labelled half-edges into the road segments the exporter writes: one segment per pair set
    /// that any half-edge actually carries, each holding the hexes that stretch of road passes through
    /// with the edges it owns merged into a single mask.
    /// </summary>
    internal sealed class RoadSegmentBuilder
    {
        private readonly Hex[] _hexes;
        private readonly int   _mapWidth;
        private readonly int   _mapHeight;

        public RoadSegmentBuilder(MapHexFile map)
        {
            _hexes     = map.HexData;
            _mapWidth  = (int)map.MapWidth;
            _mapHeight = (int)map.MapHeight;
        }

        public RoadSegment[] Build(SharedRouteLabels labels)
        {
            var roads = RoadPerPairSet(labels);

            var segments = new List<RoadSegment>();
            for (int pairSet = RegionPairSets.Empty + 1; pairSet < roads.Length; ++pairSet)
            {
                if (roads[pairSet] == null)
                    continue;

                segments.Add(new RoadSegment
                {
                    RouteGroup = labels.RouteGroupOf(pairSet),
                    Road       = roads[pairSet]
                });
            }

            return segments.ToArray();
        }

        /// <summary>
        /// Both sides of every labelled edge are contributed, the owning hex first, so a segment's hexes
        /// end up in the order the ascending half-edge sweep first reached them. Pair sets nothing points
        /// at are left null and drop out.
        /// </summary>
        private Road[] RoadPerPairSet(SharedRouteLabels labels)
        {
            var roads             = new Road[labels.PairSetCount];
            var pairSetOfHalfEdge = labels.PairSetOfHalfEdge;

            int halfEdge = 0;
            for (int hexIndex = 0; hexIndex < _hexes.Length; ++hexIndex)
            {
                var hex = _hexes[hexIndex];
                for (int direction = 0; direction < HalfEdge.DirectionsPerHex; ++direction, ++halfEdge)
                {
                    int pairSet = pairSetOfHalfEdge[halfEdge];
                    if (pairSet == RegionPairSets.Empty)
                        continue;

                    var road = roads[pairSet] ?? (roads[pairSet] = new Road());
                    road.AddOrUpdateHex(hexIndex, (sbyte)(1 << direction));

                    int neighbourHexIndex = HexGridUtility.GetNeighbourIndexFast(hex, (ushort)direction, _mapWidth, _mapHeight);
                    road.AddOrUpdateHex(neighbourHexIndex, (sbyte)(1 << HalfEdge.Opposite(direction)));
                }
            }

            return roads;
        }
    }
}
