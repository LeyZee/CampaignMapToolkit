using System;
using System.IO;
using System.Timers;
using System.Windows;

namespace CAIME
{
    internal class AutoBackupManager
    {
        private const double DEFAULT_INTERVAL = 10;

        private readonly ProjectManager _projectManager;
        private readonly PreferencesViewModel _preferences;

        private Timer timer;
        private string backupPath;

        private double intervalInMinutes = DEFAULT_INTERVAL;

        /// <summary>How often a backup is taken. Setting it retunes a running timer.</summary>
        public double IntervalInMinutes
        {
            get => intervalInMinutes;
            set
            {
                if (value <= 0)
                {
                    return;
                }

                intervalInMinutes = value;

                if (timer != null)
                {
                    timer.Interval = TimeSpan.FromMinutes(intervalInMinutes).TotalMilliseconds;
                }
            }
        }

        public AutoBackupManager(ProjectManager projectManager, PreferencesViewModel preferences)
        {
            _projectManager     = projectManager;
            _preferences        = preferences;
        }

        public bool Initialise(string projectPath)
        {
            // Tear down any previous timer and subscriptions so re-initialising on every
            // project open does not accumulate handlers or leak timers.
            Shutdown();

            IntervalInMinutes = DEFAULT_INTERVAL;

            timer = new Timer()
            {
                Interval = TimeSpan.FromMinutes(IntervalInMinutes).TotalMilliseconds,
            };

            timer.Elapsed += OnAutoBackupTimer_Elapsed;
            _preferences.OnPreferencesChanged += OnPreferencesChanged;
            _projectManager.OnFilePathChanged += OnProjectPathChanged;

            backupPath = $"{projectPath}backups\\";
            Directory.CreateDirectory(backupPath);

            CheckShouldStart();

            return true;
        }

        public void Shutdown()
        {
            _preferences.OnPreferencesChanged -= OnPreferencesChanged;
            _projectManager.OnFilePathChanged -= OnProjectPathChanged;

            if (timer != null)
            {
                timer.Stop();
                timer.Elapsed -= OnAutoBackupTimer_Elapsed;
                timer.Dispose();
                timer = null;
            }
        }

        private void OnPreferencesChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            CheckShouldStart();
        }

        private void OnProjectPathChanged(object sender, ProjectPathChangedEventArgs e)
        {
            backupPath = $"{e.NewPath}backups\\";
            Directory.CreateDirectory(backupPath);
        }

        private void OnAutoBackupTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // Saving mutates the map (hex types, edge masks), so it must run on the UI
            // thread - never concurrently with the user's edits.
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_projectManager.IsProjectOpen == false)
                {
                    return;
                }

                LoggerViewModel.Log("Auto Backups: Creating project backup...", LogLevel.Info);

                var backupName = string.Format("{0}_{1:0000}_{2:00}_{3:00}_{4:00}_{5:00}_backup", _projectManager.Project.MapHexFileName, DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute);
                _projectManager.Backup(backupPath, backupName);

                PruneOldBackups();
            }));
        }

        /// <summary>
        /// Deletes all but the newest <see cref="PreferencesViewModel.AutoBackupsToKeep"/> backups.
        /// Does nothing when the preference is <see cref="PreferencesViewModel.KEEP_ALL_BACKUPS"/>,
        /// which is the default - a full map.hex is written every interval and never pruned
        /// otherwise, so a long session can fill the disk.
        /// </summary>
        private void PruneOldBackups()
        {
            int keep = _preferences.AutoBackupsToKeep;
            if (keep <= PreferencesViewModel.KEEP_ALL_BACKUPS)
            {
                return;
            }

            try
            {
                var backups = new DirectoryInfo(backupPath).GetFiles("*_backup.hex");
                if (backups.Length <= keep)
                {
                    return;
                }

                Array.Sort(backups, (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));

                for (int i = keep; i < backups.Length; ++i)
                {
                    backups[i].Delete();
                }

                LoggerViewModel.Log($"Auto Backups: Removed {backups.Length - keep} old backup(s), keeping the newest {keep}.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                LoggerViewModel.Log($"Auto Backups: Could not prune old backups ({ex.Message}).", LogLevel.Warning);
            }
        }

        private void CheckShouldStart()
        {
            if (timer == null)
            {
                return;
            }

            if (_preferences.IsAutoBackup)
            {
                timer.Start();
            }
            else
            {
                timer.Stop();
            }
        }
    }
}
