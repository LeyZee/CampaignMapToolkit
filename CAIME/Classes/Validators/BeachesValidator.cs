using System.Threading;
using System.Threading.Tasks;

namespace CAIME.Validators
{
    internal static class BeachesValidator
    {
        public static bool Validate(Project project)
        {
            var mapHexFile = project.MapHexFile;
            var failed     = 0;

            Parallel.For(0, (int)mapHexFile.Capacity, hexIndex =>
            {
                var hex = mapHexFile.HexData[hexIndex];

                //Beach on sea
                if (hex.IsBeach && hex.IsSea)
                {
                    LoggerViewModel.Log($"Beaches validation: Hex({hex.Q}, {hex.R}) is both a beach and sea.", LogLevel.Warning);
                    Interlocked.Exchange(ref failed, 1);
                }

                //Beach and cliff on the same hex
                if (hex.IsBeach && hex.IsCliff)
                {
                    LoggerViewModel.Log($"Beaches validation: Hex({hex.Q}, {hex.R}) is both a beach and a cliff.", LogLevel.Warning);
                    Interlocked.Exchange(ref failed, 1);
                }

                if (hex.IsBeach)
                {
                    //Single neighbour pass: look for a sea neighbour and another beach neighbour
                    var seaNbr   = false;
                    var isolated = true;
                    for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                    {
                        var nbrIndex = mapHexFile.GetNeighbourIndex(hex, dir);
                        if (nbrIndex == -1)
                        {
                            continue;
                        }

                        var nbr = mapHexFile.HexData[nbrIndex];
                        if (nbr.IsSea)
                        {
                            seaNbr = true;
                        }
                        if (nbr.IsBeach)
                        {
                            isolated = false;
                        }
                    }

                    //Beach with no sea neighbor
                    if (!seaNbr)
                    {
                        LoggerViewModel.Log($"Beaches validation: Hex({hex.Q}, {hex.R}) has no neighboring sea hex.", LogLevel.Warning);
                        Interlocked.Exchange(ref failed, 1);
                    }

                    //Stray beach hex (maybe this will give too many false positives?)
                    if (isolated)
                    {
                        LoggerViewModel.Log($"Beaches validation: Hex({hex.Q}, {hex.R}) is an isolated beach hex (allowed, but maybe a mistake).", LogLevel.Info);
                    }
                }
            });

            return failed == 0;
        }
    }
}
