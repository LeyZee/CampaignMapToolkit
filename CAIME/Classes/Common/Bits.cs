using System;

namespace CAIME
{
    /// <summary>
    /// Range-checked packing of map data into the narrow bit fields of the on-disk formats.
    /// Silent truncation here corrupts unrelated fields sharing the same byte, so an out-of-range
    /// value is reported instead of being written.
    /// </summary>
    internal static class Bits
    {
        /// <summary>
        /// Returns <paramref name="value"/> unchanged if it fits in <paramref name="width"/> bits,
        /// otherwise throws naming the field.
        /// </summary>
        /// <param name="value">Value about to be packed.</param>
        /// <param name="width">Number of bits the field reserves for it.</param>
        /// <param name="name">Field name, used in the error message.</param>
        public static int Pack(int value, int width, string name)
        {
            int max = (1 << width) - 1;

            if (value < 0 || value > max)
                throw new OverflowException($"{name} is {value}, which does not fit in the {width} bit(s) the file format reserves for it (valid range 0..{max}).");

            return value;
        }
    }
}
