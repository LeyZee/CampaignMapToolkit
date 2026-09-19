using System;

namespace CAIME.Windows
{
    public class RenameSwatchViewModel : BaseViewModel
    {
        private readonly LayerType      _layerType;
        private readonly MapHexEditor   _mapHexEditor;

        private string oldSwatchName;
        public string OldSwatchName
        {
            get
            {
                return oldSwatchName;
            }
            set
            {
                oldSwatchName = value;
                OnPropertyChanged(nameof(OldSwatchName));
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

        public RenameSwatchViewModel(LayerType layerType, MapHexEditor mapHexEditor, string oldSwatchNameExt)
        {
            _layerType    = layerType;
            _mapHexEditor = mapHexEditor;
            OldSwatchName = oldSwatchNameExt;
            NewSwatchName = oldSwatchNameExt;

            switch (layerType)
            {
                case LayerType.GroundTypes:
                    WindowTitle = "Rename ground type";
                    break;
                case LayerType.Attritions:
                    WindowTitle = "Rename attrition";
                    break;
                case LayerType.Climates:
                    WindowTitle = "Rename climate";
                    break;
                case LayerType.Regions:
                    WindowTitle = "Rename region";
                    break;
            }
        }

        public bool ConfirmRenameSwatch()
        {
            switch (_layerType)
            {
                case LayerType.GroundTypes:
                    return _mapHexEditor.RenameGroundType(OldSwatchName, NewSwatchName);
                case LayerType.Attritions:
                    return _mapHexEditor.RenameAttrition(OldSwatchName, NewSwatchName);
                case LayerType.Climates:
                    return _mapHexEditor.RenameClimate(OldSwatchName, NewSwatchName);
                case LayerType.Regions:
                    return _mapHexEditor.RenameRegion(OldSwatchName, NewSwatchName);
            }

            return false;
        }
    }
}
