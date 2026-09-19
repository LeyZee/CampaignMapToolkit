using System;
using System.Windows;
using System.Windows.Input;
using CAIME.ViewModels;
using HelixToolkit.SharpDX.Utilities;

namespace CAIME
{
    public partial class MainWindow : Window
    {
        private static NVOptimusEnabler nvEnabler = new NVOptimusEnabler();

        private readonly MainWindowViewModel _mainWindowViewModel;

        private Tool prevTool;
        private bool isLAltDown;

        public MainWindow()
        {
            InitializeComponent();

            _mainWindowViewModel = new MainWindowViewModel();
            DataContext = _mainWindowViewModel;

            EditorControl.Initialize(_mainWindowViewModel.EditorViewModel);

            SourceInitialized += (sender, e) =>
            {
                NativeMethods.WindowMaximiseHelper.Window_SourceInitialized(sender, e);

                var menu = this.Template.FindName("MenuControl", this) as MenuControl;
                menu.SetViewModel(_mainWindowViewModel.MenuViewModel, _mainWindowViewModel.EditorViewModel.ProjectManager);
            };

            Closing += (sender, e) =>
            {
                this.KeyDown    -= MainWindow_KeyDown;
                this.KeyUp      -= MainWindow_KeyUp;
            };

            Closed += (sender, e) =>
            {
                if (_mainWindowViewModel.PreferencesViewModel.IsFirstAppLaunch)
                {
                    _mainWindowViewModel.PreferencesViewModel.IsFirstAppLaunch = false;
                    _mainWindowViewModel.PreferencesViewModel.Save();
                }

                prevTool = null;
                EditorControl.Dispose();
            };

            this.KeyDown    += MainWindow_KeyDown;
            this.KeyUp      += MainWindow_KeyUp;

            this.isLAltDown = false;
            this.prevTool   = null;
        }

        /// <summary>
        /// Show standard context menu on Right mouse button up
        /// </summary>
        private void Menu_MouseRightButtonUp(object sender, RoutedEventArgs e)
        {
            var point = PointToScreen(Mouse.GetPosition(this));
            SystemCommands.ShowSystemMenu(this, point);
        }

        /// <summary>
        /// Show standard context menu on Left mouse button down
        /// </summary>
        private void Menu_MouseLeftButtonDown(object sender, RoutedEventArgs e)
        {
            var point = WindowState == WindowState.Maximized ? new Point(0, 34) : new Point(Left, Top + 34);
            SystemCommands.ShowSystemMenu(this, point);
        }

        /// <summary>
        /// Minimise click handler
        /// </summary>
        private void Minimise_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// Maximise click handler
        /// </summary>
        private void Maximise_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        /// <summary>
        /// Close click handler
        /// </summary>
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _mainWindowViewModel.EditorViewModel.ExitApplication();
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (isLAltDown == false && (e.Key == Key.LeftAlt || e.Key == Key.System))
            {
                prevTool = _mainWindowViewModel.EditorViewModel.GetActiveTool();
                _mainWindowViewModel.EditorViewModel.SetActiveTool(ToolType.ColorPicker);
                isLAltDown = true;
            }

            HotkeyManager.ProcessKeyDown(sender, e);
            e.Handled = true;
        }

        private void MainWindow_KeyUp(object sender, KeyEventArgs e)
        {
            if (isLAltDown && (e.Key == Key.LeftAlt || e.Key == Key.System))
            {
                _mainWindowViewModel.EditorViewModel.SetActiveTool(prevTool.Type);
                prevTool = null;
                isLAltDown = false;
            }

            HotkeyManager.ProcessKeyUp(sender, e);
            e.Handled = true;
        }
    }
}