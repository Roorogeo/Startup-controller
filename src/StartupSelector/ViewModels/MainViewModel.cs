using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using StartupSelector.Models;
using StartupSelector.Services;

namespace StartupSelector.ViewModels
{
    public sealed class MainViewModel : ObservableObject
    {
        private const int MaxPresetNameLength = 40;

        private readonly AppSettings _settings;
        private readonly SettingsStore _store;
        private readonly StartupScanner _scanner;
        private readonly StartupController _controller;
        private readonly AppLauncher _launcher;
        private readonly IDialogService _dialogs;
        private readonly bool _startedAtLogin;
        private readonly DispatcherTimer _countdownTimer;
        private readonly HashSet<string> _loggedProblems = new(StringComparer.OrdinalIgnoreCase);

        private PresetViewModel? _selectedPreset;
        private bool _suppressPresetApply;
        private bool _isSelectionModified;
        private bool _isLaunching;
        private bool _isBusy;
        private bool _isSettingsOpen;
        private bool _isCountdownVisible;
        private bool _isCountdownPaused;
        private string _countdownText = string.Empty;
        private double _countdownProgress;
        private DateTime _countdownEndsAt;
        private TimeSpan _countdownRemaining;

        public MainViewModel(
            AppSettings settings,
            SettingsStore store,
            StartupScanner scanner,
            StartupController controller,
            AppLauncher launcher,
            IDialogService dialogs,
            bool startedAtLogin)
        {
            _settings = settings;
            _store = store;
            _scanner = scanner;
            _controller = controller;
            _launcher = launcher;
            _dialogs = dialogs;
            _startedAtLogin = startedAtLogin;

            var view = (ListCollectionView)CollectionViewSource.GetDefaultView(Items);
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(StartupItemViewModel.GroupName)));
            view.SortDescriptions.Add(new SortDescription(nameof(StartupItemViewModel.GroupOrder), ListSortDirection.Ascending));
            view.SortDescriptions.Add(new SortDescription(nameof(StartupItemViewModel.IsNew), ListSortDirection.Descending));
            view.SortDescriptions.Add(new SortDescription(nameof(StartupItemViewModel.DisplayName), ListSortDirection.Ascending));
            view.IsLiveGrouping = true;
            view.LiveGroupingProperties.Add(nameof(StartupItemViewModel.GroupName));
            view.IsLiveSorting = true;
            view.LiveSortingProperties.Add(nameof(StartupItemViewModel.GroupOrder));
            view.LiveSortingProperties.Add(nameof(StartupItemViewModel.IsNew));
            ItemsView = view;

            _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _countdownTimer.Tick += OnCountdownTick;

            AllOnCommand = new RelayCommand(() => SetAllChecked(true), () => Items.Any(i => i.IsManaged));
            AllOffCommand = new RelayCommand(() => SetAllChecked(false), () => Items.Any(i => i.IsManaged));
            LaunchSelectedCommand = new AsyncRelayCommand(LaunchSelectedAsync, () => !_isLaunching);
            RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !_isBusy);
            OpenSettingsCommand = new AsyncRelayCommand(OpenSettingsAsync);
            ManageAllCommand = new AsyncRelayCommand(ManageAllAsync, () => !_isBusy && Items.Any(i => !i.IsManaged));
            ToggleManageCommand = new AsyncRelayCommand(p => ToggleManageAsync(p as StartupItemViewModel), _ => !_isBusy);
            DismissNewCommand = new RelayCommand(p => DismissNew(p as StartupItemViewModel));
            SavePresetAsCommand = new RelayCommand(SavePresetAs);
            UpdatePresetCommand = new RelayCommand(UpdatePreset, () => SelectedPreset is not null);
            RenamePresetCommand = new RelayCommand(RenamePreset, () => SelectedPreset is not null);
            DeletePresetCommand = new RelayCommand(DeletePreset, () => SelectedPreset is not null);
            SetDefaultPresetCommand = new RelayCommand(SetDefaultPreset, () => SelectedPreset is { IsDefault: false });
            ToggleCountdownPauseCommand = new RelayCommand(ToggleCountdownPause);
        }

        /// <summary>Ask the window to hide to the tray.</summary>
        public event EventHandler? HideRequested;

        /// <summary>Ask the app to show a tray balloon (title, text, isWarning).</summary>
        public event Action<string, string, bool>? NotificationRequested;

        /// <summary>Ask the app to exit (after releasing everything).</summary>
        public event EventHandler? ExitRequested;

        public ObservableCollection<StartupItemViewModel> Items { get; } = new();

        public ICollectionView ItemsView { get; }

        public ObservableCollection<PresetViewModel> Presets { get; } = new();

        public ObservableCollection<InfoBarViewModel> InfoBars { get; } = new();

        public PresetViewModel? SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                if (SetProperty(ref _selectedPreset, value))
                {
                    if (value is not null && !_suppressPresetApply)
                    {
                        ApplyPreset(value);
                    }

                    UpdateSelectionState();
                }
            }
        }

        public bool IsSelectionModified
        {
            get => _isSelectionModified;
            private set => SetProperty(ref _isSelectionModified, value);
        }

        public bool IsCountdownVisible
        {
            get => _isCountdownVisible;
            private set => SetProperty(ref _isCountdownVisible, value);
        }

        public bool IsCountdownPaused
        {
            get => _isCountdownPaused;
            private set
            {
                if (SetProperty(ref _isCountdownPaused, value))
                {
                    OnPropertyChanged(nameof(CountdownPauseButtonText));
                }
            }
        }

        public string CountdownPauseButtonText => IsCountdownPaused ? "Resume" : "Pause";

        public string CountdownText
        {
            get => _countdownText;
            private set => SetProperty(ref _countdownText, value);
        }

        /// <summary>0-100, how much of the countdown has elapsed.</summary>
        public double CountdownProgress
        {
            get => _countdownProgress;
            private set => SetProperty(ref _countdownProgress, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set => SetProperty(ref _isBusy, value);
        }

        public bool HasItems => Items.Count > 0;

        public int CheckedCount => Items.Count(i => i.IsManaged && i.IsChecked);

        public string LaunchButtonText => CheckedCount == 0 ? "Launch Selected" : $"Launch Selected ({CheckedCount})";

        public string StatusText
        {
            get
            {
                var managed = Items.Count(i => i.IsManaged);
                var broken = Items.Count(i => i.IsBroken);
                var text = $"{managed} managed · {CheckedCount} selected · {Items.Count} startup apps found";
                return broken > 0 ? $"{text} · {broken} with problems" : text;
            }
        }

        public string AdminHint => ElevationService.IsAdministrator
            ? "Running as administrator"
            : "Entries with a shield need administrator approval to change";

        public ICommand AllOnCommand { get; }

        public ICommand AllOffCommand { get; }

        public ICommand LaunchSelectedCommand { get; }

        public ICommand RefreshCommand { get; }

        public ICommand OpenSettingsCommand { get; }

        public ICommand ManageAllCommand { get; }

        public ICommand ToggleManageCommand { get; }

        public ICommand DismissNewCommand { get; }

        public ICommand SavePresetAsCommand { get; }

        public ICommand UpdatePresetCommand { get; }

        public ICommand RenamePresetCommand { get; }

        public ICommand DeletePresetCommand { get; }

        public ICommand SetDefaultPresetCommand { get; }

        public ICommand ToggleCountdownPauseCommand { get; }

        public IReadOnlyList<string> PresetNames => Presets.Select(p => p.Name).ToList();

        private PresetViewModel? DefaultPreset => Presets.FirstOrDefault(p => p.IsDefault);

        /// <summary>Scans, restores the saved state and (at sign-in) starts the countdown.</summary>
        public async Task InitializeAsync(string? corruptSettingsBackup, bool restoredFromLastGood)
        {
            LoadPresets();
            await RefreshAsync();

            var firstRun = !_settings.FirstRunCompleted;
            if (firstRun)
            {
                // Everything present on the first run counts as already seen, not "New".
                _settings.KnownEntryIds = Items.Select(i => i.Id).ToList();
                _settings.FirstRunCompleted = true;
                AddInfoBar(
                    "welcome",
                    "Welcome! Click Manage on the apps you want to choose at every sign-in. Startup Selector disables them in "
                    + "Windows' own startup (the same way Task Manager does) and launches the ones you tick.",
                    isWarning: false);
            }

            if (corruptSettingsBackup is not null)
            {
                AddInfoBar(
                    "corrupt",
                    "Your settings file was damaged"
                    + (restoredFromLastGood ? " and the last good copy was restored." : ", so defaults were loaded.")
                    + $" The damaged file was saved to {corruptSettingsBackup}.",
                    isWarning: true);
            }

            ApplyInitialSelection();
            Save();

            if (_startedAtLogin && !firstRun && _settings.CountdownSeconds > 0)
            {
                StartCountdown(TimeSpan.FromSeconds(_settings.CountdownSeconds));
            }
        }

        /// <summary>Called by the window on any mouse click or key press: the user is taking over.</summary>
        public void PauseCountdown()
        {
            if (!IsCountdownVisible || IsCountdownPaused)
            {
                return;
            }

            _countdownRemaining = _countdownEndsAt - DateTime.UtcNow;
            _countdownTimer.Stop();
            IsCountdownPaused = true;
            CountdownText = "Countdown paused. Choose your apps, then click Launch Selected.";
        }

        public async Task LaunchPresetByNameAsync(string presetName)
        {
            var preset = Presets.FirstOrDefault(p => string.Equals(p.Name, presetName, StringComparison.OrdinalIgnoreCase));
            if (preset is null)
            {
                return;
            }

            SelectedPreset = preset;
            ApplyPreset(preset);
            await LaunchSelectedAsync();
        }

        public Task OpenSettingsAsync()
        {
            // The tray menu can ask again while the modal settings window is already open.
            if (_isSettingsOpen)
            {
                return Task.CompletedTask;
            }

            PauseCountdown();
            var vm = new SettingsViewModel(_settings, _controller.IsSelfRegistered(), ReleaseAllAsync);
            _isSettingsOpen = true;
            bool saved;
            try
            {
                saved = _dialogs.ShowSettings(vm);
            }
            finally
            {
                _isSettingsOpen = false;
            }

            if (saved)
            {
                _settings.CountdownSeconds = vm.CountdownSeconds;
                _settings.LaunchDelayMilliseconds = vm.LaunchDelayMilliseconds;
                _settings.MinimizeToTrayOnClose = vm.MinimizeToTrayOnClose;
                if (vm.StartWithWindows != _settings.StartWithWindows || vm.StartWithWindows != _controller.IsSelfRegistered())
                {
                    _settings.StartWithWindows = vm.StartWithWindows;
                    if (vm.StartWithWindows)
                    {
                        _controller.RegisterSelf(forceEnable: true);
                    }
                    else
                    {
                        _controller.UnregisterSelf();
                    }
                }

                Save();
            }
            else if (vm.ReleaseCompleted)
            {
                if (_dialogs.Confirm(
                        "Released",
                        "Every managed app is back under Windows' control and Startup Selector no longer starts at sign-in.\n\n"
                        + $"To uninstall, exit Startup Selector, delete StartupSelector.exe and the folder {AppPaths.DataDirectory}.\n\n"
                        + "Exit Startup Selector now?",
                        "Exit",
                        "Keep open"))
                {
                    ExitRequested?.Invoke(this, EventArgs.Empty);
                }
            }

            return Task.CompletedTask;
        }

        public bool MinimizeToTrayOnClose => _settings.MinimizeToTrayOnClose;

        public void Save() => _store.Save(_settings);

        private async Task RefreshAsync()
        {
            IsBusy = true;
            try
            {
                var entries = _scanner.Scan();
                var byId = entries.ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase);

                // Forget managed apps that were uninstalled (their Run value / shortcut is gone).
                var vanished = _settings.ManagedEntries.Where(m => !byId.ContainsKey(m.Id)).ToList();
                foreach (var gone in vanished)
                {
                    AppLog.Info($"Managed entry '{gone.DisplayName}' no longer exists; forgetting it.");
                    _settings.ManagedEntries.Remove(gone);
                }

                var managedIds = new HashSet<string>(_settings.ManagedEntries.Select(m => m.Id), StringComparer.OrdinalIgnoreCase);
                await EnforceManagedStateAsync(entries.Where(e => managedIds.Contains(e.Id) && e.IsEnabledInWindows).ToList());

                MergeItems(entries, managedIds);
                foreach (var broken in Items.Where(i => i.IsBroken && _loggedProblems.Add(i.Id + i.ProblemText)))
                {
                    AppLog.Warn($"Broken startup entry '{broken.DisplayName}' ({broken.Entry.SourceLabel}): {broken.ProblemText}");
                }

                // Entries that disappeared are forgotten, so if they come back they are flagged as New again.
                var prunedKnown = _settings.KnownEntryIds.RemoveAll(id => !byId.ContainsKey(id));

                UpdateNewAppsInfoBar();
                if (vanished.Count > 0 || prunedKnown > 0)
                {
                    Save();
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("Scanning startup apps failed.", ex);
                AddInfoBar("scan-error", "Startup apps could not be fully scanned. Details are in the log.", isWarning: true);
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(HasItems));
                UpdateSelectionState();
            }
        }

        /// <summary>
        /// Some apps re-enable themselves in Windows startup (e.g. after an update). Managed entries must stay
        /// disabled or they would start twice, so HKCU ones are silently switched off again; HKLM ones need a
        /// UAC prompt, so the user is asked through an info bar instead.
        /// </summary>
        private async Task EnforceManagedStateAsync(List<StartupEntry> reEnabled)
        {
            if (reEnabled.Count == 0)
            {
                return;
            }

            var silent = reEnabled.Where(e => !e.RequiresElevation || ElevationService.IsAdministrator).ToList();
            if (silent.Count > 0)
            {
                var result = await _controller.TakeControlAsync(silent);
                foreach (var entry in silent.Where(e => result.Succeeded.Any(c => c.EntryId == e.Id)))
                {
                    entry.IsEnabledInWindows = false;
                    AppLog.Info($"'{entry.DisplayName}' had been re-enabled in Windows; disabled it again.");
                }
            }

            var needsAdmin = reEnabled.Except(silent).ToList();
            if (needsAdmin.Count > 0)
            {
                RemoveInfoBar("re-enabled");
                AddInfoBar(
                    "re-enabled",
                    $"{needsAdmin.Count} managed app(s) were re-enabled in Windows' startup and may start twice.",
                    isWarning: true,
                    "Disable again",
                    new AsyncRelayCommand(async () =>
                    {
                        var result = await _controller.TakeControlAsync(needsAdmin);
                        if (result.AllSucceeded)
                        {
                            RemoveInfoBar("re-enabled");
                        }

                        await RefreshAsync();
                    }));
            }
        }

        private void MergeItems(List<StartupEntry> entries, HashSet<string> managedIds)
        {
            var known = new HashSet<string>(_settings.KnownEntryIds, StringComparer.OrdinalIgnoreCase);
            var existing = Items.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);
            var scannedIds = new HashSet<string>(entries.Select(e => e.Id), StringComparer.OrdinalIgnoreCase);

            foreach (var stale in Items.Where(i => !scannedIds.Contains(i.Id)).ToList())
            {
                stale.PropertyChanged -= OnItemPropertyChanged;
                Items.Remove(stale);
            }

            foreach (var entry in entries)
            {
                var isManaged = managedIds.Contains(entry.Id);
                if (existing.TryGetValue(entry.Id, out var item))
                {
                    item.UpdateEntry(entry);
                    item.IsManaged = isManaged;
                    if (!isManaged)
                    {
                        item.IsChecked = false;
                    }

                    continue;
                }

                var isNew = _settings.FirstRunCompleted && !isManaged && !known.Contains(entry.Id);
                item = new StartupItemViewModel(entry, isManaged, isNew);
                item.PropertyChanged += OnItemPropertyChanged;
                Items.Add(item);
            }
        }

        private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(StartupItemViewModel.IsChecked) or nameof(StartupItemViewModel.IsManaged)
                or nameof(StartupItemViewModel.IsBroken))
            {
                UpdateSelectionState();
            }
        }

        private void UpdateSelectionState()
        {
            IsSelectionModified = SelectedPreset is { } preset &&
                Items.Where(i => i.IsManaged).Any(i => i.IsChecked != preset.Includes(i.Id));
            OnPropertyChanged(nameof(CheckedCount));
            OnPropertyChanged(nameof(LaunchButtonText));
            OnPropertyChanged(nameof(StatusText));
        }

        private void LoadPresets()
        {
            Presets.Clear();
            foreach (var preset in _settings.Presets)
            {
                var isDefault = string.Equals(preset.Name, _settings.DefaultPresetName, StringComparison.OrdinalIgnoreCase);
                Presets.Add(new PresetViewModel(preset, isDefault));
            }
        }

        /// <summary>The default preset if there is one, otherwise the last launched selection.</summary>
        private void ApplyInitialSelection()
        {
            if (DefaultPreset is { } preset)
            {
                SelectedPreset = preset;
                ApplyPreset(preset);
                return;
            }

            var last = new HashSet<string>(_settings.LastUsedSelection, StringComparer.OrdinalIgnoreCase);
            foreach (var item in Items.Where(i => i.IsManaged))
            {
                item.IsChecked = last.Contains(item.Id);
            }
        }

        private void ApplyPreset(PresetViewModel preset)
        {
            foreach (var item in Items)
            {
                item.IsChecked = item.IsManaged && preset.Includes(item.Id);
            }

            UpdateSelectionState();
        }

        private void SetAllChecked(bool isChecked)
        {
            foreach (var item in Items.Where(i => i.IsManaged))
            {
                item.IsChecked = isChecked;
            }
        }

        private List<string> CurrentSelection() =>
            Items.Where(i => i.IsManaged && i.IsChecked).Select(i => i.Id).ToList();

        // ---------------------------------------------------------------- launching

        private async Task LaunchSelectedAsync()
        {
            if (_isLaunching)
            {
                return;
            }

            StopCountdown();
            var selected = Items.Where(i => i.IsManaged && i.IsChecked).ToList();
            _settings.LastUsedSelection = selected.Select(i => i.Id).ToList();
            Save();
            HideRequested?.Invoke(this, EventArgs.Empty);

            if (selected.Count == 0)
            {
                return;
            }

            _isLaunching = true;
            CommandManager.InvalidateRequerySuggested();
            try
            {
                var results = await _launcher.LaunchAsync(selected.Select(i => i.Entry).ToList(), _settings.LaunchDelayMilliseconds);
                foreach (var result in results)
                {
                    Items.FirstOrDefault(i => i.Id == result.Entry.Id)?.SetLaunchError(result.Success ? null : result.Error);
                }

                var failed = results.Where(r => !r.Success).ToList();
                if (failed.Count > 0)
                {
                    NotificationRequested?.Invoke(
                        "Some apps did not start",
                        $"{string.Join(", ", failed.Select(f => f.Entry.DisplayName))} could not be launched. Open Startup Selector for details.",
                        true);
                }

                UpdateSelectionState();
            }
            finally
            {
                _isLaunching = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // ---------------------------------------------------------------- countdown

        private void StartCountdown(TimeSpan duration)
        {
            _countdownRemaining = duration;
            _countdownEndsAt = DateTime.UtcNow + duration;
            IsCountdownPaused = false;
            IsCountdownVisible = true;
            UpdateCountdownText();
            _countdownTimer.Start();
        }

        private void StopCountdown()
        {
            _countdownTimer.Stop();
            IsCountdownVisible = false;
            IsCountdownPaused = false;
        }

        private void ToggleCountdownPause()
        {
            if (!IsCountdownPaused)
            {
                PauseCountdown();
                return;
            }

            _countdownEndsAt = DateTime.UtcNow + _countdownRemaining;
            IsCountdownPaused = false;
            UpdateCountdownText();
            _countdownTimer.Start();
        }

        private async void OnCountdownTick(object? sender, EventArgs e)
        {
            if (_countdownEndsAt <= DateTime.UtcNow)
            {
                StopCountdown();
                if (DefaultPreset is { } preset)
                {
                    SelectedPreset = preset;
                    ApplyPreset(preset);
                }

                try
                {
                    await LaunchSelectedAsync();
                }
                catch (Exception ex)
                {
                    AppLog.Error("Automatic launch failed.", ex);
                }

                return;
            }

            UpdateCountdownText();
        }

        private void UpdateCountdownText()
        {
            var total = Math.Max(1, _settings.CountdownSeconds);
            var remaining = Math.Max(0, (_countdownEndsAt - DateTime.UtcNow).TotalSeconds);
            CountdownProgress = Math.Clamp(100 * (1 - (remaining / total)), 0, 100);
            var what = DefaultPreset is { } preset ? $"preset \"{preset.Name}\"" : "your last selection";
            CountdownText = $"Launching {what} in {Math.Ceiling(remaining):0}s. Click anywhere to pause.";
        }

        // ---------------------------------------------------------------- managing

        private async Task ToggleManageAsync(StartupItemViewModel? item)
        {
            if (item is null)
            {
                return;
            }

            PauseCountdown();
            IsBusy = true;
            try
            {
                if (item.IsManaged)
                {
                    await ReleaseAsync(item);
                }
                else
                {
                    await TakeControlAsync(new[] { item });
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task TakeControlAsync(IReadOnlyList<StartupItemViewModel> items)
        {
            var wasEnabled = items.ToDictionary(i => i.Id, i => i.Entry.IsEnabledInWindows);
            var result = await _controller.TakeControlAsync(items.Select(i => i.Entry));
            var succeeded = new HashSet<string>(result.Succeeded.Select(c => c.EntryId), StringComparer.OrdinalIgnoreCase);

            foreach (var item in items.Where(i => succeeded.Contains(i.Id)))
            {
                _settings.ManagedEntries.RemoveAll(m => string.Equals(m.Id, item.Id, StringComparison.OrdinalIgnoreCase));
                _settings.ManagedEntries.Add(new ManagedEntry
                {
                    Id = item.Id,
                    DisplayName = item.DisplayName,
                    WasEnabledInWindows = wasEnabled[item.Id],
                    ManagedAtUtc = DateTime.UtcNow,
                });
                MarkKnown(item);
                item.SetEnabledInWindows(false);
                item.IsManaged = true;
                // Keep today's behaviour: an app that used to start with Windows starts checked.
                item.IsChecked = wasEnabled[item.Id];
            }

            Save();
            UpdateNewAppsInfoBar();
            ReportFailures(result, "take control of");
        }

        private async Task ReleaseAsync(StartupItemViewModel item)
        {
            var managed = _settings.ManagedEntries.FirstOrDefault(m => string.Equals(m.Id, item.Id, StringComparison.OrdinalIgnoreCase));
            var change = ApprovalChange.For(item.Entry, managed?.WasEnabledInWindows ?? true);
            var result = await _controller.ApplyAsync(new[] { change });
            if (result.Succeeded.Count > 0)
            {
                _settings.ManagedEntries.RemoveAll(m => string.Equals(m.Id, item.Id, StringComparison.OrdinalIgnoreCase));
                item.SetEnabledInWindows(change.Enable);
                item.IsManaged = false;
                item.IsChecked = false;
                Save();
            }

            ReportFailures(result, "release");
        }

        private async Task ManageAllAsync()
        {
            var unmanaged = Items.Where(i => !i.IsManaged).ToList();
            if (unmanaged.Count == 0)
            {
                return;
            }

            PauseCountdown();
            var adminNote = unmanaged.Any(i => i.ShowShield)
                ? "\n\nSome of them belong to all users, so Windows will ask for administrator approval once."
                : string.Empty;
            if (!_dialogs.Confirm(
                    "Manage all apps",
                    $"Take control of {unmanaged.Count} app(s)? They will be disabled in Windows' startup (nothing is deleted) "
                    + "and launched by Startup Selector instead. Apps that currently start with Windows stay checked." + adminNote,
                    "Manage all"))
            {
                return;
            }

            IsBusy = true;
            try
            {
                await TakeControlAsync(unmanaged);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>Re-enables every managed entry (to its pre-managed state) and removes the sign-in entry.</summary>
        private async Task<bool> ReleaseAllAsync()
        {
            if (!_dialogs.Confirm(
                    "Release all apps back to Windows",
                    "Every app managed by Startup Selector will be handed back to Windows and will start the way it did before. "
                    + "Startup Selector will also stop starting at sign-in, so it can be uninstalled cleanly.\n\nContinue?",
                    "Release all",
                    isDestructive: true))
            {
                return false;
            }

            IsBusy = true;
            try
            {
                var managedItems = Items.Where(i => i.IsManaged).ToList();
                var changes = managedItems
                    .Select(i => ApprovalChange.For(
                        i.Entry,
                        _settings.ManagedEntries.FirstOrDefault(m => string.Equals(m.Id, i.Id, StringComparison.OrdinalIgnoreCase))?.WasEnabledInWindows ?? true))
                    .ToList();

                var result = await _controller.ApplyAsync(changes);
                var succeeded = result.Succeeded.ToDictionary(c => c.EntryId, StringComparer.OrdinalIgnoreCase);
                foreach (var item in managedItems.Where(i => succeeded.ContainsKey(i.Id)))
                {
                    _settings.ManagedEntries.RemoveAll(m => string.Equals(m.Id, item.Id, StringComparison.OrdinalIgnoreCase));
                    item.SetEnabledInWindows(succeeded[item.Id].Enable);
                    item.IsManaged = false;
                    item.IsChecked = false;
                }

                // Managed entries that no longer exist on disk have nothing left to restore.
                var present = new HashSet<string>(Items.Select(i => i.Id), StringComparer.OrdinalIgnoreCase);
                _settings.ManagedEntries.RemoveAll(m => !present.Contains(m.Id));

                if (!result.AllSucceeded)
                {
                    Save();
                    _dialogs.ShowMessage(
                        "Not everything was released",
                        $"{result.Failed.Count} app(s) could not be handed back"
                        + (result.ElevationCancelled ? " because administrator approval was cancelled" : string.Empty)
                        + ". Startup Selector will keep starting at sign-in so those apps are not lost. Try again, or see the log for details.",
                        isWarning: true);
                    return false;
                }

                _controller.UnregisterSelf();
                _settings.StartWithWindows = false;
                Save();
                AppLog.Info("Released all apps back to Windows.");
                return true;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ReportFailures(ApplyResult result, string verb)
        {
            if (result.AllSucceeded)
            {
                return;
            }

            if (result.ElevationCancelled && result.Failed.All(c => c.RequiresElevation))
            {
                RemoveInfoBar("uac-cancelled");
                AddInfoBar("uac-cancelled", "Administrator approval was cancelled, so entries with a shield were not changed.", isWarning: true);
                return;
            }

            _dialogs.ShowMessage(
                "Something went wrong",
                $"Startup Selector could not {verb} {result.Failed.Count} app(s). Details were written to {AppPaths.LogFile}.",
                isWarning: true);
        }

        // ---------------------------------------------------------------- "New" apps

        private void DismissNew(StartupItemViewModel? item)
        {
            if (item is null)
            {
                return;
            }

            MarkKnown(item);
            Save();
            UpdateNewAppsInfoBar();
        }

        private void MarkKnown(StartupItemViewModel item)
        {
            item.IsNew = false;
            if (!_settings.KnownEntryIds.Contains(item.Id, StringComparer.OrdinalIgnoreCase))
            {
                _settings.KnownEntryIds.Add(item.Id);
            }
        }

        private void UpdateNewAppsInfoBar()
        {
            RemoveInfoBar("new-apps");
            var newItems = Items.Where(i => i.IsNew).ToList();
            if (newItems.Count == 0)
            {
                return;
            }

            AddInfoBar(
                "new-apps",
                newItems.Count == 1
                    ? $"\"{newItems[0].DisplayName}\" was added to Windows startup since last time. Manage it, or mark it as seen."
                    : $"{newItems.Count} apps were added to Windows startup since last time. They are marked NEW below.",
                isWarning: false,
                "Mark all as seen",
                new RelayCommand(() =>
                {
                    foreach (var item in Items.Where(i => i.IsNew).ToList())
                    {
                        MarkKnown(item);
                    }

                    Save();
                    RemoveInfoBar("new-apps");
                }));
        }

        // ---------------------------------------------------------------- info bars

        private void AddInfoBar(string key, string message, bool isWarning, string? actionText = null, ICommand? action = null)
        {
            if (InfoBars.Any(b => b.Key == key))
            {
                return;
            }

            InfoBars.Add(new InfoBarViewModel(key, message, isWarning, actionText, action, bar => InfoBars.Remove(bar)));
        }

        private void RemoveInfoBar(string key)
        {
            foreach (var bar in InfoBars.Where(b => b.Key == key).ToList())
            {
                InfoBars.Remove(bar);
            }
        }

        // ---------------------------------------------------------------- presets

        private void SavePresetAs()
        {
            PauseCountdown();
            var name = _dialogs.Prompt(
                "Save preset",
                "Save the current checkboxes as a preset named:",
                SuggestPresetName(),
                ValidatePresetName);
            if (name is null)
            {
                return;
            }

            var preset = new Preset { Name = name, CheckedIds = CurrentSelection() };
            _settings.Presets.Add(preset);
            var vm = new PresetViewModel(preset, isDefault: false);
            Presets.Add(vm);
            SelectWithoutApplying(vm);
            Save();
        }

        private void UpdatePreset()
        {
            if (SelectedPreset is not { } preset)
            {
                return;
            }

            if (preset.Model.IncludeAllManaged && !_dialogs.Confirm(
                    "Update preset",
                    $"\"{preset.Name}\" currently includes every managed app automatically, including apps you manage later. "
                    + "Replace it with exactly the apps checked now?",
                    "Replace"))
            {
                return;
            }

            preset.Model.IncludeAllManaged = false;
            preset.Model.CheckedIds = CurrentSelection();
            UpdateSelectionState();
            Save();
        }

        private void RenamePreset()
        {
            if (SelectedPreset is not { } preset)
            {
                return;
            }

            var name = _dialogs.Prompt("Rename preset", "New name:", preset.Name, n => ValidatePresetName(n, preset));
            if (name is null || name == preset.Name)
            {
                return;
            }

            if (preset.IsDefault)
            {
                _settings.DefaultPresetName = name;
            }

            preset.Name = name;
            Save();
        }

        private void DeletePreset()
        {
            if (SelectedPreset is not { } preset ||
                !_dialogs.Confirm("Delete preset", $"Delete the preset \"{preset.Name}\"?", "Delete", isDestructive: true))
            {
                return;
            }

            _settings.Presets.Remove(preset.Model);
            Presets.Remove(preset);
            if (preset.IsDefault)
            {
                var next = Presets.FirstOrDefault();
                _settings.DefaultPresetName = next?.Name;
                if (next is not null)
                {
                    next.IsDefault = true;
                }
            }

            SelectWithoutApplying(null);
            Save();
        }

        private void SetDefaultPreset()
        {
            if (SelectedPreset is not { } preset)
            {
                return;
            }

            foreach (var p in Presets)
            {
                p.IsDefault = ReferenceEquals(p, preset);
            }

            _settings.DefaultPresetName = preset.Name;
            Save();
        }

        private void SelectWithoutApplying(PresetViewModel? preset)
        {
            _suppressPresetApply = true;
            try
            {
                SelectedPreset = preset;
            }
            finally
            {
                _suppressPresetApply = false;
            }
        }

        private string SuggestPresetName()
        {
            var i = Presets.Count + 1;
            string name;
            do
            {
                name = $"Preset {i++}";
            }
            while (Presets.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)));

            return name;
        }

        private string? ValidatePresetName(string name) => ValidatePresetName(name, null);

        private string? ValidatePresetName(string name, PresetViewModel? renaming)
        {
            var trimmed = name.Trim();
            if (trimmed.Length == 0)
            {
                return "Please enter a name.";
            }

            if (trimmed.Length > MaxPresetNameLength)
            {
                return $"Names can be at most {MaxPresetNameLength} characters.";
            }

            if (Presets.Any(p => !ReferenceEquals(p, renaming) && string.Equals(p.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                return "A preset with that name already exists.";
            }

            return null;
        }
    }
}
