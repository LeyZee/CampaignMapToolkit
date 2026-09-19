namespace CAIME.TradeNetwork
{
    /// <summary>
    /// Extracts the attributes the exporter needs out of a loaded map into a <see cref="TradeGrid"/>.
    /// This is the only place that knows about <see cref="MapHexFile"/>; the map is read, never written.
    /// </summary>
    internal static class TradeGridBuilder
    {
        public static TradeGrid FromMap(MapHexFile map)
        {
            int width  = (int)map.MapWidth;
            int height = (int)map.MapHeight;
            int count  = width * height;

            var regionIndex = new int[count];
            var isSea       = new bool[count];
            var isInland    = new bool[count];
            var isPassable  = new bool[count];
            var isBridge    = new bool[count];
            var townSlot    = new sbyte[count];
            var tradeMask   = new byte[count];

            var hexes = map.HexData;
            for (int index = 0; index < count; ++index)
            {
                var hex = hexes[index];

                regionIndex[index] = hex.RegionId;
                isSea[index]       = hex.IsSea;
                isInland[index]    = hex.IsLand && !hex.IsCoast;
                isPassable[index]  = hex.IsPassable;
                isBridge[index]    = hex.IsBridge;
                townSlot[index]    = hex.TownSlotIndex;
                tradeMask[index]   = hex.TradeRouteMask;
            }

            return new TradeGrid(width, height, regionIndex, isSea, isInland, isPassable, isBridge, townSlot, tradeMask);
        }
    }
}
