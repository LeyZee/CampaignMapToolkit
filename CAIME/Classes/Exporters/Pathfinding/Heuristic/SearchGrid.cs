using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace CAIME.Pathfinding.Heuristic
{
    /// <summary>
    /// The per-hex facts every search shares: where each of a hex's six neighbours lives, which tile
    /// group the hex belongs to and which medium it is. All of it is derived once, because a few
    /// hundred searches read it and none of them change it.
    /// </summary>
    internal sealed class SearchGrid
    {
        public SearchGrid(MapHexFile mapHexFile, byte[] edgesData)
        {
            Capacity       = (int)mapHexFile.Capacity;
            Stride         = HexGridUtility.NEIGHBOURS_COUNT;
            Neighbours     = new int[Capacity * Stride];
            MediumOfHex    = new byte[Capacity];
            TileGroupOfHex = new int[Capacity];

            Build(mapHexFile, edgesData);
        }

        public int Capacity { get; }

        /// <summary>How many neighbour slots each hex owns in <see cref="Neighbours"/>.</summary>
        public int Stride { get; }

        /// <summary>Neighbour indices, <see cref="Stride"/> per hex in direction order; -1 is off-grid.</summary>
        public int[] Neighbours { get; }

        public byte[] MediumOfHex { get; }

        public int[] TileGroupOfHex { get; }

        private void Build(MapHexFile mapHexFile, byte[] edgesData)
        {
            var hexes  = mapHexFile.HexData;
            int width  = (int)mapHexFile.MapWidth;
            int height = (int)mapHexFile.MapHeight;
            int stride = Stride;

            Parallel.ForEach(Partitioner.Create(0, Capacity), range =>
            {
                for (int hexIndex = range.Item1; hexIndex < range.Item2; ++hexIndex)
                {
                    var hex = hexes[hexIndex];

                    MediumOfHex[hexIndex]    = (byte)(hex.IsSea ? TravelMedium.Sea : TravelMedium.Land);
                    TileGroupOfHex[hexIndex] = TileGroupsGenerator.ExtractTileGroupIndex(hexIndex, edgesData);

                    for (ushort direction = 0; direction < stride; ++direction)
                        Neighbours[hexIndex * stride + direction] = HexGridUtility.GetNeighbourIndexFast(hex, direction, width, height);
                }
            });
        }
    }
}
