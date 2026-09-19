using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CAIME.ViewModels;

namespace CAIME
{
    /// <summary>
    /// Interaction logic for ColourPickControl.xaml
    /// </summary>
    public partial class ColourPickControl : UserControl
    {
        public ColourPickControlViewModel ViewModel { get; private set; }

        public ColourPickControl()
        {
            InitializeComponent();

            ViewModel = new ColourPickControlViewModel();
            DataContext = ViewModel;
        }

        public void SetDefaultColour(int colour)
        {
            Utility.RgbaDecompose(colour, out byte r, out byte g, out byte b, out byte _);
            ViewModel.SelectedColor = Color.FromRgb(r, g, b);
        }
    }
}
