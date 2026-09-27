using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StartupSelector.Services
{
    /// <summary>Extracts file icons as frozen WPF images (safe to share across threads).</summary>
    public static class IconExtractor
    {
        private const uint ShgfiIcon = 0x000000100;
        private const uint ShgfiLargeIcon = 0x000000000;
        private const uint ShgfiUseFileAttributes = 0x000000010;
        private const uint FileAttributeNormal = 0x00000080;

        private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The Windows UAC shield, used for entries that need administrator rights to change.</summary>
        public static ImageSource? ShieldIcon { get; } = FromSystemIcon(System.Drawing.SystemIcons.Shield);

        public static ImageSource? GenericAppIcon { get; } = FromSystemIcon(System.Drawing.SystemIcons.Application);

        /// <summary>Icon for an executable / shortcut. Falls back to the generic application icon.</summary>
        public static ImageSource? GetIcon(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return GenericAppIcon;
            }

            if (Cache.TryGetValue(path, out var cached))
            {
                return cached;
            }

            var icon = Extract(path) ?? GenericAppIcon;
            Cache[path] = icon;
            return icon;
        }

        private static ImageSource? Extract(string path)
        {
            try
            {
                var exists = File.Exists(path) || Directory.Exists(path);
                var info = new ShFileInfo();
                var flags = ShgfiIcon | ShgfiLargeIcon;
                if (!exists)
                {
                    // Still get the icon registered for the file type (e.g. a generic .exe icon).
                    flags |= ShgfiUseFileAttributes;
                }

                var result = SHGetFileInfo(path, FileAttributeNormal, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), flags);
                if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
                {
                    return null;
                }

                try
                {
                    var source = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                    return source;
                }
                finally
                {
                    DestroyIcon(info.hIcon);
                }
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Could not extract icon for '{path}': {ex.Message}");
                return null;
            }
        }

        private static ImageSource? FromSystemIcon(System.Drawing.Icon icon)
        {
            try
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            catch (Exception)
            {
                return null;
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ShFileInfo
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref ShFileInfo psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}
