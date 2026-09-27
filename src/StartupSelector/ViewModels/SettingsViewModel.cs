using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using StartupSelector.Models;
using StartupSelector.Services;

namespace StartupSelector.ViewModels
{
    public sealed class SettingsViewModel : ObservableObject
    {
        private readonly Func<Task<bool>> _releaseAll;
        private string _countdownText;
        private string _launchDelayText;
        private bool _startWithWindows;
        private bool _minimizeToTrayOnClose;
        private string? _errorText;

        public SettingsViewModel(AppSettings settings, bool isSelfRegistered, Func<Task<bool>> releaseAll)
        {
            _releaseAll = releaseAll;
            _countdownText = settings.CountdownSeconds.ToString(CultureInfo.CurrentCulture);
            _launchDelayText = settings.LaunchDelayMilliseconds.ToString(CultureInfo.CurrentCulture);
            _startWithWindows = isSelfRegistered;
            _minimizeToTrayOnClose = settings.MinimizeToTrayOnClose;
            ManagedCount = settings.ManagedEntries.Count;

            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false));
            ReleaseAllCommand = new AsyncRelayCommand(ReleaseAllAsync);
            OpenLogCommand = new RelayCommand(OpenLog);
            OpenDataFolderCommand = new RelayCommand(() => OpenPath(AppPaths.DataDirectory));
        }

        /// <summary>Raised with true to save & close, false to close without saving.</summary>
        public event EventHandler<bool>? CloseRequested;

        public string CountdownText
        {
            get => _countdownText;
            set => SetProperty(ref _countdownText, value);
        }

        public string LaunchDelayText
        {
            get => _launchDelayText;
            set => SetProperty(ref _launchDelayText, value);
        }

        public bool StartWithWindows
        {
            get => _startWithWindows;
            set => SetProperty(ref _startWithWindows, value);
        }

        public bool MinimizeToTrayOnClose
        {
            get => _minimizeToTrayOnClose;
            set => SetProperty(ref _minimizeToTrayOnClose, value);
        }

        public string? ErrorText
        {
            get => _errorText;
            private set => SetProperty(ref _errorText, value);
        }

        public int ManagedCount { get; }

        public string DataFolder => AppPaths.DataDirectory;

        public int CountdownSeconds { get; private set; }

        public int LaunchDelayMilliseconds { get; private set; }

        /// <summary>True when "Release all apps back to Windows" completed; the caller then offers to exit.</summary>
        public bool ReleaseCompleted { get; private set; }

        public ICommand SaveCommand { get; }

        public ICommand CancelCommand { get; }

        public ICommand ReleaseAllCommand { get; }

        public ICommand OpenLogCommand { get; }

        public ICommand OpenDataFolderCommand { get; }

        private void Save()
        {
            if (!int.TryParse(CountdownText.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var countdown) ||
                countdown < 0 || countdown > 300)
            {
                ErrorText = "Countdown must be a whole number of seconds between 0 and 300.";
                return;
            }

            if (!int.TryParse(LaunchDelayText.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var delay) ||
                delay < 0 || delay > 30000)
            {
                ErrorText = "Delay between launches must be between 0 and 30000 milliseconds.";
                return;
            }

            CountdownSeconds = countdown;
            LaunchDelayMilliseconds = delay;
            ErrorText = null;
            CloseRequested?.Invoke(this, true);
        }

        private async Task ReleaseAllAsync()
        {
            if (await _releaseAll())
            {
                ReleaseCompleted = true;
                StartWithWindows = false;
                CloseRequested?.Invoke(this, false);
            }
        }

        private static void OpenLog()
        {
            if (!File.Exists(AppPaths.LogFile))
            {
                AppLog.Info("Log file created.");
            }

            OpenPath(AppPaths.LogFile);
        }

        private static void OpenPath(string path)
        {
            try
            {
                AppPaths.EnsureDataDirectory();
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
            }
            catch (Exception ex)
            {
                AppLog.Error($"Could not open '{path}'.", ex);
            }
        }
    }
}
