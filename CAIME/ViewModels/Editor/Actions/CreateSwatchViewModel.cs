using System;
using System.Windows.Media;
using CAIME.ViewModels;

namespace CAIME.Windows
{
    public class CreateSwatchViewModel : BaseViewModel
    {
        public enum CreateResult
        {
            ColourExists,
            CreateFailed,
            Success
        }

        private readonly LayerType _layerType;
        private readonly MapHexEditor _mapHexEditor;

        private string windowTitle;
        public string WindowTitle
        {
            get
            {
                return windowTitle;
            }
            set
            {
                windowTitle = value;
                OnPropertyChanged(nameof(WindowTitle));
            }
        }

        private string newSwatchName;
        public string NewSwatchName
        {
            get
            {
                return newSwatchName;
            }
            set
            {
                newSwatchName = value;
                OnPropertyChanged(nameof(NewSwatchName));
            }
        }

        private bool isSea;
        public bool IsSea
        {
            get
            {
                return isSea;
            }
            set
            {
                isSea = value;
                OnPropertyChanged(nameof(IsSea));
            }
        }

        private bool isSeaEnabled;
        public bool IsSeaEnabled
        {
            get
            {
                return isSeaEnabled;
            }
            set
            {
                isSeaEnabled = value;
                OnPropertyChanged(nameof(IsSeaEnabled));
            }
        }

        public CreateSwatchViewModel(LayerType layerType, MapHexEditor mapHexEditor)
        {
            _layerType = layerType;
            _mapHexEditor = mapHexEditor;

            switch (layerType)
            {
                case LayerType.Regions:
                    WindowTitle = "Create new region";
                    IsSeaEnabled = true;
                    break;
                case LayerType.GroundTypes:
                    WindowTitle = "Create new ground type";
                    IsSeaEnabled = true;
                    break;
                case LayerType.Attritions:
                    WindowTitle = "Create new attrition";
                    IsSeaEnabled = false;
                    break;
                case LayerType.Climates:
                    WindowTitle = "Create new climate";
                    IsSeaEnabled = false;
                    break;
                case LayerType.AreasOfInterest:
                    WindowTitle = "Create new area of interest";
                    IsSeaEnabled = false;
                    break;
            }
        }

        public CreateResult ConfirmCreateNewSwatch(Color color)
        {
            if (NewSwatchName == null || NewSwatchName.Length == 0)
            {
                return CreateResult.CreateFailed;
            }

            // var newColour = Utility.ToRgba(color.R, color.G, color.B);

            if (_layerType == LayerType.Regions)
            {
                var regionIndex = _mapHexEditor.CreateNewRegion(NewSwatchName, IsSea);
                if (regionIndex == Hex.INVALID_REGION_INDEX)
                {
                    return CreateResult.CreateFailed;
                }
            }
            else
            if (_layerType == LayerType.GroundTypes)
            {
                var groundTypeIndex = _mapHexEditor.CreateNewGroundType(NewSwatchName, IsSea);
                if (groundTypeIndex == Hex.INVALID_GROUND_TYPE_INDEX)
                {
                    return CreateResult.CreateFailed;
                }
            }
            else
            if (_layerType == LayerType.Climates)
            {
                var climateIndex = _mapHexEditor.CreateNewClimate(NewSwatchName);
                if (climateIndex == Hex.INVALID_CLIMATE_INDEX)
                {
                    return CreateResult.CreateFailed;
                }
            }
            else
            if (_layerType == LayerType.Attritions)
            {
                var attritionIndex = _mapHexEditor.CreateNewAttrition(NewSwatchName);
                if (attritionIndex == Hex.INVALID_ATTRITION_INDEX)
                {
                    return CreateResult.CreateFailed;
                }
            }
            else
            if (_layerType == LayerType.AreasOfInterest)
            {
                var areaOfInterestIndex = _mapHexEditor.CreateNewAreaOfInterest(NewSwatchName);
                if (areaOfInterestIndex == Hex.INVALID_AREA_OF_INT_INDEX)
                {
                    return CreateResult.CreateFailed;
                }
            }

            return CreateResult.Success;
        }

        public void PickRandomColour(ColourPickControlViewModel colourPickViewModel)
        {
            int newColour;

            if (IsSea)
            {
                newColour = ColourTable.GenerateRandomColour(maxR: 127, maxG: 0);
            }
            else
            {
                newColour = ColourTable.GenerateRandomColour(maxB: 0);
            }

            Utility.RgbaDecompose(newColour, out byte r, out byte g, out byte b, out byte _);
            colourPickViewModel.SelectedColor = Color.FromRgb(r, g, b);
        }
    }
}
