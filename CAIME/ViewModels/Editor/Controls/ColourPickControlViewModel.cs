using System;
using System.Windows.Media;

namespace CAIME.ViewModels
{
    public class ColourPickControlViewModel : BaseViewModel
    {
        private Color selectedColor;
        public Color SelectedColor
        {
            get
            {
                return selectedColor;
            }
            set
            {
                selectedColor = value;

                OnPropertyChanged(nameof(SelectedColor));
                OnPropertyChanged(nameof(PreviewSwatch));
                OnPropertyChanged(nameof(TextRed));
                OnPropertyChanged(nameof(TextGreen));
                OnPropertyChanged(nameof(TextBlue));
            }
        }

        public Brush PreviewSwatch
        {
            get
            {
                return new SolidColorBrush(selectedColor);
            }
        }

        public string TextRed
        {
            get
            {
                return selectedColor.R.ToString();
            }
            set
            {
                if (byte.TryParse(value, out byte red))
                {
                    selectedColor.R = red;
                    OnPropertyChanged(nameof(SelectedColor));
                    OnPropertyChanged(nameof(PreviewSwatch));
                }
            }
        }
        public string TextGreen
        {
            get
            {
                return selectedColor.G.ToString();
            }
            set
            {
                if (byte.TryParse(value, out byte green))
                {
                    selectedColor.G = green;
                    OnPropertyChanged(nameof(SelectedColor));
                    OnPropertyChanged(nameof(PreviewSwatch));
                }
            }
        }
        public string TextBlue
        {
            get
            {
                return selectedColor.B.ToString();
            }
            set
            {
                if (byte.TryParse(value, out byte blue))
                {
                    selectedColor.B = blue;
                    OnPropertyChanged(nameof(SelectedColor));
                    OnPropertyChanged(nameof(PreviewSwatch));
                }
            }
        }
    }
}
