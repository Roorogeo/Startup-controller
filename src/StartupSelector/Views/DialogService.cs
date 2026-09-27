using System;
using System.Linq;
using System.Windows;
using StartupSelector.Services;
using StartupSelector.ViewModels;

namespace StartupSelector.Views
{
    public sealed class DialogService : IDialogService
    {
        public void ShowMessage(string title, string message, bool isWarning = false)
        {
            var dialog = new DialogWindow(title, message, "OK", null, isWarning: isWarning);
            ShowModal(dialog);
        }

        public bool Confirm(string title, string message, string confirmText = "OK", string cancelText = "Cancel", bool isDestructive = false)
        {
            var dialog = new DialogWindow(title, message, confirmText, cancelText, isDestructive);
            return ShowModal(dialog);
        }

        public string? Prompt(string title, string message, string initialText, Func<string, string?>? validate = null)
        {
            var dialog = new DialogWindow(title, message, "Save", "Cancel", inputText: initialText, validate: validate);
            return ShowModal(dialog) ? dialog.InputText : null;
        }

        public bool ShowSettings(SettingsViewModel settings)
        {
            var window = new SettingsWindow(settings);
            return ShowModal(window);
        }

        private static bool ShowModal(Window dialog)
        {
            var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w != dialog)
                ?? (Application.Current?.MainWindow is { IsVisible: true } main ? main : null);
            if (owner is not null && owner != dialog)
            {
                dialog.Owner = owner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                dialog.ShowInTaskbar = true;
                dialog.Topmost = true;
            }

            return dialog.ShowDialog() == true;
        }
    }
}
