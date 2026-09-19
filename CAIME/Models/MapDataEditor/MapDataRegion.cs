using System;
using System.ComponentModel;
using System.Diagnostics;

namespace CAIME.Models
{
    public class MapDataRegion : INotifyPropertyChanged
    {
        private int index;
        public int Index
        {
            get => index;
            set
            {
                index = value;
                this.OnPropertyChanged(nameof(Index));
            }
        }

        private string name;
        public string Name
        {
            get => name;
            set
            {
                name = value;
                this.OnPropertyChanged(nameof(Name));
            }
        }

        private int slotsCount;
        public int SlotsCount
        {
            get => slotsCount;
            set
            {
                slotsCount = value;
                this.OnPropertyChanged(nameof(SlotsCount));
            }
        }

        private bool hasPort;
        public bool HasPort
        {
            get => hasPort;
            set
            {
                hasPort = value;
                this.OnPropertyChanged(nameof(HasPort));
            }
        }

        public MapDataRegion(int index, string name, int slotsCount, bool hasPort)
        {
            Index       = index;
            Name        = name;
            SlotsCount  = slotsCount;
            HasPort     = hasPort;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            var e = new PropertyChangedEventArgs(nameof(propertyName));
            PropertyChanged?.Invoke(this, e);
        }
    }
}
