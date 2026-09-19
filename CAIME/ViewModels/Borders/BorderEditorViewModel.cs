using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Media;
using Microsoft.Win32;

//TODO:
//-Improve placing position of added border points -> first border point is set to border of the 2 regions instead of (0|0)
//-Warning: Unsaved changed OR restructuring so they don't get lost when closing
//-Cleaner code

namespace CAIME
{
    using Brush = System.Windows.Media.Brush;

    class BorderEditorViewModel : BaseViewModel
    {
        private RegionSelectionViewModel regionSelectionVM;
        private MapHexFile mapHexFile;

        public List<BorderPart> Borders;
        public BorderVisualizationViewModel borderVisualizationVM { get; private set; } 

        public BorderEditorViewModel(Project project)
        {
            Borders = new List<BorderPart>();
            borderVisualizationVM = new BorderVisualizationViewModel(project);

            BPartList       = new ObservableCollection<string>();
            currentBParts   = new List<BorderPart>();
            BPointList      = new ObservableCollection<string>();
            currentBPoints  = new List<BorderPoint>();

            regionSelectionVM = new RegionSelectionViewModel(project);
            regionSelectionVM.SelectedRegion += GetResult;
            regionSelectionVM.Borders = Borders;

            var regionSelectionWindow = new RegionSelectionWindow();
            regionSelectionWindow.ViewModel = regionSelectionVM;

            mapHexFile = project.MapHexFile;
        }

        #region Region selection
        public void SelectFrom()
        {
            regionSelectionVM.SetFrom();
        }
        public void ClearFrom()
        {
            regionSelectionVM.RegionFrom = -1;
            RegFromName = null;
            RegFromColour = 0;
            borderVisualizationVM.ChangeRegFromColour(SharpDX.Color.FromRgba(0));
            RefreshBPartList();
        }
        public void SelectTo()
        {
            regionSelectionVM.SetTo();
        }
        public void ClearTo()
        {
            regionSelectionVM.RegionTo = -1;
            RegToName = null;
            RegToColour = 0;
            borderVisualizationVM.ChangeRegToColour(SharpDX.Color.FromRgba(0));
            RefreshBPartList();
        }
        public void Invert()
        {
            string name = RegFromName;
            int colour = RegFromColour;

            RegFromName = RegToName;
            RegFromColour = RegToColour;

            RegToName = name;
            RegToColour = colour;

            int regionIndex = regionSelectionVM.RegionFrom;
            regionSelectionVM.RegionFrom = regionSelectionVM.RegionTo;
            regionSelectionVM.RegionTo = regionIndex;

            RefreshBPartList();

            borderVisualizationVM.ChangeRegFromColour(SharpDX.Color.FromRgba(RegFromColour));
            borderVisualizationVM.ChangeRegToColour(SharpDX.Color.FromRgba(RegToColour));
        }
        private void GetResult(int regionIndex, Swatch swatch)
        {
            if (regionSelectionVM.State == SelectionState.RegFrom)
            {
                regionSelectionVM.RegionFrom = regionIndex;
                RegFromName = swatch.Name.Replace("_", "__");
                RegFromColour = swatch.Colour;
                borderVisualizationVM.ChangeRegFromColour(SharpDX.Color.FromRgba(RegFromColour));
            }
            else
            {
                regionSelectionVM.RegionTo = regionIndex;
                RegToName = swatch.Name.Replace("_", "__");
                RegToColour = swatch.Colour;
                borderVisualizationVM.ChangeRegToColour(SharpDX.Color.FromRgba(RegToColour));
            }

            if (RegFromName != null && RegToName != null)
            {
                RefreshBPartList();
                BothRegionsSelected = true;
            }
            else
            {
                BothRegionsSelected = false;
            }
        }

        private bool _bothRegionsSelected;
        public bool BothRegionsSelected
        {
            get
            {
                return _bothRegionsSelected;
            }
            set
            {
                _bothRegionsSelected = value;
                OnPropertyChanged(nameof(BothRegionsSelected));
            }
        }

        private string _regFromName;
        public string RegFromName
        {
            get
            {
                return _regFromName;
            }
            set
            {
                _regFromName = value;
                OnPropertyChanged(nameof(RegFromName));
            }
        }
        private int _regFromColour;
        public int RegFromColour
        {
            get
            {
                return _regFromColour;
            }
            set
            {
                _regFromColour = value;
                OnPropertyChanged(nameof(RegFromColour));
            }
        }


        private string _regToName;
        public string RegToName
        {
            get
            {
                return _regToName;
            }
            set
            {
                _regToName = value;
                OnPropertyChanged(nameof(RegToName));
            }
        }
        private int _regToColour;
        public int RegToColour
        {
            get
            {
                return _regToColour;
            }
            set
            {
                _regToColour = value;
                OnPropertyChanged(nameof(RegToColour));
            }
        }
        #endregion

        #region Border part
        private bool bPartEnabled = false;
        public bool BPartEnabled
        {
            get
            {
                return bPartEnabled;
            }
            set
            {
                bPartEnabled = value;
                OnPropertyChanged(nameof(bPartEnabled));
            }
        }
        private List<BorderPart> currentBParts;
        public ObservableCollection<string> BPartList { get; private set; }
        private int _bPartSelected;
        public int BPartSelected
        {
            get
            {
                return _bPartSelected;
            }
            set
            {
                _bPartSelected = value;
                if (value == -1 || currentBParts[_bPartSelected].BorderPoints.Count == 0)
                {
                    borderVisualizationVM.SetActive(false);
                }
                else
                {
                    borderVisualizationVM.SetActive(true, null, currentBParts[value], Borders);
                }   

                OnPropertyChanged(nameof(BPartSelected));
                BPartEnabled = value is -1 ? false : true;
                RefreshBPointList();
            }
        }

        public void ChangeFacing()
        {
            currentBParts[BPartSelected].BorderPoints.Reverse();
            RefreshBPointList();
            borderVisualizationVM.UpdateImage(null);
        }
        public void AddPart()
        {
            BorderPart bpart = new BorderPart(regionSelectionVM.RegionFrom, regionSelectionVM.RegionTo);
            Borders.Add(bpart);
            currentBParts.Add(bpart);
            BPartList.Add("Border part " + BPartList.Count + " (0 points)");
        }
        public void DeletePart()
        {
            if (BPartSelected == -1 || BPartList.Count == 0)
                return;

            BorderPart delete = currentBParts[BPartSelected];
            Borders.Remove(delete);
            currentBParts.Remove(delete);
            BPartList.RemoveAt(BPartSelected);

            BPartSelected = -1;
            OnPropertyChanged(nameof(BPartList));
        }

        public void RefreshBPartList()
        {
            BPartList.Clear();
            currentBParts.Clear();

            currentBParts.AddRange(Borders.FindAll(x => (x.regFrom == regionSelectionVM.RegionFrom) && (x.regTo == regionSelectionVM.RegionTo)));
            for (int i = 0; i < currentBParts.Count; i++)
                BPartList.Add("Border part " + i + " (" + currentBParts[i].BorderPoints.Count + " points)");

            BPartSelected = -1;
            OnPropertyChanged(nameof(BPartList));
            RefreshBPointList();
        }
        public void RefreshBPartName()
        {
            int bPartSelectedSave = _bPartSelected;
            int bPointSelectedSave = _bPointSelected;
            BPartList[_bPartSelected] = "Border part " + _bPartSelected + " (" + currentBParts[_bPartSelected].BorderPoints.Count + " points)";
            BPartSelected = bPartSelectedSave;
            BPointSelected = bPointSelectedSave;
        }
        #endregion

        #region Border point
        private bool bPointEnabled = false;
        public bool BPointEnabled
        {
            get 
            { 
                return bPointEnabled; 
            }
            set
            {
                bPointEnabled = value;
                OnPropertyChanged(nameof(bPointEnabled));
            }
        }

        List<BorderPoint> currentBPoints;
        public ObservableCollection<string> BPointList { get; private set; }

        private int _bPointSelected = -1;
        public int BPointSelected
        {
            get
            {
                return _bPointSelected;
            }
            set
            {
                _bPointSelected = value;
                OnPropertyChanged(nameof(BPointSelected));
                OnPropertyChanged(nameof(XCoord));
                OnPropertyChanged(nameof(YCoord));

                if (value == -1)
                {
                    BPointEnabled = false;
                }
                else
                {
                    BPointEnabled = true;
                    borderVisualizationVM.UpdateSelected(currentBPoints[_bPointSelected]);
                }
            }
        }

        public void RefreshBPointList()
        {
            currentBPoints = null;
            BPointList.Clear();
            if (BPartSelected == -1)
                return;

            currentBPoints = currentBParts[BPartSelected].BorderPoints;
            for (int i = 0; i < currentBPoints.Count; i++)
                BPointList.Add(MakeBPName(i));
        }

        private string MakeBPName(int index) => "Border point " + index + " (" + currentBPoints[index].X + "|" + currentBPoints[index].Y + ")";

        public uint? XCoord
        {
            get
            {
                if (BPointList.Count != 0 && BPointSelected != -1)
                {
                    return (uint)currentBPoints[BPointSelected].X;
                }

                return null;
            }
            set
            {
                if (value > mapHexFile.MapWidth - 1)
                {
                    return;
                }

                currentBPoints[BPointSelected].X = (int)value.Value;
                int bPointSelectedSave = _bPointSelected;
                BPointList[BPointSelected] = MakeBPName(BPointSelected);
                _bPointSelected = bPointSelectedSave; //BPointSelected is reset to -1 by changing the collection
                OnPropertyChanged(nameof(BPointSelected)); //UpdateSelected must not be invoked
                OnPropertyChanged(nameof(XCoord));
                OnPropertyChanged(nameof(YCoord));
                BPointEnabled = true;

                borderVisualizationVM.UpdateImage(currentBPoints[_bPointSelected]);
            }
        }

        public uint? YCoord
        {
            get
            {
                if (BPointList.Count != 0 && BPointSelected != -1)
                {
                    return (uint)currentBPoints[BPointSelected].Y;
                }
                return null;
            }
            set
            {
                if (value > mapHexFile.MapHeight - 1)
                {
                    return;
                }

                currentBPoints[BPointSelected].Y = (int)value.Value;
                int bPointSelectedSave = _bPointSelected;
                BPointList[BPointSelected] = MakeBPName(BPointSelected);
                _bPointSelected = bPointSelectedSave; //BPointSelected is reset to -1 by changing the collection
                OnPropertyChanged(nameof(BPointSelected)); //UpdateSelected must not be invoked
                OnPropertyChanged(nameof(XCoord));
                OnPropertyChanged(nameof(YCoord));
                BPointEnabled = true;

                borderVisualizationVM.UpdateImage(currentBPoints[_bPointSelected]);
            }
        }

        public void InsertPoint()
        {
            BorderPoint newPoint;
            int index;

            if (currentBPoints.Count == 0)
            {
                newPoint = new BorderPoint(0, 0, currentBParts[_bPartSelected]);
                currentBParts[_bPartSelected].BorderPoints.Insert(0, newPoint);
                borderVisualizationVM.SetActive(true, null, currentBParts[_bPartSelected], Borders);
                index = 0;
            }
            else if (_bPointSelected == -1)
            {
                index = 0;
                //TODO
                //Interim solution
                newPoint = new BorderPoint(currentBPoints[0].X + 1, currentBPoints[0].Y, currentBParts[_bPartSelected]);
                if (currentBPoints.Count != 1 && (newPoint == currentBPoints[1]))
                {
                    if (newPoint.X > 2)
                    {
                        newPoint.X -= 2;
                    }
                    else
                    if (newPoint.X < mapHexFile.MapWidth - 2)
                    {
                        newPoint.X += 2;
                    }
                }

                //newPoint.X = 0;
                //newPoint.Y = 0;
                currentBParts[_bPartSelected].BorderPoints.Insert(index, newPoint);
                borderVisualizationVM.UpdateImage(newPoint);
            }
            else
            {
                //TODO
                //Interim solution
                index = _bPointSelected;
                newPoint = new BorderPoint(currentBPoints[index].X + 1, currentBPoints[index].Y, currentBParts[_bPartSelected]);
                if ((index != 0) && (newPoint == currentBPoints[index - 1]) || (index != currentBPoints.Count() - 1 && (newPoint == currentBPoints[index + 1])))
                {
                    if (newPoint.X > 2)
                    {
                        newPoint.X -= 2;
                    }
                    else
                    if (newPoint.X < mapHexFile.MapWidth - 2)
                    {
                        newPoint.X += 2;
                    }
                }

                //newPoint.X = 0;
                //newPoint.Y = 0;

                index++;
                currentBParts[_bPartSelected].BorderPoints.Insert(index, newPoint);
                borderVisualizationVM.UpdateImage(newPoint);
            }

            RefreshBPointList();
            BPointSelected = index;
            RefreshBPartName();
        }

        public void DeletePoint()
        {
            currentBPoints.RemoveAt(_bPointSelected);
            int bPointSelectedSave = _bPointSelected;
            if (_bPointSelected == currentBPoints.Count())
            {
                bPointSelectedSave -= 1;
            }

            BPointList.RemoveAt(_bPointSelected);
            BPointSelected = bPointSelectedSave;

            if (BPointList.Count == 0)
            {
                BPointEnabled = false;
                borderVisualizationVM.SetActive(false);
            }

            borderVisualizationVM.UpdateImage(null);
            RefreshBPartName();
        }

        public void UnselectPoint()
        {
            BPointSelected = -1;
        }

        public void ChangeCoord(string coord, int value)
        {
            if ((coord == "x") && (currentBPoints[BPointSelected].X + value >= 0) && (currentBPoints[BPointSelected].X + value < mapHexFile.MapWidth))
            {
                XCoord += (uint)value;
                OnPropertyChanged(nameof(XCoord));
            }
            else
            if ((coord == "y") && (currentBPoints[BPointSelected].Y + value >= 0) && (currentBPoints[BPointSelected].Y + value < mapHexFile.MapWidth))
            {
                YCoord += (uint)value;
                OnPropertyChanged(nameof(YCoord));
            }
        }
        #endregion

        #region Other
        private string status = "You have to load a PBD file first!";
        public string Status
        {
            get
            {
                return status;
            }
            set
            {
                status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        private Brush statusColour = new SolidColorBrush(Colors.Red);
        public Brush StatusColour
        {
            get
            {
                return statusColour;
            }
            set
            {
                statusColour = value;
                OnPropertyChanged(nameof(StatusColour));
            }
        }

        public void Load()
        {
            string path = "";
            var dialog = new OpenFileDialog();
            dialog.Filter = "PBD-file (*.pbd)|*.pbd";
            if (dialog.ShowDialog() == false)
            {
                return;
            }

            Borders = new List<BorderPart>();
            regionSelectionVM.Borders = Borders;
            path = dialog.FileName;
            var br = new BinaryReader(File.Open(path, FileMode.Open));
            br.ReadUInt32();
            uint regSeaCount = br.ReadUInt32();
            for (int i = 0; i < regSeaCount; i++)
            {
                uint nameLength = br.ReadUInt32();
                for (int e = 0; e < nameLength; e++)
                {
                    br.ReadChar();
                }
            }
            uint groupsCount = br.ReadUInt32();
            for (int i = 0; i < groupsCount; i++)
            {
                uint RegFrom = br.ReadUInt32();
                uint borderingRegionsCount = br.ReadUInt32();
                for (int e = 0; e < borderingRegionsCount; e++)
                {
                    uint RegTo = br.ReadUInt32();
                    uint borderPartsCount = br.ReadUInt32();
                    for (int x = 0; x < borderPartsCount; x++)
                    {
                        BorderPart bPart = new BorderPart((int)RegFrom, (int)RegTo);
                        Borders.Add(bPart);
                        uint arraySize = br.ReadUInt32();
                        for (int y = 0; y < arraySize; y++)
                        {
                            bPart.BorderPoints.Add(new BorderPoint(br.ReadUInt16(), br.ReadUInt16(), bPart));
                        }
                    }
                }
            }
            br.Close();

            RefreshBPartList();

            Status = "Loaded PBD file successfully";
            StatusColour = new SolidColorBrush(Colors.White);
        }

        public void Save()
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "PBD-file (*.pbd)|*.pbd";
            if (dialog.ShowDialog() == false)
                return;

            var exporter = new BordersExporter(mapHexFile);
            exporter.Borders = Borders;
            exporter.Write(dialog.FileName);

            Status = "Saved PBD file successfully";
        }
        #endregion
    }
}
