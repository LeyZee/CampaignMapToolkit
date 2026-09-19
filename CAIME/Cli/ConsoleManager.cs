using System;
using System.IO;
using System.Runtime.InteropServices;

namespace CAIME
{
    /// <summary>
    /// Attaches a console to this GUI (WinExe) process so command-line output is visible.
    /// A WinExe has no console of its own; when launched from a terminal we attach to the
    /// parent console, otherwise we allocate a fresh one.
    /// </summary>
    internal static class ConsoleManager
    {
        private const int ATTACH_PARENT_PROCESS = -1;
        private const int STD_INPUT_HANDLE = -10;
        private const ushort KEY_EVENT = 0x0001;
        private const ushort VK_RETURN = 0x0D;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteConsoleInput(
            IntPtr hConsoleInput, INPUT_RECORD[] lpBuffer, uint nLength, out uint lpNumberOfEventsWritten);

        [StructLayout(LayoutKind.Sequential)]
        private struct KEY_EVENT_RECORD
        {
            [MarshalAs(UnmanagedType.Bool)] public bool bKeyDown;
            public ushort wRepeatCount;
            public ushort wVirtualKeyCode;
            public ushort wVirtualScanCode;
            public char UnicodeChar;
            public uint dwControlKeyState;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUT_RECORD
        {
            [FieldOffset(0)] public ushort EventType;
            [FieldOffset(4)] public KEY_EVENT_RECORD KeyEvent;
        }

        private static bool _initialised;
        private static bool _attachedToParent;

        /// <summary>
        /// Makes <see cref="Console"/> write to a real console. Safe to call more than once.
        /// </summary>
        public static void EnsureConsole()
        {
            if (_initialised)
            {
                return;
            }

            _initialised = true;

            // Prefer the console of the shell that launched us; fall back to a new one.
            _attachedToParent = AttachConsole(ATTACH_PARENT_PROCESS);
            if (!_attachedToParent)
            {
                AllocConsole();
            }

            // The Console writers were created during startup when no console existed,
            // so they point nowhere. Rewire them to the real standard handles.
            var stdout = Console.OpenStandardOutput();
            Console.SetOut(new StreamWriter(stdout) { AutoFlush = true });

            var stderr = Console.OpenStandardError();
            Console.SetError(new StreamWriter(stderr) { AutoFlush = true });
        }

        /// <summary>
        /// Releases the console before the process exits. When we borrowed the parent shell's
        /// console, that shell already printed its prompt and won't redraw it until it reads a
        /// newline; without this the user has to press Enter to get their prompt back. We inject
        /// that Enter for them so the process appears to return control cleanly.
        /// </summary>
        public static void Shutdown()
        {
            if (!_attachedToParent)
            {
                return;
            }

            // Don't inject keystrokes when output is piped/redirected: the launching shell
            // waited for us in that case, so the prompt returns on its own.
            if (Console.IsOutputRedirected || Console.IsInputRedirected)
            {
                return;
            }

            try
            {
                Console.Out.Flush();
                Console.Error.Flush();
                SendEnter();
            }
            catch
            {
                // Best-effort cosmetic fix-up; never let it affect the exit path.
            }
        }

        private static void SendEnter()
        {
            var hStdIn = GetStdHandle(STD_INPUT_HANDLE);
            if (hStdIn == IntPtr.Zero || hStdIn == new IntPtr(-1))
            {
                return;
            }

            var records = new INPUT_RECORD[2];

            records[0].EventType = KEY_EVENT;
            records[0].KeyEvent = new KEY_EVENT_RECORD
            {
                bKeyDown = true,
                wRepeatCount = 1,
                wVirtualKeyCode = VK_RETURN,
                UnicodeChar = '\r',
            };

            records[1].EventType = KEY_EVENT;
            records[1].KeyEvent = new KEY_EVENT_RECORD
            {
                bKeyDown = false,
                wRepeatCount = 1,
                wVirtualKeyCode = VK_RETURN,
                UnicodeChar = '\r',
            };

            WriteConsoleInput(hStdIn, records, (uint)records.Length, out _);
        }
    }
}
