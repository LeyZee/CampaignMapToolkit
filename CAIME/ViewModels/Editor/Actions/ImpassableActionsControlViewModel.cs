using System;

namespace CAIME.Controls
{
    public delegate void UpdateImpassableColourHandler(object sender, UpdateImpassableColorEventArgs args);
    public delegate void ImpassableColoursUpdatedHandler(bool resizeColors);

    public class UpdateImpassableColorEventArgs : EventArgs
    {
        public int hexIndex { get; private set; }

        public UpdateImpassableColorEventArgs(int hexIndex)
        {
            this.hexIndex = hexIndex;
        }
    }

    public class ImpassableActionsControlViewModel : ActionsControlViewModel
    {
        public event UpdateImpassableColourHandler OnUpdateImpassableColour;
        public event ImpassableColoursUpdatedHandler OnImpassableColoursUpdated;

        //Checks if you can stand on the hex (at least, a special definition of that)
        private static bool IsStandable(Hex hex)
        {
            return (!hex.IsCoast || (hex.IsCoast && hex.IsTownSprawl) || hex.IsBridgeCliff)
                && (!hex.IsRiver || (hex.IsRiver && hex.IsTownSprawl))
                && !hex.IsImpassable
                && hex.TownSlotIndex == Hex.INVALID_SLOT_INDEX;
        }

        public void PlugHolesImpassable()
        {
            var mapHexFile = _project.MapHexFile;
            bool modified = false;

            for (int hexIndex = 0; hexIndex < mapHexFile.Capacity; hexIndex++)
            {
                var hex = mapHexFile.HexData[hexIndex];

                if (IsStandable(hex))
                {
                    bool hexNeedsToBeFilled = false;
                    for (short dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                    {
                        var nbrIndex1 = mapHexFile.GetNeighbourIndex(hex, (ushort)dir);
                        var nbrIndex2 = mapHexFile.GetNeighbourIndex(hex, (ushort)((dir + 1) % 6));
                        var nbrIndex3 = mapHexFile.GetNeighbourIndex(hex, (ushort)((dir + 2) % 6));
                        var nbrIndex4 = mapHexFile.GetNeighbourIndex(hex, (ushort)((dir + 3) % 6));
                        var nbrIndex5 = mapHexFile.GetNeighbourIndex(hex, (ushort)((dir + 4) % 6));
                        var nbrIndex6 = mapHexFile.GetNeighbourIndex(hex, (ushort)((dir + 5) % 6));

                        //We treat the direction of the map edge as if it is closed, hence the false
                        var nbr1Standable = nbrIndex1 == -1 ? false : IsStandable(mapHexFile.HexData[nbrIndex1]);
                        var nbr2Standable = nbrIndex2 == -1 ? false : IsStandable(mapHexFile.HexData[nbrIndex2]);
                        var nbr3Standable = nbrIndex3 == -1 ? false : IsStandable(mapHexFile.HexData[nbrIndex3]);
                        var nbr4Standable = nbrIndex4 == -1 ? false : IsStandable(mapHexFile.HexData[nbrIndex4]);
                        var nbr5Standable = nbrIndex5 == -1 ? false : IsStandable(mapHexFile.HexData[nbrIndex5]);
                        var nbr6Standable = nbrIndex6 == -1 ? false : IsStandable(mapHexFile.HexData[nbrIndex6]);

                        //Check for these three patterns:

                        //010
                        if (!nbr1Standable && nbr2Standable && !nbr3Standable)
                        {
                            hexNeedsToBeFilled = true;
                            break;
                        }

                        //011011, special case not covered by the above, "hourglass"
                        if (!nbr1Standable && nbr2Standable && nbr3Standable && !nbr4Standable && nbr5Standable && nbr6Standable)
                        {
                            hexNeedsToBeFilled = true;
                            break;
                        }

                        //000000, another special case, "isolated"
                        //This one is mostly cosmetic
                        if (!nbr1Standable && !nbr2Standable && !nbr3Standable && !nbr4Standable && !nbr5Standable && !nbr6Standable)
                        {
                            hexNeedsToBeFilled = true;
                            break;
                        }
                    }

                    if (hexNeedsToBeFilled)
                    {
                        hex.IsPassable = false;
                        modified = true;
                    }
                }

                OnUpdateImpassableColour?.Invoke(this, new UpdateImpassableColorEventArgs(hexIndex));
            }

            if (modified)
            {
                mapHexFile.SetDirty();
            }

            OnImpassableColoursUpdated?.Invoke(resizeColors: false);
        }
    }
}
