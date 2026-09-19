using System;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CAIME
{
    /// <summary>
    /// Interaction logic for Editor.xaml
    /// </summary>
    public partial class Editor : UserControl, IDisposable
    {
        private EditorViewModel _viewModel;
        private Timer _mouseOverTimer;

        public Editor()
        {
            InitializeComponent();
        }

        private void Control_Loaded(object sender, RoutedEventArgs e)
        {
            var brushSize = toolbar.ViewModel.GetBrushSize();
            if (brushSize.HasValue)
            {
                brushSizeSlider.Value = brushSize.Value;
            }
        }

        public void Initialize(EditorViewModel viewModel)
        {
            _viewModel = viewModel;
            DataContext = _viewModel;

            _viewModel.SetToolbarVM(toolbar.ViewModel);
            _viewModel.SetSidebarVM(sidebar.ViewModel);
            _viewModel.SetViewportVM(viewport.ViewModel);
            _viewModel.Initialise();

            sidebar.ViewModel.SetViewportViewModel(viewport.ViewModel);
            viewport.ViewModel.SetMinimapViewModel(sidebar.ViewModel.MinimapVM);

            _mouseOverTimer = new Timer()
            {
                Interval = 100, // 100 ms = 1 FPS
            };

            _mouseOverTimer.Elapsed += MouseOverTimer_Elapsed;
            _mouseOverTimer.Start();
        }

        public void Dispose()
        {
            _mouseOverTimer.Stop();
            _mouseOverTimer = null;

            _viewModel.Dispose();
        }

        #region Background Image
        private void BackImgOpacitySlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            var slider = sender as Slider;
            if (slider == null || viewport == null)
            {
                return;
            }

            viewport.ViewModel.SetBackgroundImageOpacity((float)slider.Value / 100);
        }

        private void BackImgOpacitySlider_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Left && e.Key != Key.Right)
            {
                return;
            }

            var slider = sender as Slider;
            if (slider == null || viewport == null)
            {
                return;
            }

            viewport.ViewModel.SetBackgroundImageOpacity((float)slider.Value / 100);
        }

        private void ClearImage_Click(object sender, RoutedEventArgs e)
        {
            if (viewport != null)
            {
                viewport.ViewModel.RemoveBackgroundImage();
                viewport.ViewModel.SetDesiredBackgroundImageOpacity((float)backImgOpacitySlider.Maximum / 100);
            }

            backImgOpacitySlider.Value = backImgOpacitySlider.Maximum;
        }
        #endregion

        private void FloodFillSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0)
            {
                return;
            }

            var layerName = e.AddedItems[e.AddedItems.Count - 1] as string;
            var layer = sidebar.ViewModel.LayersVM.GetLayerByName(layerName);

            if (layer != null)
            {
                if (_viewModel.SetFloodFillSource(layer) == false)
                {
                    MessageBox.Show("The selected Flood Fill source is unsupported. Please, choose another source.", "Flood Fill source layer error.");
                }
            }
        }

        private void MouseOverTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var pos = Mouse.GetPosition(viewport);

                if (pos.X > 0 && pos.Y > 0 && pos.X < viewport.ActualWidth && pos.Y < viewport.ActualHeight)
                {
                    var args = new ViewportMouseEventArgs(pos, viewport.ViewModel);
                    viewport.ViewModel.OnMouseOver?.Invoke(sender, args);
                }
            });
        }
    }
}
