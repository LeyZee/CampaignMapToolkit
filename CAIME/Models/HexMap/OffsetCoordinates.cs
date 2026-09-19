using System.Numerics;

namespace CAIME
{
    /// <summary>
    /// Offset coordinates type: Even or Odd
    /// </summary>
    public enum OffsetType
    {
        Even = 1,
        Odd = -1
    }

    /// <summary>
    /// Offset Coordinates Helper
    /// </summary>
    public class OffsetCoordinates
    {
        public readonly int Q;
        public readonly int R;

        public OffsetCoordinates(int col, int row)
        {
            this.Q = col;
            this.R = row;
        }

        public Vector3 ColumnOffsetFromCube(OffsetType offset)
        {
            int q = Q;
            int r = R + (Q + (int)offset * (Q & 1)) / 2;
            int s = -q - r;
            return new Vector3(q, r, s);
        }

        public Vector3 RowOffsetFromCube(OffsetType offset)
        {
            int q = Q + (R + (int)offset * (R & 1)) / 2;
            int r = R;
            int s = -q - r;
            return new Vector3(q, r, s);
        }

        /// <summary>
        /// Column Offset style coordinates to cube coordinates system
        /// </summary>
        /// <param name="offset">Offset type</param>
        /// <returns></returns>
        public Vector3 ColumnOffsetToCube(OffsetType offset)
        {
            int q = Q;
            int r = R - (Q + (int)offset * (Q & 1)) / 2;
            int s = -q - r;
            return new Vector3(q, r, s);
        }

        /// <summary>
        /// Row Offset style coordinates to cube coordinates system
        /// </summary>
        /// <param name="offset">Offset type</param>
        /// <returns></returns>
        public Vector3 RowOffsetToCube(OffsetType offset)
        {
            int q = Q - (R + (int)offset * (R & 1)) / 2;
            int r = R;
            int s = -q - r;
            return new Vector3(q, r, s);
        }
    }
}
