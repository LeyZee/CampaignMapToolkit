using System;
using System.Diagnostics;
using System.IO;

namespace CAIME.TradeNetwork
{
    /// <summary>
    /// Exports <c>trade_routes.ptd</c>: the trade network the mapper painted, traced into routes and
    /// fitted to curves the campaign map can draw.
    /// </summary>
    public static class TradeRoutesProcessor
    {
        private const string FileName = "trade_routes.ptd";

        /// <summary>
        /// Writes <c>trade_routes.ptd</c> into <paramref name="exportPath"/>, which is a directory.
        /// </summary>
        /// <param name="project">A project whose map is fully loaded. It is read, never modified.</param>
        /// <param name="exportPath">The directory to write into, without a trailing separator.</param>
        /// <returns>False if the export failed; the reason is logged.</returns>
        public static bool Export(Project project, string exportPath)
        {
            try
            {
                return Run(project, exportPath);
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"TradeRoutesProcessor: Export failed - {ex.Message}", LogLevel.ErrorMessageBox);
                return false;
            }
        }

        private static bool Run(Project project, string exportPath)
        {
            var total = Stopwatch.StartNew();
            LoggerViewModel.Log("TradeRoutesProcessor: Starting trade routes export...", LogLevel.Info);

            var map   = project.MapHexFile;
            var stage = Stopwatch.StartNew();
            var grid  = TradeGridBuilder.FromMap(map);
            long extractMs = stage.ElapsedMilliseconds;

            // RegionId lives in the combined [land..., sea...] space and reaches Land + Sea - 1;
            // the +1 is the slot the -1 "unassigned" sentinel is shifted into.
            int regionSlots = map.LandRegions.Count + map.SeaRegions.Count + 1;

            stage.Restart();
            var connectivity = new ConnectivityScanner(grid, regionSlots).Scan();
            long scanMs      = stage.ElapsedMilliseconds;

            stage.Restart();
            var tables   = new RoutePlanner(grid, connectivity).Plan();
            long searchMs = stage.ElapsedMilliseconds;

            stage.Restart();
            var geometry = new TradeRoutesGeometryBuilder(grid).Build(tables);
            long fitMs   = stage.ElapsedMilliseconds;

            stage.Restart();
            var bytes = TradeRoutesWriter.Write(map.LandRegions, geometry);
            var path  = Path.Combine(exportPath, FileName);
            File.WriteAllBytes(path, bytes);
            long writeMs = stage.ElapsedMilliseconds;

            LoggerViewModel.Log(
                $"TradeRoutesProcessor: {geometry.LandRoutes.Count} land routes, {geometry.SeaRoutes.Count} sea routes, " +
                $"{geometry.Splines.Count} splines over {connectivity.SeaBodyCount} sea bodies.", LogLevel.Info);
            LoggerViewModel.Log(
                $"TradeRoutesProcessor: extract {extractMs}ms, connectivity {scanMs}ms, search {searchMs}ms, curves {fitMs}ms, " +
                $"serialization {writeMs}ms.", LogLevel.Info);
            LoggerViewModel.Log($"TradeRoutesProcessor: wrote {path} ({bytes.Length} bytes).", LogLevel.Info);
            LoggerViewModel.Log($"TradeRoutesProcessor: Finished trade routes export in {total.ElapsedMilliseconds}ms.", LogLevel.Info);

            return true;
        }
    }
}
