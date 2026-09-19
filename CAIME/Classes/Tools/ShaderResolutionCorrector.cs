using System;
using System.Collections.Generic;
using System.IO;

namespace CAIME.Tools
{
    /// <summary>
    /// Reads, patches and re-saves the baked-in UI-overlay resolution of a compiled Total War:
    /// Three Kingdoms shader (.fxc). Ported from the ThreeKingdomsShaderCorrection Python tools
    /// (twfxc.py, dxbc_checksum.py, patch_wizard.py): an .fxc is a small Creative Assembly
    /// container wrapping one or more standard Direct3D DXBC bytecode blobs. Resolution constants
    /// are stored as float32 immediates inside the primary blob's bytecode; patching them is a
    /// same-width 4-byte swap followed by repairing the blob's DXBC checksum (a modified MD5).
    /// </summary>
    public class ShaderResolutionCorrector
    {
        private const uint FxcMagic = 0x075BCD15;
        private static readonly byte[] DxbcMagic = { (byte)'D', (byte)'X', (byte)'B', (byte)'C' };

        // Standard MD5 per-round shift amounts (RFC 1321).
        private static readonly int[] S =
        {
            7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22,
            5,  9, 14, 20, 5,  9, 14, 20, 5,  9, 14, 20, 5,  9, 14, 20,
            4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23,
            6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21,
        };

        // Standard MD5 additive constants: floor(abs(sin(i + 1)) * 2^32), i = 0..63 (RFC 1321).
        // Hard-coded (rather than recomputed from Math.Sin) so the checksum can never drift from
        // the reference due to a last-bit difference between platform libm implementations.
        private static readonly uint[] K =
        {
            0xd76aa478, 0xe8c7b756, 0x242070db, 0xc1bdceee,
            0xf57c0faf, 0x4787c62a, 0xa8304613, 0xfd469501,
            0x698098d8, 0x8b44f7af, 0xffff5bb1, 0x895cd7be,
            0x6b901122, 0xfd987193, 0xa679438e, 0x49b40821,
            0xf61e2562, 0xc040b340, 0x265e5a51, 0xe9b6c7aa,
            0xd62f105d, 0x02441453, 0xd8a1e681, 0xe7d3fbc8,
            0x21e1cde6, 0xc33707d6, 0xf4d50d87, 0x455a14ed,
            0xa9e3e905, 0xfcefa3f8, 0x676f02d9, 0x8d2a4c8a,
            0xfffa3942, 0x8771f681, 0x6d9d6122, 0xfde5380c,
            0xa4beea44, 0x4bdecfa9, 0xf6bb4b60, 0xbebfbc70,
            0x289b7ec6, 0xeaa127fa, 0xd4ef3085, 0x04881d05,
            0xd9d4d039, 0xe6db99e5, 0x1fa27cf8, 0xc4ac5665,
            0xf4292244, 0x432aff97, 0xab9423a7, 0xfc93a039,
            0x655b59c3, 0x8f0ccc92, 0xffeff47d, 0x85845dd1,
            0x6fa87e4f, 0xfe2ce6e0, 0xa3014314, 0x4e0811a1,
            0xf7537e82, 0xbd3af235, 0x2ad7d2bb, 0xeb86d391,
        };

        private byte[] headerBytes;
        private bool isRawMode;
        private uint blobFlags;
        private List<byte[]> blobs;

        public string LoadedFilePath { get; private set; }
        public bool IsLoaded => blobs != null;

        /// <summary>Reads and parses an .fxc container. Throws on I/O failure or malformed input.</summary>
        public void Load(string path)
        {
            var data = File.ReadAllBytes(path);
            var parsed = ParseContainer(data);

            headerBytes = parsed.headerBytes;
            isRawMode = parsed.isRawMode;
            blobFlags = parsed.flags;
            blobs = parsed.blobs;
            LoadedFilePath = path;
        }

        /// <summary>
        /// Best-guess (width, height) resolution baked into the primary blob. Looks at float
        /// immediates that are whole numbers in a plausible display range, ignores power-of-two
        /// "tech" constants, and picks the most frequent pair with a landscape-ish aspect ratio.
        /// Returns false if nothing convincing is found (the caller should fall back to manual entry).
        /// </summary>
        public bool TryDetectResolution(out int width, out int height)
        {
            EnsureLoaded();
            var (w, h) = DetectResolution(blobs[0]);
            width = w ?? 0;
            height = h ?? 0;
            return w.HasValue && h.HasValue;
        }

        /// <summary>
        /// Replaces the width/height float32 constants in the primary blob and repairs its DXBC
        /// checksum. Does not write anything to disk; call <see cref="Save"/> with the result.
        /// </summary>
        public byte[] PatchResolution(int currentWidth, int currentHeight, int newWidth, int newHeight,
            out int widthOccurrences, out int heightOccurrences)
        {
            EnsureLoaded();

            var blob = blobs[0];

            if (currentWidth == currentHeight && newWidth != newHeight)
            {
                throw new InvalidDataException(
                    $"The current width and height are both {currentWidth}, so the two constants are " +
                    "the same bytes in the bytecode and cannot be told apart. Patch them to the same " +
                    "new value, or edit the shader by hand.");
            }

            // Both patterns are located in the untouched blob. Searching for the height after the
            // width has been written finds the bytes just written whenever newWidth == currentHeight.
            var widthOld  = BitConverter.GetBytes((float)currentWidth);
            var heightOld = BitConverter.GetBytes((float)currentHeight);

            var widthOffsets  = FindAllNonOverlapping(blob, widthOld);
            var heightOffsets = FindAllNonOverlapping(blob, heightOld);

            widthOccurrences  = widthOffsets.Count;
            heightOccurrences = heightOffsets.Count;

            if (widthOccurrences == 0)
            {
                throw new InvalidDataException(
                    $"The current width ({currentWidth}) was not found in the shader bytecode. " +
                    "Double-check the current value (auto-detection can be wrong).");
            }

            if (heightOccurrences == 0)
            {
                throw new InvalidDataException(
                    $"The current height ({currentHeight}) was not found in the shader bytecode. " +
                    "Double-check the current value (auto-detection can be wrong).");
            }

            var patched = (byte[])blob.Clone();
            WriteAt(patched, widthOffsets,  BitConverter.GetBytes((float)newWidth));
            WriteAt(patched, heightOffsets, BitConverter.GetBytes((float)newHeight));

            return FixChecksum(patched);
        }

        /// <summary>
        /// Re-wraps the container with <paramref name="patchedPrimaryBlob"/> swapped in for the
        /// original primary blob (preserving any trailing blobs, e.g. a vertex input signature)
        /// and writes it to <paramref name="outputPath"/>. The header bytes are kept verbatim, so
        /// only the primary blob and its size prefix change.
        /// </summary>
        public void Save(string outputPath, byte[] patchedPrimaryBlob)
        {
            EnsureLoaded();

            if (LoadedFilePath != null &&
                string.Equals(Path.GetFullPath(outputPath), Path.GetFullPath(LoadedFilePath), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Save to a different file so the original shader stays intact.");
            }

            var newBlobs = new List<byte[]>(blobs) { [0] = patchedPrimaryBlob };
            var output = Serialize(headerBytes, isRawMode, blobFlags, newBlobs);

            // Sanity re-parse: a corrupt re-wrap must never silently reach disk.
            ParseContainer(output);

            File.WriteAllBytes(outputPath, output);
        }

        private void EnsureLoaded()
        {
            if (IsLoaded == false)
            {
                throw new InvalidOperationException("No shader file has been loaded yet.");
            }
        }

        // ------------------------------------------------------------------ container (de)serialization
        private readonly struct ParsedContainer
        {
            public readonly byte[] headerBytes;
            public readonly bool isRawMode;
            public readonly uint flags;
            public readonly List<byte[]> blobs;

            public ParsedContainer(byte[] headerBytes, bool isRawMode, uint flags, List<byte[]> blobs)
            {
                this.headerBytes = headerBytes;
                this.isRawMode = isRawMode;
                this.flags = flags;
                this.blobs = blobs;
            }
        }

        private static ParsedContainer ParseContainer(byte[] data)
        {
            int p = 0;

            uint magic = ReadUInt32(data, ref p);
            ReadUInt32(data, ref p); // version - preserved verbatim in headerBytes, not otherwise needed
            uint defineCount = ReadUInt32(data, ref p);

            if (magic != FxcMagic)
            {
                throw new InvalidDataException("Not a Total War: Three Kingdoms shader (bad magic 0x" + magic.ToString("X8") + ").");
            }

            for (uint i = 0; i < defineCount; i++)
            {
                uint stringLength = ReadUInt32(data, ref p);
                p += (int)stringLength; // define string (includes trailing NUL)
                p += 5;                 // flag: u32 + pad: u8
            }

            ReadUInt32(data, ref p); // src_hash
            uint depCount = ReadUInt32(data, ref p);

            for (uint i = 0; i < depCount; i++)
            {
                uint pathLength = ReadUInt16(data, ref p);
                p += (int)pathLength; // include path
                p += 4;               // per-include hash
            }

            var headerBytes = new byte[p];
            Array.Copy(data, 0, headerBytes, 0, p);

            bool isRawMode;
            uint flags = 0;
            var blobs = new List<byte[]>();

            if (RegionEquals(data, p, DxbcMagic))
            {
                isRawMode = true;
                var blob = new byte[data.Length - p];
                Array.Copy(data, p, blob, 0, blob.Length);
                blobs.Add(blob);
                p = data.Length;
            }
            else
            {
                isRawMode = false;
                flags = ReadUInt32(data, ref p);

                while (p < data.Length)
                {
                    uint size = ReadUInt32(data, ref p);
                    if (p + size > data.Length)
                    {
                        throw new InvalidDataException("Malformed shader container: a blob size runs past the end of the file.");
                    }

                    var blob = new byte[size];
                    Array.Copy(data, p, blob, 0, (int)size);
                    blobs.Add(blob);
                    p += (int)size;
                }
            }

            if (p != data.Length)
            {
                throw new InvalidDataException("Malformed shader container: trailing bytes after the last blob.");
            }

            for (int i = 0; i < blobs.Count; i++)
            {
                if (RegionEquals(blobs[i], 0, DxbcMagic) == false)
                {
                    throw new InvalidDataException($"Malformed shader container: blob {i} is not a DXBC container.");
                }
            }

            return new ParsedContainer(headerBytes, isRawMode, flags, blobs);
        }

        private static byte[] Serialize(byte[] headerBytes, bool isRawMode, uint flags, List<byte[]> blobs)
        {
            using (var stream = new MemoryStream())
            {
                stream.Write(headerBytes, 0, headerBytes.Length);

                if (isRawMode)
                {
                    if (blobs.Count != 1)
                    {
                        throw new InvalidOperationException("A raw-mode (v4) shader container holds exactly one blob.");
                    }
                    stream.Write(blobs[0], 0, blobs[0].Length);
                }
                else
                {
                    WriteUInt32(stream, flags);
                    foreach (var blob in blobs)
                    {
                        WriteUInt32(stream, (uint)blob.Length);
                        stream.Write(blob, 0, blob.Length);
                    }
                }

                return stream.ToArray();
            }
        }

        // ------------------------------------------------------------------ resolution auto-detection
        private static (int? width, int? height) DetectResolution(byte[] blob)
        {
            var seen = new Dictionary<int, int>();

            for (int i = 0; i + 4 <= blob.Length; i++)
            {
                float v = BitConverter.ToSingle(blob, i);
                if (v < 256.0f || v > 16384.0f || v != Math.Floor(v))
                {
                    continue;
                }

                int iv = (int)v;
                if ((iv & (iv - 1)) == 0) // skip exact powers of two (tech constants)
                {
                    continue;
                }

                seen.TryGetValue(iv, out int count);
                seen[iv] = count + 1;
            }

            var candidates = new List<int>();
            foreach (var kvp in seen)
            {
                if (kvp.Value >= 2)
                {
                    candidates.Add(kvp.Key);
                }
            }

            bool haveBest = false;
            int bestSum = 0, bestA = 0, bestB = 0;

            foreach (int a in candidates)
            {
                foreach (int b in candidates)
                {
                    if (a == b || a < b)
                    {
                        continue;
                    }

                    double ratio = b / (double)a;
                    if (ratio < 0.4 || ratio > 1.0)
                    {
                        continue;
                    }

                    int sum = seen[a] + seen[b];
                    if (haveBest == false || sum > bestSum || (sum == bestSum && a > bestA))
                    {
                        haveBest = true;
                        bestSum = sum;
                        bestA = a;
                        bestB = b;
                    }
                }
            }

            if (haveBest)
            {
                return (bestA, bestB);
            }

            if (candidates.Count > 0)
            {
                candidates.Sort((x, y) =>
                {
                    int byCount = seen[y].CompareTo(seen[x]);
                    return byCount != 0 ? byCount : y.CompareTo(x);
                });

                int first = candidates[0];
                int? second = candidates.Count > 1 ? candidates[1] : (int?)null;

                if (second.HasValue && second.Value > first)
                {
                    return (second.Value, first);
                }
                return (first, second);
            }

            return (null, null);
        }

        // ------------------------------------------------------------------ byte-pattern helpers
        private static List<int> FindAllNonOverlapping(byte[] data, byte[] pattern)
        {
            var offsets = new List<int>();
            int index = 0;
            int found;
            while ((found = IndexOf(data, pattern, index)) >= 0)
            {
                offsets.Add(found);
                index = found + pattern.Length;
            }
            return offsets;
        }

        private static void WriteAt(byte[] data, List<int> offsets, byte[] value)
        {
            foreach (int offset in offsets)
            {
                Array.Copy(value, 0, data, offset, value.Length);
            }
        }

        private static int IndexOf(byte[] data, byte[] pattern, int startIndex)
        {
            int last = data.Length - pattern.Length;
            for (int i = startIndex; i <= last; i++)
            {
                if (RegionEquals(data, i, pattern))
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool RegionEquals(byte[] data, int offset, byte[] pattern)
        {
            if (offset < 0 || offset + pattern.Length > data.Length)
            {
                return false;
            }
            for (int i = 0; i < pattern.Length; i++)
            {
                if (data[offset + i] != pattern[i])
                {
                    return false;
                }
            }
            return true;
        }

        // ------------------------------------------------------------------ little-endian primitives
        private static uint ReadUInt32(byte[] data, ref int offset)
        {
            uint value = BitConverter.ToUInt32(data, offset);
            offset += 4;
            return value;
        }

        private static uint ReadUInt16(byte[] data, ref int offset)
        {
            ushort value = BitConverter.ToUInt16(data, offset);
            offset += 2;
            return value;
        }

        private static void WriteUInt32(Stream stream, uint value)
        {
            stream.Write(BitConverter.GetBytes(value), 0, 4);
        }

        // ------------------------------------------------------------------ DXBC checksum (modified MD5)
        // Bytes [4:20) of a DXBC blob are a modified MD5 over the blob region [20:]. It matches
        // stock MD5 (RFC 1321) except for the final padding block, where the bit-length is written
        // into the first word (data shifted 4 bytes) and a "nibble count | 1" value into the last
        // word, instead of the usual 64-bit length tail. This mirrors RenderDoc's
        // DXBCContainer::HashContainer / Microsoft's hlsl-specs INF-0004.
        private static byte[] FixChecksum(byte[] blob)
        {
            var checksum = ComputeDxbcChecksum(blob);
            var result = (byte[])blob.Clone();
            Array.Copy(checksum, 0, result, 4, 16);
            return result;
        }

        private static byte[] ComputeDxbcChecksum(byte[] blob)
        {
            if (RegionEquals(blob, 0, DxbcMagic) == false)
            {
                throw new InvalidDataException("Not a DXBC container (missing 'DXBC' magic).");
            }

            int length = blob.Length - 20;
            uint numBits = unchecked((uint)(length * 8));
            uint numBitsP2 = (numBits >> 2) | 1;
            int leftover = length % 64;
            int full = length - leftover;

            var state = new uint[] { 0x67452301, 0xEFCDAB89, 0x98BADCFE, 0x10325476 };

            for (int off = 0; off < full; off += 64)
            {
                Transform(state, blob, 20 + off);
            }

            int tailOffset = 20 + full;

            if (leftover >= 56)
            {
                var block1 = new byte[64];
                Array.Copy(blob, tailOffset, block1, 0, leftover);
                block1[leftover] = 0x80;
                Transform(state, block1, 0);

                var block2 = new byte[64];
                Array.Copy(BitConverter.GetBytes(numBits), 0, block2, 0, 4);
                Array.Copy(BitConverter.GetBytes(numBitsP2), 0, block2, 60, 4);
                Transform(state, block2, 0);
            }
            else
            {
                var final = new byte[64];
                Array.Copy(BitConverter.GetBytes(numBits), 0, final, 0, 4);
                Array.Copy(blob, tailOffset, final, 4, leftover);
                final[4 + leftover] = 0x80;
                Array.Copy(BitConverter.GetBytes(numBitsP2), 0, final, 60, 4);
                Transform(state, final, 0);
            }

            var result = new byte[16];
            for (int i = 0; i < 4; i++)
            {
                Array.Copy(BitConverter.GetBytes(state[i]), 0, result, i * 4, 4);
            }
            return result;
        }

        private static void Transform(uint[] state, byte[] block, int blockOffset)
        {
            uint a = state[0], b = state[1], c = state[2], d = state[3];

            var m = new uint[16];
            for (int j = 0; j < 16; j++)
            {
                m[j] = BitConverter.ToUInt32(block, blockOffset + j * 4);
            }

            for (int i = 0; i < 64; i++)
            {
                uint f;
                int g;
                if (i < 16)
                {
                    f = (b & c) | (~b & d);
                    g = i;
                }
                else if (i < 32)
                {
                    f = (d & b) | (~d & c);
                    g = (5 * i + 1) & 15;
                }
                else if (i < 48)
                {
                    f = b ^ c ^ d;
                    g = (3 * i + 5) & 15;
                }
                else
                {
                    f = c ^ (b | ~d);
                    g = (7 * i) & 15;
                }

                unchecked
                {
                    f = f + a + K[i] + m[g];
                    a = d;
                    d = c;
                    c = b;
                    b = b + RotateLeft(f, S[i]);
                }
            }

            unchecked
            {
                state[0] += a;
                state[1] += b;
                state[2] += c;
                state[3] += d;
            }
        }

        private static uint RotateLeft(uint x, int count)
        {
            return (x << count) | (x >> (32 - count));
        }
    }
}
