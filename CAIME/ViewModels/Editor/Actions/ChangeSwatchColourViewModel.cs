using System;
using System.Windows;
using System.Windows.Media;

namespace CAIME.ViewModels
{
    public class ChangeSwatchColourViewModel : BaseViewModel
    {
        private readonly LayerType _layerType;
        private readonly MapHexEditor _mapHexEditor;
        private readonly int _swatchValue;

        private int selectedColor;
        public int DefaultColour
        {
            get
            {
                return selectedColor;
            }
            set
            {
                selectedColor = value;
                OnPropertyChanged(nameof(DefaultColour));
            }
        }

        public ChangeSwatchColourViewModel(LayerType layerType, MapHexEditor mapHexEditor, int swatchValue, int initColour)
        {
            _layerType      = layerType;
            _mapHexEditor   = mapHexEditor;
            _swatchValue    = swatchValue;
            DefaultColour   = initColour;
        }

        public bool ChangeColour(Color newColour)
        {
            return false;
        }
    }
}
