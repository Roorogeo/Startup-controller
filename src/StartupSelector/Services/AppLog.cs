using System;
using System.IO;
using System.Text;

namespace StartupSelector.Services
{
    /// <summary>Minimal thread-safe file logger writing to %AppData%\StartupSelector\log.txt.</summary>
    public static class AppLog
    {
        private const long MaxLogBytes = 1024 * 1024;
        private static readonly object Gate = new();

        public static void Info(string message) => Write("INFO ", message, null);

        public static void Warn(string message) => Write("WARN ", message, null);

        public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

        private static void Write(string level, string message, Exception? exception)
        {
            var line = new StringBuilder()
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                .Append(" [").Append(level).Append("] ")
                .Append(message);
            if (exception is not null)
            {
                line.Append(" | ").Append(exception.GetType().Name).Append(": ").Append(exception.Message);
            }

            lock (Gate)
            {
                try
                {
                    AppPaths.EnsureDataDirectory();
                    RotateIfNeeded();
                    File.AppendAllText(AppPaths.LogFile, line.AppendLine().ToString(), Encoding.UTF8);
                }
                catch (Exception)
                {
                    // Logging must never crash the app (disk full, file locked, ...).
                }
            }
        }

        private static void RotateIfNeeded()
        {
            var file = new FileInfo(AppPaths.LogFile);
            if (!file.Exists || file.Length < MaxLogBytes)
            {
                return;
            }

            var previous = Path.Combine(AppPaths.DataDirectory, "log.old.txt");
            File.Copy(file.FullName, previous, overwrite: true);
            File.Delete(file.FullName);
        }
    }
}
