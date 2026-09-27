using System.Windows;
using StartupSelector.ViewModels;

namespace StartupSelector.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow(SettingsViewModel viewModel)
        {
            InitializeComponent();
            WindowTheming.UseDarkTitleBar(this);
            DataContext = viewModel;
            viewModel.CloseRequested += (_, saved) =>
            {
                if (IsLoaded && IsVisible)
                {
                    DialogResult = saved;
                }
            };
        }
    }
}
