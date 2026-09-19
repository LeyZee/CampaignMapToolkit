using System;
using System.IO;

namespace CAIME.Tests.Helpers
{
    /// <summary>
    /// Serializes a single .ppd section to a byte array using the exporter's own writer seams,
    /// so the bytes are guaranteed to be in the exact game-ready layout the exporter produces.
    /// </summary>
    internal static class PpdSection
    {
        public static byte[] Serialize(Action<BinaryWriter> write)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                write(bw);
                bw.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Writes both sides of a section diff to .raw files in <paramref name="outDir"/> so they
        /// can be opened in Photoshop for visual comparison. Byte order is identical to the binary
        /// file layout (little-endian, as written by BinaryWriter / read from the .ppd).
        /// </summary>
        public static void WriteRaw(string outDir, string mapName, string section, byte[] expected, byte[] actual)
        {
            Directory.CreateDirectory(outDir);
            File.WriteAllBytes(Path.Combine(outDir, $"{mapName}_{section}_expected.raw"), expected);
            File.WriteAllBytes(Path.Combine(outDir, $"{mapName}_{section}_actual.raw"), actual);
        }
    }
}
