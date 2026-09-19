using System.Threading;
using System.Threading.Tasks;

namespace CAIME.Validators
{
    internal static class ImpassableValidator
    {
        public static bool Validate(Project project)
        {
            var mapHexFile = project.MapHexFile;
            var failed     = 0;

            Parallel.For(0, (int)mapHexFile.Capacity, hexIndex =>
            {
                var hex = mapHexFile.HexData[hexIndex];

                //Town slot placed on impassable ground
                if (hex.IsImpassable && hex.TownSlotIndex != Hex.INVALID_SLOT_INDEX)
                {
                    LoggerViewModel.Log($"Impassable validation: Hex({hex.Q}, {hex.R}) has a town slot but is marked impassable.", LogLevel.Error);
                    Interlocked.Exchange(ref failed, 1);
                }

                //Single neighbour pass: check for a passable neighbour (holes) and an impassable one (isolation)
                var hasRealNeighbour = false;
                var hasPassableNbr   = false;
                var hasImpassableNbr = false;
                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    var nbrIndex = mapHexFile.GetNeighbourIndex(hex, dir);
                    if (nbrIndex == -1)
                    {
                        continue;
                    }

                    hasRealNeighbour = true;
                    if (mapHexFile.HexData[nbrIndex].IsImpassable)
                    {
                        hasImpassableNbr = true;
                    }
                    else
                    {
                        hasPassableNbr = true;
                    }
                }

                //Passable hole fully enclosed by impassable hexes
                if (hex.IsPassable && hasRealNeighbour && !hasPassableNbr)
                {
                    LoggerViewModel.Log($"Impassable validation: Hex({hex.Q}, {hex.R}) is a passable hole fully enclosed by impassable hexes.", LogLevel.Warning);
                    Interlocked.Exchange(ref failed, 1);
                }

                //Isolated impassable hex (allowed, but maybe a mistake)
                if (hex.IsImpassable && hasRealNeighbour && !hasImpassableNbr)
                {
                    LoggerViewModel.Log($"Impassable validation: Hex({hex.Q}, {hex.R}) is an isolated impassable hex.", LogLevel.Info);
                }
            });

            return failed == 0;
        }
    }
}
