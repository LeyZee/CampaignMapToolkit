using System;
using System.Windows;
using System.Runtime.InteropServices;
using WinInterop = System.Windows.Interop;

namespace CAIME {
    internal static class NativeMethods {
        [DllImport("user32")]
        internal static extern bool GetMonitorInfo(IntPtr hMonitor, MonitorInfo lpmi);

        [DllImport("User32")]
        internal static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

        internal enum WM {
            GETMINMAXINFO       = 0x0024
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Point {
            public int X;
            public int Y;

            public Point(int x, int y) {
                X = x;
                Y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MinMaxInfo {
            public Point ptReserved;
            public Point ptMaxSize;
            public Point ptMaxPosition;
            public Point ptMinTrackSize;
            public Point ptMaxTrackSize;
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class MonitorInfo {
            public int cbSize = Marshal.SizeOf(typeof(MonitorInfo));
            public Rect rcMonitor = new Rect();
            public Rect rcWork = new Rect();
            public int dwFlags = 0;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        public struct Rect {
            public static readonly Rect Empty = new Rect();
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width {
                get { return Math.Abs(Right - Left); }  // Abs needed for BIDI OS
            }
            public int Height {
                get { return Bottom - Top; }
            }

            public Rect(int left, int top, int right, int bottom) {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public Rect(Rect rcSrc) {
                Left = rcSrc.Left;
                Top = rcSrc.Top;
                Right = rcSrc.Right;
                Bottom = rcSrc.Bottom;
            }

            /// <summary> Return a user friendly representation of this struct </summary>
            public override string ToString() {
                if (this == Empty) { return "Rect {Empty}"; }
                return "Rect { Left : " + Left + " / top : " + Top + " / Right : " + Right + " / Bottom : " + Bottom + " }";
            }

            /// <summary> Determine if 2 Rect are equal (deep compare) </summary>
            public override bool Equals(object obj) {
                if (!(obj is System.Windows.Rect)) { return false; }
                return (this == (Rect)obj);
            }

            /// <summary>Return the HashCode for this struct (not garanteed to be unique)</summary>
            public override int GetHashCode() {
                return Left.GetHashCode() + Top.GetHashCode() + Right.GetHashCode() + Bottom.GetHashCode();
            }


            /// <summary> Determine if 2 Rect are equal (deep compare)</summary>
            public static bool operator ==(Rect rect1, Rect rect2) {
                return (rect1.Left == rect2.Left && rect1.Top == rect2.Top && rect1.Right == rect2.Right && rect1.Bottom == rect2.Bottom);
            }

            /// <summary> Determine if 2 Rect are different(deep compare)</summary>
            public static bool operator !=(Rect rect1, Rect rect2) {
                return !(rect1 == rect2);
            }
        }

        /// <summary>
        /// Helper to handle a proper borderless window maximization
        /// </summary>
        public class WindowMaximiseHelper {
            public static void Window_SourceInitialized(object sender, EventArgs e) {
                IntPtr handle = new WinInterop.WindowInteropHelper(sender as Window).Handle;
                WinInterop.HwndSource.FromHwnd(handle).AddHook(new WinInterop.HwndSourceHook(WindowProc));
            }

            private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
                switch ((WM)msg) {
                    case WM.GETMINMAXINFO:
                        WmGetMinMaxInfo(hwnd, lParam);
                        //handled = true; // true flag messes window's MinWidth and MinHeight
                        break;
                }

                return IntPtr.Zero;
            }

            private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam) {
                MinMaxInfo mmi = (MinMaxInfo)Marshal.PtrToStructure(lParam, typeof(MinMaxInfo));

                // Adjust the maximized size and position to fit the work area of the correct monitor
                int monitorDefaultToNearest = 0x00000002;
                IntPtr monitor = MonitorFromWindow(hwnd, monitorDefaultToNearest);

                if (monitor != IntPtr.Zero) {
                    MonitorInfo monitorInfo = new MonitorInfo();
                    GetMonitorInfo(monitor, monitorInfo);
                    Rect rcWorkArea = monitorInfo.rcWork;
                    Rect rcMonitorArea = monitorInfo.rcMonitor;
                    mmi.ptMaxPosition.X = Math.Abs(rcWorkArea.Left - rcMonitorArea.Left);
                    mmi.ptMaxPosition.Y = Math.Abs(rcWorkArea.Top - rcMonitorArea.Top); //-V3127
                    mmi.ptMaxSize.X = Math.Abs(rcWorkArea.Right - rcWorkArea.Left);
                    mmi.ptMaxSize.Y = Math.Abs(rcWorkArea.Bottom - rcWorkArea.Top);
                }

                Marshal.StructureToPtr(mmi, lParam, true);
            }
        }
    }

}
