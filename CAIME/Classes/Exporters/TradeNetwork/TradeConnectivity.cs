using System.Collections.Generic;

namespace CAIME.TradeNetwork
{
    /// <summary>
    /// The trade network's connectivity, as derived from the painted map: which regions have a
    /// settlement, where a route search may start, which regions share a land border and which
    /// regions sit on the same sea body.
    /// <para>
    /// Every per-region array is indexed by <c>region + 1</c>, so the invalid region index -1 has a
    /// slot of its own instead of needing a separate case at every use.
    /// </para>
    /// </summary>
    internal sealed class TradeConnectivity
    {
        private readonly bool[]            _hasSettlement;
        private readonly int[][]           _endpoints;
        private readonly List<int>[]       _landBorders;
        private readonly List<List<int>>   _seaBodyRegions;

        public TradeConnectivity(bool[] hasSettlement, int[][] endpoints, List<int>[] landBorders,
                                 List<List<int>> seaBodyRegions)
        {
            _hasSettlement  = hasSettlement;
            _endpoints      = endpoints;
            _landBorders    = landBorders;
            _seaBodyRegions = seaBodyRegions;
        }

        public int RegionSlotCount
        {
            get { return _hasSettlement.Length; }
        }

        public int SeaBodyCount
        {
            get { return _seaBodyRegions.Count; }
        }

        public bool HasSettlement(int region)
        {
            return _hasSettlement[region + 1];
        }

        /// <summary>
        /// Hexes a search may start from for <paramref name="region"/>, in insertion order. Handed
        /// out as a flat array because the path search reads it once per partial path per candidate.
        /// </summary>
        public int[] Endpoints(int region)
        {
            return _endpoints[region + 1];
        }

        /// <summary>
        /// Regions sharing a land border with <paramref name="region"/>, ascending and distinct.
        /// A border is stored once, under the smaller of the two region indices.
        /// </summary>
        public IReadOnlyList<int> LandBorders(int region)
        {
            return _landBorders[region + 1];
        }

        /// <summary>Regions with a port on sea body <paramref name="seaBody"/>, ascending and distinct.</summary>
        public IReadOnlyList<int> SeaBodyRegions(int seaBody)
        {
            return _seaBodyRegions[seaBody];
        }
    }
}
