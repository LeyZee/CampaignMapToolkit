namespace CAIME.TradeNetwork
{
    /// <summary>
    /// The only thing that differs between land and sea routing: which hexes a route may run over,
    /// and which hexes end a search. Everything else - the endpoint lists, the search, the
    /// segmentation and the curve fitting - is shared.
    /// </summary>
    internal readonly struct RouteDomain
    {
        public static readonly RouteDomain Land = new RouteDomain(true, TradeGrid.MainSlot);
        public static readonly RouteDomain Sea  = new RouteDomain(false, TradeGrid.PortSlot);

        public readonly bool  IsLand;
        private readonly sbyte _terminatingSlot;

        private RouteDomain(bool isLand, sbyte terminatingSlot)
        {
            IsLand           = isLand;
            _terminatingSlot = terminatingSlot;
        }

        /// <summary>Land routes run over inland hexes, sea routes over sea hexes.</summary>
        public bool Contains(TradeGrid grid, int hex)
        {
            return IsLand ? grid.IsInland[hex] : grid.IsSea[hex];
        }

        /// <summary>
        /// A path whose tail satisfies this has gone too far: it has reached the settlement or port
        /// itself, or left its domain. Bridges are the one water hex a land route may stand on.
        /// </summary>
        public bool Terminates(TradeGrid grid, int hex)
        {
            if (grid.TownSlot[hex] == _terminatingSlot)
                return true;

            return IsLand ? grid.IsSea[hex] && !grid.IsBridge[hex]
                          : grid.IsInland[hex];
        }
    }
}
