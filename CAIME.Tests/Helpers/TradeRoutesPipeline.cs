using System;
using System.IO;
using CAIME;
using CAIME.TradeNetwork;

namespace CAIME.Tests.Helpers
{
    /// <summary>
    /// Runs the trade-route export from a map.hex to a trade_routes.ptd through the exporter's public
    /// entry point, so tests exercise exactly what the application calls.
    /// </summary>
    internal static class TradeRoutesPipeline
    {
        /// <summary>Exports into <paramref name="ptdPath"/>, whatever the file is named.</summary>
        public static void Export(string mapHexPath, string ptdPath)
        {
            File.WriteAllBytes(ptdPath, Export(mapHexPath));
        }

        /// <summary>Exports <paramref name="mapHexPath"/> and returns the produced file's bytes.</summary>
        public static byte[] Export(string mapHexPath)
        {
            var project = new Project();
            if (!project.MapHexFile.Load(mapHexPath))
            {
                throw new InvalidOperationException($"Failed to load map.hex from {mapHexPath}.");
            }

            // Export writes into a directory under a fixed name, so give it one of its own.
            var directory = Path.Combine(Path.GetTempPath(), $"caime_traderoutes_{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);

            try
            {
                if (!TradeRoutesProcessor.Export(project, directory.TrimEnd(Path.DirectorySeparatorChar)))
                {
                    throw new InvalidOperationException($"Trade routes export failed for {mapHexPath}.");
                }

                return File.ReadAllBytes(Path.Combine(directory, "trade_routes.ptd"));
            }
            finally
            {
                try
                {
                    Directory.Delete(directory, recursive: true);
                }
                catch (IOException)
                {
                    // A leftover temp directory must not fail an otherwise good export.
                }
            }
        }
    }
}
