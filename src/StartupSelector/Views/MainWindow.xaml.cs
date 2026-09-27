using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using StartupSelector.ViewModels;

namespace StartupSelector.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            WindowTheming.UseDarkTitleBar(this);
            _viewModel = viewModel;
            DataContext = viewModel;
        }

        /// <summary>Set by the app right before a real shutdown so closing is not turned into "hide".</summary>
        public bool AllowClose { get; set; }

        /// <summary>Raised when the window really closed (tray mode off) and the app should exit.</summary>
        public event EventHandler? ExitRequested;

        public void ShowAndActivate()
        {
            if (!IsVisible)
            {
                Show();
            }

            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            Activate();
            // Toggling Topmost reliably brings the window in front of other apps at sign-in.
            Topmost = true;
            Topmost = false;
            Focus();
        }

        /// <summary>Any click or key press inside the window pauses the sign-in countdown.
        /// The Pause/Resume button is excluded so it can toggle the countdown itself.</summary>
        private void OnUserActivity(object sender, InputEventArgs e)
        {
            if (!IsWithin(e.OriginalSource, PauseButton))
            {
                _viewModel.PauseCountdown();
            }
        }

        private static bool IsWithin(object? source, DependencyObject target)
        {
            var current = source as DependencyObject;
            while (current is not null)
            {
                if (ReferenceEquals(current, target))
                {
                    return true;
                }

                current = current is Visual or System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(current)
                    : LogicalTreeHelper.GetParent(current);
            }

            return false;
        }

        private void OnPresetMenuClick(object sender, RoutedEventArgs e)
        {
            if (PresetMenuButton.ContextMenu is { } menu)
            {
                menu.PlacementTarget = PresetMenuButton;
                menu.Placement = PlacementMode.Bottom;
                menu.IsOpen = true;
            }
        }

        private void OnHideClick(object sender, RoutedEventArgs e) => Hide();

        private void OnClosing(object? sender, CancelEventArgs e)
        {
            if (AllowClose)
            {
                return;
            }

            // Hide / Close must not be called while the window is closing, so defer them.
            e.Cancel = true;
            if (_viewModel.MinimizeToTrayOnClose)
            {
                Dispatcher.BeginInvoke(Hide);
            }
            else
            {
                Dispatcher.BeginInvoke(() => ExitRequested?.Invoke(this, EventArgs.Empty));
            }
        }
    }
}
