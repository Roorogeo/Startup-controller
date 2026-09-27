using System;
using System.IO;

namespace StartupSelector.Services
{
    public static class AppPaths
    {
        public static string DataDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StartupSelector");

        public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");

        public static string LogFile => Path.Combine(DataDirectory, "log.txt");

        /// <summary>Full path of the running executable (the single-file exe when published).</summary>
        public static string ExecutablePath { get; } =
            Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "StartupSelector.exe");

        public static void EnsureDataDirectory() => Directory.CreateDirectory(DataDirectory);
    }
}
