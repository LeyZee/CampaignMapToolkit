namespace CAIME.TradeNetwork
{
    /// <summary>One region pair the search is asked to connect, and the shortest path it found.</summary>
    internal sealed class RouteCandidate
    {
        public readonly int   DestinationRegion;
        public readonly int[] DestinationEndpoints;

        /// <summary>The shortest path found so far, or null while the pair is still unreachable.</summary>
        public int[] Path;

        public RouteCandidate(int destinationRegion, int[] destinationEndpoints)
        {
            DestinationRegion    = destinationRegion;
            DestinationEndpoints = destinationEndpoints;
        }
    }

    /// <summary>A resolved trade connection: a region pair and the hexes the line runs through.</summary>
    internal sealed class TradeRoute
    {
        public readonly int   SourceRegion;
        public readonly int   DestinationRegion;
        /// <summary>The traced path, or null when the pair is connected but no path could be found.</summary>
        public readonly int[] Path;

        public TradeRoute(int sourceRegion, int destinationRegion, int[] path)
        {
            SourceRegion      = sourceRegion;
            DestinationRegion = destinationRegion;
            Path              = path;
        }
    }
}
