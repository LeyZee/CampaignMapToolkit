using System;
using System.Windows.Controls;

namespace CAIME.ViewModels
{
    public class ActionsViewModel : BaseViewModel
    {
        private UserControl layerControl;
        public UserControl LayerControl
        {
            get
            {
                return layerControl;
            }
            set
            {
                layerControl = value;
                OnPropertyChanged(nameof(LayerControl));
            }
        }

        public ActionsViewModel()
        {

        }
    }
}
