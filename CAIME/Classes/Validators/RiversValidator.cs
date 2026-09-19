using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CAIME.Validators
{
    internal static class RiversValidator
    {
        public static bool Validate(Project project)
        {
            var mapHexFile = project.MapHexFile;

            //The authored river edge masks are validated as they stand. Recomputing them here would
            //discard paint-order dependent chords the map was authored with (see MapHexFile).

            var capacity = (int)mapHexFile.Capacity;
            var failed   = 0;

            Parallel.For(0, capacity, hexIndex =>
            {
                var hex = mapHexFile.HexData[hexIndex];

                //Nothing to validate on a plain non-river hex
                if (hex.IsRiver == false && hex.RiverEdgeMask == 0)
                {
                    return;
                }

                //River flag / edge mask consistency
                if (hex.IsRiver != (hex.RiverEdgeMask != 0))
                {
                    LoggerViewModel.Log($"Rivers validation: Hex({hex.Q}, {hex.R}) river flag ({hex.IsRiver}) is inconsistent with its river edge mask (0x{hex.RiverEdgeMask.ToString("X2")}); it does not connect to a neighbouring river.", LogLevel.Warning);
                    Interlocked.Exchange(ref failed, 1);
                }

                if (hex.IsRiver == false)
                {
                    return;
                }

                //River on sea
                if (hex.IsSea)
                {
                    LoggerViewModel.Log($"Rivers validation: Hex({hex.Q}, {hex.R}) is both a river and sea.", LogLevel.Warning);
                    Interlocked.Exchange(ref failed, 1);
                }

                var isolated = true;
                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    var nbrIndex = mapHexFile.GetNeighbourIndex(hex, dir);

                    //River edge pointing at sea or off the map
                    if ((hex.RiverEdgeMask & (1 << dir)) != 0)
                    {
                        if (nbrIndex == -1)
                        {
                            LoggerViewModel.Log($"Rivers validation: Hex({hex.Q}, {hex.R}) has a river edge pointing off the map.", LogLevel.Warning);
                            Interlocked.Exchange(ref failed, 1);
                        }
                        else if (mapHexFile.HexData[nbrIndex].IsSea)
                        {
                            LoggerViewModel.Log($"Rivers validation: Hex({hex.Q}, {hex.R}) has a river edge pointing into a sea hex.", LogLevel.Warning);
                            Interlocked.Exchange(ref failed, 1);
                        }
                    }

                    if (nbrIndex != -1 && mapHexFile.HexData[nbrIndex].IsRiver)
                    {
                        isolated = false;
                    }
                }

                //Stray river hex
                if (isolated)
                {
                    LoggerViewModel.Log($"Rivers validation: Hex({hex.Q}, {hex.R}) is an isolated river hex (allowed, but maybe a mistake).", LogLevel.Info);
                }
            });

            return failed == 0;
        }
    }
}
