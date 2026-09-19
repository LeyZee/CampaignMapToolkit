using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace CAIME
{
    public delegate void SelectedRegionHandler(int regionIndex, Swatch swatch);
    
    public enum SelectionState
    {
        RegFrom,
        RegTo
    }

    public class RegionSelectionViewModel : ObservableObject
    {
        private MapHexFile mapHexFile;
        private List<Swatch> allRegions;

        public SelectionState State { get; private set; }

        public List<BorderPart> Borders;
        public event SelectedRegionHandler SelectedRegion;

        public int RegionFrom; //unbound
        public int RegionTo; //unbound

        public RegionSelectionViewModel(Project project)
        {
            mapHexFile = project.MapHexFile;

            allRegions = new List<Swatch>();
            UpdateSwatches();
            RegionFrom = -1;
            RegionTo = -1;
        }

        public void CreateSwatches(List<string> regionList, ColoursContainer coloursList)
        {
            for (int index = 0; index < regionList.Count; ++index)
            {
                string region = regionList[index];
                Swatch swatch = new RegionSwatch(region, index, coloursList.Colours[index]);
                allRegions.Add(swatch);
            }
        }

        public void UpdateSwatches()
        {
            allRegions = new List<Swatch>();
            CreateSwatches(mapHexFile.LandRegions, mapHexFile.ColoursLand);
            CreateSwatches(mapHexFile.SeaRegions, mapHexFile.ColoursSea);
        }

        public void SetFrom()
        {
            UpdateSwatches();
            State = SelectionState.RegFrom;
            RegionSelected = null;
            Title = "Region selection: Source region";


            if (HideEmpty)
            {
                RegionList = new ObservableCollection<Swatch>();
                for (int i = 0; i < allRegions.Count; i++)
                {
                    int index = Borders.FindIndex(x => x.regFrom == i);
                    if ((index != -1) && ((RegionTo == -1) || (Borders.FindIndex(index, x => x.regFrom == i && x.regTo == RegionTo) != -1)))
                    {
                        RegionList.Add(allRegions[i]);
                    }
                }
            }
            else
            {
                RegionList = new ObservableCollection<Swatch>(allRegions);
            }
            OnPropertyChanged(nameof(RegionList));

            OnPropertyChanged(nameof(HideEmpty));
            Visibility = Visibility.Visible;
        }
        public void SetTo()
        {
            UpdateSwatches();
            State = SelectionState.RegTo;
            RegionSelected = null;
            Title = "Region selection: Target region";
            if (HideEmpty)
            {
                RegionList = new ObservableCollection<Swatch>();
                for (int i = 0; i < allRegions.Count; i++)
                {
                    int index = Borders.FindIndex(x => x.regTo == i);
                    if ((index != -1) && ((RegionFrom == -1) || (Borders.FindIndex(index, x => x.regFrom == RegionFrom && x.regTo == i) != -1)))
                    {
                        RegionList.Add(allRegions[i]);
                    }
                }
            }
            else
            {
                RegionList = new ObservableCollection<Swatch>(allRegions);
            }
            OnPropertyChanged(nameof(RegionList));

            OnPropertyChanged(nameof(HideEmpty));
            Visibility = Visibility.Visible;
        }
        public void Confirmed()
        {
            int regionIndex = allRegions.IndexOf(RegionSelected);
            if (State == SelectionState.RegFrom)
                RegionFrom = regionIndex;
            else
                RegionTo = regionIndex;

            SelectedRegion?.Invoke(regionIndex, RegionSelected);
            Visibility = Visibility.Hidden;
        }

        private string _title;
        public string Title
        {
            get
            {
                return _title;
            }
            set
            {
                _title = value;
                OnPropertyChanged(nameof(Title));
            }
        }

        private Visibility _visibility = Visibility.Hidden;
        public Visibility Visibility
        {
            get
            {
                return _visibility;
            }
            set
            {
                _visibility = value;
                OnPropertyChanged(nameof(Visibility));
            }
        }

        public bool ConfirmEnabled => RegionSelected != null;

        public ObservableCollection<Swatch> RegionList { get; private set; }
        private Swatch _regFromSelected;
        
        private Swatch _regToSelected;
        
        public Swatch RegionSelected
        {
            get
            {
                return State is SelectionState.RegFrom ? _regFromSelected : _regToSelected;
            }
            set
            {
                if (State == SelectionState.RegFrom)
                {
                    _regFromSelected = value;
                }
                else
                {
                    _regToSelected = value;
                }
                OnPropertyChanged(nameof(RegionSelected));
                OnPropertyChanged(nameof(ConfirmEnabled));
            }
        }

        private bool _regFromHide = true;
        private bool _regToHide = true;
        public bool HideEmpty
        {
            get
            {
                return State is SelectionState.RegFrom ? _regFromHide : _regToHide;
            }
            set
            {
                if (State == SelectionState.RegFrom)
                {
                    _regFromHide = value;
                    SetFrom();
                }
                else
                {
                    _regToHide = value;
                    SetTo();
                }
            }
        }
    }
}
