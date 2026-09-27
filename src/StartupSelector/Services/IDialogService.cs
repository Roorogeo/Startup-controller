using StartupSelector.ViewModels;

namespace StartupSelector.Services
{
    /// <summary>Lets view models show dark-themed dialogs without referencing any view.</summary>
    public interface IDialogService
    {
        void ShowMessage(string title, string message, bool isWarning = false);

        bool Confirm(string title, string message, string confirmText = "OK", string cancelText = "Cancel", bool isDestructive = false);

        /// <summary>Asks for a line of text. Returns null when cancelled. The validator returns an error message or null.</summary>
        string? Prompt(string title, string message, string initialText, System.Func<string, string?>? validate = null);

        /// <summary>Shows the settings dialog. Returns true when the user saved.</summary>
        bool ShowSettings(SettingsViewModel settings);
    }
}
