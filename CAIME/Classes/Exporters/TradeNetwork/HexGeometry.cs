using System;

namespace CAIME.TradeNetwork
{
    /// <summary>
    /// One end of a curve: where it is anchored, which way it leaves, and how far its control point
    /// sits along that direction.
    /// </summary>
    internal struct CurveEndpoint
    {
        public int  X;
        public int  Y;
        public int  DirectionX;
        public int  DirectionY;
        public uint ControlLength;

        public short ControlX
        {
            // The multiplication wraps at 16 bits and the low half is read back as signed. Long
            // control lengths rely on that wrap, so the cast is the behaviour, not a narrowing bug.
            get { return unchecked((short)(DirectionX * (int)ControlLength)); }
        }

        public short ControlY
        {
            get { return unchecked((short)(DirectionY * (int)ControlLength)); }
        }

        public float Length
        {
            get { return (float)Math.Sqrt(DirectionX * DirectionX + DirectionY * DirectionY); }
        }
    }

    /// <summary>
    /// Where a hex's edges sit in the file's fixed-point space, and how to find the edge a trade
    /// route leaves a hex by.
    /// </summary>
    internal static class HexGeometry
    {
        public const uint DefaultControlLength = 10;
        public const uint CornerControlLength  = 5;

        private static readonly int[] EdgeOffsetX      = {  0,   3,   3,  0,  -3,  -3 };
        private static readonly int[] EdgeOffsetY      = {  4,   2,  -2, -4,  -2,   2 };
        private static readonly int[] ControlDirectionX = {  0, -30, -30,  0,  30,  30 };
        private static readonly int[] ControlDirectionY = { -40, -20,  20, 40,  20, -20 };

        public static CurveEndpoint Edge(TradeGrid grid, int hex, int direction, uint controlLength)
        {
            var centre = grid.Centre(hex);

            var endpoint           = new CurveEndpoint();
            endpoint.X             = centre.X + EdgeOffsetX[direction];
            endpoint.Y             = centre.Y + EdgeOffsetY[direction];
            endpoint.DirectionX    = ControlDirectionX[direction];
            endpoint.DirectionY    = ControlDirectionY[direction];
            endpoint.ControlLength = controlLength;

            return endpoint;
        }

        public static HexPoint EdgePoint(TradeGrid grid, int hex, int direction)
        {
            var centre = grid.Centre(hex);
            return new HexPoint(centre.X + EdgeOffsetX[direction], centre.Y + EdgeOffsetY[direction]);
        }

        /// <summary>The first trade edge of <paramref name="hex"/> that does not lead to <paramref name="excluded"/>.</summary>
        public static CurveEndpoint OutboundEdge(TradeGrid grid, int hex, int excluded, uint controlLength)
        {
            for (int direction = 0; direction < TradeGrid.DirectionCount; ++direction)
            {
                int neighbour = grid.Neighbour(hex, direction);
                if (neighbour < 0 || neighbour == excluded)
                    continue;

                if (grid.TradeEdgesMeet(hex, neighbour))
                    return Edge(grid, hex, direction, controlLength);
            }

            throw new InvalidOperationException(
                $"Hex {hex} has no outbound trade edge away from hex {excluded}; the map's trade-route masks are inconsistent.");
        }

        public static CurveEndpoint EdgeFacing(TradeGrid grid, int hex, int neighbour, uint controlLength)
        {
            return Edge(grid, hex, DirectionFacing(grid, hex, neighbour), controlLength);
        }

        public static int DirectionFacing(TradeGrid grid, int hex, int neighbour)
        {
            for (int direction = 0; direction < TradeGrid.DirectionCount; ++direction)
            {
                if (grid.Neighbour(hex, direction) == neighbour)
                    return direction;
            }

            throw new InvalidOperationException($"Hex {neighbour} is not a neighbour of hex {hex}.");
        }
    }
}
