using System;
using System.Windows;
using System.Windows.Controls;

namespace StartupSelector.Views
{
    /// <summary>Dark replacement for MessageBox: message, confirmation, or single-line text prompt.</summary>
    public partial class DialogWindow : Window
    {
        private readonly Func<string, string?>? _validate;

        public DialogWindow(
            string title,
            string message,
            string okText,
            string? cancelText,
            bool isDestructive = false,
            bool isWarning = false,
            string? inputText = null,
            Func<string, string?>? validate = null)
        {
            InitializeComponent();
            WindowTheming.UseDarkTitleBar(this);

            _validate = validate;
            Title = title;
            MessageText.Text = message;
            OkButton.Content = okText;
            if (isDestructive)
            {
                OkButton.Style = (Style)FindResource("DangerButton");
            }

            if (cancelText is null)
            {
                CancelButton.Visibility = Visibility.Collapsed;
                OkButton.IsCancel = true;
            }
            else
            {
                CancelButton.Content = cancelText;
            }

            WarningIcon.Visibility = isWarning ? Visibility.Visible : Visibility.Collapsed;

            if (inputText is not null)
            {
                InputBox.Text = inputText;
                InputBox.Visibility = Visibility.Visible;
                Loaded += (_, _) =>
                {
                    InputBox.Focus();
                    InputBox.SelectAll();
                };
            }
        }

        public string InputText => InputBox.Text.Trim();

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (InputBox.Visibility == Visibility.Visible && _validate?.Invoke(InputBox.Text) is { } error)
            {
                ErrorText.Text = error;
                ErrorText.Visibility = Visibility.Visible;
                InputBox.Focus();
                return;
            }

            DialogResult = true;
        }

        private void OnInputChanged(object sender, TextChangedEventArgs e) => ErrorText.Visibility = Visibility.Collapsed;
    }
}
