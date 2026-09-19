using System;

namespace CAIME.Classes.Importers
{
    public static class LayerImporter
    {
        /// <summary>Header length in bytes: the layer type the file was exported for.</summary>
        private const int HEADER_LENGTH = 4;

        /// <summary>
        /// Exact size a layer file must have to be imported as <paramref name="layer"/>. Regions
        /// store two bytes per hex, every other layer one.
        /// </summary>
        public static int ExpectedFileLength(MapHexFile file, LayerType layer)
        {
            int bytesPerHex = layer == LayerType.Regions ? 2 : 1;
            return HEADER_LENGTH + (int)file.Capacity * bytesPerHex;
        }

        /// <summary>
        /// The layer the file was exported for, or null if it is too short to say.
        /// </summary>
        public static LayerType? ReadLayerType(byte[] layerData)
        {
            if (layerData == null || layerData.Length < HEADER_LENGTH)
                return null;

            return (LayerType)BitConverter.ToInt32(layerData, 0);
        }

        public static void Import(Project project, byte[] layerData, LayerType layer)
        {
            var file = project.MapHexFile;

            // Every per-layer importer below indexes layerData by hex, so a file sized for a
            // different layer (or a truncated one) would read past the end part-way through,
            // leaving the map half-overwritten with no way back.
            int expectedLength = ExpectedFileLength(file, layer);
            if (layerData == null || layerData.Length != expectedLength)
            {
                LoggerViewModel.Log(
                    $"Import Layer Failed - a {layer} layer file for this map must be {expectedLength} bytes, but this one is {layerData?.Length ?? 0}.",
                    LogLevel.ErrorMessageBox);
                return;
            }

            switch (layer)
            {
                case LayerType.GroundTypes:
                    ImportBinaryGroundTypes(file, layerData);
                    break;
                case LayerType.Rivers:
                    ImportBinaryRivers(file, layerData);
                    break;
                case LayerType.Climates:
                    ImportBinaryClimates(file, layerData);
                    break;
                case LayerType.Attritions:
                    ImportBinaryAttritions(file, layerData);
                    break;
                case LayerType.Regions:
                    ImportBinaryRegions(file, layerData);
                    break;
                case LayerType.RegionBorders:
                    ImportBinaryRegionBorders(file, layerData);
                    break;
                case LayerType.Beaches:
                    ImportBinaryBeaches(file, layerData);
                    break;
                case LayerType.Bridges:
                    ImportBinaryBridges(file, layerData);
                    break;
                case LayerType.TownSprawl:
                    ImportBinaryTownSprawl(file, layerData);
                    break;
                case LayerType.TownSlots:
                    ImportBinaryTownSlots(file, layerData);
                    break;
                case LayerType.Roads:
                    ImportBinaryRoads(file, layerData);
                    break;
                case LayerType.TradeRoutes:
                    ImportBinaryTradeRoutes(file, layerData);
                    break;
                case LayerType.Impassable:
                    ImportBinaryImpassable(file, layerData);
                    break;
                default:
                    return;
            }

            file.UpdateHexTypes();
            file.SetDirty();
            file.NotifyLayerChanged(layer);

            LoggerViewModel.Log($"Imported binary data for {layer} layer.", LogLevel.Info);
        }

        private static void ImportBinaryGroundTypes(MapHexFile file, byte[] layerData)
        {
            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                hex.GroundTypeIndex = (sbyte)(layerData[4 + hexIndex] - 1);
            }
        }

        private static void ImportBinaryImpassable(MapHexFile file, byte[] layerData)
        {
            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                hex.IsPassable = layerData[4 + hexIndex] == 1;
            }
        }

        private static void ImportBinaryRivers(MapHexFile file, byte[] layerData)
        {
            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                hex.RiverEdgeMask = layerData[4 + hexIndex];
                hex.IsRiver = hex.RiverEdgeMask > 0;
            }

            // Imported layer data is presence-only: build proper directional masks and break
            // any triangles now, then flag the layer so save/export keeps them consistent.
            file.CalculateRiverEdgeMasks();
            MapHexFile.RiverMasksNeedRecalculation = true;
        }

        private static void ImportBinaryRoads(MapHexFile file, byte[] layerData)
        {
            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                hex.RoadEdgeMask = layerData[4 + hexIndex];
                hex.IsRoad = hex.RoadEdgeMask > 0;
            }

            // Imported layer data is presence-only: build proper directional masks and break
            // any triangles now, then flag the layer so save/export keeps them consistent.
            file.CalculateRoadEdgeMasks();
            MapHexFile.RoadMasksNeedRecalculation = true;
        }

        private static void ImportBinaryTradeRoutes(MapHexFile file, byte[] layerData)
        {
            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                hex.TradeRouteMask = layerData[4 + hexIndex];
                hex.IsTradeRoute = hex.TradeRouteMask > 0;
            }
        }

        private static void ImportBinaryBridges(MapHexFile file, byte[] layerData)
        {
            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                hex.IsBridge = layerData[4 + hexIndex] == 1;
            }
        }

        private static void ImportBinaryBeaches(MapHexFile file, byte[] layerData)
        {
            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                hex.IsBeach = layerData[4 + hexIndex] == 1;
            }
        }

        private static void ImportBinaryClimates(MapHexFile file, byte[] layerData)
        {
            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                hex.ClimateIndex = (sbyte)(layerData[4 + hexIndex] - 1);
            }
        }

        private static void ImportBinaryAttritions(MapHexFile file, byte[] layerData)
        {
            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                hex.AttritionIndex = (sbyte)(layerData[4 + hexIndex] - 1);
            }
        }

        private static void ImportBinaryRegions(MapHexFile file, byte[] layerData)
        {
            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];

                var lowerBits = layerData[4 + hexIndex * 2 + 0];
                var upperBits = layerData[4 + hexIndex * 2 + 1];

                hex.RegionId = ((upperBits << 5) | (lowerBits >> 3)) - 1;
            }

            // Region ids changed - region border masks must be rebuilt on save/export.
            MapHexFile.RegionMasksNeedRecalculation = true;
        }

        private static void ImportBinaryRegionBorders(MapHexFile file, byte[] layerData)
        {
            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                hex.RegionEdgeMask = layerData[4 + hexIndex];
                hex.IsBorder = hex.RegionEdgeMask > 0;
            }
        }

        private static void ImportBinaryTownSprawl(MapHexFile file, byte[] layerData)
        {
            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                hex.IsTownSprawl = layerData[4 + hexIndex] == 1;
            }
        }

        private static void ImportBinaryTownSlots(MapHexFile file, byte[] layerData)
        {
            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                hex.TownSlotIndex = (sbyte)(layerData[4 + hexIndex] - 1);
            }
        }
    }
}
