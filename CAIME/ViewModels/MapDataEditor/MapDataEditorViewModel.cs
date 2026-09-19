using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CAIME.Classes.Editor.MapDataEditor;
using CAIME.Models;
using EsfLibrary;

namespace CAIME.ViewModels
{
    public class MapDataEditorViewModel : BaseViewModel
    {
        private readonly Project project;
        private EsfFile mapDataFile;
        private bool isDirty;
        private string appliedConfigPath;

        public string Filename { get; private set; }

        public ObservableCollection<MapDataRegion> MapDataRegions { get; private set; }

        public MapDataEditorViewModel(Project project)
        {
            this.project = project;

            MapDataRegions = new ObservableCollection<MapDataRegion>();
            MapDataRegions.CollectionChanged += OnMapDataRegions_CollectionChanged;

            isDirty = false;
        }

        public void OnMapDataFileOpened(object sender, MapDataOpenedEventArgs e)
        {
            if (MapDataRegions.Count > 0)
            {
                MapDataRegions.Clear();
            }

            mapDataFile     = EsfCodecUtil.LoadEsfFile(e.MapDataFilename);

            var rootNode    = mapDataFile.RootNode as RecordNode;
            var version     = rootNode.Version;

            if (version > 0)
            {
                var warningMsg = "Map Data Editor: The map_data.esf file you're trying to use is from the game where it's not required to duplicate settlement slots. Therefore the feature is not supported.";
                LoggerViewModel.Log(warningMsg, LogLevel.ErrorMessageBox);

                Filename = null;
                return;
            }

            this.Filename       = e.MapDataFilename;
            appliedConfigPath   = null;

            var regionsDataNode = rootNode["REGIONS_DATA"];

            // if (version == 1 || version == 2)
            // {
            //     regionsDataNode = regionsDataNode.Children.First().Children.First();
            // }

            var regionsBlockNode = regionsDataNode["REGIONS_BLOCK"];
            var index = 1;

            foreach (var child in regionsBlockNode.Children)
            {
                var regionDataNode = child["REGION_DATA"];
                var regionKey = regionDataNode.Values.First() as EsfValueNode<string>;

                var townNodeIndex = version == 0 ? 15 : 16;
                var hasTownInfo = (regionDataNode.Value[townNodeIndex] as EsfValueNode<bool>).Value;

                if (hasTownInfo)
                {
                    var townInfoNode = regionDataNode["SETTLEMENT_INFO"];

                    var numSlots = 1;
                    var hasPort = false;

                    //if (version == 0)
                    {
                        numSlots = (townInfoNode["SLOT_ARRAY_BLOCK"] as RecordArrayNode).Children.Count;
                        hasPort = townInfoNode["PORT_AREA_BLOCK"].Children.Count > 0;

                        if (hasPort)
                        {
                            ++numSlots;
                        }
                    }

                    MapDataRegions.Add(new MapDataRegion(index, regionKey.Value, numSlots, hasPort));
                    ++index;
                }
            }

            isDirty = false;
        }

        public void OnApplyConfig(object sender, MapDataConfigEventArgs e)
        {
            if (mapDataFile == null)
            {
                var message = "Map Data Editor - No supported map_data.esf file is open.";
                LoggerViewModel.Log(message, LogLevel.ErrorMessageBox);
                return;
            }

            var config = new MapDataConfig();
            if (config.ApplyConfigFile(e.ConfigFilename, out MapDataRegion[] regions) == false)
            {
                var message = "Map Data Editor - Failed to apply map_data config.";
                LoggerViewModel.Log(message, LogLevel.ErrorMessageBox);
                return;
            }

            var canApply = true;

            if (canApply)
            {
                // TODO: Change canApply on a per-region level. Might still be useful to apply config, even if there were changes to regions (some got renamed, some got removed; still saves time)
                MapDataRegions.CollectionChanged -= OnMapDataRegions_CollectionChanged;
                MapDataRegions = new ObservableCollection<MapDataRegion>(regions);
                MapDataRegions.CollectionChanged += OnMapDataRegions_CollectionChanged;
                OnPropertyChanged(nameof(MapDataRegions));
            }
            else
            {
                LoggerViewModel.Log("Map Data Editor - Failed to apply map_data config. Config does not match the open file.", LogLevel.ErrorMessageBox);
                return;
            }

            appliedConfigPath = e.ConfigFilename;
            isDirty = true;
            LoggerViewModel.Log($"Map Data Editor - Successfully applied map data config.", LogLevel.Info);
        }

        public void OnCreateConfig(object sender, MapDataConfigEventArgs e)
        {
            var config = new MapDataConfig();
            config.WriteConfigFile(MapDataRegions.ToArray(), e.ConfigFilename);

            var message = $"MapDataConfig.WriteConfigFile() - Successfully created {e.ConfigFilename} config file.";
            LoggerViewModel.Log(message, LogLevel.Info);
        }

        public bool SaveChanges()
        {
            if (isDirty == false)
            {
                LoggerViewModel.Log("Map Data Editor: there is nothing to save - the file was not modified.", LogLevel.ErrorMessageBox);
                return false;
            }

            var res = System.Windows.MessageBox.Show("Do you confirm you want to save the changes?", "Confirmation", System.Windows.MessageBoxButton.YesNoCancel);
            if (res != System.Windows.MessageBoxResult.Yes)
            {
                LoggerViewModel.Log("Map Data Editor: Save deined - user cancelled.", LogLevel.Info);
                return false;
            }

            if (MapDataRegions.Count == 0)
            {
                var message = "Map Data Editor: Save denied - something went wrong. No regions with settlement info have been detected in the provided file.";
                LoggerViewModel.Log(message, LogLevel.ErrorMessageBox);
                return false;
            }

            var rootNode = mapDataFile.RootNode as RecordNode;
            var version = rootNode.Version;

            if (version > 0)
            {
                var message = "Map Data Editor: The map_data.esf file you're trying to use is from the game where it's not required to duplicate settlement slots. Therefore the feature is not supported.";
                LoggerViewModel.Log(message, LogLevel.ErrorMessageBox);
                return false;
            }

            var regionsDataNode = rootNode["REGIONS_DATA"];

            // if (version == 1 || version == 2)
            // {
            //     regionsDataNode = regionsDataNode.Children.First().Children.First();
            // }

            var regionsBlockNode = regionsDataNode["REGIONS_BLOCK"];

            foreach (var child in regionsBlockNode.Children)
            {
                var regionDataNode = child["REGION_DATA"];
                var regionKey = (regionDataNode.Values.First() as EsfValueNode<string>).Value;

                var townNodeIndex = version == 0 ? 15 : 16;
                var hasTownInfo = (regionDataNode.Value[townNodeIndex] as EsfValueNode<bool>).Value;

                if (hasTownInfo)
                {
                    var townInfoNode = regionDataNode["SETTLEMENT_INFO"];
                    var slotArrayNode = townInfoNode["SLOT_ARRAY_BLOCK"] as RecordArrayNode;

                    MapDataRegion entry = null;

                    foreach (var item in MapDataRegions)
                    {
                        if (item.Name == regionKey)
                        {
                            entry = item;
                        }
                    }

                    if (entry == null)
                    {
                        LoggerViewModel.Log($"Map Data Editor: Didn't find {regionKey} region. Skipping...", LogLevel.Info);
                        continue;
                    }

                    var hasPort = townInfoNode["PORT_AREA_BLOCK"].Children.Count > 0;
                    var numOldSlots = slotArrayNode.Children.Count;
                    var numNewSlots = entry.SlotsCount;
                    var numSlotsDiff = numNewSlots - numOldSlots;

                    if (numSlotsDiff < 0)
                    {
                        var diffCount = numSlotsDiff * (-1);
                        if (hasPort)
                        {
                            ++diffCount;
                        }

                        if (diffCount >= numOldSlots)
                        {
                            LoggerViewModel.Log($"Map Data Editor: Trying to remove more slots than {regionKey} contains. Skipping...", LogLevel.Warning);
                            continue;
                        }

                        slotArrayNode.Value.RemoveRange(numOldSlots - diffCount, diffCount);
                        slotArrayNode.Modified = true;
                    }
                    else
                    if (numSlotsDiff > 0)
                    {
                        var mainSlotArray = slotArrayNode.Children.First();
                        if (hasPort)
                        {
                            --numSlotsDiff;
                        }

                        for (int n = numSlotsDiff; n != 0; --n)
                        {
                            slotArrayNode.Value.Add(mainSlotArray);
                        }

                        slotArrayNode.Modified = true;
                    }
                }
            }

            rootNode.Modified = true;
            EsfCodecUtil.WriteEsfFile(this.Filename, mapDataFile);

            isDirty = false;
            LoggerViewModel.Log($"Map Data Editor - successfully saved the file to {this.Filename}", LogLevel.Info);

            if (!string.IsNullOrEmpty(appliedConfigPath))
            {
                WriteMetadata();
            }

            return true;
        }

        private void OnMapDataRegions_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs args)
        {
            if (args.OldItems != null)
            {
                foreach (MapDataRegion oldItem in args.OldItems)
                {
                    oldItem.PropertyChanged -= OnMapDataRegion_ItemChanged;
                }
            }

            if (args.NewItems != null)
            {
                foreach (MapDataRegion newItem in args.NewItems)
                {
                    newItem.PropertyChanged += OnMapDataRegion_ItemChanged;
                }
            }
        }

        private void OnMapDataRegion_ItemChanged(object sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            isDirty = true;
        }

        private void WriteMetadata()
        {
            if (project == null)
            {
                LoggerViewModel.Log("Map Data Editor - no project is currently open, skipping metadata file write.", LogLevel.Warning);
                return;
            }

            try
            {
                // Read-modify-write so we only touch the map_data config fields and leave any
                // other metadata (e.g. the RPFM pack path) intact.
                CAIME.Rpfm.MetadataService.Update(project.ProjectPath, metadata =>
                {
                    metadata.MapDataConfigPath = appliedConfigPath;
                    metadata.CampaignMapName   = project.MapName;
                });

                LoggerViewModel.Log($"Map Data Editor - wrote metadata file to {CAIME.Rpfm.MetadataService.GetMetadataPath(project.ProjectPath)}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"Map Data Editor - failed to write metadata file: {ex.Message}", LogLevel.Error);
            }
        }
    }
}
