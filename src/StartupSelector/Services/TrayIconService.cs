using System;
using System.Collections.Generic;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace StartupSelector.Services
{
    /// <summary>System tray icon with a dark context menu: Open, Launch Preset ▸, Settings, Exit.</summary>
    public sealed class TrayIconService : IDisposable
    {
        private readonly WinForms.NotifyIcon _notifyIcon;
        private readonly WinForms.ContextMenuStrip _menu;
        private readonly WinForms.ToolStripMenuItem _presetsItem;
        private readonly Func<IReadOnlyList<string>> _getPresetNames;
        private readonly Drawing.Icon _icon;

        public TrayIconService(Func<IReadOnlyList<string>> getPresetNames)
        {
            _getPresetNames = getPresetNames;
            _icon = LoadIcon();

            _menu = new WinForms.ContextMenuStrip
            {
                Renderer = new DarkMenuRenderer(),
                ShowImageMargin = false,
                Font = new Drawing.Font("Segoe UI", 9.5f),
                BackColor = DarkMenuRenderer.Background,
                ForeColor = DarkMenuRenderer.Text,
                Padding = new WinForms.Padding(2, 4, 2, 4),
            };

            var openItem = CreateItem("Open Startup Selector", () => OpenRequested?.Invoke(this, EventArgs.Empty));
            openItem.Font = new Drawing.Font(_menu.Font, Drawing.FontStyle.Bold);
            _presetsItem = CreateItem("Launch Preset", null);
            _presetsItem.DropDown = new WinForms.ToolStripDropDownMenu
            {
                Renderer = _menu.Renderer,
                ShowImageMargin = false,
                BackColor = DarkMenuRenderer.Background,
                ForeColor = DarkMenuRenderer.Text,
                Font = _menu.Font,
            };

            _menu.Items.Add(openItem);
            _menu.Items.Add(_presetsItem);
            _menu.Items.Add(CreateItem("Settings", () => SettingsRequested?.Invoke(this, EventArgs.Empty)));
            _menu.Items.Add(new WinForms.ToolStripSeparator());
            _menu.Items.Add(CreateItem("Exit", () => ExitRequested?.Invoke(this, EventArgs.Empty)));
            _menu.Opening += (_, _) => RebuildPresetMenu();

            _notifyIcon = new WinForms.NotifyIcon
            {
                Icon = _icon,
                Text = "Startup Selector",
                ContextMenuStrip = _menu,
                Visible = true,
            };
            _notifyIcon.MouseClick += (_, e) =>
            {
                if (e.Button == WinForms.MouseButtons.Left)
                {
                    OpenRequested?.Invoke(this, EventArgs.Empty);
                }
            };
            _notifyIcon.BalloonTipClicked += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? OpenRequested;

        public event EventHandler<string>? LaunchPresetRequested;

        public event EventHandler? SettingsRequested;

        public event EventHandler? ExitRequested;

        public void ShowNotification(string title, string text, bool isWarning)
        {
            _notifyIcon.ShowBalloonTip(
                4000,
                title,
                text,
                isWarning ? WinForms.ToolTipIcon.Warning : WinForms.ToolTipIcon.Info);
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _menu.Dispose();
            _icon.Dispose();
        }

        private void RebuildPresetMenu()
        {
            _presetsItem.DropDownItems.Clear();
            var names = _getPresetNames();
            if (names.Count == 0)
            {
                _presetsItem.DropDownItems.Add(new WinForms.ToolStripMenuItem("(no presets)") { Enabled = false });
                return;
            }

            foreach (var name in names)
            {
                var presetName = name;
                _presetsItem.DropDownItems.Add(CreateItem(presetName, () => LaunchPresetRequested?.Invoke(this, presetName)));
            }
        }

        private static WinForms.ToolStripMenuItem CreateItem(string text, Action? onClick)
        {
            var item = new WinForms.ToolStripMenuItem(text)
            {
                ForeColor = DarkMenuRenderer.Text,
                Padding = new WinForms.Padding(6, 3, 6, 3),
            };
            if (onClick is not null)
            {
                item.Click += (_, _) => onClick();
            }

            return item;
        }

        private static Drawing.Icon LoadIcon()
        {
            try
            {
                var resource = System.Windows.Application.GetResourceStream(
                    new Uri("pack://application:,,,/Resources/app.ico", UriKind.Absolute));
                if (resource is not null)
                {
                    using var stream = resource.Stream;
                    return new Drawing.Icon(stream, WinForms.SystemInformation.SmallIconSize);
                }
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Could not load tray icon: {ex.Message}");
            }

            return (Drawing.Icon)Drawing.SystemIcons.Application.Clone();
        }

        /// <summary>Paints the WinForms tray menu with the same dark palette as the WPF windows.</summary>
        private sealed class DarkMenuRenderer : WinForms.ToolStripProfessionalRenderer
        {
            public static readonly Drawing.Color Background = Drawing.Color.FromArgb(0x2D, 0x2D, 0x2D);
            public static readonly Drawing.Color Hover = Drawing.Color.FromArgb(0x3E, 0x3E, 0x42);
            public static readonly Drawing.Color Border = Drawing.Color.FromArgb(0x45, 0x45, 0x45);
            public static readonly Drawing.Color Text = Drawing.Color.FromArgb(0xE0, 0xE0, 0xE0);
            public static readonly Drawing.Color DisabledText = Drawing.Color.FromArgb(0x80, 0x80, 0x80);

            public DarkMenuRenderer()
                : base(new DarkColorTable())
            {
                RoundedEdges = false;
            }

            protected override void OnRenderItemText(WinForms.ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Enabled ? Text : DisabledText;
                base.OnRenderItemText(e);
            }

            protected override void OnRenderArrow(WinForms.ToolStripArrowRenderEventArgs e)
            {
                e.ArrowColor = Text;
                base.OnRenderArrow(e);
            }

            protected override void OnRenderMenuItemBackground(WinForms.ToolStripItemRenderEventArgs e)
            {
                var bounds = new Drawing.Rectangle(2, 0, e.Item.Width - 4, e.Item.Height);
                using var brush = new Drawing.SolidBrush(e.Item.Selected && e.Item.Enabled ? Hover : Background);
                e.Graphics.FillRectangle(brush, bounds);
            }

            protected override void OnRenderSeparator(WinForms.ToolStripSeparatorRenderEventArgs e)
            {
                var y = e.Item.Height / 2;
                using var pen = new Drawing.Pen(Border);
                e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
            }
        }

        private sealed class DarkColorTable : WinForms.ProfessionalColorTable
        {
            public override Drawing.Color ToolStripDropDownBackground => DarkMenuRenderer.Background;

            public override Drawing.Color MenuBorder => DarkMenuRenderer.Border;

            public override Drawing.Color MenuItemBorder => DarkMenuRenderer.Hover;

            public override Drawing.Color MenuItemSelected => DarkMenuRenderer.Hover;

            public override Drawing.Color MenuItemSelectedGradientBegin => DarkMenuRenderer.Hover;

            public override Drawing.Color MenuItemSelectedGradientEnd => DarkMenuRenderer.Hover;

            public override Drawing.Color MenuItemPressedGradientBegin => DarkMenuRenderer.Hover;

            public override Drawing.Color MenuItemPressedGradientEnd => DarkMenuRenderer.Hover;

            public override Drawing.Color ImageMarginGradientBegin => DarkMenuRenderer.Background;

            public override Drawing.Color ImageMarginGradientMiddle => DarkMenuRenderer.Background;

            public override Drawing.Color ImageMarginGradientEnd => DarkMenuRenderer.Background;

            public override Drawing.Color SeparatorDark => DarkMenuRenderer.Border;

            public override Drawing.Color SeparatorLight => DarkMenuRenderer.Border;
        }
    }
}
