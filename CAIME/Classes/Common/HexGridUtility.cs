using System;

namespace CAIME
{
    public static class HexGridUtility
    {
        public readonly static ushort NEIGHBOURS_COUNT = 6;

        public static int GetHexIndex(Hex hex, int width)
        {
            return IndexFromCoords(hex.R, hex.Q, width);
        }

        /// <summary>
        /// Get cell index from cell coordinates (offset coordinates)
        /// </summary>
        /// <param name="row">Cell row</param>
        /// <param name="col">Cell column</param>
        /// <param name="stride">Grid array stride aka grid width</param>
        /// <returns>Integer representing cell index in a grid 1d array</returns>
        public static int IndexFromCoords(int row, int col, int stride)
        {
            return row * stride + col;
        }

        /// <summary>
        /// Get cell coordinates from it's grid 1d array index
        /// </summary>
        /// <param name="index">Cell index</param>
        /// <param name="stride">Grid array stride aka grid width</param>
        /// <param name="row">Row (x) coordinate</param>
        /// <param name="col">Column (y) coordinate</param>
        public static void CoordsFromIndex(int index, int stride, out int row, out int col)
        {
            row = index / stride;
            col = index % stride;
        }

        public static ushort InverseDir(ushort dir)
        {
            return (ushort)((dir + 3) % NEIGHBOURS_COUNT);
        }

        // Computes the neighbour array index without allocating an intermediate Hex object,
        // avoiding the hidden cost of Hex.Add() inside MapHexFile.GetNeighbour().
        public static int GetNeighbourIndexFast(Hex hex, ushort dir, int width, int height)
        {
            var d  = Hex.Directions_FlatTop[hex.Q & 1, dir];
            int nq = hex.Q + d.Q;
            int nr = hex.R + d.R;
            // Unsigned comparison catches both the negative and the >= bound cases in one test.
            if ((uint)nq >= (uint)width || (uint)nr >= (uint)height)
                return -1;
            return nr * width + nq;
        }

        public static short GetDirFromEdgeMask(byte edgeMask)
        {
            if (edgeMask == 0)
            {
                return -1;
            }

            // Only one bit is expected to be set
            if ((edgeMask & (edgeMask - 1)) != 0)
            {
                return -1;
            }

            short dir = 0;

            while (edgeMask > 1)
            {
                edgeMask >>= 1;
                ++dir;
            }

            return dir;
        }

        public static byte GetNumBitsSetInEdgeMask(byte edgeMask)
        {
            byte count = 0;

            while (edgeMask != 0)
            {
                edgeMask &= (byte)(edgeMask - 1);
                count++;
            }

            return count;
        }
    }
}
