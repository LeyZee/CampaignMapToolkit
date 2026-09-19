using System;

namespace CAIME.Controls
{
    public delegate void UpdateBorderColourHandler(object sender, UpdateBorderColorEventArgs args);
    public delegate void BorderColoursUpdatedHandler(bool resizeColors);

    public class UpdateBorderColorEventArgs : EventArgs
    {
        public int hexIndex { get; private set; }

        public UpdateBorderColorEventArgs(int hexIndex)
        {
            this.hexIndex = hexIndex;
        }
    }

    public class BorderActionsControlViewModel : ActionsControlViewModel
    {
        public event UpdateBorderColourHandler OnUpdateBorderColour;
        public event BorderColoursUpdatedHandler OnBorderColoursUpdated;

        public void AutoGenerateBorders()
        {
            BordersExporter.GenerateRegionEdges(_project.MapHexFile);

            // IsBorder was just recomputed for the whole map, but RegionEdgeMask - the part
            // that actually gets written to the .hex file - is only rebuilt on Save() when
            // flagged. Without this, the freshly-generated borders render fine in the live
            // view but silently revert to the stale on-disk mask after a reload.
            MapHexFile.RegionMasksNeedRecalculation = true;

            for (int hexIndex = 0; hexIndex < _project.MapHexFile.Capacity; hexIndex++)
            {
                OnUpdateBorderColour?.Invoke(this, new UpdateBorderColorEventArgs(hexIndex));
            }

            _project.MapHexFile.SetDirty();
            OnBorderColoursUpdated?.Invoke(resizeColors: false);
        }
    }
}
