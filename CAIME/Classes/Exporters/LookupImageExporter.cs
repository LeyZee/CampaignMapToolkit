using SharpDX.Direct2D1.Effects;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using TGASharpLib;

namespace CAIME
{
    /// <summary>
    /// Writes a campaign map's lookup and minimap images. One instance owns one export's state.
    /// </summary>
    public sealed class LookupImageExporter
    {
        private readonly static double  LOOKUP_RATIO_WIDTH          = 2.36220472441;
        private readonly static double  LOOKUP_RATIO_HEIGHT         = 2.36388888889;
        private readonly static double  MINIMAP_RATIO_WIDTH         = 0.5157480314961;
        private readonly static double  MINIMAP_RATIO_HEIGHT        = 0.5166666666667;
        private readonly static double  MAIN_LOOKUP_RATIO_WIDTH     = 4.0; //BOB expects this size
        private readonly static double  MAIN_LOOKUP_RATIO_HEIGHT    = 4.0 * 2.0 / Math.Sqrt(3); //BOB expects this size
        private readonly static double  MAIN_LOOKUP_RATIO_WIDTH_3K  = 4.3050; //It is what it is
        private readonly static double  MAIN_LOOKUP_RATIO_HEIGHT_3K = 4.3077; //It is what it is

        private int[]            regionIndices;
        private string           outPath;
        private string           lookupName;
        private string           minimapName;
        private ushort           lookupWidth;
        private ushort           lookupHeight;
        private ushort           minimapWidth;
        private ushort           minimapHeight;
        private ushort           mainLookupWidth;
        private ushort           mainLookupHeight;

        private TGA              lookupImage;
        private TGA              minimapImage;
        private Bitmap           mainLookupImage;

        public static bool Export(Project project, string exportPath)
        {
            return new LookupImageExporter().Run(project, exportPath);
        }

        private bool Run(Project project, string exportPath)
        {
            var mapHexFile = project.MapHexFile;
            var db = project.Database;

            if (Initialise(exportPath, project) == false)
            {
                return false;
            }

            if (mapHexFile.GameName == "warhammer" || mapHexFile.GameName == "warhammer2" || mapHexFile.GameName == "warhammer3" || mapHexFile.GameName == "three_kingdoms" || mapHexFile.GameName == "troy")
            {
                //WH3's lookups are two 16bpp TGAs and a weird int16 dds
                //So instead of dealing with that, we just output a BMP that the user can give BOB to convert
                //Not sure about warhammer1 and warhammer2...
                mainLookupImage = CreateBmpImage((int)mapHexFile.MapWidth, (int)mapHexFile.MapHeight, mainLookupWidth, mainLookupHeight, regionIndices, db);
            }
            else
            {
                lookupImage = CreateTgaImage((int)mapHexFile.MapWidth, (int)mapHexFile.MapHeight, lookupWidth, lookupHeight, regionIndices, db);
                minimapImage = CreateTgaImage((int)mapHexFile.MapWidth, (int)mapHexFile.MapHeight, minimapWidth, minimapHeight, regionIndices, db);
            }

            Finalise(mapHexFile);

            return true;
        }

        private bool Initialise(string path, Project project)
        {
            outPath = path;

            var mapHexFile = project.MapHexFile;
            var db = project.Database;

            if (mapHexFile.GameName == "warhammer" || mapHexFile.GameName == "warhammer2" || mapHexFile.GameName == "warhammer3" || mapHexFile.GameName == "troy")
            {
                mainLookupWidth     = (ushort)(MAIN_LOOKUP_RATIO_WIDTH  * mapHexFile.MapWidth);
                mainLookupHeight    = (ushort)(MAIN_LOOKUP_RATIO_HEIGHT * mapHexFile.MapHeight);
                //others not used
            }
            else
            if (mapHexFile.GameName == "three_kingdoms")
            {
                //3K seems to use 4.3 and 4.0 causes everything to be offset, whatever...
                mainLookupWidth     = (ushort)(MAIN_LOOKUP_RATIO_WIDTH_3K  * mapHexFile.MapWidth);
                mainLookupHeight    = (ushort)(MAIN_LOOKUP_RATIO_HEIGHT_3K * mapHexFile.MapHeight);
                //others not used
            }
            else
            {
                lookupWidth         = (ushort)(LOOKUP_RATIO_WIDTH   * mapHexFile.MapWidth);
                lookupHeight        = (ushort)(LOOKUP_RATIO_HEIGHT  * mapHexFile.MapHeight);
                minimapWidth        = (ushort)(MINIMAP_RATIO_WIDTH  * mapHexFile.MapWidth);
                minimapHeight       = (ushort)(MINIMAP_RATIO_HEIGHT * mapHexFile.MapHeight);
            }

            var campaign = db.CachedCampaigns.Find(record => record.CampaignMapName == mapHexFile.CampaignMapName);
            if (campaign == null)
            {
                LoggerViewModel.Log($"Lookup Image Exporter - no campaign uses the map '{mapHexFile.CampaignMapName}'. Aborting...", LogLevel.Error);
                return false;
            }

            var campaignName = campaign.CampaignName;

            lookupName  = $"{campaignName}_lookup";
            minimapName = $"{campaignName}_lookup_minimap";

            return CacheColours(project);
        }

        private void Finalise(MapHexFile mapHexFile)
        {
            if (mapHexFile.GameName == "warhammer" || mapHexFile.GameName == "warhammer2" || mapHexFile.GameName == "warhammer3" || mapHexFile.GameName == "three_kingdoms" || mapHexFile.GameName == "troy")
            {
                mainLookupImage.Save($"{outPath}{lookupName}.bmp", ImageFormat.Bmp);

                // Release the GDI handle - the bitmap lives in a static field and would
                // otherwise leak until the next export overwrites it.
                mainLookupImage.Dispose();
                mainLookupImage = null;
            }
            else
            {
                lookupImage.Save($"{outPath}{lookupName}.tga");
                minimapImage.Save($"{outPath}{minimapName}.tga");

                lookupImage  = null;
                minimapImage = null;
            }
        }

        private bool CacheColours(Project project)
        {
            var mapHexFile = project.MapHexFile;
            var db = project.Database;

            var regions = new List<string>();
            regions.AddRange(mapHexFile.LandRegions);
            regions.AddRange(mapHexFile.SeaRegions);

            var regionsMismatchFound = false;

            // Reload regions table in case any db changes were made after the project was opened
            if (db.CacheTableData(project.Game, Constants.TABLE_REGIONS) == false)
            {
                return false;
            }

            foreach (var region in regions)
            {
                if (db.CachedRegions.FindIndex(x => x.Key == region) == -1)
                {
                    LoggerViewModel.Log($"Lookup Image Exporter - {region} is missing in the database. Aborting...", LogLevel.Error);
                    regionsMismatchFound = true;
                }
            }

            if (regionsMismatchFound)
            {
                return false;
            }

            regionIndices = new int[mapHexFile.Capacity];

            for (int hexIndex = 0; hexIndex < mapHexFile.Capacity; ++hexIndex)
            {
                // TODO: Add newer titles support - their max value > byte.MaxValue
                regionIndices[hexIndex] = mapHexFile.HexData[hexIndex].RegionId;
            }

            regionIndices = Utility.FlipRawDataVert(regionIndices, (int)mapHexFile.MapWidth);

            return true;
        }

        private static Bitmap CreateBmpImage(int hexMapWidth, int hexMapHeight, int outWidth, int outHeight, int[] imageData, DatabaseViewModel db)
        {
            var bitmap = new Bitmap(outWidth, outHeight, PixelFormat.Format32bppRgb);
            var bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                                            ImageLockMode.WriteOnly, bitmap.PixelFormat);

            int[] resampledData;
            resampledData = TransferHexGridToPixelGridUpscale(imageData, hexMapWidth, hexMapHeight, bitmap.Width, bitmap.Height);

            var pixelData       = new int[resampledData.Length];

            for (int i = 0; i < resampledData.Length; ++i)
            {
                // Hexes without a region (RegionId == -1) are painted black instead of crashing.
                var regionIndex = resampledData[i];
                if (regionIndex < 0 || regionIndex >= db.CachedRegions.Count)
                {
                    pixelData[i] = unchecked((int)0xFF000000);
                    continue;
                }

                var color = db.CachedRegions[regionIndex].Colour;
                // Swap red and blue components
                var swappedColor = (int)(color & 0xFF00FF00) | (int)((color & 0x00FF0000) >> 16) | (int)((color & 0x000000FF) << 16);
                pixelData[i] = swappedColor;
            }

            Marshal.Copy(pixelData, 0, bmpData.Scan0, resampledData.Length);

            bitmap.UnlockBits(bmpData);

            return bitmap;
        }

        private static TGA CreateTgaImage(int hexMapWidth, int hexMapHeight, int outWidth, int outHeight, int[] imageData, DatabaseViewModel db)
        {
            imageData = Utility.FlipRawDataVert(imageData, hexMapWidth);

            using (var bitmap = new Bitmap(outWidth, outHeight, PixelFormat.Format8bppIndexed))
            {
                var bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                                                ImageLockMode.WriteOnly, bitmap.PixelFormat);

                var resampledData           = NearestNeighbour(imageData, hexMapWidth, hexMapHeight, bitmap.Width, bitmap.Height);
                var resampledDataByteArray  = new byte[resampledData.Length];

                for (int i = 0; i < resampledData.Length; i++)
                {
                    resampledDataByteArray[i] = (byte)resampledData[i];
                }

                Utility.CopyRowsToBitmap(resampledDataByteArray, bmpData, bitmap.Width, bitmap.Height);
                
                bitmap.UnlockBits(bmpData);

                var pal = bitmap.Palette;

                for (int i = 0; i < 256; ++i)
                {
                    if (i >= db.CachedRegions.Count)
                    {
                        pal.Entries[i] = Color.FromArgb(255, 0, 0, 0);
                        continue;
                    }

                    var region = db.CachedRegions[i];
                    Utility.RgbaDecompose(region.Colour, out byte r, out byte g, out byte b, out byte _);
                    pal.Entries[i] = Color.FromArgb(255, r, g, b);
                }

                bitmap.Palette = pal;

                var result = new TGA(bitmap);

                //This part is a fix because the TGA library is doing something very wrong
                result.ImageOrColorMapArea.ImageData = resampledDataByteArray;

                return result;
            }
        }
    
        private static int[] NearestNeighbour(int[] input, int oldWidth, int oldHeight, int newWidth, int newHeight)
        {
            var output  = new int[newWidth * newHeight];
            var sx      = (double)oldWidth  / (double)newWidth;
            var sy      = (double)oldHeight / (double)newHeight;

            for (int pixelIndex = 0; pixelIndex < newWidth * newHeight; ++pixelIndex)
            {
                double x = pixelIndex % newWidth;
                double y = pixelIndex / newWidth;

                int projX = (int)Math.Floor(x * sx);
                int projY = (int)Math.Floor(y * sy);

                output[pixelIndex] = input[projX + projY * oldWidth];
            }

            return output;
        }


        private static Tuple<int, int> PixelToEvenQHex(double pixelX, double pixelY, double sizeX, double sizeY)
        {
            // Normalize x/y by their respective hex sizes so the axial transform sees isotropic hex units
            double nx = pixelX / sizeX;
            double ny = pixelY / sizeY;

            double q = (2.0 / 3.0) * nx;
            double r = (-1.0 / 3.0) * nx + (Math.Sqrt(3.0) / 3.0) * ny;

            // Round the resulting axial coordinates
            Tuple<int, int> axialRounded = AxialRound(q, r);

            // Convert to even-q offset coordinates
            int col = axialRounded.Item1;
            int row = (int)(axialRounded.Item2 + (col + (col % 2)) / 2.0);

            return new Tuple<int, int>(col, row);
        }

        private static Tuple<int, int> AxialRound(double x, double y)
        {
            int xgrid = (int)Math.Round(x);
            int ygrid = (int)Math.Round(y);

            x -= xgrid;
            y -= ygrid;

            int dx = (int)Math.Round(x + 0.5 * y) * (Math.Abs(x) >= Math.Abs(y) ? 1 : 0);
            int dy = (int)Math.Round(y + 0.5 * x) * (Math.Abs(x) < Math.Abs(y) ? 1 : 0);

            return new Tuple<int, int>(xgrid + dx, ygrid + dy);
        }

        //A different sort of upscale, basically doing what CAIME's viewport already does, directly transfering the hexagonal grid to a larger pixel grid
        //Looks nicer than nearest neighbor IMO, less blocky
        private static int[] TransferHexGridToPixelGridUpscale(int[] input, int oldWidth, int oldHeight, int newWidth, int newHeight)
        {
            var output = new int[newWidth * newHeight];

            double scaleX = (double)newWidth / oldWidth;
            double scaleY = (double)newHeight / oldHeight;

            double hexSize = 2.0 / 3.0;
            double hexSizeX = hexSize * scaleX;
            double hexSizeY = hexSize * scaleY * Math.Sqrt(3) / 2.0 ;

            for (int pixelIndex = 0; pixelIndex < newWidth * newHeight; ++pixelIndex)
            {
                float x = (int)(pixelIndex % newWidth);
                float y = (int)(pixelIndex / newWidth);

                (int col, int row) = PixelToEvenQHex(x - 1.5, y - 1.5, hexSizeX, hexSizeY);

                //Handle pixels technically being outside of the hex grid (around the edges when a pixel is in a gap)
                col = Math.Max(col, 0);
                col = Math.Min(col, oldWidth - 1);
                row = Math.Max(row, 0);
                row = Math.Min(row, oldHeight - 1);

                output[pixelIndex] = input[col + row * oldWidth];
            }

            return output;
        }
    }
}
