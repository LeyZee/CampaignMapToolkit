namespace CAIME.Pathfinding.Roads
{
    /// <summary>
    /// Produces the ROAD_SEGMENTS section of pathfinding.ppd from a prepared map: it traces every
    /// settlement-to-settlement route through the painted road network, labels each half-edge with the
    /// routes sharing it, and emits one segment per distinct label.
    ///
    /// <para>
    /// The map is read, never modified, and the road edge masks are taken exactly as authored - the
    /// export pipeline has already decided what they are.
    /// </para>
    /// </summary>
    internal sealed class RoadsGenerator
    {
        private readonly MapHexFile _map;

        public RoadsGenerator(MapHexFile mapHexFile, string debugPath)
        {
            _map = mapHexFile;
        }

        public void Generate(out RoadSegment[] roadSegments)
        {
            var routes = new TownRouteTracer(_map).Trace();
            var labels = new SharedRouteLabels(routes, HalfEdge.CountIn(_map));

            roadSegments = new RoadSegmentBuilder(_map).Build(labels);
        }
    }
}
