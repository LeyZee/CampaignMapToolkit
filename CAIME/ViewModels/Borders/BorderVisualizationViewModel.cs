using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace CAIME
{
    using BitmapSource = System.Windows.Media.Imaging.BitmapSource;

    class BorderVisualizationViewModel : BaseViewModel
    {
#if DEBUG
        private bool debugExportStreamFree;
        private string debugExportFile;
#endif

        #region Bindings
        public BitmapSource Bitmap { get; private set; }
        public Visibility ShowBitmap
        {
            get
            {
                return Bitmap == null ? Visibility.Hidden : Visibility.Visible;
            }
        }
        public SolidColorBrush BorderBrush
        {
            get
            {
                return new SolidColorBrush(bImage.BorderColour);
            }
        }
        public SolidColorBrush BorderPBrush
        {
            get
            {
                return new SolidColorBrush(bImage.BorderPColour);
            }
        }
        public SolidColorBrush OtherPartBrush
        {
            get
            {
                return new SolidColorBrush(bImage.OtherPartColour);
            }
        }
        public SolidColorBrush ComplementaryBrush
        {
            get
            {
                return new SolidColorBrush(bImage.ComplementaryColour);
            }
        }
        public SolidColorBrush RegFromBrush
        {
            get
            {
                return new SolidColorBrush(bImage.RegFromColour);
            }
        }
        public SolidColorBrush RegToBrush
        {
            get
            {
                return new SolidColorBrush(bImage.RegToColour);
            }
        }
        #endregion

        private Random random;
        private MapHexFile mapHexFile;

        public BorderImage bImage { get; private set; }
        public bool Active { get; private set; }

        private readonly int dpiX = 96;
        private readonly int dpiY = 96;
        private readonly PixelFormat format = PixelFormats.Bgra32;

        int margin;

        public BorderVisualizationViewModel(Project project) 
        {
            random = new Random();
            bImage = new BorderImage(project);
            margin = 2;

            mapHexFile = project.MapHexFile;
#if DEBUG
            debugExportFile = project.ProjectPath + "borderVis.png";
#endif
        }

        public void SetActive(bool active, BorderPoint bPoint = null, BorderPart bPart = null, List<BorderPart> borders = null)
        {
            Active = active;
            if (active && bPart != null)
            {
                CreateImage(bPoint, bPart, borders);
            }
            else
            {
                Bitmap = null;
            }

            OnPropertyChanged(nameof(ShowBitmap));
        }

        public void CreateBitmap()
        {
#if DEBUG
            if (debugExportStreamFree)
            {
                debugExportStreamFree = false;
                try
                {
                    // Synchronous on purpose: as a fire-and-forget task this raced the buffer it
                    // reads and any failure became an unobserved exception.
                    bImage.DebugExport(debugExportFile);
                }
                catch (Exception ex)
                {
                    LoggerViewModel.Log($"Border debug export failed: {ex.Message}", LogLevel.Warning);
                }
                finally
                {
                    debugExportStreamFree = true;
                }
            }
#endif

            int stride = bImage.width * format.BitsPerPixel;
            stride += 31;
            stride /= 32;
            stride *= 4;
            Bitmap = BitmapSource.Create(bImage.width, bImage.height, dpiX, dpiY, format, null, bImage.composition, stride);

            OnPropertyChanged(nameof(Bitmap));
        }

        public void CreateImage(BorderPoint bPoint, BorderPart bPart, List<BorderPart> borders)
        {
            bImage.SetValues(bPoint, bPart, borders, margin);
            bImage.Create();
            CreateBitmap();
        }

        public void UpdateImage(BorderPoint affected)
        {
            bImage.Update(affected);
            CreateBitmap();
        }

        private void UpdateColour(BorderImageLayer layerIndex, Color oldColour, Color newColour)
        {
            bImage.UpdateColour(layerIndex, oldColour, newColour, true);
            CreateBitmap();
        }

        public void UpdateSelected(BorderPoint bPoint)
        {
            bImage.UpdateSelected(bPoint, true);
            CreateBitmap();
        }

        private Color GetRandomColour()
        {
            var isDifferent = false;
            var colour = Colors.Black;

            while (!isDifferent)
            {
                colour = Color.FromArgb(255, (byte)random.Next(0, 255), (byte)random.Next(0, 255), (byte)random.Next(0, 255));

                if ((colour == bImage.BorderColour) ||
                    (colour == bImage.BorderPColour) ||
                    (colour == bImage.OtherPartColour) ||
                    (colour == bImage.ComplementaryColour) ||
                    (colour == bImage.RegFromColour) ||
                    (colour == bImage.RegToColour))
                    continue;

                isDifferent = true;
            }

            return colour;
        }

        public void ChangeBorderColour()
        {
            var oldColour = bImage.BorderColour;
            bImage.BorderColour = GetRandomColour();
            OnPropertyChanged(nameof(BorderBrush));

            if (Active)
            {
                UpdateColour(BorderImageLayer.Border, oldColour, bImage.BorderColour);
            }
        }

        public void ChangeBorderPColour()
        {
            var oldColour = bImage.BorderPColour;
            bImage.BorderPColour = GetRandomColour();
            OnPropertyChanged(nameof(BorderPBrush));

            if (Active)
            {
                UpdateColour(BorderImageLayer.Border, oldColour, bImage.BorderPColour);
            }
        }

        public void ChangeOtherPartColour()
        {
            var oldColour = bImage.OtherPartColour;
            bImage.OtherPartColour = GetRandomColour();
            OnPropertyChanged(nameof(OtherPartBrush));

            if (Active)
            {
                UpdateColour(BorderImageLayer.OtherBorders, oldColour, bImage.OtherPartColour);
            }
        }

        public void ChangeComplementaryColour()
        {
            var oldColour = bImage.ComplementaryColour;
            bImage.ComplementaryColour = GetRandomColour();
            OnPropertyChanged(nameof(ComplementaryBrush));

            if (Active)
            {
                UpdateColour(BorderImageLayer.OtherBorders, oldColour, bImage.ComplementaryColour);
            }
        }

        public void ChangeRegFromColour(SharpDX.Color? colour = null)
        {
            var oldColour = bImage.RegFromColour;
            bImage.RegFromColour = colour == null ? GetRandomColour() : Color.FromArgb(colour.Value.A, colour.Value.R, colour.Value.G, colour.Value.B);
            OnPropertyChanged(nameof(RegFromBrush));

            if (Active)
            {
                UpdateColour(BorderImageLayer.Regions, oldColour, bImage.RegFromColour);
            }
        }

        public void ChangeRegToColour(SharpDX.Color? colour = null)
        {
            var oldColour = bImage.RegToColour;
            bImage.RegToColour = colour == null ? GetRandomColour() : Color.FromArgb(colour.Value.A, colour.Value.R, colour.Value.G, colour.Value.B);
            OnPropertyChanged(nameof(RegToBrush));

            if (Active)
            {
                UpdateColour(BorderImageLayer.Regions, oldColour, bImage.RegToColour);
            }
        }
    }
}
