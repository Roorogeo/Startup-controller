using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using StartupSelector.Models;

namespace StartupSelector.Services
{
    /// <summary>Starts startup items one after another with a configurable stagger delay.</summary>
    public sealed class AppLauncher
    {
        public async Task<IReadOnlyList<LaunchResult>> LaunchAsync(
            IReadOnlyList<StartupEntry> entries,
            int delayMilliseconds,
            CancellationToken cancellationToken = default)
        {
            var results = new List<LaunchResult>(entries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                if (i > 0 && delayMilliseconds > 0)
                {
                    await Task.Delay(delayMilliseconds, cancellationToken);
                }

                var entry = entries[i];
                results.Add(await Task.Run(() => LaunchOne(entry), cancellationToken));
            }

            return results;
        }

        private static LaunchResult LaunchOne(StartupEntry entry)
        {
            try
            {
                ProcessStartInfo startInfo;
                if (entry.IsFolderEntry)
                {
                    // Let the shell open the shortcut itself so its arguments, working folder and
                    // window state are honoured exactly as Windows would at sign-in.
                    if (entry.FilePath is null || !File.Exists(entry.FilePath))
                    {
                        return Fail(entry, $"Shortcut not found: {entry.FilePath}");
                    }

                    if (entry.ExecutablePath is not null && Path.IsPathRooted(entry.ExecutablePath) &&
                        !File.Exists(entry.ExecutablePath) && !Directory.Exists(entry.ExecutablePath))
                    {
                        return Fail(entry, $"Shortcut target not found: {entry.ExecutablePath}");
                    }

                    startInfo = new ProcessStartInfo(entry.FilePath) { UseShellExecute = true };
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(entry.ExecutablePath))
                    {
                        return Fail(entry, $"Could not determine the program to run from '{entry.Command}'.");
                    }

                    if (Path.IsPathRooted(entry.ExecutablePath) && !File.Exists(entry.ExecutablePath))
                    {
                        return Fail(entry, $"Executable not found: {entry.ExecutablePath}");
                    }

                    startInfo = new ProcessStartInfo(entry.ExecutablePath, entry.Arguments) { UseShellExecute = true };
                    if (entry.WorkingDirectory is { } workingDirectory && Directory.Exists(workingDirectory))
                    {
                        startInfo.WorkingDirectory = workingDirectory;
                    }
                }

                using var process = Process.Start(startInfo);
                AppLog.Info($"Launched '{entry.DisplayName}' ({entry.DisplayPath} {entry.Arguments})".TrimEnd());
                return new LaunchResult(entry, true, null);
            }
            catch (Win32Exception ex)
            {
                return Fail(entry, $"Windows could not start it: {ex.Message}");
            }
            catch (Exception ex)
            {
                return Fail(entry, ex.Message);
            }
        }

        private static LaunchResult Fail(StartupEntry entry, string error)
        {
            AppLog.Error($"Failed to launch '{entry.DisplayName}': {error}");
            return new LaunchResult(entry, false, error);
        }
    }
}
