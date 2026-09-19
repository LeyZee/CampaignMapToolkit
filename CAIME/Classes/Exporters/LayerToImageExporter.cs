using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace CAIME
{
    public enum LayerExportMode
    {
        ToImage,
        ToBinary,
    }

    public static class LayerToImageExporter
    {
        public static void Export(LayerExportMode exportMode, string exportPath, Project project, LayerType layerToExport)
        {
            if (exportMode == LayerExportMode.ToImage)
            {
                ExportToImage(exportPath, project.MapHexFile, layerToExport);
            }
            else if (exportMode == LayerExportMode.ToBinary)
            {
                ExportToBinary(exportPath, project.MapHexFile, layerToExport);
            }
        }

        public static void ExportToImage(string exportPath, MapHexFile file, LayerType layerToExport)
        {
            switch (layerToExport)
            {
                case LayerType.GroundTypes:
                    ExportImageGroundTypes(exportPath, file);
                    break;
                case LayerType.Rivers:
                    ExportImageRivers(exportPath, file);
                    break;
                case LayerType.Climates:
                    ExportImageClimates(exportPath, file);
                    break;
                case LayerType.Attritions:
                    ExportImageAttritions(exportPath, file);
                    break;
                case LayerType.Regions:
                    ExportImageRegions(exportPath, file);
                    break;
                case LayerType.RegionBorders:
                    ExportImageRegionBorders(exportPath, file);
                    break;
                case LayerType.Beaches:
                    ExportImageBeaches(exportPath, file);
                    break;
                case LayerType.Bridges:
                    ExportImageBridges(exportPath, file);
                    break;
                case LayerType.TownSprawl:
                    ExportImageTownSprawl(exportPath, file);
                    break;
                case LayerType.TownSlots:
                    ExportImageTownSlots(exportPath, file);
                    break;
                case LayerType.Roads:
                    ExportImageRoads(exportPath, file);
                    break;
                case LayerType.TradeRoutes:
                    ExportImageTradeRoutes(exportPath, file);
                    break;
                case LayerType.Impassable:
                    ExportImageImpassable(exportPath, file);
                    break;
                default:
                    return;
            }

            LoggerViewModel.Log($"Exported {layerToExport} layer to image.", LogLevel.Info);
        }

        public static void ExportToBinary(string exportPath, MapHexFile file, LayerType layerToExport)
        {
            switch (layerToExport)
            {
                case LayerType.GroundTypes:
                    ExportBinaryGroundTypes(layerToExport, exportPath, file);
                    break;
                case LayerType.Rivers:
                    ExportBinaryRivers(layerToExport, exportPath, file);
                    break;
                case LayerType.Climates:
                    ExportBinaryClimates(layerToExport, exportPath, file);
                    break;
                case LayerType.Attritions:
                    ExportBinaryAttritions(layerToExport, exportPath, file);
                    break;
                case LayerType.Regions:
                    ExportBinaryRegions(layerToExport, exportPath, file);
                    break;
                case LayerType.RegionBorders:
                    ExportBinaryRegionBorders(layerToExport, exportPath, file);
                    break;
                case LayerType.Beaches:
                    ExportBinaryBeaches(layerToExport, exportPath, file);
                    break;
                case LayerType.Bridges:
                    ExportBinaryBridges(layerToExport, exportPath, file);
                    break;
                case LayerType.TownSprawl:
                    ExportBinaryTownSprawl(layerToExport, exportPath, file);
                    break;
                case LayerType.TownSlots:
                    ExportBinaryTownSlots(layerToExport, exportPath, file);
                    break;
                case LayerType.Roads:
                    ExportBinaryRoads(layerToExport, exportPath, file);
                    break;
                case LayerType.TradeRoutes:
                    ExportBinaryTradeRoutes(layerToExport, exportPath, file);
                    break;
                case LayerType.Impassable:
                    ExportBinaryImpassable(layerToExport, exportPath, file);
                    break;
                default:
                    return;
            }

            LoggerViewModel.Log($"Exported {layerToExport} layer to binary.", LogLevel.Info);
        }

        private static void ExportImage(string path, uint width, uint height, int[] imageData)
        {
            using (Bitmap bitmap = new Bitmap((int)width, (int)height, PixelFormat.Format32bppRgb))
            {
                var bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                                                ImageLockMode.WriteOnly, bitmap.PixelFormat);

                int[] pixelData = new int[imageData.Length];

                for (int i = 0; i < imageData.Length; ++i)
                {
                    var color = imageData[i];
                    // Swap red and blue components
                    var swappedColor = (int)(color & 0xFF00FF00) | (int)((color & 0x00FF0000) >> 16) | (int)((color & 0x000000FF) << 16);
                    pixelData[i] = swappedColor;
                }

                Marshal.Copy(pixelData, 0, bmpData.Scan0, imageData.Length);

                bitmap.UnlockBits(bmpData);

                bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);
                bitmap.Save(path);
            }
        }

        private static void ExportBinary(string path, byte[] layerData)
        {
            File.WriteAllBytes(path, layerData);
        }

        private static void ExportImageGroundTypes(string exportPath, MapHexFile file)
        {
            var imageData = new int[file.Capacity];

            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                var isSea = file.IsSeaGroundType(hex.GroundTypeIndex);
                imageData[hexIndex] = file.GetColour(isSea, hex.GroundTypeIndex);
            }

            ExportImage($@"{exportPath}layer_ground_types.png", file.MapWidth, file.MapHeight, imageData);
        }

        private static void ExportImageImpassable(string exportPath, MapHexFile file)
        {
            var imageData = new int[file.Capacity];

            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                imageData[hexIndex] = ColourTable.GetNogoColour(hex.IsPassable);
            }

            ExportImage($@"{exportPath}layer_impassable.png", file.MapWidth, file.MapHeight, imageData);
        }

        private static void ExportImageRivers(string exportPath, MapHexFile file)
        {
            var imageData = new int[file.Capacity];

            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                imageData[hexIndex] = ColourTable.GetRiverColour(hex.IsRiver);
            }

            ExportImage($@"{exportPath}layer_rivers.png", file.MapWidth, file.MapHeight, imageData);
        }

        private static void ExportImageRoads(string exportPath, MapHexFile file)
        {
            var imageData = new int[file.Capacity];

            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                imageData[hexIndex] = ColourTable.GetRoadColour(hex.IsRoad);
            }

            ExportImage($@"{exportPath}layer_roads.png", file.MapWidth, file.MapHeight, imageData);
        }

        private static void ExportImageTradeRoutes(string exportPath, MapHexFile file)
        {
            var imageData = new int[file.Capacity];

            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                imageData[hexIndex] = ColourTable.GetTradeRouteColour(hex.IsTradeRoute);
            }

            ExportImage($@"{exportPath}layer_trade_routes.png", file.MapWidth, file.MapHeight, imageData);
        }

        private static void ExportImageBridges(string exportPath, MapHexFile file)
        {
            var imageData = new int[file.Capacity];

            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                imageData[hexIndex] = ColourTable.GetBridgeColour(hex.IsBridge);
            }

            ExportImage($@"{exportPath}layer_bridges.png", file.MapWidth, file.MapHeight, imageData);
        }

        private static void ExportImageBeaches(string exportPath, MapHexFile file)
        {
            var imageData = new int[file.Capacity];

            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                imageData[hexIndex] = ColourTable.GetBeachColour(hex.IsBeach);
            }

            ExportImage($@"{exportPath}layer_beaches.png", file.MapWidth, file.MapHeight, imageData);
        }

        private static void ExportImageClimates(string exportPath, MapHexFile file)
        {
            var imageData = new int[file.Capacity];

            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                imageData[hexIndex] = file.GetColour(false, hex.ClimateIndex);
            }

            ExportImage($@"{exportPath}layer_climates.png", file.MapWidth, file.MapHeight, imageData);
        }

        private static void ExportImageAttritions(string exportPath, MapHexFile file)
        {
            var imageData = new int[file.Capacity];

            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                imageData[hexIndex] = file.GetColour(false, hex.AttritionIndex);
            }

            ExportImage($@"{exportPath}layer_attritions.png", file.MapWidth, file.MapHeight, imageData);
        }

        private static void ExportImageRegions(string exportPath, MapHexFile file)
        {
            var imageData = new int[file.Capacity];

            file.UpdateLandSea();
            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                int colourIndex = hex.IsSea ? hex.RegionId - file.LandRegions.Count : hex.RegionId;
                imageData[hexIndex] = file.GetColour(hex.IsSea, colourIndex);
            }

            ExportImage($@"{exportPath}layer_regions.png", file.MapWidth, file.MapHeight, imageData);
        }

        private static void ExportImageRegionBorders(string exportPath, MapHexFile file)
        {
            var imageData = new int[file.Capacity];

            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                imageData[hexIndex] = ColourTable.GetRegionBorderColour(hex.IsBorder);
            }

            ExportImage($@"{exportPath}layer_region_borders.png", file.MapWidth, file.MapHeight, imageData);
        }

        private static void ExportImageTownSprawl(string exportPath, MapHexFile file)
        {
            var imageData = new int[file.Capacity];

            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                imageData[hexIndex] = ColourTable.GetTownSprawlColour(hex.IsTownSprawl);
            }

            ExportImage($@"{exportPath}layer_town_sprawl.png", file.MapWidth, file.MapHeight, imageData);
        }

        private static void ExportImageTownSlots(string exportPath, MapHexFile file)
        {
            var imageData = new int[file.Capacity];

            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                imageData[hexIndex] = ColourTable.GetTownSlotColour(hex.TownSlotIndex);
            }

            ExportImage($@"{exportPath}layer_town_slots.png", file.MapWidth, file.MapHeight, imageData);
        }

        private static void ExportBinaryGroundTypes(LayerType layerToExport, string exportPath, MapHexFile file)
        {
            var layerData = new byte[4 + file.Capacity];
            BitConverter.GetBytes((int)layerToExport).CopyTo(layerData, 0);

            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                layerData[4 + hexIndex] = (byte)(hex.GroundTypeIndex + 1);
            }

            ExportBinary($@"{exportPath}layer_ground_types.hex_layer", layerData);
        }

        private static void ExportBinaryImpassable(LayerType layerToExport, string exportPath, MapHexFile file)
        {
            var layerData = new byte[4 + file.Capacity];
            BitConverter.GetBytes((int)layerToExport).CopyTo(layerData, 0);

            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                layerData[4 + hexIndex] = hex.IsPassable ? (byte)1 : (byte)0;
            }

            ExportBinary($@"{exportPath}layer_impassable.hex_layer", layerData);
        }

        private static void ExportBinaryRivers(LayerType layerToExport, string exportPath, MapHexFile file)
        {
            var layerData = new byte[4 + file.Capacity];
            BitConverter.GetBytes((int)layerToExport).CopyTo(layerData, 0);

            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                layerData[4 + hexIndex] = hex.RiverEdgeMask;
            }

            ExportBinary($@"{exportPath}layer_rivers.hex_layer", layerData);
        }

        private static void ExportBinaryRoads(LayerType layerToExport, string exportPath, MapHexFile file)
        {
            var layerData = new byte[4 + file.Capacity];
            BitConverter.GetBytes((int)layerToExport).CopyTo(layerData, 0);

            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                layerData[4 + hexIndex] = hex.RoadEdgeMask;
            }

            ExportBinary($@"{exportPath}layer_roads.hex_layer", layerData);
        }

        private static void ExportBinaryTradeRoutes(LayerType layerToExport, string exportPath, MapHexFile file)
        {
            var layerData = new byte[4 + file.Capacity];
            BitConverter.GetBytes((int)layerToExport).CopyTo(layerData, 0);

            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                layerData[4 + hexIndex] = hex.TradeRouteMask;
            }

            ExportBinary($@"{exportPath}layer_trade_routes.hex_layer", layerData);
        }

        private static void ExportBinaryBridges(LayerType layerToExport, string exportPath, MapHexFile file)
        {
            var layerData = new byte[4 + file.Capacity];
            BitConverter.GetBytes((int)layerToExport).CopyTo(layerData, 0);

            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                layerData[4 + hexIndex] = hex.IsBridge ? (byte)1 : (byte)0;
            }

            ExportBinary($@"{exportPath}layer_bridges.hex_layer", layerData);
        }

        private static void ExportBinaryBeaches(LayerType layerToExport, string exportPath, MapHexFile file)
        {
            var layerData = new byte[4 + file.Capacity];
            BitConverter.GetBytes((int)layerToExport).CopyTo(layerData, 0);

            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                layerData[4 + hexIndex] = hex.IsBeach ? (byte)1 : (byte)0;
            }

            ExportBinary($@"{exportPath}layer_beaches.hex_layer", layerData);
        }

        private static void ExportBinaryClimates(LayerType layerToExport, string exportPath, MapHexFile file)
        {
            var layerData = new byte[4 + file.Capacity];
            BitConverter.GetBytes((int)layerToExport).CopyTo(layerData, 0);

            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                layerData[4 + hexIndex] = (byte)(hex.ClimateIndex + 1);
            }

            ExportBinary($@"{exportPath}layer_climates.hex_layer", layerData);
        }

        private static void ExportBinaryAttritions(LayerType layerToExport, string exportPath, MapHexFile file)
        {
            var layerData = new byte[4 + file.Capacity];
            BitConverter.GetBytes((int)layerToExport).CopyTo(layerData, 0);

            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                layerData[4 + hexIndex] = (byte)(hex.AttritionIndex + 1);
            }

            ExportBinary($@"{exportPath}layer_attritions.hex_layer", layerData);
        }

        private static void ExportBinaryRegions(LayerType layerToExport, string exportPath, MapHexFile file)
        {
            var layerData = new byte[4 + file.Capacity * 2];
            BitConverter.GetBytes((int)layerToExport).CopyTo(layerData, 0);

            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                var regionIndex = hex.RegionId + 1;

                var regionIndexLowerBits = (byte)(regionIndex << 3);
                var regionIndexUpperBits = (byte)(regionIndex >> 5);

                layerData[4 + hexIndex * 2 + 0] = regionIndexLowerBits;
                layerData[4 + hexIndex * 2 + 1] = regionIndexUpperBits;
            }

            ExportBinary($@"{exportPath}layer_regions.hex_layer", layerData);
        }

        private static void ExportBinaryRegionBorders(LayerType layerToExport, string exportPath, MapHexFile file)
        {
            var layerData = new byte[4 + file.Capacity];
            BitConverter.GetBytes((int)layerToExport).CopyTo(layerData, 0);

            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                layerData[4 + hexIndex] = hex.RegionEdgeMask;
            }

            ExportBinary($@"{exportPath}layer_region_borders.hex_layer", layerData);
        }

        private static void ExportBinaryTownSprawl(LayerType layerToExport, string exportPath, MapHexFile file)
        {
            var layerData = new byte[4 + file.Capacity];
            BitConverter.GetBytes((int)layerToExport).CopyTo(layerData, 0);

            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                layerData[4 + hexIndex] = hex.IsTownSprawl ? (byte)1 : (byte)0;
            }

            ExportBinary($@"{exportPath}layer_town_sprawl.hex_layer", layerData);
        }

        private static void ExportBinaryTownSlots(LayerType layerToExport, string exportPath, MapHexFile file)
        {
            var layerData = new byte[4 + file.Capacity];
            BitConverter.GetBytes((int)layerToExport).CopyTo(layerData, 0);

            for (int hexIndex = 0; hexIndex < file.Capacity; ++hexIndex)
            {
                var hex = file.HexData[hexIndex];
                layerData[4 + hexIndex] = (byte)(hex.TownSlotIndex + 1);
            }

            ExportBinary($@"{exportPath}layer_town_slots.hex_layer", layerData);
        }
    }
}
