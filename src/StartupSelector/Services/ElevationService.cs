using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StartupSelector.Models;

namespace StartupSelector.Services
{
    /// <summary>
    /// Applies HKLM StartupApproved changes by re-launching Startup Selector elevated with
    /// <c>--apply-elevated &lt;request.json&gt;</c>. Only that one short-lived helper process runs as admin;
    /// it shows no UI, applies the whitelisted changes and exits.
    /// </summary>
    public sealed class ElevationService
    {
        public const string ApplyElevatedArgument = "--apply-elevated";
        private const int ErrorCancelled = 1223;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Converters = { new JsonStringEnumConverter() },
        };

        public static bool IsAdministrator { get; } = CheckAdministrator();

        /// <summary>Returns null if the user cancelled the UAC prompt, otherwise whether the helper ran.</summary>
        public async Task<bool?> RunElevatedAsync(IReadOnlyList<ApprovalChange> changes)
        {
            var requestPath = Path.Combine(Path.GetTempPath(), $"StartupSelector-{Guid.NewGuid():N}.json");
            try
            {
                await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(changes, JsonOptions));
                var startInfo = new ProcessStartInfo
                {
                    FileName = AppPaths.ExecutablePath,
                    Arguments = $"{ApplyElevatedArgument} \"{requestPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden,
                };

                using var process = Process.Start(startInfo);
                if (process is null)
                {
                    AppLog.Error("The elevated helper process could not be started.");
                    return false;
                }

                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                {
                    AppLog.Warn($"Elevated helper exited with code {process.ExitCode}.");
                }

                return true;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
            {
                AppLog.Info("User cancelled the UAC prompt.");
                return null;
            }
            catch (Exception ex)
            {
                AppLog.Error("Could not run the elevated helper.", ex);
                return false;
            }
            finally
            {
                TryDelete(requestPath);
            }
        }

        /// <summary>Entry point of the elevated helper instance. Returns the process exit code.</summary>
        public static int RunHelper(string requestPath)
        {
            try
            {
                if (!IsAdministrator)
                {
                    AppLog.Error("Elevated helper started without administrator rights.");
                    return 3;
                }

                var changes = JsonSerializer.Deserialize<List<ApprovalChange>>(File.ReadAllText(requestPath), JsonOptions)
                    ?? new List<ApprovalChange>();

                var failures = 0;
                foreach (var change in changes)
                {
                    // Whitelist: only StartupApproved values under HKLM, with a plain value name.
                    if (change.Hive != ApprovedHive.LocalMachine || !Enum.IsDefined(change.Kind) ||
                        string.IsNullOrWhiteSpace(change.ValueName) || change.ValueName.Length > 260)
                    {
                        AppLog.Warn($"Elevated helper rejected change: {change}");
                        failures++;
                        continue;
                    }

                    try
                    {
                        StartupApprovedRegistry.Apply(change);
                        AppLog.Info($"Elevated helper applied: {change}");
                    }
                    catch (Exception ex)
                    {
                        AppLog.Error($"Elevated helper failed: {change}", ex);
                        failures++;
                    }
                }

                return failures == 0 ? 0 : 1;
            }
            catch (Exception ex)
            {
                AppLog.Error("Elevated helper could not read its request.", ex);
                return 2;
            }
        }

        private static bool CheckAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
                // Temp file cleanup is best effort.
            }
        }
    }
}
