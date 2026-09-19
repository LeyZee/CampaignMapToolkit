using System;

namespace CAIME.Controls
{
    public delegate void UpdateSprawlColourHandler(object sender, UpdateSprawlColorEventArgs args);
    public delegate void SprawlColoursUpdatedHandler(bool resizeColors);

    public class UpdateSprawlColorEventArgs : EventArgs
    {
        public int hexIndex { get; private set; }

        public UpdateSprawlColorEventArgs(int hexIndex)
        {
            this.hexIndex = hexIndex;
        }
    }

    public class SprawlActionsControlViewModel : ActionsControlViewModel
    {
        public event UpdateSprawlColourHandler OnUpdateSprawlColour;
        public event SprawlColoursUpdatedHandler OnSprawlColoursUpdated;

        public void AutoGenerateSprawl()
        {
            var mapHexFile = _project.MapHexFile;
            bool modified = false;

            for (int hexIndex = 0; hexIndex < mapHexFile.Capacity; hexIndex++)
            {
                //really simple, if the hex is a townslot, make it a townsprawl too
                var hex = mapHexFile.HexData[hexIndex];

                if (hex.TownSlotIndex > Hex.INVALID_SLOT_INDEX)
                {
                    hex.IsTownSprawl = true;
                    modified = true;
                }

                //for WH, Troy, and 3K, also do the converse, if the hex isn't a townslot, make it not a townsprawl
                //the earlier games can have sprawl outside a townslot, so don't do it for those
                if (_project.Game == GameTemplate.Warhammer3 ||
                    _project.Game == GameTemplate.Three_Kingdoms ||
                    _project.Game == GameTemplate.Troy ||
                    _project.Game == GameTemplate.Pharaoh ||
                    _project.Game == GameTemplate.Pharaoh_Dynasties)
                {
                    if (hex.TownSlotIndex == Hex.INVALID_SLOT_INDEX)
                    {
                        hex.IsTownSprawl = false;
                        modified = true;
                    }
                }

                OnUpdateSprawlColour?.Invoke(this, new UpdateSprawlColorEventArgs(hexIndex));
            }

            if (modified)
            {
                mapHexFile.SetDirty();
            }

            OnSprawlColoursUpdated?.Invoke(resizeColors: false);
        }
    }
}
