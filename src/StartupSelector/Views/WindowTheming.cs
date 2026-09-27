using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace StartupSelector.Views
{
    /// <summary>
    /// Gives WPF windows a dark title bar via DwmSetWindowAttribute(DWMWA_USE_IMMERSIVE_DARK_MODE), and on
    /// Windows 11 paints the caption pure black (DWMWA_CAPTION_COLOR) to match the Noir background.
    /// </summary>
    public static class WindowTheming
    {
        // 20 on Windows 10 20H1+ and Windows 11; 19 on earlier Windows 10 builds.
        private const int DwmwaUseImmersiveDarkMode = 20;
        private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
        private const int DwmwaCaptionColor = 35;
        private const int CaptionColorBlack = 0x00000000; // COLORREF 0x00BBGGRR

        public static void UseDarkTitleBar(Window window)
        {
            window.SourceInitialized += (_, _) => Apply(new WindowInteropHelper(window).Handle);
        }

        private static void Apply(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            {
                return;
            }

            var enabled = 1;
            if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeBefore20H1, ref enabled, sizeof(int));
            }

            // Windows 11 only; earlier versions reject the attribute and keep the dark caption above.
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            {
                var caption = CaptionColorBlack;
                DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref caption, sizeof(int));
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
