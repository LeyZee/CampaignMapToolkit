using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CAIME.Shapes
{
    static class Hexagon
    {
        // 2 characters (horizontal) = 1x; 1 Row = 1y
        //             ____________
        //          __|            |__
        //       __|                  |__
        //      |                        |
        //    __|                        |__
        // __|                              |__
        //|                                    |
        //|__                                __|
        //   |__                          __|
        //      |                        |
        //      |__                    __|
        //         |__              __|
        //            |____________|

        //System.Windows.Media.Color
        public static void Paint(byte[] image, int width, int height, int midX, int midY, System.Windows.Media.Color colour)
        {
            Paint(image, width, height, midX, midY, colour.A, colour.R, colour.G, colour.B);
        }
        //SharpDX.Color
        public static void Paint(byte[] image, int width, int height, int midX, int midY, Color colour) 
        {
            Paint(image, width, height, midX, midY, colour.A, colour.R, colour.G, colour.B);
        }
        private static void Paint(byte[] image, int width, int height, int midX, int midY, byte a, byte r, byte g, byte b)
        {
            //line 1
            int x = midX - 3;
            int y = midY - 6;
            int index = ((height - 1 - y)  * width + x) * 4;
            while (x < midX + 3)
            {
                image[index] = b;
                index++;
                image[index] = g;
                index++;
                image[index] = r;
                index++;
                image[index] = a;
                index++;

                x++;
            }

            //line2
            x = midX - 4;
            y = midY - 5;
            index = ((height - 1 - y)  * width + x) * 4;
            while (x < midX + 4)
            {
                image[index] = b;
                index++;
                image[index] = g;
                index++;
                image[index] = r;
                index++;
                image[index] = a;
                index++;

                x++;
            }

            //line3
            x = midX - 5;
            y = midY - 4;
            index = ((height - 1 - y)  * width + x) * 4;
            while (x < midX + 5)
            {
                image[index] = b;
                index++;
                image[index] = g;
                index++;
                image[index] = r;
                index++;
                image[index] = a;
                index++;

                x++;
            }

            //line4
            x = midX - 5;
            y = midY - 3;
            index = ((height - 1 - y)  * width + x) * 4;
            while (x < midX + 5)
            {
                image[index] = b;
                index++;
                image[index] = g;
                index++;
                image[index] = r;
                index++;
                image[index] = a;
                index++;

                x++;
            }

            //line5
            x = midX - 6;
            y = midY - 2;
            index = ((height - 1 - y)  * width + x) * 4;
            while (x < midX + 6)
            {
                image[index] = b;
                index++;
                image[index] = g;
                index++;
                image[index] = r;
                index++;
                image[index] = a;
                index++;

                x++;
            }

            //line6
            x = midX - 7;
            y = midY - 1;
            index = ((height - 1 - y)  * width + x) * 4;
            while (x < midX + 7)
            {
                image[index] = b;
                index++;
                image[index] = g;
                index++;
                image[index] = r;
                index++;
                image[index] = a;
                index++;

                x++;
            }

            //middle

            //line7
            x = midX - 7;
            y = midY + 0;
            index = ((height - 1 - y)  * width + x) * 4;
            while (x < midX + 7)
            {
                image[index] = b;
                index++;
                image[index] = g;
                index++;
                image[index] = r;
                index++;
                image[index] = a;
                index++;

                x++;
            }

            //line8
            x = midX - 6;
            y = midY + 1;
            index = ((height - 1 - y)  * width + x) * 4;
            while (x < midX + 6)
            {
                image[index] = b;
                index++;
                image[index] = g;
                index++;
                image[index] = r;
                index++;
                image[index] = a;
                index++;

                x++;
            }

            //line9
            x = midX - 5;
            y = midY + 2;
            index = ((height - 1 - y)  * width + x) * 4;
            while (x < midX + 5)
            {
                image[index] = b;
                index++;
                image[index] = g;
                index++;
                image[index] = r;
                index++;
                image[index] = a;
                index++;

                x++;
            }

            //line10
            x = midX - 5;
            y = midY + 3;
            index = ((height - 1 - y)  * width + x) * 4;
            while (x < midX + 5)
            {
                image[index] = b;
                index++;
                image[index] = g;
                index++;
                image[index] = r;
                index++;
                image[index] = a;
                index++;

                x++;
            }

            //line11
            x = midX - 4;
            y = midY + 4;
            index = ((height - 1 - y)  * width + x) * 4;
            while (x < midX + 4)
            {
                image[index] = b;
                index++;
                image[index] = g;
                index++;
                image[index] = r;
                index++;
                image[index] = a;
                index++;

                x++;
            }

            //line12
            x = midX - 3;
            y = midY + 5;
            index = ((height - 1 - y)  * width + x) * 4;
            while (x < midX + 3)
            {
                image[index] = b;
                index++;
                image[index] = g;
                index++;
                image[index] = r;
                index++;
                image[index] = a;
                index++;

                x++;
            }
        }
    }

    static class Square
    {
        //SharpDX.Color
        public static void Paint(byte[] image, int width, int height, int midX, int midY, Color colour, int sideLenght)
        {
            Paint(image, width, height, midX, midY, colour.A, colour.R, colour.G, colour.B, sideLenght);
        }
        //System.Windows.Media.Color
        public static void Paint(byte[] image, int width, int height, int midX, int midY, System.Windows.Media.Color colour, int sideLenght)
        {
            Paint(image, width, height, midX, midY, colour.A, colour.R, colour.G, colour.B, sideLenght);
        }
        private static void Paint(byte[] image, int width, int height, int midX, int midY, byte a, byte r, byte g, byte b, int sideLenght)
        {
            int startX = midX - sideLenght / 2;
            int y = midY - sideLenght / 2;

            int index = ((height - 1 - y) * width + startX) * 4;

            while (y < midY + sideLenght / 2)
            {
                for (int x = startX; x < midX + sideLenght / 2; x++)
                {
                    image[index] = b;
                    index++;
                    image[index] = g;
                    index++;
                    image[index] = r;
                    index++;
                    image[index] = a;
                    index++;
                }
                index -= (width + sideLenght) * 4;
                y++;
            }
        }

        
    }
}
