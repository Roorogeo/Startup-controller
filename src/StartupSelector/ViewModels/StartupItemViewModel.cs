using System.Windows.Media;
using StartupSelector.Models;
using StartupSelector.Services;

namespace StartupSelector.ViewModels
{
    /// <summary>One row in the startup list.</summary>
    public sealed class StartupItemViewModel : ObservableObject
    {
        private StartupEntry _entry;
        private bool _isChecked;
        private bool _isManaged;
        private bool _isNew;
        private string? _launchError;
        private ImageSource? _icon;

        public StartupItemViewModel(StartupEntry entry, bool isManaged, bool isNew)
        {
            _entry = entry;
            _isManaged = isManaged;
            _isNew = isNew;
        }

        public StartupEntry Entry => _entry;

        public string Id => _entry.Id;

        public string DisplayName => _entry.DisplayName;

        public string? Description =>
            string.IsNullOrEmpty(_entry.Description) ||
            string.Equals(_entry.Description, _entry.DisplayName, System.StringComparison.OrdinalIgnoreCase)
                ? null
                : _entry.Description;

        public string DisplayPath => _entry.DisplayPath;

        public string SourceLabel => _entry.SourceLabel;

        public string Details =>
            $"{_entry.SourceLocation}\n{(_entry.IsFolderEntry ? "File" : "Value")}: {_entry.Name}\nCommand: {_entry.Command}";

        /// <summary>Lazily extracted so the list can appear before every icon has been read.</summary>
        public ImageSource? Icon => _icon ??= IconExtractor.GetIcon(_entry.IsFolderEntry ? _entry.FilePath : _entry.ExecutablePath);

        public ImageSource? ShieldIcon => IconExtractor.ShieldIcon;

        /// <summary>Shows the UAC shield when changing this entry will prompt for elevation.</summary>
        public bool ShowShield => _entry.RequiresElevation && !ElevationService.IsAdministrator;

        public bool IsChecked
        {
            get => _isChecked;
            set => SetProperty(ref _isChecked, value);
        }

        public bool IsManaged
        {
            get => _isManaged;
            set
            {
                if (SetProperty(ref _isManaged, value))
                {
                    OnPropertyChanged(nameof(GroupName));
                    OnPropertyChanged(nameof(GroupOrder));
                    OnPropertyChanged(nameof(ManageButtonText));
                    OnPropertyChanged(nameof(ManageButtonToolTip));
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(CheckBoxToolTip));
                }
            }
        }

        public bool IsNew
        {
            get => _isNew;
            set => SetProperty(ref _isNew, value);
        }

        public bool IsBroken => ProblemText is not null;

        public string? ProblemText => _launchError ?? _entry.Problem;

        public string GroupName => IsManaged ? "Managed by Startup Selector" : "Controlled by Windows";

        public int GroupOrder => IsManaged ? 0 : 1;

        public string ManageButtonText => IsManaged ? "Release" : "Manage";

        public string ManageButtonToolTip => IsManaged
            ? "Give this app back to Windows (re-enables its normal startup)."
            : "Let Startup Selector decide when this app starts (disables it in Windows' startup, non-destructively)."
              + (ShowShield ? "\nRequires administrator approval." : string.Empty);

        public string CheckBoxToolTip => IsManaged
            ? "Launch this app when you click Launch Selected"
            : "Click Manage to let Startup Selector control this app";

        public string StatusText
        {
            get
            {
                if (IsManaged)
                {
                    return _entry.IsEnabledInWindows ? "Also enabled in Windows" : "Launched by Startup Selector";
                }

                return _entry.IsEnabledInWindows ? "Starts with Windows" : "Disabled in Windows";
            }
        }

        public bool IsEnabledInWindowsWhileManaged => IsManaged && _entry.IsEnabledInWindows;

        /// <summary>Swaps in a freshly scanned entry for the same id (after Refresh or a state change).</summary>
        public void UpdateEntry(StartupEntry entry)
        {
            var iconChanged = _entry.ExecutablePath != entry.ExecutablePath || _entry.FilePath != entry.FilePath;
            _entry = entry;
            _launchError = null;
            if (iconChanged)
            {
                _icon = null;
                OnPropertyChanged(nameof(Icon));
            }

            RaiseEntryChanged();
        }

        public void SetEnabledInWindows(bool enabled)
        {
            _entry.IsEnabledInWindows = enabled;
            RaiseEntryChanged();
        }

        public void SetLaunchError(string? error)
        {
            _launchError = error;
            OnPropertyChanged(nameof(ProblemText));
            OnPropertyChanged(nameof(IsBroken));
        }

        private void RaiseEntryChanged()
        {
            OnPropertyChanged(nameof(Entry));
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(DisplayPath));
            OnPropertyChanged(nameof(Details));
            OnPropertyChanged(nameof(ProblemText));
            OnPropertyChanged(nameof(IsBroken));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(IsEnabledInWindowsWhileManaged));
        }
    }
}
