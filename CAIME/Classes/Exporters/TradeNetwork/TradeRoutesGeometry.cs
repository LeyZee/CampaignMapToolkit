using System;
using System.Collections.Generic;

namespace CAIME.TradeNetwork
{
    /// <summary>A route as the file stores it: a region pair and the splines to draw between them.</summary>
    internal sealed class SplineRoute
    {
        public readonly int    SourceRegion;
        public readonly int    DestinationRegion;
        public readonly uint[] SplineReferences;

        public SplineRoute(int sourceRegion, int destinationRegion, uint[] splineReferences)
        {
            SourceRegion      = sourceRegion;
            DestinationRegion = destinationRegion;
            SplineReferences  = splineReferences;
        }
    }

    /// <summary>Everything the writer needs beyond the region names.</summary>
    internal sealed class TradeRoutesGeometry
    {
        public readonly IReadOnlyList<Spline>      Splines;
        public readonly IReadOnlyList<SplineRoute> LandRoutes;
        public readonly IReadOnlyList<SplineRoute> SeaRoutes;

        public TradeRoutesGeometry(IReadOnlyList<Spline> splines, IReadOnlyList<SplineRoute> landRoutes,
                                   IReadOnlyList<SplineRoute> seaRoutes)
        {
            Splines    = splines;
            LandRoutes = landRoutes;
            SeaRoutes  = seaRoutes;
        }
    }

    /// <summary>
    /// Cuts every route into pieces and fits the ones the pool has not seen before. The pool's order
    /// is part of the output, so routes are processed strictly in the order the file writes them.
    /// </summary>
    internal sealed class TradeRoutesGeometryBuilder
    {
        private readonly PathSegmenter    _segmenter;
        private readonly CurveFitter      _fitter;
        private readonly SplinePool       _pool   = new SplinePool();
        private readonly List<RoutePiece> _pieces = new List<RoutePiece>();

        public TradeRoutesGeometryBuilder(TradeGrid grid)
        {
            _segmenter = new PathSegmenter(grid);
            _fitter    = new CurveFitter(grid);
        }

        public TradeRoutesGeometry Build(TradeRouteTables tables)
        {
            var landRoutes = BuildRoutes(tables.LandRoutes, RouteDomain.Land);
            var seaRoutes  = BuildRoutes(tables.SeaRoutes, RouteDomain.Sea);

            return new TradeRoutesGeometry(_pool.Splines, landRoutes, seaRoutes);
        }

        private List<SplineRoute> BuildRoutes(IReadOnlyList<TradeRoute> routes, RouteDomain domain)
        {
            var built = new List<SplineRoute>(routes.Count);

            for (int i = 0; i < routes.Count; ++i)
            {
                var route = routes[i];

                if (route.Path == null)
                {
                    built.Add(new SplineRoute(route.SourceRegion, route.DestinationRegion, Array.Empty<uint>()));
                    continue;
                }

                _segmenter.Segment(route.Path, domain, _pieces);

                var references = new uint[_pieces.Count];
                for (int p = 0; p < _pieces.Count; ++p)
                {
                    references[p] = ReferenceFor(route.Path, _pieces[p]);
                }

                built.Add(new SplineRoute(route.SourceRegion, route.DestinationRegion, references));
            }

            return built;
        }

        private uint ReferenceFor(int[] path, RoutePiece piece)
        {
            int firstHex = path[piece.StartIndex];
            int lastHex  = path[piece.EndIndex];

            int existing;
            if (_pool.TryFind(firstHex, lastHex, out existing))
                return _pool.Reference(existing, firstHex);

            return _pool.Add(firstHex, lastHex, piece, _fitter.Fit(path, piece));
        }
    }
}
