using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CAIME
{
    /// <summary>
    /// Times the method that constructs it. The work - walking a StackTrace to resolve the caller's
    /// name, and writing to the console - is compiled out of release builds.
    /// </summary>
    public class ExecutionTimeProfiler
    {
#if DEBUG
        private Stopwatch watch;
        private string invokerName;
#endif

        public ExecutionTimeProfiler()
        {
#if DEBUG
            watch = Stopwatch.StartNew();

            var st = new StackTrace();
            var sf = st.GetFrame(1);

            invokerName = sf.GetMethod().Name;
#endif
        }

        public void Stop()
        {
#if DEBUG
            watch.Stop();
            Console.WriteLine($"{invokerName} exec time: " + ((float)watch.ElapsedMilliseconds).ToString() + " milliseconds");
#endif
        }
    }

    public class Utility
    {
        /// <summary>
        /// Copies a tightly packed 8-bit-per-pixel buffer into a locked bitmap one row at a time.
        /// <para>GDI+ rounds each row up to a 4-byte boundary, so a single packed Marshal.Copy
        /// lands every row after the first at the wrong offset and shears the image whenever the
        /// width is not a multiple of 4.</para>
        /// </summary>
        public static void CopyRowsToBitmap(byte[] source, BitmapData destination, int width, int height)
        {
            for (int row = 0; row < height; ++row)
            {
                Marshal.Copy(source, row * width, IntPtr.Add(destination.Scan0, row * destination.Stride), width);
            }
        }

        public static int RgbaToBgra(int rgba)
        {
            uint u = (uint)rgba;
            uint r = (u      ) & 0xFF;
            uint g = (u >>  8) & 0xFF;
            uint b = (u >> 16) & 0xFF;
            uint a = (u >> 24) & 0xFF;
            return (int)(b | (g << 8) | (r << 16) | (a << 24));
        }

        public static int ToRgba(byte r, byte g, byte b, byte a = 255)
        {
            int value = r;
            value |= g << 8;
            value |= b << 16;
            value |= a << 24;
            return value;
        }

        public static void RgbaDecompose(int color, out byte r, out byte g, out byte b, out byte a)
        {
            r = (byte)((color & 0x000000FF));
            g = (byte)((color & 0x0000FF00) >> 8);
            b = (byte)((color & 0x00FF0000) >> 16);
            a = (byte)((color & 0xFF000000) >> 24);
        }

        public static void ArrayToRaw(string path, string filename, byte[] data)
        {
            using (var br = new BinaryWriter(new FileStream($"{path}{filename}.raw", FileMode.Create)))
            {
                br.Write(data);
            }
        }

        public static void ArrayToBmp(string path, string filename, uint width, uint height, byte[] data)
        {
            var indexedColours = new int[data.Length];
            for (int i = 0; i < indexedColours.Length; ++i)
            {
                indexedColours[i] = ColourTable.MicrosoftColourTable[data[i]];
            }

            using (var bitmap = new Bitmap((int)width, (int)height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                var bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                                                ImageLockMode.WriteOnly, bitmap.PixelFormat);
                
                Marshal.Copy(indexedColours, 0, bmpData.Scan0, data.Length);
                
                bitmap.UnlockBits(bmpData);
                bitmap.Save($"{path}{filename}.bmp", ImageFormat.Bmp);
            }
        }
    
        /// <summary>
        /// Separates the words of a PascalCase identifier: "TownSlots2" becomes "Town Slots 2".
        /// </summary>
        public static string InsertSpacesBetweenCapitals(string inStr)
        {
            if (string.IsNullOrEmpty(inStr))
            {
                return inStr;
            }

            var result = new StringBuilder(inStr.Length * 2);
            result.Append(inStr[0]);

            for (int i = 1; i < inStr.Length; ++i)
            {
                char c = inStr[i];

                if (char.IsUpper(c) || char.IsDigit(c))
                {
                    result.Append(' ');
                }

                result.Append(c);
            }

            return result.ToString();
        }

        public static T[] FlipRawDataVert<T>(in T[] data, int width)
        {
            var outData = new T[data.Length];

            for (int i = 0, j = data.Length - width; i < data.Length; i += width, j -= width)
            {
                for (int k = 0; k < width; ++k)
                {
                    outData[i + k] = data[j + k];
                }
            }

            return outData;
        }
    }

    public class MathHelper
    {
        public static T Clamp<T>(T value, T min, T max) where T : IComparable<T>
        {
            return (value.CompareTo(min) < 0) ? min : (value.CompareTo(max) > 0) ? max : value;
        }
    }

    public class StringHelper
    {
        public static string NameFixup(string inStr)
        {
            var outStr = inStr;
            outStr = outStr.ToLower();
            outStr = outStr.Replace(' ', '_');

            var charsToRemove = new string[] { "!", "@", "#", "$", "%", "^", "&", "*", "(", ")", "-", "+", "=", "/", "`", "~", "\\", "|", ":", ";", "\"", "'", "?", ",", ".", "<", ">" };
            foreach (var c in charsToRemove)
            {
                outStr = outStr.Replace(c, string.Empty);
            }

            return outStr;
        }
    }

    public class CursorHelper
    {
        public static Cursor CreateCursor(int diameter, SolidColorBrush brush)
        {
            var radius = diameter / 2;

            var vis = new DrawingVisual();
            using (var dc = vis.RenderOpen())
            {
                dc.DrawEllipse(brush, new System.Windows.Media.Pen(System.Windows.Media.Brushes.Black, 1), new System.Windows.Point(radius, radius), radius, radius);
                dc.Close();
            }

            var rtb = new RenderTargetBitmap(diameter, diameter, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(vis);

            using (var ms1 = new MemoryStream())
            {
                var penc = new PngBitmapEncoder();
                penc.Frames.Add(BitmapFrame.Create(rtb));
                penc.Save(ms1);

                var pngBytes = ms1.ToArray();
                var size = pngBytes.GetLength(0);

                //.cur format spec http://en.wikipedia.org/wiki/ICO_(file_format)
                using (var ms = new MemoryStream())
                {
                    {//ICONDIR Structure
                        ms.Write(BitConverter.GetBytes((short)0), 0, 2); // Reserved must be zero; 2 bytes
                        ms.Write(BitConverter.GetBytes((short)2), 0, 2); // image type 1 = ico 2 = cur; 2 bytes
                        ms.Write(BitConverter.GetBytes((short)1), 0, 2); // number of images; 2 bytes
                    }

                    {//ICONDIRENTRY structure
                        ms.WriteByte(32); //image width in pixels
                        ms.WriteByte(32); //image height in pixels

                        ms.WriteByte(0); //Number of Colors in the color palette. Should be 0 if the image doesn't use a color palette
                        ms.WriteByte(0); //reserved must be 0

                        ms.Write(BitConverter.GetBytes((short)radius), 0, 2);//2 bytes. In CUR format: Specifies the horizontal coordinates of the hotspot in number of pixels from the left.
                        ms.Write(BitConverter.GetBytes((short)radius), 0, 2);//2 bytes. In CUR format: Specifies the vertical coordinates of the hotspot in number of pixels from the top.

                        ms.Write(BitConverter.GetBytes(size), 0, 4);//Specifies the size of the image's data in bytes
                        ms.Write(BitConverter.GetBytes(22), 0, 4);//Specifies the offset of BMP or PNG data from the beginning of the ICO/CUR file
                    }

                    ms.Write(pngBytes, 0, size);//write the png data.
                    ms.Seek(0, SeekOrigin.Begin);
                    return new Cursor(ms);
                }
            }
        }
    }
}
