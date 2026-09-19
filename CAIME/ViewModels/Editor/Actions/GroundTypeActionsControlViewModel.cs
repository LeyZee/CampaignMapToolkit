using System;
using System.Linq;

namespace CAIME.Controls
{
    public delegate void UpdateGroundTypeColourHandler(object sender, UpdateGroundTypeColorEventArgs args);
    public delegate void GroundTypeColoursUpdatedHandler(bool resizeColors);

    public class UpdateGroundTypeColorEventArgs : EventArgs
    {
        public int hexIndex { get; private set; }

        public UpdateGroundTypeColorEventArgs(int hexIndex)
        {
            this.hexIndex = hexIndex;
        }
    }

    public class GroundTypeActionsControlViewModel : ActionsControlViewModel
    {
        public event UpdateGroundTypeColourHandler OnUpdateGroundTypeColour;
        public event GroundTypeColoursUpdatedHandler OnGroundTypeColoursUpdated;

        public void AutoGenerateGroundType()
        {
            var mapHexFile = _project.MapHexFile;

            for (int hexIndex = 0; hexIndex < mapHexFile.Capacity; hexIndex++)
            {
                //if you find a land region hex with a sea groundtype below it
                //change that sea groundtype to a land ground type, either from a neighboring hex or grasslands as a fallback
                //if you find a sea region hex with a land groundtype below it
                //do something similiar

                //also this will fill-in invalid ground types

                var hex = mapHexFile.HexData[hexIndex];
                var groundType = mapHexFile.GetGroundTypeName(hex.GroundTypeIndex);

                var isRegionSea = hex.RegionId >= mapHexFile.LandRegions.Count();

                if (!isRegionSea && (mapHexFile.IsSeaGroundType(hex.GroundTypeIndex) || hex.GroundTypeIndex == Hex.INVALID_GROUND_TYPE_INDEX))
                {
                    //look for land ground type neighbors
                    sbyte nbrGroundType = -1;
                    for (short dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                    {
                        var nbrIndex = mapHexFile.GetNeighbourIndex(hex, (ushort)dir);
                        if (nbrIndex == -1)
                        {
                            break;
                        }

                        var nbr = mapHexFile.HexData[nbrIndex];

                        if (!mapHexFile.IsSeaGroundType(nbr.GroundTypeIndex))
                        {
                            nbrGroundType = nbr.GroundTypeIndex;
                            break;
                        }
                    }
                    if (nbrGroundType >= 0)
                    {
                        //use nbr's ground type if found
                        hex.GroundTypeIndex = nbrGroundType;
                    }
                    else if (mapHexFile.LandGroundTypes.IndexOf("grassland") >= 0)
                    {
                        //fallback to grassland
                        hex.GroundTypeIndex = (sbyte)mapHexFile.LandGroundTypes.IndexOf("grassland");
                    }
                    else
                    {
                        //second fallback, to first ground type (which will be a land one)
                        hex.GroundTypeIndex = 0;
                    }
                }

                if (isRegionSea && (!mapHexFile.IsSeaGroundType(hex.GroundTypeIndex) || hex.GroundTypeIndex == Hex.INVALID_GROUND_TYPE_INDEX))
                {
                    //look for sea ground type neighbors
                    var nbrGroundType = -1;
                    for (short dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                    {
                        var nbrIndex = mapHexFile.GetNeighbourIndex(hex, (ushort)dir);
                        if (nbrIndex == -1)
                        {
                            break;
                        }

                        var nbr = mapHexFile.HexData[nbrIndex];

                        if (mapHexFile.IsSeaGroundType(nbr.GroundTypeIndex))
                        {
                            nbrGroundType = nbr.GroundTypeIndex;
                            break;
                        }
                    }
                    if (nbrGroundType >= 0)
                    {
                        //use nbr's ground type if found
                        hex.GroundTypeIndex = (sbyte)nbrGroundType;
                    }
                    else if (mapHexFile.SeaGroundTypes.IndexOf("sea_ocean") >= 0)
                    {
                        //fallback to sea_ocean
                        hex.GroundTypeIndex = (sbyte)(mapHexFile.LandGroundTypes.Count() + mapHexFile.SeaGroundTypes.IndexOf("sea_ocean"));
                    }
                    else
                    {
                        //fallback to first ground type after the land ones (which will be a sea one)
                        hex.GroundTypeIndex = (sbyte)mapHexFile.LandGroundTypes.Count();
                    }
                }

                OnUpdateGroundTypeColour?.Invoke(this, new UpdateGroundTypeColorEventArgs(hexIndex));
            }

            mapHexFile.SetDirty();
            OnGroundTypeColoursUpdated?.Invoke(resizeColors: false);
        }
    }
}
