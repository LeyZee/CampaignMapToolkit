using System.Collections.Generic;
using System.IO;
using System.Text;
using Force.Crc32;

namespace CAIME.TradeNetwork
{
    /// <summary>
    /// Encodes the trade network as a version 1 <c>trade_routes.ptd</c> buffer: little-endian, no
    /// padding, every count immediately in front of what it counts.
    /// </summary>
    internal static class TradeRoutesWriter
    {
        private static readonly byte[] Magic = { 0x89, 0x50, 0x54, 0x44, 0x0D, 0x0A, 0x1A, 0x0A };

        private const uint Version = 1;

        public static byte[] Write(IReadOnlyList<string> landRegionNames, TradeRoutesGeometry geometry)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
                {
                    writer.Write(Magic);
                    writer.Write(Version);

                    WriteRegionNames(writer, landRegionNames);
                    WriteSplines(writer, geometry.Splines);
                    WriteRoutes(writer, geometry.LandRoutes);
                    WriteRoutes(writer, geometry.SeaRoutes);

                    writer.Flush();

                    var body = stream.ToArray();
                    writer.Write(Crc32Algorithm.Compute(body, 0, body.Length));
                }

                return stream.ToArray();
            }
        }

        private static void WriteRegionNames(BinaryWriter writer, IReadOnlyList<string> names)
        {
            writer.Write((uint)names.Count);

            for (int i = 0; i < names.Count; ++i)
            {
                var bytes = Encoding.ASCII.GetBytes(names[i]);
                writer.Write((uint)bytes.Length);
                writer.Write(bytes);
            }
        }

        private static void WriteSplines(BinaryWriter writer, IReadOnlyList<Spline> splines)
        {
            writer.Write((uint)splines.Count);

            for (int i = 0; i < splines.Count; ++i)
            {
                var spline = splines[i];

                writer.Write(spline.IsLand);
                writer.Write(spline.IsLandBridge);
                writer.Write(spline.TerminatesAtStart);
                writer.Write(spline.TerminatesAtEnd);
                writer.Write((uint)spline.Curves.Length);

                for (int c = 0; c < spline.Curves.Length; ++c)
                {
                    var curve = spline.Curves[c];

                    writer.Write(curve.StartX);
                    writer.Write(curve.StartY);
                    writer.Write(curve.StartControlX);
                    writer.Write(curve.StartControlY);
                    writer.Write(curve.EndControlX);
                    writer.Write(curve.EndControlY);
                    writer.Write(curve.EndX);
                    writer.Write(curve.EndY);
                }
            }
        }

        private static void WriteRoutes(BinaryWriter writer, IReadOnlyList<SplineRoute> routes)
        {
            writer.Write((uint)routes.Count);

            for (int i = 0; i < routes.Count; ++i)
            {
                var route = routes[i];

                writer.Write(route.SourceRegion);
                writer.Write(route.DestinationRegion);
                writer.Write((uint)route.SplineReferences.Length);

                for (int r = 0; r < route.SplineReferences.Length; ++r)
                {
                    writer.Write(route.SplineReferences[r]);
                }
            }
        }
    }
}
