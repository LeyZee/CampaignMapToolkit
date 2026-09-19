using System.Collections.Generic;

namespace CAIME.TradeNetwork
{
    /// <summary>One shareable piece of drawable trade-route geometry.</summary>
    internal sealed class Spline
    {
        public readonly bool    IsLand;
        public readonly bool    IsLandBridge;
        public readonly bool    TerminatesAtStart;
        public readonly bool    TerminatesAtEnd;
        public readonly Curve[] Curves;

        public Spline(bool isLand, bool isLandBridge, bool terminatesAtStart, bool terminatesAtEnd, Curve[] curves)
        {
            IsLand            = isLand;
            IsLandBridge      = isLandBridge;
            TerminatesAtStart = terminatesAtStart;
            TerminatesAtEnd   = terminatesAtEnd;
            Curves            = curves;
        }
    }

    /// <summary>
    /// The pool every route draws from. A piece is identified by its pair of end hexes alone, so the
    /// stretch between two junctions is fitted once and referenced by every route that walks it, in
    /// either direction.
    /// </summary>
    internal sealed class SplinePool
    {
        private readonly Dictionary<long, int> _byEndHexes = new Dictionary<long, int>();
        private readonly List<Spline>          _splines    = new List<Spline>();
        private readonly List<int>             _firstHexes = new List<int>();

        public IReadOnlyList<Spline> Splines
        {
            get { return _splines; }
        }

        public bool TryFind(int firstHex, int lastHex, out int index)
        {
            return _byEndHexes.TryGetValue(Identity(firstHex, lastHex), out index);
        }

        /// <summary>
        /// A reference carries the pool index and a direction bit, set when this route walks the
        /// stored piece backwards.
        /// </summary>
        public uint Reference(int index, int firstHex)
        {
            uint reversed = _firstHexes[index] == firstHex ? 0u : 1u;
            return ((uint)index << 1) | reversed;
        }

        /// <summary>
        /// Adds a piece to the pool. The flags belong to the route that got here first; later routes
        /// walking the same stretch reuse the spline exactly as it stands.
        /// </summary>
        public uint Add(int firstHex, int lastHex, RoutePiece piece, Curve[] curves)
        {
            int index = _splines.Count;

            _splines.Add(new Spline(piece.IsLand, piece.IsLandBridge, piece.TerminatesAtStart, piece.TerminatesAtEnd, curves));
            _firstHexes.Add(firstHex);
            _byEndHexes.Add(Identity(firstHex, lastHex), index);

            return (uint)index << 1;
        }

        private static long Identity(int firstHex, int lastHex)
        {
            int low  = firstHex < lastHex ? firstHex : lastHex;
            int high = firstHex < lastHex ? lastHex : firstHex;

            return ((long)low << 32) | (uint)high;
        }
    }
}
