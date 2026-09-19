using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CAIME.Tests.Helpers
{
    /// <summary>A trade route between two regions, as read back from a PTD file.</summary>
    internal sealed class PtdRoute
    {
        public int Source { get; }
        public int Destination { get; }
        public IReadOnlyList<uint> SplineReferences { get; }

        public PtdRoute(int source, int destination, IReadOnlyList<uint> splineReferences)
        {
            Source           = source;
            Destination      = destination;
            SplineReferences = splineReferences;
        }
    }

    /// <summary>One spline, reduced to what the tests assert on.</summary>
    internal sealed class PtdSpline
    {
        public bool IsLand { get; }
        public bool IsLandBridge { get; }
        public bool TerminatesAtStart { get; }
        public bool TerminatesAtEnd { get; }
        public int CurveCount { get; }

        public PtdSpline(bool isLand, bool isLandBridge, bool terminatesAtStart, bool terminatesAtEnd, int curveCount)
        {
            IsLand            = isLand;
            IsLandBridge      = isLandBridge;
            TerminatesAtStart = terminatesAtStart;
            TerminatesAtEnd   = terminatesAtEnd;
            CurveCount        = curveCount;
        }
    }

    /// <summary>
    /// Reads a <c>trade_routes.ptd</c> file, following Docs/trade_routes_ptd_format.md. This is an
    /// independent reader owned by the tests: the exporter only writes, so parsing its output here
    /// checks the written bytes against the published format rather than against the writer.
    /// </summary>
    internal sealed class PtdFile
    {
        private static readonly byte[] Magic = { 0x89, (byte)'P', (byte)'T', (byte)'D', 0x0D, 0x0A, 0x1A, 0x0A };

        private PtdFile() { }

        public uint Version { get; private set; }
        public IReadOnlyList<string> LandRegions { get; private set; }
        public IReadOnlyList<PtdSpline> Splines { get; private set; }
        public IReadOnlyList<PtdRoute> LandRoutes { get; private set; }
        public IReadOnlyList<PtdRoute> SeaRoutes { get; private set; }

        public static PtdFile Read(string path)
        {
            return Read(File.ReadAllBytes(path));
        }

        public static PtdFile Read(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes, writable: false))
            using (var reader = new BinaryReader(stream))
            {
                if (!reader.ReadBytes(Magic.Length).SequenceEqual(Magic))
                {
                    throw new InvalidDataException("Not a PTD file: magic header mismatch.");
                }

                return new PtdFile
                {
                    Version     = reader.ReadUInt32(),
                    LandRegions = ReadRegionNames(reader),
                    Splines     = ReadSplines(reader),
                    LandRoutes  = ReadRoutes(reader),
                    SeaRoutes   = ReadRoutes(reader),
                };
            }
        }

        private static List<string> ReadRegionNames(BinaryReader reader)
        {
            uint count = reader.ReadUInt32();
            var names  = new List<string>((int)count);

            for (uint i = 0; i < count; ++i)
            {
                int length = (int)reader.ReadUInt32();
                names.Add(Encoding.ASCII.GetString(reader.ReadBytes(length)));
            }

            return names;
        }

        private static List<PtdSpline> ReadSplines(BinaryReader reader)
        {
            uint count   = reader.ReadUInt32();
            var  splines = new List<PtdSpline>((int)count);

            for (uint i = 0; i < count; ++i)
            {
                bool isLand            = reader.ReadBoolean();
                bool isLandBridge      = reader.ReadBoolean();
                bool terminatesAtStart = reader.ReadBoolean();
                bool terminatesAtEnd   = reader.ReadBoolean();

                uint curveCount = reader.ReadUInt32();
                reader.BaseStream.Seek(curveCount * 16, SeekOrigin.Current);

                splines.Add(new PtdSpline(isLand, isLandBridge, terminatesAtStart, terminatesAtEnd, (int)curveCount));
            }

            return splines;
        }

        private static List<PtdRoute> ReadRoutes(BinaryReader reader)
        {
            uint count  = reader.ReadUInt32();
            var  routes = new List<PtdRoute>((int)count);

            for (uint i = 0; i < count; ++i)
            {
                int source      = reader.ReadInt32();
                int destination = reader.ReadInt32();

                uint referenceCount = reader.ReadUInt32();
                var  references     = new List<uint>((int)referenceCount);
                for (uint r = 0; r < referenceCount; ++r)
                {
                    references.Add(reader.ReadUInt32());
                }

                routes.Add(new PtdRoute(source, destination, references));
            }

            return routes;
        }
    }
}
