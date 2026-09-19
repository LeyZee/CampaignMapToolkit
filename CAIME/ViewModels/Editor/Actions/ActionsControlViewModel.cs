using System;
using System.Windows;
using CAIME.Windows;
using CAIME.ViewModels;
using CAIME.Validators;
using System.Collections.Generic;

namespace CAIME.Controls
{
    public class ActionsControlViewModel : BaseViewModel
    {
        private const int INVALID_SWATCH_VALUE = -1;

        private LayerType   _layerType;
        private int         _swatchValue;

        protected Project   _project;

        public ActionsControlViewModel()
        {
            _swatchValue = INVALID_SWATCH_VALUE;
        }

        public void Initialise(Project project)
        {
            _project = project;
        }

        public void ActiveSwatchChanged(LayerType layerType, int value)
        {
            _layerType = layerType;
            _swatchValue = value;
        }

        public void RenameSwatch()
        {
            if (_swatchValue == INVALID_SWATCH_VALUE)
            {
                MessageBox.Show("This swatch cannot be renamed. Please, select another swatch under Swatches menu.");
                return;
            }

            var oldSwatchName = string.Empty;

            switch (_layerType)
            {
                case LayerType.GroundTypes:
                    oldSwatchName = _project.MapHexFile.GetGroundTypeName(_swatchValue);
                    break;
                case LayerType.Attritions:
                    oldSwatchName = _project.MapHexFile.GetAttritionName(_swatchValue);
                    break;
                case LayerType.Climates:
                    oldSwatchName = _project.MapHexFile.GetClimateName(_swatchValue);
                    break;
                case LayerType.Regions:
                    oldSwatchName = _project.MapHexFile.GetRegionName(_swatchValue);
                    break;
                case LayerType.AreasOfInterest:
                    oldSwatchName = _project.MapHexFile.GetAreaOfInterestName(_swatchValue);
                    break;
            }
            
            var renameSwatchViewModel = new RenameSwatchViewModel(_layerType, _project.MapHexEditor, oldSwatchName);
            var renameSwatchWindow = new RenameSwatchWindow(renameSwatchViewModel)
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            renameSwatchWindow.Show();
        }

        public void CreateNewSwatch()
        {
            var createSwatchViewModel = new CreateSwatchViewModel(_layerType, _project.MapHexEditor);
            var createSwatchWindow = new CreateNewSwatchWindow(createSwatchViewModel)
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            createSwatchWindow.Show();
        }

        public void RemoveSwatch()
        {
            var swatchToDeleteName = string.Empty;

            switch (_layerType)
            {
                case LayerType.GroundTypes:
                    swatchToDeleteName = _project.MapHexFile.GetGroundTypeName(_swatchValue);
                    break;
                case LayerType.Attritions:
                    swatchToDeleteName = _project.MapHexFile.GetAttritionName(_swatchValue);
                    break;
                case LayerType.Climates:
                    swatchToDeleteName = _project.MapHexFile.GetClimateName(_swatchValue);
                    break;
                case LayerType.Regions:
                    swatchToDeleteName = _project.MapHexFile.GetRegionName(_swatchValue);
                    break;
                case LayerType.AreasOfInterest:
                    swatchToDeleteName = _project.MapHexFile.GetAreaOfInterestName(_swatchValue);
                    break;
            }

            if (_swatchValue == INVALID_SWATCH_VALUE)
            {
                MessageBox.Show("This swatch cannot be deleted. Please, select another swatch under Swatches menu.");
                return;
            }

            var result = MessageBox.Show(Application.Current.MainWindow, $"Do you want to remove {swatchToDeleteName}?", "Remove swatch", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                var didDelete = false;

                switch (_layerType)
                {
                    case LayerType.GroundTypes:
                        didDelete = _project.MapHexEditor.DeleteGroundType(swatchToDeleteName);
                        break;
                    case LayerType.Attritions:
                        didDelete = _project.MapHexEditor.DeleteAttrition(swatchToDeleteName);
                        break;
                    case LayerType.Climates:
                        didDelete = _project.MapHexEditor.DeleteClimate(swatchToDeleteName);
                        break;
                    case LayerType.Regions:
                        didDelete = _project.MapHexEditor.DeleteRegion(swatchToDeleteName);
                        break;
                    case LayerType.AreasOfInterest:
                        didDelete = _project.MapHexEditor.DeleteAreaOfInterest(swatchToDeleteName);
                        break;
                }

                if (didDelete)
                {
                    LoggerViewModel.Log($"Successfully deleted {swatchToDeleteName}.", LogLevel.Info);
                }
            }
        }

        public void ChangeSwatchColour()
        {
            if (_swatchValue == INVALID_SWATCH_VALUE)
            {
                MessageBox.Show("This swatch is not suitable for this action. Please, select another swatch.");
                return;
            }

            int oldColour = 0;

            switch (_layerType)
            {
                case LayerType.GroundTypes:
                    oldColour = _project.MapHexEditor.ColourTable.GetTerrainColour((sbyte)_swatchValue);
                    break;
                case LayerType.Attritions:
                    oldColour = _project.MapHexEditor.ColourTable.GetAttritionColour((sbyte)_swatchValue);
                    break;
                case LayerType.Climates:
                    oldColour = _project.MapHexEditor.ColourTable.GetClimateColour((sbyte)_swatchValue);
                    break;
                case LayerType.Regions:
                    oldColour = _project.MapHexEditor.ColourTable.GetRegionColour(_swatchValue);
                    break;
                case LayerType.AreasOfInterest:
                    oldColour = _project.MapHexEditor.ColourTable.GetAreaOfInterestColour((sbyte)_swatchValue);
                    break;
            }

            var viewModel = new ChangeSwatchColourViewModel(_layerType, _project.MapHexEditor, _swatchValue, oldColour);
            var changeColourWindow = new ChangeSwatchColourWindow(viewModel)
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            changeColourWindow.Show();
        }

        //Deletes swatches that aren't used in the file. Mostly gonna be used for regions.
        public void CleanupSwatches()
        {
            var mapHexFile = _project.MapHexFile;

            var swatchesToRemove = new List<string>();
            switch (_layerType)
            {
                case LayerType.GroundTypes:
                    swatchesToRemove = new List<string>(mapHexFile.LandGroundTypes);
                    swatchesToRemove.AddRange(mapHexFile.SeaGroundTypes);
                    break;
                case LayerType.Attritions:
                    swatchesToRemove = new List<string>(mapHexFile.Attritions);
                    break;
                case LayerType.Climates:
                    swatchesToRemove = new List<string>(mapHexFile.Climates);
                    break;
                case LayerType.Regions:
                    swatchesToRemove = new List<string>(mapHexFile.LandRegions);
                    swatchesToRemove.AddRange(mapHexFile.SeaRegions);
                    break;
                case LayerType.AreasOfInterest:
                    swatchesToRemove = new List<string>(mapHexFile.AreasOfInterest);
                    break;
            }

            for (int hexIndex = 0; hexIndex < mapHexFile.Capacity; ++hexIndex)
            {
                var hex = mapHexFile.HexData[hexIndex];
                switch (_layerType)
                {
                    case LayerType.GroundTypes:
                        var groundTypeIndex = hex.GroundTypeIndex;
                        var groundTypeName = mapHexFile.GetGroundTypeName(groundTypeIndex);
                        swatchesToRemove.Remove(groundTypeName);
                        break;
                    case LayerType.Attritions:
                        var attritionIndex = hex.AttritionIndex;
                        var attritionName = mapHexFile.GetAttritionName(attritionIndex);
                        swatchesToRemove.Remove(attritionName);
                        break;
                    case LayerType.Climates:
                        var climateIndex = hex.ClimateIndex;
                        var climateName = mapHexFile.GetClimateName(climateIndex);
                        swatchesToRemove.Remove(climateName);
                        break;
                    case LayerType.Regions:
                        var regionIndex = hex.RegionId;
                        var regionName = mapHexFile.GetRegionName(regionIndex);
                        swatchesToRemove.Remove(regionName);
                        break;
                    case LayerType.AreasOfInterest:
                        var areaOfInterestIndex = hex.InterestIndex;
                        var areaOfInterestName = mapHexFile.GetAreaOfInterestName(areaOfInterestIndex);
                        swatchesToRemove.Remove(areaOfInterestName);
                        break;
                }
            }

            if (swatchesToRemove.Count == 0)
            {
                MessageBox.Show("All swatches in this layer are in use, so there is nothing to clean-up.");
                return;
            }

            var result = MessageBox.Show(Application.Current.MainWindow, $"Do you want to remove {swatchesToRemove.Count} unused swatches? This should really only be done for regions.", "Remove swatches", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {

                foreach (string swatchToRemoveName in swatchesToRemove)
                {
                    var didDelete = false;

                    switch (_layerType)
                    {
                        case LayerType.GroundTypes:
                            didDelete = _project.MapHexEditor.DeleteGroundType(swatchToRemoveName);
                            break;
                        case LayerType.Attritions:
                            didDelete = _project.MapHexEditor.DeleteAttrition(swatchToRemoveName);
                            break;
                        case LayerType.Climates:
                            didDelete = _project.MapHexEditor.DeleteClimate(swatchToRemoveName);
                            break;
                        case LayerType.Regions:
                            didDelete = _project.MapHexEditor.DeleteRegion(swatchToRemoveName);
                            break;
                        case LayerType.AreasOfInterest:
                            didDelete = _project.MapHexEditor.DeleteAreaOfInterest(swatchToRemoveName);
                            break;
                    }

                    if (didDelete)
                    {
                        LoggerViewModel.Log($"Successfully deleted {swatchToRemoveName}.", LogLevel.Info);
                    }
                }
            }
        }

        public void Validate()
        {
            switch (_layerType)
            {
                case LayerType.Regions:
                    ShowValidationResult("Regions", RegionsValidator.Validate(_project));
                    break;
                case LayerType.Attritions:
                    ShowValidationResult("Attritions", AttritionsValidator.Validate(_project));
                    break;
                case LayerType.Climates:
                    ShowValidationResult("Climates", ClimatesValidator.Validate(_project));
                    break;
                case LayerType.GroundTypes:
                    ShowValidationResult("Ground Types", GroundTypesValidator.Validate(_project));
                    break;
                case LayerType.Roads:
                    ShowValidationResult("Roads", RoadsValidator.Validate(_project));
                    break;
                case LayerType.Rivers:
                    ShowValidationResult("Rivers", RiversValidator.Validate(_project));
                    break;
                case LayerType.Bridges:
                    ShowValidationResult("Bridges", BridgesValidator.Validate(_project));
                    break;
                case LayerType.Beaches:
                    ShowValidationResult("Beaches", BeachesValidator.Validate(_project));
                    break;
                case LayerType.TownSlots:
                    ShowValidationResult("Town Slots", TownSlotsValidator.Validate(_project));
                    break;
                case LayerType.Impassable:
                    ShowValidationResult("Impassable", ImpassableValidator.Validate(_project));
                    break;
                case LayerType.TownSprawl:
                    ShowValidationResult("Town Sprawl", SprawlValidator.Validate(_project));
                    break;
                default:
                    MessageBox.Show("This layer has no validation available.");
                    break;
            }
        }

        private static void ShowValidationResult(string layerName, bool isSuccess)
        {
            if (isSuccess)
            {
                MessageBox.Show($"No issues have been found during {layerName} layer validation.", "Validation completed");
            }
            else
            {
                MessageBox.Show($"Validating {layerName} layer failed. See output logs for details.", "Validation completed");
            }
        }
    }
}
