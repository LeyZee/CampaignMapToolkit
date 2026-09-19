using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using CAIME;
using CAIME.Models;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="LookupImageExporter"/>'s resampling and pixel-writing stages - the
    /// part that turns a hex grid of region indices into the lookup images the game reads back as
    /// region ownership. A wrong colour or a shifted sample makes provinces belong to the wrong
    /// region in game, which no earlier stage can catch.
    ///
    /// <para>
    /// The public entry point needs a configured Assembly Kit (it re-caches the regions table), so
    /// these drive the private static stages directly.
    /// </para>
    /// </summary>
    [TestClass]
    public class LookupImageExporterTests
    {
        private static readonly Type ExporterType = typeof(LookupImageExporter);

        [TestMethod]
        public void NearestNeighbour_SameSize_ReturnsTheInputUnchanged()
        {
            var input  = new[] { 0, 1, 2, 3, 4, 5 };
            var output = NearestNeighbour(input, 3, 2, 3, 2);

            CollectionAssert.AreEqual(input, output, "Resampling to the same size must be an identity.");
        }

        [TestMethod]
        public void NearestNeighbour_IntegerUpscale_RepeatsEachSourcePixel()
        {
            var input  = new[] { 1, 2, 3, 4 };           // 2x2
            var output = NearestNeighbour(input, 2, 2, 4, 4);

            CollectionAssert.AreEqual(
                new[]
                {
                    1, 1, 2, 2,
                    1, 1, 2, 2,
                    3, 3, 4, 4,
                    3, 3, 4, 4,
                },
                output,
                "A 2x upscale must repeat each source pixel in a 2x2 block.");
        }

        [TestMethod]
        public void NearestNeighbour_Downscale_TakesTheFlooredSourcePixel()
        {
            var input = new[]
            {
                1, 2, 3, 4,
                5, 6, 7, 8,
                9, 10, 11, 12,
                13, 14, 15, 16,
            };

            CollectionAssert.AreEqual(new[] { 1, 3, 9, 11 }, NearestNeighbour(input, 4, 4, 2, 2),
                "Halving must sample the top-left pixel of each 2x2 block.");
        }

        [TestMethod]
        public void NearestNeighbour_NonIntegerRatio_StaysInsideTheSourceBuffer()
        {
            var input  = new int[7 * 5];
            for (int i = 0; i < input.Length; ++i)
                input[i] = i;

            var output = NearestNeighbour(input, 7, 5, 17, 11);

            Assert.AreEqual(17 * 11, output.Length, "Output size");
            foreach (var value in output)
                Assert.IsTrue(value >= 0 && value < input.Length, "Sampled outside the source buffer.");
        }

        // Every output pixel must resolve to a real hex, including the pixels that land in the gaps
        // around the edges of the hexagonal grid.
        [TestMethod]
        public void TransferHexGridToPixelGridUpscale_ClampsEdgePixelsIntoTheGrid()
        {
            const int oldWidth = 6, oldHeight = 5;

            var input = new int[oldWidth * oldHeight];
            for (int i = 0; i < input.Length; ++i)
                input[i] = i;

            var newWidth  = (int)(4.0 * oldWidth);
            var newHeight = (int)(4.0 * 2.0 / Math.Sqrt(3) * oldHeight);

            var output = TransferHexGridToPixelGridUpscale(input, oldWidth, oldHeight, newWidth, newHeight);

            Assert.AreEqual(newWidth * newHeight, output.Length, "Output size");
            foreach (var value in output)
                Assert.IsTrue(value >= 0 && value < input.Length,
                    "A pixel in the gap around the hex grid was not clamped back into it.");
        }

        [TestMethod]
        public void TransferHexGridToPixelGridUpscale_UniformGrid_ProducesAUniformImage()
        {
            var input = new int[6 * 5];
            for (int i = 0; i < input.Length; ++i)
                input[i] = 7;

            var output = TransferHexGridToPixelGridUpscale(input, 6, 5, 24, 27);

            foreach (var value in output)
                Assert.AreEqual(7, value, "A grid of one region must upscale to one region everywhere.");
        }

        // Utility.ToRgba packs R into the low byte, GDI reads an int as 0xAARRGGBB. The exporter's
        // red/blue exchange is that byte-order conversion, so the region's colour must come out
        // unchanged - dropping the exchange would tint the whole map instead.
        [TestMethod]
        public void CreateBmpImage_WritesTheRegionColourUnchangedAfterTheByteOrderSwap()
        {
            var db = DatabaseWithRegions(
                Utility.ToRgba(0xAA, 0xBB, 0xCC, 0xFF));

            var hexGrid = new int[4 * 4];   // every hex belongs to region 0

            using (var bitmap = CreateBmpImage(4, 4, 16, 16, hexGrid, db))
            {
                var pixel = bitmap.GetPixel(8, 8);

                Assert.AreEqual(0xAA, pixel.R, "Red");
                Assert.AreEqual(0xBB, pixel.G, "Green");
                Assert.AreEqual(0xCC, pixel.B, "Blue");
            }
        }

        // A hex with no region (RegionId -1) is normal on an unfinished map and must not crash the
        // export - it is painted black instead.
        [TestMethod]
        public void CreateBmpImage_HexesWithNoRegion_ArePaintedBlack()
        {
            var db = DatabaseWithRegions(Utility.ToRgba(0xAA, 0xBB, 0xCC, 0xFF));

            var hexGrid = new int[4 * 4];
            for (int i = 0; i < hexGrid.Length; ++i)
                hexGrid[i] = Hex.INVALID_REGION_INDEX;

            using (var bitmap = CreateBmpImage(4, 4, 16, 16, hexGrid, db))
            {
                var pixel = bitmap.GetPixel(8, 8);

                Assert.AreEqual(0, pixel.R, "Red");
                Assert.AreEqual(0, pixel.G, "Green");
                Assert.AreEqual(0, pixel.B, "Blue");
                Assert.AreEqual(255, pixel.A, "An unassigned hex must stay opaque.");
            }
        }

        [TestMethod]
        public void CreateBmpImage_RegionIndexBeyondTheTable_IsPaintedBlackRatherThanThrowing()
        {
            var db = DatabaseWithRegions(Utility.ToRgba(0xAA, 0xBB, 0xCC, 0xFF));

            var hexGrid = new int[4 * 4];
            for (int i = 0; i < hexGrid.Length; ++i)
                hexGrid[i] = 99;   // no such region in the database

            using (var bitmap = CreateBmpImage(4, 4, 16, 16, hexGrid, db))
            {
                var pixel = bitmap.GetPixel(8, 8);

                Assert.AreEqual(Color.FromArgb(255, 0, 0, 0).ToArgb(), pixel.ToArgb(),
                    "A region index the database does not know must be painted black.");
            }
        }

        // The 8bpp lookup carries region indices as palette entries, so the palette is the mapping the
        // game reads. Entries past the end of the regions table must be black, not stale.
        [TestMethod]
        public void CreateTgaImage_BuildsAFull256EntryPaletteFromTheRegionsTable()
        {
            var db = DatabaseWithRegions(
                Utility.ToRgba(0x10, 0x20, 0x30, 0xFF),
                Utility.ToRgba(0x40, 0x50, 0x60, 0xFF));

            var hexGrid = new int[4 * 4];

            var tga = CreateTgaImage(4, 4, 8, 8, hexGrid, db);

            Assert.IsNotNull(tga, "CreateTgaImage returned nothing.");
            Assert.AreEqual(8 * 8, tga.ImageOrColorMapArea.ImageData.Length,
                "The image data must cover every output pixel.");
        }

        [TestMethod]
        public void CreateTgaImage_IndexedImageDataHoldsRegionIndices()
        {
            var db = DatabaseWithRegions(
                Utility.ToRgba(0x10, 0x20, 0x30, 0xFF),
                Utility.ToRgba(0x40, 0x50, 0x60, 0xFF));

            var hexGrid = new int[4 * 4];
            for (int i = 0; i < hexGrid.Length; ++i)
                hexGrid[i] = 1;

            var tga = CreateTgaImage(4, 4, 8, 8, hexGrid, db);

            foreach (var index in tga.ImageOrColorMapArea.ImageData)
                Assert.AreEqual(1, index, "Every pixel of a single-region map must carry that region's index.");
        }

        // The exporter flips vertically because the hex grid's row 0 is the bottom of the map while
        // an image's row 0 is the top. A lost flip mirrors the whole campaign map.
        [TestMethod]
        public void FlipRawDataVert_ReversesRowOrderWithoutReversingRows()
        {
            var input = new[]
            {
                1, 2, 3,
                4, 5, 6,
                7, 8, 9,
            };

            CollectionAssert.AreEqual(
                new[]
                {
                    7, 8, 9,
                    4, 5, 6,
                    1, 2, 3,
                },
                Utility.FlipRawDataVert(input, 3),
                "Rows must swap top to bottom while each row keeps its own left-to-right order.");
        }

        private static DatabaseViewModel DatabaseWithRegions(params int[] colours)
        {
            var db = new DatabaseViewModel { CachedRegions = new List<DBRegion>() };

            for (int i = 0; i < colours.Length; ++i)
                db.CachedRegions.Add(new DBRegion { Id = i, Key = $"region_{i}", Colour = colours[i] });

            return db;
        }

        private static int[] NearestNeighbour(int[] input, int oldWidth, int oldHeight, int newWidth, int newHeight)
            => (int[])Invoke("NearestNeighbour", input, oldWidth, oldHeight, newWidth, newHeight);

        private static int[] TransferHexGridToPixelGridUpscale(
            int[] input, int oldWidth, int oldHeight, int newWidth, int newHeight)
            => (int[])Invoke("TransferHexGridToPixelGridUpscale", input, oldWidth, oldHeight, newWidth, newHeight);

        private static Bitmap CreateBmpImage(
            int hexMapWidth, int hexMapHeight, int outWidth, int outHeight, int[] imageData, DatabaseViewModel db)
            => (Bitmap)Invoke("CreateBmpImage", hexMapWidth, hexMapHeight, outWidth, outHeight, imageData, db);

        private static TGASharpLib.TGA CreateTgaImage(
            int hexMapWidth, int hexMapHeight, int outWidth, int outHeight, int[] imageData, DatabaseViewModel db)
            => (TGASharpLib.TGA)Invoke("CreateTgaImage", hexMapWidth, hexMapHeight, outWidth, outHeight, imageData, db);

        private static object Invoke(string methodName, params object[] args)
        {
            var method = ExporterType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, $"LookupImageExporter.{methodName} was not found.");

            return method.Invoke(null, args);
        }
    }
}
