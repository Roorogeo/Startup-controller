using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using StartupSelector.Services;
using StartupSelector.ViewModels;
using StartupSelector.Views;

namespace StartupSelector
{
    /// <summary>
    /// Composition root. Command line:
    ///   (none)                     open the selector normally
    ///   --startup                  launched at sign-in: show the countdown and auto-launch the default preset
    ///   --apply-elevated &lt;file&gt;   internal: elevated helper that applies HKLM StartupApproved changes, no UI
    /// </summary>
    public partial class App : Application
    {
        private SingleInstance? _singleInstance;
        private TrayIconService? _tray;
        private MainWindow? _window;
        private MainViewModel? _viewModel;
        private bool _isExiting;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var args = e.Args;

            var helperIndex = Array.FindIndex(args, a => a.Equals(ElevationService.ApplyElevatedArgument, StringComparison.OrdinalIgnoreCase));
            if (helperIndex >= 0)
            {
                var exitCode = helperIndex + 1 < args.Length ? ElevationService.RunHelper(args[helperIndex + 1]) : 2;
                Shutdown(exitCode);
                return;
            }

            _singleInstance = new SingleInstance();
            if (!_singleInstance.IsFirstInstance)
            {
                SingleInstance.SignalFirstInstance();
                _singleInstance.Dispose();
                _singleInstance = null;
                Shutdown(0);
                return;
            }

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
                AppLog.Error("Unhandled exception.", ex.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (_, ex) =>
            {
                AppLog.Error("Unobserved task exception.", ex.Exception);
                ex.SetObserved();
            };

            var startedAtLogin = args.Any(a => a.Equals(StartupController.StartupArgument, StringComparison.OrdinalIgnoreCase));
            AppLog.Info($"Startup Selector starting{(startedAtLogin ? " at sign-in" : string.Empty)} (admin: {ElevationService.IsAdministrator}).");

            var store = new SettingsStore();
            var settings = store.Load();
            var controller = new StartupController(new ElevationService());
            if (settings.StartWithWindows)
            {
                // Always refresh the path (the exe may have moved); only force "enabled" on the very first run
                // so a user who disabled it in Task Manager is respected.
                controller.RegisterSelf(forceEnable: !settings.FirstRunCompleted);
            }

            _viewModel = new MainViewModel(
                settings,
                store,
                new StartupScanner(),
                controller,
                new AppLauncher(),
                new DialogService(),
                startedAtLogin);

            _window = new MainWindow(_viewModel);
            MainWindow = _window;
            _window.ExitRequested += (_, _) => ExitApplication();
            _window.IsVisibleChanged += (_, _) =>
            {
                if (!_window.IsVisible && !_isExiting && !settings.TrayHintShown && _tray is not null)
                {
                    settings.TrayHintShown = true;
                    _viewModel.Save();
                    _tray.ShowNotification(
                        "Startup Selector is still running",
                        "It lives in the notification area. Click the icon to reopen it, or right-click for presets and Exit.",
                        isWarning: false);
                }
            };

            _tray = new TrayIconService(() => _viewModel.PresetNames);
            _tray.OpenRequested += (_, _) => ShowMainWindow();
            _tray.SettingsRequested += async (_, _) => await _viewModel.OpenSettingsAsync();
            _tray.LaunchPresetRequested += async (_, name) => await _viewModel.LaunchPresetByNameAsync(name);
            _tray.ExitRequested += (_, _) => ExitApplication();

            _viewModel.HideRequested += (_, _) => _window.Hide();
            _viewModel.NotificationRequested += (title, text, isWarning) => _tray.ShowNotification(title, text, isWarning);
            _viewModel.ExitRequested += (_, _) => ExitApplication();

            _singleInstance.ListenForActivation(() => Dispatcher.BeginInvoke(ShowMainWindow));

            try
            {
                await _viewModel.InitializeAsync(store.CorruptBackupPath, store.RestoredFromLastGood);
            }
            catch (Exception ex)
            {
                AppLog.Error("Initialization failed.", ex);
            }

            ShowMainWindow();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _tray?.Dispose();
            _tray = null;
            _singleInstance?.Dispose();
            _singleInstance = null;
            base.OnExit(e);
        }

        private void ShowMainWindow()
        {
            if (!_isExiting)
            {
                _window?.ShowAndActivate();
            }
        }

        private void ExitApplication()
        {
            if (_isExiting)
            {
                return;
            }

            _isExiting = true;
            _viewModel?.Save();
            AppLog.Info("Startup Selector exiting.");
            if (_window is not null)
            {
                _window.AllowClose = true;
                _window.Close();
            }

            Shutdown();
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            AppLog.Error("Unexpected error.", e.Exception);
            e.Handled = true;
            try
            {
                new DialogService().ShowMessage(
                    "Unexpected error",
                    $"Something went wrong, but Startup Selector is still running.\n\n{e.Exception.Message}\n\nDetails were written to {AppPaths.LogFile}.",
                    isWarning: true);
            }
            catch (Exception)
            {
                // Never let error reporting itself take the app down.
            }
        }
    }
}
