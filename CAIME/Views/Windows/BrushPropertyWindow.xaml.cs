using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace CAIME.Windows
{
    /// <summary>
    /// Interaction logic for BrushPropertyWindow.xaml
    /// </summary>
    public partial class BrushPropertyWindow : Window, INotifyPropertyChanged
    {
        private ToolbarViewModel toolbarVM;
        private int brushSize;
        public int BrushSize
        {
            get
            {
                return brushSize;
            }
            set
            {
                brushSize = value;
                RaisePropertyChanged(nameof(BrushSize));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public BrushPropertyWindow(ToolbarViewModel toolbarRef)
        {
            InitializeComponent();
            toolbarVM = toolbarRef;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            int? brushSize = toolbarVM.GetBrushSize();
            if (brushSize.HasValue)
            {
                brushSizeSlider.Value = brushSize.Value;
                BrushSize = brushSize.Value;
            }
        }

        private void Okay_Click(object sender, RoutedEventArgs e)
        {
            BrushSize = (int)brushSizeSlider.Value;
            ApplyChanges(BrushSize);
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            brushSizeSlider.Value = BrushSize;
            ApplyChanges(BrushSize);
            Close();
        }

        private void BrushSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var slider = sender as Slider;
            if (slider == null)
                return;

            ApplyChanges((int)slider.Value);
        }

        private void ApplyChanges(int brushSize)
        {
            if (toolbarVM == null)
                return;

            toolbarVM.ChangeBrushProperties(brushSize);
        }
    }
}
