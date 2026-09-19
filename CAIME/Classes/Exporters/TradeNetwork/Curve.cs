namespace CAIME.TradeNetwork
{
    /// <summary>
    /// One cubic Bezier as the file stores it: absolute anchors, and control points as offsets from
    /// their own anchor in hundredths of a unit.
    /// </summary>
    internal readonly struct Curve
    {
        public readonly ushort StartX;
        public readonly ushort StartY;
        public readonly short  StartControlX;
        public readonly short  StartControlY;
        public readonly short  EndControlX;
        public readonly short  EndControlY;
        public readonly ushort EndX;
        public readonly ushort EndY;

        public Curve(ushort startX, ushort startY, short startControlX, short startControlY,
                     short endControlX, short endControlY, ushort endX, ushort endY)
        {
            StartX        = startX;
            StartY        = startY;
            StartControlX = startControlX;
            StartControlY = startControlY;
            EndControlX   = endControlX;
            EndControlY   = endControlY;
            EndX          = endX;
            EndY          = endY;
        }

        public float FirstControlX  { get { return StartX + StartControlX / 100.0f; } }
        public float FirstControlY  { get { return StartY + StartControlY / 100.0f; } }
        public float SecondControlX { get { return EndX + EndControlX / 100.0f; } }
        public float SecondControlY { get { return EndY + EndControlY / 100.0f; } }

        /// <summary>
        /// The curve at <paramref name="t"/>, accumulated from the far anchor back to the near one.
        /// Single precision throughout, and in this order: the results feed a threshold comparison
        /// and an integer truncation, so a last-bit difference reaches the output file.
        /// </summary>
        public void Evaluate(float t, out float x, out float y)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            float dWeight = t3;
            float cWeight = 3.0f * (t2 - t3);
            float bWeight = 3.0f * (t3 + t - 2.0f * t2);
            float aWeight = 3.0f * (t2 - t) + 1.0f - t3;

            x = dWeight * EndX;
            x += cWeight * SecondControlX;
            x += bWeight * FirstControlX;
            x += aWeight * StartX;

            y = dWeight * EndY;
            y += cWeight * SecondControlY;
            y += bWeight * FirstControlY;
            y += aWeight * StartY;
        }
    }
}
