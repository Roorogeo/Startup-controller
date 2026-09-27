using System;
using System.Windows.Input;

namespace StartupSelector.ViewModels
{
    /// <summary>A dismissible message strip shown above the list (welcome, new apps, warnings...).</summary>
    public sealed class InfoBarViewModel : ObservableObject
    {
        public InfoBarViewModel(string key, string message, bool isWarning, string? actionText, ICommand? actionCommand, Action<InfoBarViewModel> dismiss)
        {
            Key = key;
            Message = message;
            IsWarning = isWarning;
            ActionText = actionText;
            ActionCommand = actionCommand;
            DismissCommand = new RelayCommand(() => dismiss(this));
        }

        /// <summary>Identifies the kind of message so it is never shown twice.</summary>
        public string Key { get; }

        public string Message { get; }

        public bool IsWarning { get; }

        public string? ActionText { get; }

        public ICommand? ActionCommand { get; }

        public bool HasAction => ActionText is not null && ActionCommand is not null;

        public ICommand DismissCommand { get; }
    }
}
