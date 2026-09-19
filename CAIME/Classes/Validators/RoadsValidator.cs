using System.Threading;
using System.Threading.Tasks;

namespace CAIME.Validators
{
    internal static class RoadsValidator
    {
        public static bool Validate(Project project)
        {
            var mapHexFile = project.MapHexFile;

            //The authored road edge masks are validated as they stand. Recomputing them here would
            //discard paint-order dependent chords the map was authored with (see MapHexFile).

            var failed = 0;

            Parallel.For(0, (int)mapHexFile.Capacity, hexIndex =>
            {
                var hex = mapHexFile.HexData[hexIndex];

                if (hex.IsRoad == false)
                {
                    return;
                }

                //Bad practice to put roads on impassable?
                if (hex.IsImpassable)
                {
                    LoggerViewModel.Log($"Roads validation: Hex({hex.Q}, {hex.R}) is both a road and impassable (allowed, but frowned upon?).", LogLevel.Info);
                    Interlocked.Exchange(ref failed, 1);
                }

                //Road on sea, not counting bridges
                if (hex.IsSea && !hex.IsBridge)
                {
                    LoggerViewModel.Log($"Roads validation: Hex({hex.Q}, {hex.R}) is both a road and sea, and not a bridge.", LogLevel.Warning);
                    Interlocked.Exchange(ref failed, 1);
                }

                //Road on cliff or beach, not counting bridge stuff
                if ((hex.IsCliff || hex.IsBeach) && !hex.IsBridgeCliff)
                {
                    LoggerViewModel.Log($"Roads validation: Hex({hex.Q}, {hex.R}) is both a road and cliff or beach, and is not leading to a bridge.", LogLevel.Warning);
                    Interlocked.Exchange(ref failed, 1);
                }

                //Road that connects to nothing (empty road edge mask after recomputation)
                if (hex.RoadEdgeMask == 0)
                {
                    LoggerViewModel.Log($"Roads validation: Hex({hex.Q}, {hex.R}) is a road but connects to no other road (empty road edge mask).", LogLevel.Warning);
                    Interlocked.Exchange(ref failed, 1);
                }

                //Stray road hex
                var isolated = true;
                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    var nbrIndex = mapHexFile.GetNeighbourIndex(hex, dir);
                    if (nbrIndex == -1)
                    {
                        continue;
                    }

                    if (mapHexFile.HexData[nbrIndex].IsRoad)
                    {
                        isolated = false;
                        break;
                    }
                }
                if (isolated)
                {
                    LoggerViewModel.Log($"Roads validation: Hex({hex.Q}, {hex.R}) is an isolated road hex (allowed, but maybe a mistake).", LogLevel.Info);
                }

                //Triple intersection check
                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    var nbr1Index = mapHexFile.GetNeighbourIndex(hex, dir);
                    var nbr2Index = mapHexFile.GetNeighbourIndex(hex, (ushort)((dir + 1) % 6));
                    if (nbr1Index == -1 || nbr2Index == -1)
                    {
                        continue;
                    }

                    if (mapHexFile.HexData[nbr1Index].IsRoad && mapHexFile.HexData[nbr2Index].IsRoad)
                    {
                        LoggerViewModel.Log($"Roads validation: Hex({hex.Q}, {hex.R}) creates a three-way insersection (allowed, but messy).", LogLevel.Info);
                        break;
                    }
                }
            });

            return failed == 0;
        }
    }
}
