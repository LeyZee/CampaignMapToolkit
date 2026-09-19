using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace CAIME
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        ErrorMessageBox,
    }

    public class LoggerViewModel : BaseViewModel
    {
        private string appVersion;
        public string AppVersion
        {
            get
            {
                if (string.IsNullOrEmpty(appVersion))
                {
                    appVersion = AppInfo.Version == AppInfo.DevBuild ? AppInfo.DevBuild : $"v{AppInfo.Version}";
                }

                return appVersion;
            }
        }

        private string fps;
        public string FPS
        {
            get
            {
                return fps;
            }
            set
            {
                fps = value;
                OnPropertyChanged(nameof(FPS));
            }
        }

        private int coordX;
        public string CoordX
        {
            get
            {
                return $"X: {coordX}";
            }
            set
            {
                if (int.TryParse(value, out int coord))
                {
                    coordX = coord;
                    OnPropertyChanged(nameof(CoordX));
                }
            }
        }

        private int coordY;
        public string CoordY
        {
            get
            {
                return $"Y: {coordY}";
            }
            set
            {
                if (int.TryParse(value, out int coord))
                {
                    coordY = coord;
                    OnPropertyChanged(nameof(CoordY));
                }
            }
        }

        private string visibleMessage;
        public string VisibleMessage
        {
            get
            {
                return visibleMessage;
            }
            private set
            {
                visibleMessage = value;
                OnPropertyChanged(nameof(VisibleMessage));
            }
        }

        private string  logFilePath;
        private bool    logToFile;

        public static readonly LoggerViewModel Instance = new LoggerViewModel();
        public ObservableCollection<MessageItem> MessagesStack { get; private set; }

        /// <summary>
        /// Optional sink for headless (CLI) runs. When set, every logged message is
        /// forwarded here so it can be written to the console. Independent of whether a
        /// WPF <see cref="Application"/> exists.
        /// </summary>
        public static Action<string, LogLevel> ConsoleSink;

        private readonly object _logFileLock = new object();

        /// <summary>
        /// Messages logged but not yet written out. Validators run Parallel.For over the whole map
        /// and log once per offending hex; a Dispatcher.BeginInvoke and a separate open/write/close
        /// of the log file per message freezes the app on a map with many warnings. Messages are
        /// queued here and delivered in batches instead.
        /// </summary>
        private readonly ConcurrentQueue<PendingMessage> _pending = new ConcurrentQueue<PendingMessage>();

        private readonly object _flushLock = new object();
        private int _flushScheduled;

        private struct PendingMessage
        {
            public string      FileLine;   // null when file logging is off
            public string      Raw;
            public MessageItem Item;       // null when the message is not shown in the log window
        }

        /// <summary>
        /// Oldest messages are dropped past this count so a long session cannot grow the
        /// logger window's backing collection without bound.
        /// </summary>
        private const int MAX_MESSAGES = 2000;

        private LoggerViewModel()
        {
            logFilePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\tool.log";

            try
            {
                File.Delete(logFilePath);
            }
            catch (Exception)
            {
                // A locked log file (e.g. a second running instance) must not crash the app -
                // this runs inside the static Instance initializer.
            }

            MessagesStack = new ObservableCollection<MessageItem>();
        }

        public static void Log(string message, LogLevel level)
        {
            var msg = message;
            var col = Brushes.White;
            var wgt = FontWeights.Normal;
            var stl = FontStyles.Normal;

            switch (level)
            {
                case LogLevel.Debug:
                    msg = $"Debug: {msg}";
                    stl = FontStyles.Italic;
                    break;
                case LogLevel.Info:
                    msg = $"Info: {msg}";
                    break;
                case LogLevel.Warning:
                    wgt = FontWeights.SemiBold;
                    col = Brushes.LightYellow;
                    msg = $"Warning: {msg}";
                    break;
                case LogLevel.Error:
                case LogLevel.ErrorMessageBox:
                    wgt = FontWeights.SemiBold;
                    col = Brushes.IndianRed;
                    msg = $"Error: {msg}";
                    break;
            }

            ConsoleSink?.Invoke(msg, level);

#if DEBUG
            Console.WriteLine(message);
#endif

            bool showInLogWindow = true;
#if !DEBUG
            showInLogWindow = level != LogLevel.Debug;
#endif

            Instance._pending.Enqueue(new PendingMessage
            {
                FileLine = Instance.logToFile ? msg : null,
                Raw      = message,
                Item     = showInLogWindow ? new MessageItem(msg, col, stl, wgt) : null,
            });

            var dispatcher = Application.Current?.Dispatcher;

            if (dispatcher == null)
            {
                // Headless (CLI, tests): nothing pumps the dispatcher, so deliver inline.
                Instance.FlushPending();
            }
            else if (Interlocked.Exchange(ref Instance._flushScheduled, 1) == 0)
            {
                dispatcher.BeginInvoke(new Action(Instance.FlushPending), DispatcherPriority.Background);
            }

            if (level == LogLevel.ErrorMessageBox && dispatcher != null)
            {
                dispatcher.BeginInvoke(new Action(() => MessageBox.Show(message, "Error")));
            }
        }

        /// <summary>
        /// Delivers everything queued since the last flush: one append to the log file for the
        /// whole batch, and one pass over the log window's collection.
        /// </summary>
        private void FlushPending()
        {
            Interlocked.Exchange(ref _flushScheduled, 0);

            List<string>      fileLines = null;
            List<MessageItem> items     = null;
            string            lastRaw   = null;

            while (_pending.TryDequeue(out PendingMessage pending))
            {
                if (pending.FileLine != null)
                {
                    if (fileLines == null) fileLines = new List<string>();
                    fileLines.Add(pending.FileLine);
                }

                if (pending.Item != null)
                {
                    if (items == null) items = new List<MessageItem>();
                    items.Add(pending.Item);
                }

                lastRaw = pending.Raw;
            }

            if (fileLines != null)
            {
                lock (_logFileLock)
                {
                    try
                    {
                        File.AppendAllLines(logFilePath, fileLines);
                    }
                    catch (Exception)
                    {
                        // A locked or unwritable log file must not take the app down.
                    }
                }
            }

            if (items == null)
            {
                return;
            }

            // FlushPending runs on the UI thread when there is one; the lock only guards the
            // headless case, where it can be reached from validator worker threads.
            lock (_flushLock)
            {
                foreach (var item in items)
                {
                    MessagesStack.Add(item);
                }

                while (MessagesStack.Count > MAX_MESSAGES)
                {
                    MessagesStack.RemoveAt(0);
                }
            }

            if (lastRaw != null)
            {
                VisibleMessage = lastRaw;
            }
        }
        
        public static void SetLogToFile(bool shouldLogToFile)
        {
            Instance.logToFile = shouldLogToFile;
        }

        public static void Clear()
        {
            Instance.MessagesStack.Clear();
        }
    }
}
