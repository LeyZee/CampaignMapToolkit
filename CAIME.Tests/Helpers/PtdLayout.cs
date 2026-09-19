using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CAIME.Tests.Helpers
{
    /// <summary>
    /// Walks a <c>trade_routes.ptd</c> buffer according to the published file-format specification
    /// (Docs/trade_routes_ptd_format.md) and maps any byte offset back to the field that occupies it.
    /// Used to turn "byte 123456 differs" into a diagnosis a reader can act on.
    /// </summary>
    internal sealed class PtdLayout
    {
        private readonly struct Field
        {
            public readonly long Offset;
            public readonly long Length;
            public readonly string Description;

            public Field(long offset, long length, string description)
            {
                Offset      = offset;
                Length      = length;
                Description = description;
            }
        }

        private readonly List<Field> _fields = new List<Field>();

        public string ParseError { get; private set; }

        /// <summary>Best-effort parse: a truncated or malformed file still describes everything up to the break.</summary>
        public static PtdLayout Parse(byte[] bytes)
        {
            var layout = new PtdLayout();
            try
            {
                layout.Walk(bytes);
            }
            catch (Exception ex)
            {
                layout.ParseError = ex.Message;
            }

            return layout;
        }

        /// <summary>Names the field containing <paramref name="offset"/>, e.g. "spline[12].segment[0].Ax".</summary>
        public string Describe(long offset)
        {
            // Fields are appended in ascending offset order, so a binary search over their starts works.
            int low = 0, high = _fields.Count - 1, found = -1;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (_fields[mid].Offset <= offset)
                {
                    found = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            if (found < 0)
                return "before the start of the file";

            var field = _fields[found];
            if (offset >= field.Offset + field.Length)
                return $"past the last described field ({field.Description})";

            return offset == field.Offset
                ? field.Description
                : $"{field.Description} (+{offset - field.Offset} of {field.Length} bytes)";
        }

        private void Walk(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes, writable: false))
            using (var reader = new BinaryReader(stream))
            {
                Add(stream, 8, "header.magic");
                reader.ReadBytes(8);

                Add(stream, 4, "header.version");
                reader.ReadUInt32();

                Add(stream, 4, "header.landRegionCount");
                uint regionCount = reader.ReadUInt32();
                for (uint i = 0; i < regionCount; ++i)
                {
                    Add(stream, 4, $"landRegion[{i}].nameLength");
                    uint length = reader.ReadUInt32();
                    Add(stream, length, $"landRegion[{i}].name");
                    var name = Encoding.ASCII.GetString(reader.ReadBytes((int)length));
                    Rename($"landRegion[{i}].name = \"{name}\"");
                }

                Add(stream, 4, "splineCount");
                uint splineCount = reader.ReadUInt32();
                for (uint i = 0; i < splineCount; ++i)
                {
                    Add(stream, 1, $"spline[{i}].isLand");           reader.ReadByte();
                    Add(stream, 1, $"spline[{i}].isLandBridge");     reader.ReadByte();
                    Add(stream, 1, $"spline[{i}].terminatesAtStart");reader.ReadByte();
                    Add(stream, 1, $"spline[{i}].terminatesAtEnd");  reader.ReadByte();

                    Add(stream, 4, $"spline[{i}].curveCount");
                    uint curveCount = reader.ReadUInt32();
                    for (uint c = 0; c < curveCount; ++c)
                    {
                        foreach (var component in CurveComponents)
                        {
                            Add(stream, 2, $"spline[{i}].curve[{c}].{component}");
                            reader.ReadUInt16();
                        }
                    }
                }

                WalkRoutes(stream, reader, "landRoute");
                WalkRoutes(stream, reader, "seaRoute");

                Add(stream, 4, "crc32");
            }
        }

        private static readonly string[] CurveComponents = { "startX", "startY", "startControlX", "startControlY", "endControlX", "endControlY", "endX", "endY" };

        private void WalkRoutes(Stream stream, BinaryReader reader, string kind)
        {
            Add(stream, 4, $"{kind}Count");
            uint count = reader.ReadUInt32();
            for (uint i = 0; i < count; ++i)
            {
                Add(stream, 4, $"{kind}[{i}].sourceRegionIndex");      reader.ReadInt32();
                Add(stream, 4, $"{kind}[{i}].destinationRegionIndex"); reader.ReadInt32();

                Add(stream, 4, $"{kind}[{i}].splineReferenceCount");
                uint references = reader.ReadUInt32();
                for (uint s = 0; s < references; ++s)
                {
                    Add(stream, 4, $"{kind}[{i}].splineReference[{s}]");
                    reader.ReadUInt32();
                }
            }
        }

        private void Add(Stream stream, long length, string description)
            => _fields.Add(new Field(stream.Position, length, description));

        private void Rename(string description)
        {
            var last = _fields[_fields.Count - 1];
            _fields[_fields.Count - 1] = new Field(last.Offset, last.Length, description);
        }
    }
}
