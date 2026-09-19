using System.Collections.Generic;
using System.Threading.Tasks;

namespace CAIME.TradeNetwork
{
    /// <summary>The two route tables the output file carries, each already in its written order.</summary>
    internal sealed class TradeRouteTables
    {
        public readonly IReadOnlyList<TradeRoute> LandRoutes;
        public readonly IReadOnlyList<TradeRoute> SeaRoutes;

        public TradeRouteTables(IReadOnlyList<TradeRoute> landRoutes, IReadOnlyList<TradeRoute> seaRoutes)
        {
            LandRoutes = landRoutes;
            SeaRoutes  = seaRoutes;
        }
    }

    /// <summary>
    /// Decides which region pairs to attempt, runs a search for each source and lays the results out
    /// in the order the file wants them: every pair that resolved, then every pair that did not.
    /// </summary>
    internal sealed class RoutePlanner
    {
        private sealed class SourceRequest
        {
            public readonly RouteDomain      Domain;
            public readonly int              SourceRegion;
            public readonly RouteCandidate[] Candidates;

            public SourceRequest(RouteDomain domain, int sourceRegion, RouteCandidate[] candidates)
            {
                Domain       = domain;
                SourceRegion = sourceRegion;
                Candidates   = candidates;
            }
        }

        private readonly TradeGrid         _grid;
        private readonly TradeConnectivity _connectivity;

        public RoutePlanner(TradeGrid grid, TradeConnectivity connectivity)
        {
            _grid         = grid;
            _connectivity = connectivity;
        }

        public TradeRouteTables Plan()
        {
            var landRequests = BuildLandRequests();
            var seaRequests  = BuildSeaRequests();

            var all = new List<SourceRequest>(landRequests.Count + seaRequests.Count);
            all.AddRange(landRequests);
            all.AddRange(seaRequests);

            Search(all);

            return new TradeRouteTables(Collect(landRequests), Collect(seaRequests));
        }

        private List<SourceRequest> BuildLandRequests()
        {
            var requests = new List<SourceRequest>();

            for (int slot = 0; slot < _connectivity.RegionSlotCount; ++slot)
            {
                int source       = slot - 1;
                var destinations = _connectivity.LandBorders(source);
                if (destinations.Count == 0)
                    continue;

                requests.Add(BuildRequest(RouteDomain.Land, source, destinations, 0));
            }

            return requests;
        }

        private List<SourceRequest> BuildSeaRequests()
        {
            var requests = new List<SourceRequest>();

            for (int seaBody = 0; seaBody < _connectivity.SeaBodyCount; ++seaBody)
            {
                var regions = _connectivity.SeaBodyRegions(seaBody);

                for (int i = 0; i + 1 < regions.Count; ++i)
                {
                    requests.Add(BuildRequest(RouteDomain.Sea, regions[i], regions, i + 1));
                }
            }

            return requests;
        }

        private SourceRequest BuildRequest(RouteDomain domain, int source, IReadOnlyList<int> destinations, int from)
        {
            var candidates = new RouteCandidate[destinations.Count - from];

            for (int i = 0; i < candidates.Length; ++i)
            {
                int destination = destinations[from + i];
                candidates[i]   = new RouteCandidate(destination, _connectivity.Endpoints(destination));
            }

            return new SourceRequest(domain, source, candidates);
        }

        /// <summary>
        /// Sources are independent of one another, so they are searched in parallel. Nothing
        /// order-dependent happens here: the results are laid out afterwards, in canonical order.
        /// </summary>
        private void Search(List<SourceRequest> requests)
        {
            Parallel.For(0, requests.Count,
                () => new PathSearcher(_grid),
                (index, state, searcher) =>
                {
                    var request = requests[index];
                    searcher.Resolve(request.Domain, _connectivity.Endpoints(request.SourceRegion), request.Candidates);
                    return searcher;
                },
                searcher => { });
        }

        /// <summary>
        /// Unreachable pairs are appended after every resolved route of the table, not after the
        /// resolved routes of their own source.
        /// </summary>
        private static List<TradeRoute> Collect(List<SourceRequest> requests)
        {
            var resolved    = new List<TradeRoute>();
            var unreachable = new List<TradeRoute>();

            for (int i = 0; i < requests.Count; ++i)
            {
                var request = requests[i];

                for (int c = 0; c < request.Candidates.Length; ++c)
                {
                    var candidate = request.Candidates[c];
                    var route     = new TradeRoute(request.SourceRegion, candidate.DestinationRegion, candidate.Path);

                    if (candidate.Path != null)
                    {
                        resolved.Add(route);
                    }
                    else
                    {
                        unreachable.Add(route);
                    }
                }
            }

            resolved.AddRange(unreachable);
            return resolved;
        }
    }
}
