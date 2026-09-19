using System;
using System.Windows;

namespace CAIME.ViewModels
{
    public class MapDataOpenedEventArgs : RoutedEventArgs
    {
        public string MapDataFilename { get; private set; }

        public MapDataOpenedEventArgs(string mapDataFileName)
        {
            MapDataFilename = mapDataFileName;
        }
    }

    public class MapDataConfigEventArgs : RoutedEventArgs
    {
        public string ConfigFilename { get; private set; }

        public MapDataConfigEventArgs(string configFileName)
        {
            ConfigFilename = configFileName;
        }
    }

    public delegate void MapDataOpenedHandler(object sender, MapDataOpenedEventArgs e);
    public delegate void MapDataConfigEventHandler(object sender, MapDataConfigEventArgs e);

    public class MapDataSettingsViewModel : BaseViewModel
    {
        public bool IsFileOpen
        {
            get => !string.IsNullOrEmpty(mapDataFilename);
        }

        private string mapDataFilename;
        public string MapDataFilename
        {
            get => mapDataFilename;
            set
            {
                mapDataFilename = value;
                OnPropertyChanged(nameof(MapDataFilename));
                OnPropertyChanged(nameof(IsFileOpen));
            }
        }

        public event MapDataOpenedHandler OnMapDataFileOpened;
        public event MapDataConfigEventHandler OnApplyConfig;
        public event MapDataConfigEventHandler OnCreateConfig;

        public MapDataSettingsViewModel()
        {
        }

        public void OpenFile(string file)
        {
            MapDataFilename = file;
            OnMapDataFileOpened?.Invoke(this, new MapDataOpenedEventArgs(file));
        }

        public void ApplyConfig(string filename)
        {
            var args = new MapDataConfigEventArgs(filename);
            OnApplyConfig?.Invoke(this, args);
        }

        public void CreateConfig(string filename)
        {
            var args = new MapDataConfigEventArgs(filename);
            OnCreateConfig?.Invoke(this, args);
        }
    }
}
