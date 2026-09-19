using System.Numerics;

namespace CAIME {
    /// <summary>
    /// Hex orietation style: Flat or Pointy
    /// </summary>
    public enum HexStyle {
        Pointy = 0,
        FlatTop = 1
    }

    /// <summary>
    /// Hex Orientation Helper
    /// </summary>
    public class HexOrientation {
        public readonly Vector4 Front;
        public readonly Vector4 Back;
        public readonly float StartAngle; // In multiples of 60°

        public HexOrientation(Vector4 f, Vector4 b, float startAngle) {
            Front = f;
            Back = b;
            StartAngle = startAngle;
        }
    };
}
