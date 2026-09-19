namespace CAIME.Pathfinding.Roads
{
    /// <summary>
    /// A hex edge addressed from one of its two sides: the hex that owns it plus the neighbour
    /// direction it points at. Roads are painted, traced and emitted in terms of these, and the two
    /// half-edges of one physical edge are always tracked separately.
    /// </summary>
    internal static class HalfEdge
    {
        public const int DirectionsPerHex = 6;

        public static int CountIn(MapHexFile map)
        {
            return (int)map.Capacity * DirectionsPerHex;
        }

        public static int Of(int hexIndex, int direction)
        {
            return hexIndex * DirectionsPerHex + direction;
        }

        public static int Opposite(int direction)
        {
            return HexGridUtility.InverseDir((ushort)direction);
        }
    }
}
