using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace StartupSelector.Services
{
    /// <summary>Reads target path and arguments from .lnk files via the IShellLink COM interface.</summary>
    public static class ShortcutResolver
    {
        private const int MaxPath = 32768;
        private const int StgmRead = 0;

        public static (string? TargetPath, string Arguments) Resolve(string shortcutPath)
        {
            object? link = null;
            try
            {
                link = new ShellLink();
                ((IPersistFile)link).Load(shortcutPath, StgmRead);
                var shellLink = (IShellLinkW)link;

                var target = new StringBuilder(MaxPath);
                shellLink.GetPath(target, target.Capacity, IntPtr.Zero, 0);

                var arguments = new StringBuilder(MaxPath);
                shellLink.GetArguments(arguments, arguments.Capacity);

                var targetPath = target.ToString();
                return (string.IsNullOrWhiteSpace(targetPath) ? null : Environment.ExpandEnvironmentVariables(targetPath),
                    arguments.ToString());
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Could not read shortcut '{shortcutPath}': {ex.Message}");
                return (null, string.Empty);
            }
            finally
            {
                if (link is not null && Marshal.IsComObject(link))
                {
                    Marshal.FinalReleaseComObject(link);
                }
            }
        }

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink
        {
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);

            void GetIDList(out IntPtr ppidl);

            void SetIDList(IntPtr pidl);

            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);

            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);

            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);

            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);

            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);

            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);

            void GetHotkey(out short pwHotkey);

            void SetHotkey(short wHotkey);

            void GetShowCmd(out int piShowCmd);

            void SetShowCmd(int iShowCmd);

            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);

            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);

            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);

            void Resolve(IntPtr hwnd, uint fFlags);

            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }
    }
}
