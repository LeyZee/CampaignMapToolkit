using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HelixToolkit.Wpf.SharpDX;

namespace CAIME 
{
    public class MinimapViewModel : BaseViewModel
    {
        private readonly int dpiX = 96;
        private readonly int dpiY = 96;
        private readonly PixelFormat format = PixelFormats.Bgra32;
        
        private ViewportViewModel viewportVM;
        private float aspectRatio;
        private float viewportToMinimapScale;

        #region Binded properties
        public BitmapSource Minimap { get; private set; }

        private int minimapWidth;
        public int MinimapWidth
        {
            get => minimapWidth;
            private set
            {
                minimapWidth = value;
                OnPropertyChanged(nameof(MinimapWidth));
            }
        }

        private int minimapHeight;
        public int MinimapHeight
        {
            get => minimapHeight;
            private set
            {
                minimapHeight = value;
                OnPropertyChanged(nameof(MinimapHeight));
            }
        }

        private int viewFrameWidth;
        public int ViewFrameWidth
        {
            get => viewFrameWidth;
            private set
            {
                viewFrameWidth = value;
                OnPropertyChanged(nameof(ViewFrameWidth));
            }
        }

        private int viewFrameHeight;
        public int ViewFrameHeight
        {
            get => viewFrameHeight;
            private set
            {
                viewFrameHeight = value;
                OnPropertyChanged(nameof(ViewFrameHeight));
            }
        }

        private int viewFrameX;
        public int ViewFrameX
        {
            get => viewFrameX;
            private set
            {
                viewFrameX = value;
                OnPropertyChanged(nameof(ViewFrameX));
            }
        }

        private int viewFrameY;
        public int ViewFrameY
        {
            get => viewFrameY;
            private set
            {
                viewFrameY = value;
                OnPropertyChanged(nameof(ViewFrameY));
            }
        }
        #endregion

        public MinimapViewModel()
        {}

        public void UpdateMinimap(int width, int height, int[] gridColors)
        {
            var pixels = new int[gridColors.Length];
            for (int index = 0; index < pixels.Length; ++index)
            {
                pixels[index] = Utility.RgbaToBgra(gridColors[index]);
            }

            int stride = (width * format.BitsPerPixel + 7) / 8;
            Minimap = BitmapSource.Create(width, height, dpiX, dpiY, format, null, pixels, stride);

            OnPropertyChanged(nameof(Minimap));
            // LoggerViewModel.Log("Minimap has been set", LogLevel.Info);
        }

        public void MoveViewFrame(Point pos)
        {
            float centerX = ViewFrameWidth  / 2.0f;
            float centerY = ViewFrameHeight / 2.0f;

            ViewFrameX = (int)(pos.X - centerX);
            ViewFrameY = (int)(pos.Y - centerY);

            float x = (float)(pos.X - (MinimapWidth  / 2.0f));
            float y = (float)(pos.Y - (MinimapHeight / 2.0f));

            double cameraX = viewportVM.GridOutlineGeometry.Bound.Center.X + viewportToMinimapScale * x;
            double cameraY = viewportVM.GridOutlineGeometry.Bound.Center.Y + viewportToMinimapScale * -y;
            double cameraZ = viewportVM.Camera.Position.Z;

            viewportVM.Camera.Position = new System.Windows.Media.Media3D.Point3D(cameraX, cameraY, cameraZ);
        }

        public void ScaleViewFrame(int delta)
        {
            float factor = 30.0f;
            float step = delta / factor;

            double vw = viewportVM.Viewport.ActualWidth;
            double vh = viewportVM.Viewport.ActualHeight;
            float viewportAspect = (vw > 0 && vh > 0) ? (float)(vw / vh) : (1.0f / aspectRatio);

            int width  = (int)(step + ViewFrameWidth);
            int height = (int)(width / viewportAspect);

            if (height < 8 || width < 8)
                return;

            float prevX = ViewFrameWidth;
            float prevY = ViewFrameHeight;

            ViewFrameWidth  = Math.Min(width,  MinimapWidth);
            ViewFrameHeight = Math.Min(height, MinimapHeight);

            ViewFrameX += (int)((prevX - ViewFrameWidth)  / 2.0f);
            ViewFrameY += (int)((prevY - ViewFrameHeight) / 2.0f);

            viewportVM.Viewport.AddZoomForce(step / (factor / 10.0f * viewportToMinimapScale));
        }

        public void UpdateViewFrameFromCamera()
        {
            if (viewportVM?.Camera == null || MinimapWidth == 0 || viewportToMinimapScale <= 0
                || viewportVM.GridOutlineGeometry == null)
                return;

            var camera = viewportVM.Camera as OrthographicCamera;
            if (camera == null) return;

            double vw = viewportVM.Viewport.ActualWidth;
            double vh = viewportVM.Viewport.ActualHeight;
            if (vw <= 0 || vh <= 0) return;

            float camWorldWidth  = (float)camera.Width;
            float camWorldHeight = camWorldWidth * (float)(vh / vw);

            int newFrameWidth  = Math.Min((int)(camWorldWidth  / viewportToMinimapScale), MinimapWidth);
            int newFrameHeight = Math.Min((int)(camWorldHeight / viewportToMinimapScale), MinimapHeight);
            newFrameWidth  = Math.Max(1, newFrameWidth);
            newFrameHeight = Math.Max(1, newFrameHeight);

            var bounds = viewportVM.GridOutlineGeometry.Bound;
            float camOffsetX = (float)(camera.Position.X - bounds.Center.X);
            float camOffsetY = (float)(camera.Position.Y - bounds.Center.Y);

            float minimapCenterX = MinimapWidth  / 2.0f + camOffsetX / viewportToMinimapScale;
            float minimapCenterY = MinimapHeight / 2.0f - camOffsetY / viewportToMinimapScale;

            ViewFrameWidth  = newFrameWidth;
            ViewFrameHeight = newFrameHeight;
            ViewFrameX = (int)(minimapCenterX - ViewFrameWidth  / 2.0f);
            ViewFrameY = (int)(minimapCenterY - ViewFrameHeight / 2.0f);
        }

        public void SetViewportViewModel(ViewportViewModel inViewportVM)
        {
            viewportVM = inViewportVM;
            viewportVM.GridCreated += OnGridCreated;
        }

        private void OnGridCreated(object sender, EventArgs e)
        {
            aspectRatio     = (float)viewportVM.GridOutlineGeometry.Bound.Height / (float)viewportVM.GridOutlineGeometry.Bound.Width;
            
            MinimapWidth    = 229;
            MinimapHeight   = (int)(MinimapWidth * aspectRatio);

            ViewFrameWidth    = MinimapWidth;
            ViewFrameHeight   = MinimapHeight;

            ViewFrameX        = 0;
            ViewFrameY        = 0;

            viewportToMinimapScale = viewportVM.GridOutlineGeometry.Bound.Width / MinimapWidth;
        }
    }
}
