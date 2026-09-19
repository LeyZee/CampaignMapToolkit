using System.Collections.Generic;

namespace CAIME.TradeNetwork
{
    /// <summary>
    /// One shareable stretch of a route, expressed as positions in the route's path. A span runs
    /// from <see cref="StartIndex"/> to <see cref="EndIndex"/>; a corner turns through the three
    /// hexes those two indices bracket.
    /// </summary>
    internal readonly struct RoutePiece
    {
        public readonly int  StartIndex;
        public readonly int  EndIndex;
        public readonly bool IsCorner;
        public readonly bool IsLand;
        public readonly bool IsLandBridge;
        public readonly bool TerminatesAtStart;
        public readonly bool TerminatesAtEnd;

        public RoutePiece(int startIndex, int endIndex, bool isCorner, bool isLand, bool isLandBridge,
                          bool terminatesAtStart, bool terminatesAtEnd)
        {
            StartIndex        = startIndex;
            EndIndex          = endIndex;
            IsCorner          = isCorner;
            IsLand            = isLand;
            IsLandBridge      = isLandBridge;
            TerminatesAtStart = terminatesAtStart;
            TerminatesAtEnd   = terminatesAtEnd;
        }
    }

    /// <summary>
    /// Cuts a route's path into the pieces other routes can share: straight spans, and corners where
    /// the line turns at a junction or crosses the waterline.
    /// </summary>
    internal sealed class PathSegmenter
    {
        private readonly TradeGrid _grid;

        public PathSegmenter(TradeGrid grid)
        {
            _grid = grid;
        }

        public void Segment(int[] path, RouteDomain domain, List<RoutePiece> pieces)
        {
            pieces.Clear();

            int  last           = path.Length - 1;
            int  spanStart      = 0;
            bool nothingEmitted = true;
            bool water          = !domain.IsLand;

            for (int i = 0; i <= last; ++i)
            {
                // A hex counts as water if it is water, or if it is the last inland hex before water.
                bool nextIsWater = !_grid.IsInland[path[i]]
                                || (i != last && !_grid.IsInland[path[i + 1]]);

                if (IsJunction(path[i]) || nextIsWater != water)
                {
                    if (i > spanStart)
                    {
                        pieces.Add(new RoutePiece(spanStart, i - 1, false, domain.IsLand,
                                                  domain.IsLand && water, nothingEmitted, i == last));
                        nothingEmitted = false;
                    }

                    if (i != 0 && i != last)
                    {
                        pieces.Add(new RoutePiece(i - 1, i + 1, true, domain.IsLand,
                                                  domain.IsLand && water, nothingEmitted, false));
                        nothingEmitted = false;
                    }

                    spanStart = i + 1;
                }

                water = nextIsWater;
            }

            if (spanStart <= last)
            {
                pieces.Add(new RoutePiece(spanStart, last, false, domain.IsLand,
                                          domain.IsLand && water, nothingEmitted, true));
            }
        }

        private bool IsJunction(int hex)
        {
            return HexGridUtility.GetNumBitsSetInEdgeMask(_grid.TradeMask[hex]) >= 3;
        }
    }
}
