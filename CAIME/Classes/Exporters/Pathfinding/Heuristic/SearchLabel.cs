namespace CAIME.Pathfinding.Heuristic
{
    /// <summary>Which media a route has travelled over. Land and sea combine into <see cref="Both"/>.</summary>
    internal static class TravelMedium
    {
        public const uint Land = 1;
        public const uint Sea  = 2;
        public const uint Both = Land | Sea;
    }

    /// <summary>
    /// The layout of the working label a search keeps per hex: the queued flag in the top bit, the media
    /// touched on the way here below it, and the cheapest distance found so far in the rest. Packing all
    /// three into one unsigned 32-bit value is what lets the frontier order whole labels with a single
    /// integer comparison, which is the ordering the output depends on.
    /// </summary>
    internal static class SearchLabel
    {
        public const uint Queued       = 1u << 31;
        public const int  MediumShift  = 28;
        public const uint DistanceMask = (1u << MediumShift) - 1;

        /// <summary>The distance a hex starts every search at, and the ceiling no recorded route reaches.</summary>
        public const uint Unreachable = 65535;
    }
}
