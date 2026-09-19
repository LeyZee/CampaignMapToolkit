using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Media;

namespace CAIME
{
    using Color = System.Windows.Media.Color;

    public enum BorderImageLayer
    {
        Regions      = 0,
        OtherBorders = 1,
        Border       = 2,
    }

    class BorderImage
    {
        public Color BorderColour;
        public Color BorderPColour;
        public Color OtherPartColour;
        public Color ComplementaryColour;
        public Color RegFromColour;
        public Color RegToColour;

        private byte[] regionLayer;
        private byte[] otherBLayer;
        private byte[] borderLayer;

        public byte[] composition;

        public int width { get; private set; }
        public int height { get; private set; }
        public int minQ { get; private set; }
        public int minR { get; private set; }
        public int maxQ { get; private set; }
        public int maxR { get; private set; }

        private int sideLength;
        private int margin;
        private BorderPoint bPoint;
        private BorderPart bPart;
        private List<BorderPart> borders;
        private MapHexFile mapHexFile;

        public BorderImage(Project project)
        {
            BorderColour = Colors.Black;
            BorderPColour = Colors.White;
            OtherPartColour = Colors.Gray;
            ComplementaryColour = Colors.DarkGray;

            sideLength = 6;

            mapHexFile = project.MapHexFile;
        }

        public void SetValues(BorderPoint bPoint, BorderPart bPart, List<BorderPart> borders, int margin)
        {
            this.bPoint     = bPoint;
            this.bPart      = bPart;
            this.borders    = borders;
            this.margin     = margin;
        }

        /// <summary>
        /// Writes the composed image to <paramref name="path"/> as a PNG.
        /// <para>The buffer is already 32bpp in the order GDI+ wants, so it is blitted in one
        /// LockBits pass rather than through SetPixel, which locks and unlocks per pixel.</para>
        /// </summary>
        public void DebugExport(string path)
        {
            int width  = this.width;
            int height = this.height;

            using (var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                var bmpData = bitmap.LockBits(new Rectangle(0, 0, width, height),
                                              ImageLockMode.WriteOnly, bitmap.PixelFormat);

                try
                {
                    // 32bpp rows are already a multiple of 4 bytes, so the stride matches exactly.
                    Marshal.Copy(composition, 0, bmpData.Scan0, width * height * 4);
                }
                finally
                {
                    bitmap.UnlockBits(bmpData);
                }

                bitmap.Save(path, ImageFormat.Png);
            }
        }

        public byte[] Update(BorderPoint affected)
        {
            if (affected is null)
            {
                borderLayer = new byte[regionLayer.Length];
                PaintBorderLayer(bPart);
                UpdateSelected(bPoint, false);
                MergeLayers();
            }
            else
            {
                Create();
            }

            return composition;
        }
        public void UpdateColour(BorderImageLayer layerIndex, Color oldColour, Color newColour, bool updateComposition)
        {
            byte[] layer;
            switch ((int)layerIndex)
            {
                case 0:
                    layer = regionLayer; break;
                case 1: 
                    layer = otherBLayer; break;
                case 2:
                    layer = borderLayer; break;
                default:
                    layer = null; break;
            }

            for (int i = 0; i < layer.Length; i += 4)
            {
                if (layer[i] == oldColour.B && layer[i + 1] == oldColour.G && layer[i + 2] == oldColour.R && layer[i + 3] == oldColour.A)
                {
                    layer[i + 0] = newColour.B;
                    layer[i + 1] = newColour.G;
                    layer[i + 2] = newColour.R;
                    layer[i + 3] = newColour.A;
                }
                if (!updateComposition)
                    continue;
                if (composition[i] == oldColour.B && composition[i + 1] == oldColour.G && composition[i + 2] == oldColour.R && composition[i + 3] == oldColour.A)
                {
                    composition[i + 0] = newColour.B;
                    composition[i + 1] = newColour.G;
                    composition[i + 2] = newColour.R;
                    composition[i + 3] = newColour.A;
                }
            }
        }

        public void Create()
        {
            //TODO: add case handling: bPart.BorderPoints is empty
            minQ = (int)mapHexFile.MapWidth - 1;
            minR = (int)mapHexFile.MapHeight - 1;
            maxQ = 0;
            maxR = 0;
            foreach (BorderPoint point in bPart.BorderPoints)
            {
                if (point.X < minQ)
                    minQ = point.X;
                if (point.Y < minR)
                    minR = point.Y;
                if (point.X > maxQ)
                    maxQ = point.X;
                if (point.Y > maxR)
                    maxR = point.Y;
            }
            //Add margin
            minQ = minQ - margin > 0 ? minQ - margin : 0;
            minR = minR - margin > 0 ? minR - margin : 0;

            maxQ = maxQ + margin < mapHexFile.MapWidth ? maxQ + margin : (int)mapHexFile.MapWidth - 1;
            maxR = maxR + margin < mapHexFile.MapHeight ? maxR + margin : (int)mapHexFile.MapHeight - 1;

            width = 14 + (maxQ - minQ) * 10;
            height = (maxR - minR + 1) * 12;
            if (maxR - minR > 0)
                height += 6;

            regionLayer = new byte[width * height * 4];
            otherBLayer = new byte[width * height * 4];
            borderLayer = new byte[width * height * 4];

            //Paint hexes
            int iR = 0;
            for (int r = minR; r <= maxR; r++)
            {
                bool isEven = (minQ & 1) == 0;

                int iQ = 0;
                for (int q = minQ; q <= maxQ; q++)
                {
                    int x = 7 + 10 * iQ;
                    int y = 6 + 12 * iR + (isEven is true ? 0 : 1) * 6;
                    isEven = !isEven;

                    int regID = mapHexFile.HexData[r * mapHexFile.MapWidth + q].RegionId;
                    SharpDX.Color colour = SharpDX.Color.FromRgba(mapHexFile.ColoursLand.Colours[regID]);
                    Shapes.Hexagon.Paint(regionLayer, width, height, x, y, colour);

                    iQ++;
                }
                iR++;
            }

            int prevX = -1;
            int prevY = -1;
            //Paint complementary border
            List<BorderPart> comBorder = borders.FindAll(x => x.regFrom == bPart.regTo && x.regTo== bPart.regFrom);
            foreach (BorderPart part in comBorder)
            {
                prevX = -1;
                prevY = -1;
                foreach (BorderPoint point in part.BorderPoints)
                {
                    if (point.X < minQ || point.X > maxQ || point.Y < minR || point.Y > maxR)
                        continue;

                    int x = 7 + 10 * (point.X - minQ);
                    int y = 6 + 12 * (point.Y - minR) + (point.X & 1) * 6;


                    Shapes.Square.Paint(otherBLayer, width, height, x, y, ComplementaryColour, sideLength);

                    if (prevX != -1)
                        PaintConnection(otherBLayer, x, y, prevX, prevY, ComplementaryColour);

                    prevX = x;
                    prevY = y;
                }
            }

            //Paint other border parts (of current border)
            List<BorderPart> otherParts = borders.FindAll(x => x.regFrom == bPart.regFrom && x.regTo == bPart.regTo && x != bPart);
            foreach (BorderPart part in otherParts)
            {
                prevX = -1;
                prevY = -1;
                foreach (BorderPoint point in part.BorderPoints)
                {
                    if (point.X <= minQ || point.X >= maxQ || point.Y <= minR || point.Y >= maxR)
                        continue;

                    int x = 7 + 10 * (point.X - minQ);
                    int y = 6 + 12 * (point.Y - minR) + (point.X & 1) * 6;

                    Shapes.Square.Paint(otherBLayer, width, height, x, y, OtherPartColour, sideLength);

                    if (prevX != -1)
                        PaintConnection(otherBLayer, x, y, prevX, prevY, OtherPartColour);

                    prevX = x;
                    prevY = y;
                }
            }

            //Paint border points
            PaintBorderLayer(bPart);
            UpdateSelected(bPoint, false);

            //Merge the layers
            MergeLayers();
        }
        public void UpdateSelected(BorderPoint bp, bool updateComposition)
        {
            if (bp is null == false)
                bPoint = bp;
            else if (bPoint is null)
                return;

            UpdateColour(BorderImageLayer.Border, BorderPColour, BorderColour, updateComposition);

            int x = 7 + 10 * (bPoint.X - minQ);
            int y = 6 + 12 * (bPoint.Y - minR) + (bPoint.X & 1) * 6;
            Shapes.Square.Paint(borderLayer, width, height, x, y, BorderPColour, sideLength);

            if (updateComposition)
                Shapes.Square.Paint(composition, width, height, x, y, BorderPColour, sideLength);
        }
        public void PaintBorderLayer(BorderPart bPart)
        {
            int prevX = -1;
            int prevY = -1;
            foreach (BorderPoint point in bPart.BorderPoints)
            {
                int x = 7 + 10 * (point.X - minQ);
                int y = 6 + 12 * (point.Y - minR) + (point.X & 1) * 6;

                Shapes.Square.Paint(borderLayer, width, height, x, y, BorderColour, sideLength);

                if (prevX != -1)
                    PaintConnection(borderLayer, x, y, prevX, prevY, BorderColour);

                prevX = x;
                prevY = y;
            }
        }
        public void MergeLayers()
        {
            composition = (byte[])regionLayer.Clone();
            for (int i = 0; i < regionLayer.Length; i += 4)
                if (borderLayer[i + 3] != 0)
                {
                    composition[i + 0] = borderLayer[i + 0];
                    composition[i + 1] = borderLayer[i + 1];
                    composition[i + 2] = borderLayer[i + 2];
                    composition[i + 3] = borderLayer[i + 3];
                }
                else if (otherBLayer[i + 3] != 0)
                {
                    composition[i + 0] = otherBLayer[i + 0];
                    composition[i + 1] = otherBLayer[i + 1];
                    composition[i + 2] = otherBLayer[i + 2];
                    composition[i + 3] = otherBLayer[i + 3];
                }
        }
        private void PaintConnection(byte[] layer, int originX, int originY, int targetX, int targetY, Color colour)
        {
            int deltaX = targetX - originX;
            int deltaY = targetY - originY;

            double angle = Math.Atan2(deltaY, deltaX);

            double orthogonal = angle - Math.PI / 2;

            //Thin (inner) border line
            int xThinO = originX - (int)Math.Round(Math.Cos(orthogonal) * 2);
            int yThinO = originY - (int)Math.Round(Math.Sin(orthogonal) * 2);

            int xThinT = targetX - (int)Math.Round(Math.Cos(orthogonal) * 2);
            int yThinT = targetY - (int)Math.Round(Math.Sin(orthogonal) * 2);

            PaintLine(layer, xThinO, yThinO, xThinT, yThinT, angle, colour);

            //Thick (outer) border line
            int xThickO = originX + (int)Math.Round(Math.Cos(orthogonal) * 1.5);
            int yThickO = originY + (int)Math.Round(Math.Sin(orthogonal) * 1.5);

            int xThickT = targetX + (int)Math.Round(Math.Cos(orthogonal) * 1.5);
            int yThickT = targetY + (int)Math.Round(Math.Sin(orthogonal) * 1.5);

            PaintLine(layer, xThickO, yThickO, xThickT, yThickT, angle, colour);


            xThickO = originX + (int)Math.Round(Math.Cos(orthogonal) * 2.5);
            yThickO = originY + (int)Math.Round(Math.Sin(orthogonal) * 2.5);

            xThickT = targetX + (int)Math.Round(Math.Cos(orthogonal) * 2.5);
            yThickT = targetY + (int)Math.Round(Math.Sin(orthogonal) * 2.5);

            PaintLine(layer, xThickO, yThickO, xThickT, yThickT, angle, colour);
        }
        private void PaintLine(byte[] layer, int x1, int y1, int x2, int y2, double angle, Color colour)
        {
            if (x1 == x2) //90°
            {
                if (y2 > y1)
                {
                    int index = ((height - 1 - y1) * width + x1) * 4;
                    for (int y = y1; y < y2; y++)
                    {
                        layer[index] = colour.B;
                        index++;
                        layer[index] = colour.G;
                        index++;
                        layer[index] = colour.R;
                        index++;
                        layer[index] = colour.A;
                        index -= 3 + width * 4;
                    }
                }
                else if (y1 > y2)
                {
                    int index = ((height - 1 - y2) * width + x1) * 4;
                    for (int y = y2; y < y1; y++)
                    {
                        layer[index] = colour.B;
                        index++;
                        layer[index] = colour.G;
                        index++;
                        layer[index] = colour.R;
                        index++;
                        layer[index] = colour.A;
                        index -= 3 + width * 4;
                    }
                }
                //else: identical position
            }
            else if (y1 == y2) //0°
            {
                if (x2 > x1)
                {
                    int index = ((height - 1 - y1) * width + x1) * 4;
                    for (int x = x1; x < x2; x++)
                    {
                        layer[index] = colour.B;
                        index++;
                        layer[index] = colour.G;
                        index++;
                        layer[index] = colour.R;
                        index++;
                        layer[index] = colour.A;
                        index++;
                    }
                }
                else if (x1 > x2)
                {
                    int index = ((height - 1 - y1) * width + x2) * 4;
                    for (int x = x2; x < x1; x++)
                    {
                        layer[index] = colour.B;
                        index++;
                        layer[index] = colour.G;
                        index++;
                        layer[index] = colour.R;
                        index++;
                        layer[index] = colour.A;
                        index++;
                    }
                }
                //else: identical position
            }

            else if (Math.Abs(angle) < Math.PI / 4 || Math.Abs(angle) > Math.PI * 3 / 4) //x is increasing faster than y
            {
                int originX;
                int originY;
                int targetX;
                int targetY;

                if (y2 > y1)
                {
                    originX = x1;
                    originY = y1;
                    targetX = x2;
                    targetY = y2;
                }
                else
                {
                    originX = x2;
                    originY = y2;
                    targetX = x1;
                    targetY = y1;
                }
                int xFactor = targetX > originX ? 1 : -1;

                double xStep = 1 / Math.Tan(angle);
                double x = originX + 0.5;
                double endX = x + xStep;

                for (int y = originY; y <= targetY; y++)
                {
                    endX += xStep;
                    for (  ; x * xFactor < endX * xFactor; x += 1 * xFactor)
                    {
                        if (x * xFactor <= targetX * xFactor + 0.5)
                        {
                            int index = ((height - 1 - y) * width + (int)x) * 4;
                            layer[index] = colour.B;
                            index++;
                            layer[index] = colour.G;
                            index++;
                            layer[index] = colour.R;
                            index++;
                            layer[index] = colour.A;
                        }
                    }
                }
            }
            else //y is increasing faster than x
            {
                int originX;
                int originY;
                int targetX;
                int targetY;

                if (x2 > x1)
                {
                    originX = x1;
                    originY = y1;
                    targetX = x2;
                    targetY = y2;
                }
                else
                {
                    originX = x2;
                    originY = y2;
                    targetX = x1;
                    targetY = y1;
                }

                int yFactor = targetY > originY ? 1 : -1;

                double yStep = 1 * Math.Tan(angle);
                double y = originY + 0.5;
                double endY = y + yStep;

                for (int x = originX; x <= targetX; x++)
                {
                    endY += yStep;
                    for (  ; y * yFactor < endY * yFactor; y += 1 * yFactor)
                    {
                        if (y * yFactor <= targetY * yFactor + 0.5)
                        {
                            int index = ((height - 1 - (int)y) * width + x) * 4;
                            layer[index] = colour.B;
                            index++;
                            layer[index] = colour.G;
                            index++;
                            layer[index] = colour.R;
                            index++;
                            layer[index] = colour.A;
                        }
                    }
                }
            }
        }
    }
}
