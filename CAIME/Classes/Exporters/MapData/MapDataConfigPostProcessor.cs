using System;
using System.IO;
using System.Linq;
using CAIME.Classes.Editor.MapDataEditor;
using CAIME.Models;
using EsfLibrary;

namespace CAIME.Exporters
{
    public static class MapDataConfigPostProcessor
    {
        public static void PostProcess(Project project, string mapDataEsfPath)
        {
            var metadataPath = Path.Combine(project.ProjectPath, CaimeMetadata.FILENAME);

            if (!File.Exists(metadataPath))
            {
                LoggerViewModel.Log($"Map Data post-process: no metadata file found at {metadataPath}, skipping auto-patch.", LogLevel.Info);
                return;
            }

            LoggerViewModel.Log($"Map Data post-process: found metadata file at {metadataPath}.", LogLevel.Info);

            CaimeMetadata metadata;
            try
            {
                metadata = CaimeMetadata.Load(metadataPath);
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"Map Data post-process: failed to read metadata file - {ex.Message}", LogLevel.Error);
                return;
            }

            if (metadata == null)
            {
                LoggerViewModel.Log("Map Data post-process: metadata file is empty or invalid.", LogLevel.Error);
                return;
            }

            if (!string.Equals(metadata.CampaignMapName, project.MapName, StringComparison.OrdinalIgnoreCase))
            {
                LoggerViewModel.Log($"Map Data post-process: metadata campaign map name '{metadata.CampaignMapName}' does not match current map '{project.MapName}'. Skipping.", LogLevel.Warning);
                return;
            }

            if (string.IsNullOrEmpty(metadata.MapDataConfigPath) || !File.Exists(metadata.MapDataConfigPath))
            {
                LoggerViewModel.Log($"Map Data post-process: map_data_config file not found at '{metadata.MapDataConfigPath}'. Skipping.", LogLevel.Error);
                return;
            }

            var config = new MapDataConfig();
            if (!config.ApplyConfigFile(metadata.MapDataConfigPath, out MapDataRegion[] regions))
            {
                LoggerViewModel.Log("Map Data post-process: failed to load map_data_config. Skipping.", LogLevel.Error);
                return;
            }

            EsfFile mapDataFile;
            try
            {
                mapDataFile = EsfCodecUtil.LoadEsfFile(mapDataEsfPath);
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"Map Data post-process: failed to load map_data.esf - {ex.Message}", LogLevel.Error);
                return;
            }

            var rootNode = mapDataFile.RootNode as RecordNode;
            if (rootNode.Version > 0)
            {
                LoggerViewModel.Log("Map Data post-process: map_data.esf version not supported for config patching. Skipping.", LogLevel.Warning);
                return;
            }

            var regionsDataNode  = rootNode["REGIONS_DATA"];
            var regionsBlockNode = regionsDataNode["REGIONS_BLOCK"];

            foreach (var child in regionsBlockNode.Children)
            {
                var regionDataNode = child["REGION_DATA"];
                var regionKey      = (regionDataNode.Values.First() as EsfValueNode<string>).Value;
                var hasTownInfo    = (regionDataNode.Value[15] as EsfValueNode<bool>).Value;

                if (!hasTownInfo)
                    continue;

                var entry = regions.FirstOrDefault(r => r != null && r.Name == regionKey);
                if (entry == null)
                {
                    LoggerViewModel.Log($"Map Data post-process: region '{regionKey}' not found in config. Skipping region.", LogLevel.Info);
                    continue;
                }

                var townInfoNode  = regionDataNode["SETTLEMENT_INFO"];
                var slotArrayNode = townInfoNode["SLOT_ARRAY_BLOCK"] as RecordArrayNode;
                var hasPort       = townInfoNode["PORT_AREA_BLOCK"].Children.Count > 0;
                var numOldSlots   = slotArrayNode.Children.Count;
                var numNewSlots   = entry.SlotsCount;
                var numSlotsDiff  = numNewSlots - numOldSlots;

                if (numSlotsDiff < 0)
                {
                    var diffCount = -numSlotsDiff;
                    if (hasPort) ++diffCount;

                    if (diffCount >= numOldSlots)
                    {
                        LoggerViewModel.Log($"Map Data post-process: cannot remove more slots than '{regionKey}' has. Skipping region.", LogLevel.Warning);
                        continue;
                    }

                    slotArrayNode.Value.RemoveRange(numOldSlots - diffCount, diffCount);
                    slotArrayNode.Modified = true;
                }
                else if (numSlotsDiff > 0)
                {
                    var mainSlotArray = slotArrayNode.Children.First();
                    if (hasPort) --numSlotsDiff;

                    for (int n = numSlotsDiff; n != 0; --n)
                        slotArrayNode.Value.Add(mainSlotArray);

                    slotArrayNode.Modified = true;
                }
            }

            rootNode.Modified = true;

            try
            {
                EsfCodecUtil.WriteEsfFile(mapDataEsfPath, mapDataFile);
                LoggerViewModel.Log($"Map Data post-process: successfully patched map_data.esf with config '{metadata.MapDataConfigPath}'.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"Map Data post-process: failed to write patched map_data.esf - {ex.Message}", LogLevel.Error);
            }
        }
    }
}
