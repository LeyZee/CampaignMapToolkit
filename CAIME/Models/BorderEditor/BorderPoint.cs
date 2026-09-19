using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Navigation;

namespace CAIME
{
    [DebuggerDisplay("X = {X}, Y = {Y}")]
    public class BorderPoint
    {
        public int X;
        public int Y;
        public BorderPart BPart;

        public BorderPoint(int q, int r, BorderPart part)
        {
            X = q;
            Y = r;
            BPart = part;
        }
        public static bool operator ==(BorderPoint a, Hex b)
        {
            if (a is null || b is null)
                return a is null && b is null;

            return a.X == b.Q && a.Y == b.R;
        }

        public static bool operator !=(BorderPoint a, Hex b) => !(a == b);

        public static bool operator ==(BorderPoint a, BorderPoint b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (a is null || b is null)
                return false;

            return a.X == b.X && a.Y == b.Y && a.BPart == b.BPart;
        }

        public static bool operator !=(BorderPoint a, BorderPoint b) => !(a == b);

        /// <summary>
        /// A hex's coordinates as a border point not yet attached to a <see cref="BorderPart"/>.
        /// Explicit, so a <see cref="Hex"/> never turns into a part-less point by accident.
        /// </summary>
        public static explicit operator BorderPoint(Hex hex) => new BorderPoint(hex.Q, hex.R, null);

        public override bool Equals(object obj)
        {
            if (!(obj is BorderPoint other))
                return false;

            return (X == other.X) && (Y == other.Y) && (BPart == other.BPart);
        }

        public override int GetHashCode()
        {
            return X.GetHashCode() + Y.GetHashCode() + (BPart?.GetHashCode() ?? 0);
        }
    }
}
