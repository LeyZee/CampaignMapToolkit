using System;
using System.Windows.Input;
using System.Windows.Controls;
using System.Timers;
using System.Windows;

namespace CAIME
{
    /// <summary>
    /// Interaction logic for Viewport.xaml
    /// </summary>
    public partial class Viewport : UserControl
    {
        public readonly ViewportViewModel ViewModel = new ViewportViewModel();

        private MouseButtonState cachedLeftButtonState;

        public Viewport()
        {
            InitializeComponent();
            DataContext = ViewModel;
            ViewModel.SetViewport(viewport);
        }

        /// <summary>
        /// Calculates hit test results and invokes tool action
        /// </summary>
        private void Viewport_PreviewMouseDown(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var newPos = e.GetPosition(viewport);
                var args = new ViewportMouseEventArgs(newPos, ViewModel);
                ViewModel.OnLeftMouseDown?.Invoke(sender, args);
            }

            cachedLeftButtonState = e.LeftButton;
        }

        static bool s_bIsEven = false;
        private void Viewport_MouseMove(object sender, MouseEventArgs e)
        {
            s_bIsEven = !s_bIsEven;
            
            // WORKAROUND to fix duplicate execution of code
            if (s_bIsEven == false)
            {
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var newPos = e.GetPosition(viewport);
                var args = new ViewportMouseEventArgs(newPos, ViewModel);
                ViewModel.OnLeftMouseMove?.Invoke(sender, args);
            }

            cachedLeftButtonState = e.LeftButton;
        }

        private void Viewport_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released && cachedLeftButtonState == MouseButtonState.Pressed)
            {
                var newPos = e.GetPosition(viewport);
                var args = new ViewportMouseEventArgs(newPos, ViewModel);
                ViewModel.OnLeftMouseUp?.Invoke(sender, args);
            }

            cachedLeftButtonState = e.LeftButton;
        }

        private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            ViewModel.Zoom(e.Delta);
        }
    }
}
