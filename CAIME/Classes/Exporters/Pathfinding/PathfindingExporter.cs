using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using CAIME.Pathfinding;
using CAIME.Pathfinding.Heuristic;
using CAIME.Pathfinding.Roads;
using Force.Crc32;

namespace CAIME
{
    /// <summary>
    /// Writes the game-ready <c>pathfinding.ppd</c> for a project. One instance owns one export's
    /// state; the Write* seams below are stateless and take their writer explicitly.
    /// </summary>
    public sealed class PathfindingExporter
    {
        private class ProcessedData
        {
            public byte[]                   EdgesData;
            public List<ushort>             EdgeCosts;

            public byte[]                   HLCIAreas;
            public List<HLCIPair>           HLCIAreasPairs;
            public int                      HLCIAreasCount;

            public BridgesData              BridgesData;
            public RoadSegment[]            RoadSegments;
            public List<BeachesContainer>   BeachesData;
            public BordersData              BordersData;
            public Border[]                 PassableLandBorders;

            public RegionPassableHexes[]    RegionEdges;
            public int[]                    RegionEdgeData;

            public List<TileGroupItem>      TileGroupsData;
            public uint[]                   HeuristicCacheData;
            public Restriction[]            RestrictionsData;

            public List<string>             LandRegionKeys;
            public int                      TotalRegionsCount;
            public int                      LandRegionsCount;
            public int                      SeaRegionsCount;
        }

        private readonly static long        PPD_MAGIC_NUMBER    = 0x0A1A0A0D44505089;
        private readonly static int         PPD_VERSION         = 0x00000002;
        private readonly static string      FILE_NAME           = "pathfinding";

        private readonly string             _exportPath;
        private readonly string             _debugPath;
        private BinaryWriter                _writer;

        private PathfindingExporter(string exportPath, string debugPath)
        {
            _exportPath = exportPath;
            _debugPath  = debugPath;
        }

        /// <returns>False if the export failed; the reason is logged.</returns>
        public static bool Export(Project project, string exportPath, string debugPath)
        {
            return new PathfindingExporter(exportPath, debugPath).Run(project);
        }

        private bool Run(Project project)
        {
            var exportTimer = Stopwatch.StartNew();
            LoggerViewModel.Log("PathfindingExporter: Starting pathfinding export...", LogLevel.Info);

#if DEBUG
            bool writeDebug = true;
#else
            bool writeDebug = false;
#endif

            var mapHexFile  = project.MapHexFile;
            var db          = project.Database;
            var outData     = new ProcessedData();

            try
            {
                _writer = new BinaryWriter(new FileStream($"{_exportPath}{FILE_NAME}.bin", FileMode.Create, FileAccess.ReadWrite));

                try
                {
                    ExportSequential(mapHexFile, db, outData, writeDebug);

                    // ================================= STEP 5 =================================
                    // Write the processed pathfinding data to a file.
                    // ==========================================================================

                    WriteProcessedData(mapHexFile, outData);
                }
                finally
                {
                    _writer.Dispose();
                    _writer = null;
                }

                // ================================= STEP 6 =================================
                // Append CRC32 checksum of the processed data.
                // ==========================================================================

                Finalise();
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"PathfindingExporter: Export failed - {ex.Message}", LogLevel.ErrorMessageBox);
                DeleteIntermediateFile();
                return false;
            }

            exportTimer.Stop();
            LoggerViewModel.Log($"PathfindingExporter: Total export time: {exportTimer.ElapsedMilliseconds}ms", LogLevel.Info);

            return true;
        }

        /// <summary>Removes the intermediate .bin a failed export would otherwise leave behind.</summary>
        private void DeleteIntermediateFile()
        {
            try
            {
                var binPath = $"{_exportPath}{FILE_NAME}.bin";
                if (File.Exists(binPath))
                {
                    File.Delete(binPath);
                }
            }
            catch (Exception)
            {
                // Best effort - the export has already failed and been reported.
            }
        }

        private void ExportSequential(MapHexFile mapHexFile, DatabaseViewModel db, ProcessedData outData, bool writeDebug)
        {
            outData.EdgesData = new byte[mapHexFile.Capacity * 8];

            mapHexFile.UpdateHexTypes();
            // Keep the authored masks loaded from the source map; only the layers the user actually
            // edited are rebuilt (fully on import, incrementally on paint). See MapHexFile.
            mapHexFile.EnsureRegionEdgeMasks();
            mapHexFile.EnsureRoadEdgeMasks();

            var bridgesGenerator = new BridgesGenerator(mapHexFile, _debugPath, writeDebug);
            var hlciGenerator = new HLCIGenerator(mapHexFile, _debugPath);
            var roadsGenerator = new RoadsGenerator(mapHexFile, _debugPath);
            var bordersGenerator = new BordersGenerator(mapHexFile, _debugPath, writeDebug);
            var beachesGenerator = new BeachesGenerator(mapHexFile, _debugPath);
            var tileGroupsGenerator = new TileGroupsGenerator(mapHexFile, _debugPath);
            var edgeCostsGenerator = new MovementCostsGenerator(mapHexFile, db, _debugPath);
            var regionEdgeGenerator = new RegionEdgesGenerator(mapHexFile, _debugPath);
            var heuristicCacheGenerator = new HeuristicCacheGenerator(mapHexFile, _debugPath);
            var bordersPerTileGroupGenerator = new BordersCountPerTileGroupGenerator(mapHexFile, _debugPath);

            PrepareRegions(mapHexFile, outData);

            var timer = Stopwatch.StartNew();
            bridgesGenerator.Generate(out outData.BridgesData);
            timer.Stop();
            LoggerViewModel.Log($"PathfindingExporter: BridgesGenerator - {timer.ElapsedMilliseconds}ms", LogLevel.Info);

            timer.Restart();
            hlciGenerator.Generate(out outData.HLCIAreas, in outData.BridgesData);
            outData.HLCIAreasCount = hlciGenerator.GetAreasCount();
            outData.HLCIAreasPairs = hlciGenerator.HLCIPairs;
            timer.Stop();
            LoggerViewModel.Log($"PathfindingExporter: HLCIGenerator - {timer.ElapsedMilliseconds}ms", LogLevel.Info);

            timer.Restart();
            roadsGenerator.Generate(out outData.RoadSegments);
            timer.Stop();
            LoggerViewModel.Log($"PathfindingExporter: RoadsGenerator - {timer.ElapsedMilliseconds}ms", LogLevel.Info);

            timer.Restart();
            tileGroupsGenerator.Generate(ref outData.EdgesData, out outData.TileGroupsData, in outData.HLCIAreas, in outData.TotalRegionsCount, in outData.HLCIAreasCount);
            timer.Stop();
            LoggerViewModel.Log($"PathfindingExporter: TileGroupsGenerator - {timer.ElapsedMilliseconds}ms", LogLevel.Info);

            var tileGroupIndices = TileGroupsGenerator.ExtractTileGroupIndices(outData.EdgesData);

            timer.Restart();
            bordersGenerator.Generate(out outData.PassableLandBorders, out outData.BordersData, outData.LandRegionsCount, tileGroupIndices);
            timer.Stop();
            LoggerViewModel.Log($"PathfindingExporter: BordersGenerator - {timer.ElapsedMilliseconds}ms", LogLevel.Info);

            timer.Restart();
            edgeCostsGenerator.Generate(ref outData.EdgesData);
            outData.EdgeCosts = edgeCostsGenerator.GetMoveCosts();
            timer.Stop();
            LoggerViewModel.Log($"PathfindingExporter: MovementCostsGenerator - {timer.ElapsedMilliseconds}ms", LogLevel.Info);

            timer.Restart();
            regionEdgeGenerator.Generate(out outData.RegionEdges, in outData.PassableLandBorders);
            timer.Stop();
            LoggerViewModel.Log($"PathfindingExporter: RegionEdgesGenerator - {timer.ElapsedMilliseconds}ms", LogLevel.Info);

            timer.Restart();
            beachesGenerator.Generate(out outData.BeachesData, in outData.HLCIAreas, in outData.HLCIAreasPairs);
            timer.Stop();
            LoggerViewModel.Log($"PathfindingExporter: BeachesGenerator - {timer.ElapsedMilliseconds}ms", LogLevel.Info);

            timer.Restart();
            heuristicCacheGenerator.Generate(db, out outData.HeuristicCacheData, outData.EdgesData, in outData.EdgeCosts, in outData.TileGroupsData, outData.BridgesData);
            timer.Stop();
            LoggerViewModel.Log($"PathfindingExporter: HeuristicCacheGenerator - {timer.ElapsedMilliseconds}ms", LogLevel.Info);

            timer.Restart();
            bordersPerTileGroupGenerator.Generate(out outData.RegionEdgeData, in outData.EdgesData, in outData.TileGroupsData, in outData.BordersData);
            timer.Stop();
            LoggerViewModel.Log($"PathfindingExporter: BordersCountPerTileGroupGenerator - {timer.ElapsedMilliseconds}ms", LogLevel.Info);

            if (mapHexFile.MinorFileVersion > 0x0D)
            {
                var restrictionsGenerator = new RestrictionsGenerator(mapHexFile, _debugPath);
                timer.Restart();
                restrictionsGenerator.Generate(out outData.RestrictionsData);
                timer.Stop();
                LoggerViewModel.Log($"PathfindingExporter: RestrictionsGenerator - {timer.ElapsedMilliseconds}ms", LogLevel.Info);
            }
        }

        private static void PrepareRegions(MapHexFile mapHexFile, ProcessedData data)
        {
            data.LandRegionKeys      = mapHexFile.LandRegions;
            data.LandRegionsCount    = mapHexFile.LandRegions.Count;
            data.SeaRegionsCount     = mapHexFile.SeaRegions.Count;
            data.TotalRegionsCount   = data.LandRegionsCount + data.SeaRegionsCount;
        }

        private void Finalise()
        {
            WriteChecksum();

            LoggerViewModel.Log("Successfuly exported pathfinding.ppd file.", LogLevel.Info);
        }

        private void WriteProcessedData(MapHexFile mapHexFile, ProcessedData data)
        {
            WriteHeader(_writer, mapHexFile, data);
            WriteHexCellData(_writer, data);
            WriteEdgeCosts(_writer, data);
            WriteTileGroups(_writer, data.TileGroupsData);
            WriteRegionEdges(_writer, data);
            WriteBeaches(_writer, mapHexFile, data);
            WriteHlciConnections(_writer, data);
            WriteBridges(_writer, data.BridgesData);
            WriteHeuristicCache(_writer, data);
            WritePassableBorderHexes(_writer, data.BordersData);
            WriteCumulativeBorderHexes(_writer, data);
            WriteRoads(_writer, mapHexFile, data);
            WriteRestrictions(_writer, data.RestrictionsData);
        }

        private static void WriteHeader(BinaryWriter writer, MapHexFile mapHexFile, ProcessedData data)
        {
            writer.Write(PPD_MAGIC_NUMBER);
            writer.Write(PPD_VERSION);

            WriteRegionsList(writer, data);
            WriteMapSize(writer, (ushort)mapHexFile.MapWidth, (ushort)mapHexFile.MapHeight);
        }

        private static void WriteRegionsList(BinaryWriter writer, ProcessedData data)
        {
            writer.Write(data.LandRegionKeys.Count);

            foreach (var regionKey in data.LandRegionKeys)
            {
                writer.Write(regionKey.Length);
                writer.Write(Encoding.ASCII.GetBytes(regionKey));
            }
        }

        private static void WriteMapSize(BinaryWriter writer, ushort width, ushort height)
        {
            writer.Write(width);
            writer.Write(height);
        }

        private static void WriteHexCellData(BinaryWriter writer, ProcessedData data)
        {
            writer.Write(data.EdgesData);
        }

        private static void WriteEdgeCosts(BinaryWriter writer, ProcessedData data)
        {
            writer.Write(data.EdgeCosts.Count);
            foreach (var cost in data.EdgeCosts)
            {
                writer.Write(cost);
            }
        }

        internal static void WriteTileGroups(BinaryWriter writer, List<TileGroupItem> tileGroupsData)
        {
            writer.Write((ushort)tileGroupsData.Count);
            foreach (var tileGroup in tileGroupsData)
            {
                writer.Write(tileGroup.HighLevelConnectivityIndex);
                writer.Write(tileGroup.RegionIndex);
            }
        }

        private static void WriteRegionEdges(BinaryWriter writer, ProcessedData data)
        {
            foreach (var landRegionEdges in data.RegionEdges)
            {
                writer.Write((ushort)landRegionEdges.Hexes.Count);
                for (int i = 0; i < landRegionEdges.Hexes.Count; ++i)
                {
                    var hex = landRegionEdges.Hexes[i];
                    var mask = landRegionEdges.HexMask[i];

                    writer.Write((ushort)hex.Q);
                    writer.Write((ushort)hex.R);
                    writer.Write(mask);
                }
            }
        }

        private static void WriteBeaches(BinaryWriter writer, MapHexFile mapHexFile, ProcessedData data)
        {
            WriteBeaches(writer, mapHexFile, data.BeachesData);
        }

        internal static void WriteBeaches(BinaryWriter writer, MapHexFile mapHexFile, List<BeachesContainer> beachesData)
        {
            writer.Write((ushort)beachesData.Count);
            foreach (var beachesGroup in beachesData)
            {
                writer.Write((ushort)beachesGroup.AreaIndexEnter);
                writer.Write((ushort)beachesGroup.AreaIndexLeave);

                var landToSeaHexes = beachesGroup.BeachesEnter;
                var seaToLandHexes = beachesGroup.BeachesLeave;

                writer.Write((ushort)landToSeaHexes.Count);
                foreach (var beachHex in landToSeaHexes)
                {
                    var hex = mapHexFile.HexData[beachHex.HexIndex];
                    writer.Write((ushort)hex.Q);
                    writer.Write((ushort)hex.R);
                    writer.Write(beachHex.EdgeMask);
                }

                writer.Write((ushort)seaToLandHexes.Count);
                foreach (var beachHex in seaToLandHexes)
                {
                    var hex = mapHexFile.HexData[beachHex.HexIndex];
                    writer.Write((ushort)hex.Q);
                    writer.Write((ushort)hex.R);
                    writer.Write(beachHex.EdgeMask);
                }
            }
        }

        private static void WriteHlciConnections(BinaryWriter writer, ProcessedData data)
        {
            WriteHlciConnections(writer, data.HLCIAreasPairs);
        }

        internal static void WriteHlciConnections(BinaryWriter writer, List<HLCIPair> hlciAreasPairs)
        {
            writer.Write((ushort)hlciAreasPairs.Count);
            foreach (var connection in hlciAreasPairs)
            {
                if (connection.AreaIndexEnter < connection.AreaIndexLeave)
                {
                    writer.Write(connection.AreaIndexEnter);
                    writer.Write(connection.AreaIndexLeave);
                }
                else
                {
                    writer.Write(connection.AreaIndexLeave);
                    writer.Write(connection.AreaIndexEnter);
                }
            }
        }

        internal static void WriteBridges(BinaryWriter writer, BridgesData bridgesData)
        {
            writer.Write((ushort)bridgesData.BridgeEdges.Count);
            foreach (var bridgeEdge in bridgesData.BridgeEdges)
            {
                writer.Write((ushort)bridgeEdge.Parts[0].Count);
                foreach (var hex in bridgeEdge.Parts[0])
                {
                    writer.Write((ushort)hex.Q);
                    writer.Write((ushort)hex.R);
                }

                writer.Write((ushort)bridgeEdge.Parts[1].Count);
                foreach (var hex in bridgeEdge.Parts[1])
                {
                    writer.Write((ushort)hex.Q);
                    writer.Write((ushort)hex.R);
                }
            }
        }

        private static void WriteHeuristicCache(BinaryWriter writer, ProcessedData data)
        {
            WriteHeuristicCache(writer, data.HeuristicCacheData);
        }

        internal static void WriteHeuristicCache(BinaryWriter writer, uint[] heuristicCacheData)
        {
            var byteArray = new byte[heuristicCacheData.Length * sizeof(int)];
            Buffer.BlockCopy(heuristicCacheData, 0, byteArray, 0, byteArray.Length);
            writer.Write(byteArray);
        }

        internal static void WritePassableBorderHexes(BinaryWriter writer, BordersData bordersData)
        {
            writer.Write(bordersData.Hexes.Count);
            foreach (var hex in bordersData.Hexes)
            {
                writer.Write((ushort)hex.Q);
                writer.Write((ushort)hex.R);
            }
        }

        private static void WriteCumulativeBorderHexes(BinaryWriter writer, ProcessedData data)
        {
            WriteCumulativeBorderHexes(writer, data.RegionEdgeData);
        }

        internal static void WriteCumulativeBorderHexes(BinaryWriter writer, int[] cumulativeBorderHexes)
        {
            foreach (var edgesCount in cumulativeBorderHexes)
            {
                writer.Write(edgesCount);
            }
        }

        private static void WriteRoads(BinaryWriter writer, MapHexFile mapHexFile, ProcessedData data)
        {
            WriteRoads(writer, mapHexFile, data.RoadSegments);
        }

        internal static void WriteRoads(BinaryWriter writer, MapHexFile mapHexFile, RoadSegment[] roadSegments)
        {
            writer.Write((ushort)roadSegments.Length);
            foreach (var segment in roadSegments)
            {
                writer.Write(segment.RouteGroup.RegionPairs.Count);
                foreach (var regionsPair in segment.RouteGroup.RegionPairs)
                {
                    writer.Write((ushort)(regionsPair.SrcRegionIndex + 1));
                    writer.Write((ushort)(regionsPair.DstRegionIndex + 1));
                }

                writer.Write((ushort)segment.Road.Hexes.Count);
                foreach (var roadHex in segment.Road.Hexes)
                {
                    var hex = mapHexFile.HexData[roadHex.HexIndex];
                    writer.Write((ushort)hex.Q);
                    writer.Write((ushort)hex.R);
                    writer.Write(roadHex.EdgeMask);
                }
            }
        }

        internal static void WriteRestrictions(BinaryWriter writer, Restriction[] restrictionsData)
        {
            if (restrictionsData == null) // Rome 2 does not have restrictions support
                return;

            var restrictionsCount = 0;
            for (int lvl = 0; lvl < Hex.MAX_RESTRICTIONS_COUNT; ++lvl)
            {
                if (restrictionsData[lvl].Hexes.Count > 0)
                {
                    ++restrictionsCount;
                }
            }

            writer.Write((byte)restrictionsCount);
            for (int lvl = 0; lvl < Hex.MAX_RESTRICTIONS_COUNT; ++lvl)
            {
                var restriction = restrictionsData[lvl];
                if (restriction.Hexes.Count > 0)
                {
                    writer.Write(restriction.Hexes.Count);
                    for (int i = 0; i < restriction.Hexes.Count; ++i)
                    {
                        writer.Write((ushort)restriction.Hexes[i].Q);
                        writer.Write((ushort)restriction.Hexes[i].R);
                        writer.Write(restriction.EdgeMasks[i]);
                    }
                }
            }
        }

        private void WriteChecksum()
        {
            var binPath     = $"{_exportPath}{FILE_NAME}.bin";
            var ppdPath     = $"{_exportPath}{FILE_NAME}.ppd";
            var inputData   = File.ReadAllBytes(binPath);
            var outputData  = new byte[inputData.Length + 4];

            Array.Copy(inputData, 0, outputData, 0, inputData.Length);
            Crc32Algorithm.ComputeAndWriteToEnd(outputData);

            File.Delete(binPath);
            File.WriteAllBytes(ppdPath, outputData);
        }
    }
}
