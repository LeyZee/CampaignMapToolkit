using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;
using CAIME.Classes.Importers;
using Microsoft.Win32;

namespace CAIME.ViewModels
{
    public class ImportLayerViewModel : BaseViewModel
    {
        private bool _canApply;
        public bool CanApply
        {
            get => _canApply;
            set
            {
                _canApply = value;
                OnPropertyChanged(nameof(CanApply));
            }
        }

        private string _fileName;
        public string FileName
        {
            get => _fileName;
            set
            {
                _fileName = value;
                OnPropertyChanged(nameof(FileName));
            }
        }

        public int _selectedIndex;
        public int SelectedLayerIndex
        {
            get => _selectedIndex;
            set
            {
                _selectedIndex = value;
                OnPropertyChanged(nameof(SelectedLayerIndex));
            }
        }

        public List<LayerType> Layers { get; private set; }

        private readonly Project project;
        private byte[] layerData;

        public ImportLayerViewModel(Project project, List<LayerType> layers)
        {
            this.project = project;
            this.Layers = layers;

            SelectedLayerIndex = 0;
        }

        public void Browse()
        {
            var dialog = new OpenFileDialog()
            {
                Filter = "Layer binary file (*.hex_layer)|*.hex_layer"
            };

            if (dialog.ShowDialog() == true)
            {
                var fileName = dialog.FileName;

                var data = File.ReadAllBytes(fileName);

                var layerType = LayerImporter.ReadLayerType(data);
                if (layerType == null)
                {
                    LoggerViewModel.Log($"Import Layer Failed - {fileName} is too short to be a layer file.", LogLevel.ErrorMessageBox);
                    return;
                }

                var expectedFileLength = LayerImporter.ExpectedFileLength(project.MapHexFile, layerType.Value);

                if (data.Length != expectedFileLength)
                {
                    LoggerViewModel.Log($"Import Layer Failed - binary layer resolution {data.Length} mismatches the hexmap resolution {project.MapHexFile.Capacity}!", LogLevel.ErrorMessageBox);
                    return;
                }

                layerData = data;

                var layerIndex = Layers.IndexOf(layerType.Value);
                if (layerIndex != -1)
                {
                    SelectedLayerIndex = layerIndex;
                }

                FileName = fileName;
                CanApply = true;
            }
        }

        public void Confirm()
        {
            if (project == null || layerData == null)
            {
                LoggerViewModel.Log($"Import Layer Failed - Either a project or layer data were null.", LogLevel.ErrorMessageBox);
                CanApply = false;
                return;
            }

            // The file was size-checked against the layer named in its header; the user is free to
            // pick a different one from the combo box, and those sizes need not agree.
            var selectedLayer  = Layers[SelectedLayerIndex];
            var expectedLength = LayerImporter.ExpectedFileLength(project.MapHexFile, selectedLayer);

            if (layerData.Length != expectedLength)
            {
                LoggerViewModel.Log(
                    $"Import Layer Failed - this file is {layerData.Length} bytes, but importing it as the {selectedLayer} layer needs {expectedLength}. Pick the layer the file was exported for.",
                    LogLevel.ErrorMessageBox);
                return;
            }

            LayerImporter.Import(project, layerData, selectedLayer);
        }
    }
}
