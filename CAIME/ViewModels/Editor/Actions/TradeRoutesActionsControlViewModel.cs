using System;
using CAIME.Controls;

namespace CAIME.ViewModels
{
    public delegate void UpdateTradeRouteColourHandler(object sender, UpdateTradeRouteColorEventArgs args);
    public delegate void TradeRouteColoursUpdatedHandler(bool resizeColors);

    public class UpdateTradeRouteColorEventArgs : EventArgs
    {
        public int hexIndex { get; private set; }

        public UpdateTradeRouteColorEventArgs(int hexIndex)
        {
            this.hexIndex = hexIndex;
        }
    }

    public class TradeRoutesActionsControlViewModel : ActionsControlViewModel
    {
        public event UpdateTradeRouteColourHandler OnUpdateTradeRouteColour;
        public event TradeRouteColoursUpdatedHandler OnTradeRouteColoursUpdated;

        public void GenerateRoutesFromRoads()
        {
            var mapHexFile = _project.MapHexFile;
            bool modified = false;

            int width = (int)mapHexFile.MapWidth;
            int height = (int)mapHexFile.MapHeight;

            for (int hexIndex = 0; hexIndex < mapHexFile.Capacity; ++hexIndex)
            {
                var hex = mapHexFile.GetHex(hexIndex);

                if (hex.IsSea)
                {
                    continue;
                }

                bool oldIsTradeRoute = hex.IsTradeRoute;
                hex.IsTradeRoute = hex.IsRoad;

                if (oldIsTradeRoute != hex.IsTradeRoute)
                {
                    modified = true;
                }

                OnUpdateTradeRouteColour?.Invoke(this, new UpdateTradeRouteColorEventArgs(hexIndex));
            }

            mapHexFile.CalculateTradeRouteEdgeMasks();

            if (modified)
            {
                mapHexFile.SetDirty();
            }

            OnTradeRouteColoursUpdated?.Invoke(resizeColors: false);
        }
    }
}
