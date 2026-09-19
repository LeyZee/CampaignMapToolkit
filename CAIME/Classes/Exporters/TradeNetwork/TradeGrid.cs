using System;

namespace CAIME.TradeNetwork
{
    /// <summary>
    /// A point in the fixed-point space the PTD format stores anchors in: hex centres and hex edge
    /// midpoints both land on whole numbers.
    /// </summary>
    internal readonly struct HexPoint
    {
        public readonly int X;
        public readonly int Y;

        public HexPoint(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// The hex grid reduced to the eight attributes the trade-route exporter reads, held as flat
    /// index-parallel arrays. Every later stage reads only these, so a pass over the grid streams a
    /// few arrays instead of walking the map's <see cref="Hex"/> references.
    /// </summary>
    internal sealed class TradeGrid
    {
        public const int   DirectionCount = 6;
        public const int   InvalidRegion  = -1;
        public const sbyte MainSlot       = 0;
        public const sbyte PortSlot       = 1;

        public readonly int      Width;
        public readonly int      Height;
        public readonly int      Count;

        public readonly int[]    RegionIndex;
        public readonly bool[]   IsSea;
        /// <summary>Land and not coastal: the domain land routes are allowed to run over.</summary>
        public readonly bool[]   IsInland;
        public readonly bool[]   IsPassable;
        public readonly bool[]   IsBridge;
        public readonly sbyte[]  TownSlot;
        public readonly byte[]   TradeMask;

        private readonly ushort[] _column;
        private readonly int[]    _indexDelta;
        private readonly int[]    _columnDelta;

        public TradeGrid(int width, int height, int[] regionIndex, bool[] isSea, bool[] isInland,
                         bool[] isPassable, bool[] isBridge, sbyte[] townSlot, byte[] tradeMask)
        {
            Width       = width;
            Height      = height;
            Count       = width * height;

            RegionIndex = regionIndex;
            IsSea       = isSea;
            IsInland    = isInland;
            IsPassable  = isPassable;
            IsBridge    = isBridge;
            TownSlot    = townSlot;
            TradeMask   = tradeMask;

            _column      = new ushort[Count];
            _indexDelta  = new int[2 * DirectionCount];
            _columnDelta = new int[2 * DirectionCount];

            for (int parity = 0; parity < 2; ++parity)
            {
                for (int direction = 0; direction < DirectionCount; ++direction)
                {
                    var step = Hex.Directions_FlatTop[parity, direction];
                    _columnDelta[parity * DirectionCount + direction] = step.Q;
                    _indexDelta[parity * DirectionCount + direction]  = step.R * width + step.Q;
                }
            }

            for (int index = 0, row = 0; row < height; ++row)
            {
                for (int column = 0; column < width; ++column, ++index)
                {
                    _column[index] = (ushort)column;
                }
            }
        }

        /// <summary>The neighbour of <paramref name="index"/> in <paramref name="direction"/>, or -1 off-grid.</summary>
        public int Neighbour(int index, int direction)
        {
            int column     = _column[index];
            int lookup     = (column & 1) * DirectionCount + direction;
            int nextColumn = column + _columnDelta[lookup];

            if ((uint)nextColumn >= (uint)Width)
                return -1;

            int neighbour = index + _indexDelta[lookup];
            return (uint)neighbour < (uint)Count ? neighbour : -1;
        }

        public void FillNeighbours(int index, int[] buffer)
        {
            for (int direction = 0; direction < DirectionCount; ++direction)
            {
                buffer[direction] = Neighbour(index, direction);
            }
        }

        /// <summary>
        /// The coarse pairing test of the specification: rotating a neighbour's mask by three
        /// positions lines its edges up with this hex's, so the intersection answers "does any edge
        /// of mine meet any edge of theirs" in one operation. It deliberately does not check that
        /// the two hexes share the specific edge between them.
        /// </summary>
        public bool TradeEdgesMeet(int index, int neighbour)
        {
            int neighbourMask = TradeMask[neighbour];
            int rotated       = ((neighbourMask & 0x07) << 3) | (neighbourMask >> 3);
            return (rotated & TradeMask[index]) != 0;
        }

        public HexPoint Centre(int index)
        {
            int column = _column[index];
            int row    = index / Width;
            return new HexPoint(column * 6 + 4, (row * 2 + (column & 1)) * 4 + 4);
        }
    }
}
