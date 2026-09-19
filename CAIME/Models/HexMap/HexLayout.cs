using System;
using System.Numerics;

namespace CAIME
{
    /// <summary>
    /// Hex layout model
    /// </summary>
    public class HexLayout
    {
        public readonly HexOrientation  Orientation;
        public readonly HexStyle        Style;
        public readonly OffsetType      Type;
        public readonly int             HexSize;
        public readonly float           HexScale;

        public HexLayout(HexStyle style, OffsetType type, int hexSize, float hexScale = 1.0f)
        {
            switch (style)
            {
                case HexStyle.Pointy:
                    var pointyF = new Vector4((float)Math.Sqrt(3.0f), (float)Math.Sqrt(3.0f) / 2.0f, 0.0f, 3.0f / 2.0f);
                    var pointyB = new Vector4((float)Math.Sqrt(3.0f) / 3.0f, -1.0f / 3.0f, 0.0f, 2.0f / 3.0f);
                    Orientation = new HexOrientation(pointyF, pointyB, 0.5f);
                    break;

                case HexStyle.FlatTop:
                    var flatF = new Vector4(3.0f / 2.0f, 0.0f, (float)Math.Sqrt(3.0f) / 2.0f, (float)Math.Sqrt(3.0f));
                    var flatB = new Vector4(2.0f / 3.0f, 0.0f, -1.0f / 3.0f, (float)Math.Sqrt(3.0f) / 3.0f);
                    Orientation = new HexOrientation(flatF, flatB, 0.0f);
                    break;
            }

            Style       = style;
            Type        = type;
            HexSize     = hexSize;
            HexScale    = hexScale;
        }

        /// <summary>
        /// Hex coordinates to screen-space coordinates
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        public Vector2 HexToPixel(Vector3 hex)
        {
            float x = (Orientation.Front.X * hex.X + Orientation.Front.Y * hex.Y) * HexSize;
            float y = (Orientation.Front.Z * hex.X + Orientation.Front.W * hex.Y) * HexSize;
            return new Vector2(x, y);
        }

        public Vector3 PixelToHex(Vector2 point)
        {
            Vector2 pt = new Vector2(point.X / HexSize, point.Y / HexSize);
            int q = (int)Math.Round(Orientation.Back.X * pt.X + Orientation.Back.Y * pt.Y);
            int r = (int)Math.Round(Orientation.Back.Z * pt.X + Orientation.Back.W * pt.Y);
            return new OffsetCoordinates(q, r).ColumnOffsetFromCube(Type);
        }

        /// <summary>
        /// Get distance at which hex corner is located from the hex centre
        /// </summary>
        /// <param name="corner">Index of a vertex corner</param>
        /// <returns>Distance of a hex corner</returns>
        public Vector2 GetHexCornerOffset(int corner)
        {
            double angle = 2.0 * Math.PI * (Orientation.StartAngle - corner) / 6.0;
            return new Vector2((float)(HexSize * Math.Cos(angle)), (float)(HexSize * Math.Sin(angle)));
        }

        /// <summary>
        /// Get a list of all hex corners
        /// </summary>
        /// <param name="hex">Hex towards which corners are calculated</param>
        /// <returns>An array of corners</returns>
        public Vector3[] GetPolygonCorners(int col, int row)
        {
            var hexInCubeCoords = new OffsetCoordinates(col, row);
            var corners = new Vector3[6];
            var center = Style.Equals(HexStyle.Pointy)
                ? HexToPixel(hexInCubeCoords.RowOffsetToCube(Type))
                : HexToPixel(hexInCubeCoords.ColumnOffsetToCube(Type));

            for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
            {
                var offset = GetHexCornerOffset(dir);
                corners[dir] = new Vector3(center.X + offset.X * HexScale, center.Y + offset.Y * HexScale, 0);
            }

            return corners;
        }

        /// <summary>
        /// Use this method to generate a grid and fill hexes with initial data
        /// </summary>
        /// <param name="totalCols">Total amount of columns in hex grid</param>
        /// <param name="totalRows">Total amount of rows in hex grid</param>
        /// <returns></returns>
        public Hex[] GenerateGrid(uint totalCols, uint totalRows)
        {
            // Declare a hex array;
            // Read all bytes from .raw file;
            var hexArray = new Hex[totalCols * totalRows];

            // For each cell in the grid, create a hex and set it's attributes data.
            for (int row = 0; row < totalRows; ++row)
            {
                for (int col = 0; col < totalCols; ++col)
                {
                    var index = (int)(row * totalCols + col);
                    var hex = new Hex(col, row, index);
                    hexArray[index] = hex;
                }
            }

            return hexArray;
        }
    }
}
