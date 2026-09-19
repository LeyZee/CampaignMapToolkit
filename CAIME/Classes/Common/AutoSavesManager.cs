using System;
using System.Timers;
using System.Windows;

namespace CAIME
{
    internal class AutoSavesManager
    {
        private const double DEFAULT_INTERVAL = 5;

        private readonly ProjectManager _projectManager;
        private readonly PreferencesViewModel _preferences;

        private Timer timer;

        private double intervalInMinutes = DEFAULT_INTERVAL;

        /// <summary>How often the project is auto-saved. Setting it retunes a running timer.</summary>
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

        public AutoSavesManager(ProjectManager projectManager, PreferencesViewModel preferences)
        {
            _projectManager     = projectManager;
            _preferences        = preferences;
        }

        public bool Initialise()
        {
            // Tear down any previous timer and subscriptions so re-initialising on every
            // project open does not accumulate handlers or leak timers.
            Shutdown();

            IntervalInMinutes = DEFAULT_INTERVAL;

            timer = new Timer()
            {
                Interval = TimeSpan.FromMinutes(IntervalInMinutes).TotalMilliseconds,
            };

            timer.Elapsed += OnAutoSaveTimer_Elapsed;
            _preferences.OnPreferencesChanged += OnPreferencesChanged;

            CheckShouldStart();

            return true;
        }

        public void Shutdown()
        {
            _preferences.OnPreferencesChanged -= OnPreferencesChanged;

            if (timer != null)
            {
                timer.Stop();
                timer.Elapsed -= OnAutoSaveTimer_Elapsed;
                timer.Dispose();
                timer = null;
            }
        }

        private void OnPreferencesChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            CheckShouldStart();
        }

        private void OnAutoSaveTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // Saving mutates the map (hex types, edge masks), so it must run on the UI
            // thread - never concurrently with the user's edits.
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_projectManager.IsProjectOpen == false)
                {
                    return;
                }

                LoggerViewModel.Log("Auto Saves: Saving the project...", LogLevel.Info);

                var saveParams = new SaveParameters()
                {
                    IsAutoSave = true,
                    CustomFileName = $"{_projectManager.Project.MapHexFileName}_autosave",
                    Flags = SaveParameters.SaveFlags.MapHexFile
                };

                _projectManager.Save(saveParams);
            }));
        }

        private void CheckShouldStart()
        {
            if (timer == null)
            {
                return;
            }

            if (_preferences.IsAutoSave)
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
