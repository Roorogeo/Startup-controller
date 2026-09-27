using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using StartupSelector.Models;

namespace StartupSelector.Services
{
    /// <summary>
    /// Takes startup items away from Windows (and gives them back) through StartupApproved, and manages
    /// Startup Selector's own HKCU Run entry.
    /// </summary>
    public sealed class StartupController
    {
        public const string SelfValueName = "StartupSelector";
        public const string StartupArgument = "--startup";
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        private readonly ElevationService _elevation;

        public StartupController(ElevationService elevation)
        {
            _elevation = elevation;
        }

        /// <summary>True for Startup Selector's own entry; it is never listed, so it can never be disabled.</summary>
        public static bool IsSelf(StartupEntry entry)
        {
            if (entry.Source == StartupSource.CurrentUserRun &&
                string.Equals(entry.Name, SelfValueName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.IsNullOrEmpty(entry.ExecutablePath) || !Path.IsPathRooted(entry.ExecutablePath))
            {
                return false;
            }

            try
            {
                return string.Equals(Path.GetFullPath(entry.ExecutablePath), AppPaths.ExecutablePath, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public Task<ApplyResult> TakeControlAsync(IEnumerable<StartupEntry> entries) =>
            ApplyAsync(entries.Select(e => ApprovalChange.For(e, enable: false)).ToList());

        /// <summary>
        /// Applies HKCU changes directly and HKLM changes directly when already elevated, otherwise through a
        /// single UAC prompt for the whole batch. The final state is re-read from the registry to verify.
        /// </summary>
        public async Task<ApplyResult> ApplyAsync(IReadOnlyList<ApprovalChange> changes)
        {
            var result = new ApplyResult();
            var needsHelper = new List<ApprovalChange>();

            foreach (var change in changes)
            {
                if (change.RequiresElevation && !ElevationService.IsAdministrator)
                {
                    needsHelper.Add(change);
                    continue;
                }

                try
                {
                    StartupApprovedRegistry.Apply(change);
                    result.Succeeded.Add(change);
                    AppLog.Info($"Applied {change}");
                }
                catch (Exception ex)
                {
                    AppLog.Error($"Could not apply {change}", ex);
                    result.Failed.Add(change);
                }
            }

            if (needsHelper.Count > 0)
            {
                var ran = await _elevation.RunElevatedAsync(needsHelper);
                if (ran is null)
                {
                    result.ElevationCancelled = true;
                    result.Failed.AddRange(needsHelper);
                }
                else
                {
                    foreach (var change in needsHelper)
                    {
                        if (StartupApprovedRegistry.IsApplied(change))
                        {
                            result.Succeeded.Add(change);
                        }
                        else
                        {
                            AppLog.Error($"Elevated change did not take effect: {change}");
                            result.Failed.Add(change);
                        }
                    }
                }
            }

            return result;
        }

        public bool IsSelfRegistered()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                return key?.GetValue(SelfValueName) is string &&
                    StartupApprovedRegistry.IsEnabled(ApprovedHive.CurrentUser, ApprovedKind.Run, SelfValueName);
            }
            catch (Exception ex)
            {
                AppLog.Error("Could not read the self-registration entry.", ex);
                return false;
            }
        }

        /// <summary>
        /// Writes (or refreshes the path of) the HKCU Run entry. With <paramref name="forceEnable"/> the
        /// StartupApproved value is also reset to enabled, e.g. after the user turned it back on in Settings.
        /// </summary>
        public void RegisterSelf(bool forceEnable)
        {
            try
            {
                var command = $"\"{AppPaths.ExecutablePath}\" {StartupArgument}";
                using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
                if (!string.Equals(key.GetValue(SelfValueName) as string, command, StringComparison.OrdinalIgnoreCase))
                {
                    key.SetValue(SelfValueName, command, RegistryValueKind.String);
                    AppLog.Info($"Registered at sign-in: {command}");
                }

                if (forceEnable)
                {
                    StartupApprovedRegistry.SetEnabled(ApprovedHive.CurrentUser, ApprovedKind.Run, SelfValueName, enabled: true);
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("Could not register Startup Selector to start at sign-in.", ex);
            }
        }

        public void UnregisterSelf()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
                key?.DeleteValue(SelfValueName, throwOnMissingValue: false);
                StartupApprovedRegistry.DeleteValue(ApprovedHive.CurrentUser, ApprovedKind.Run, SelfValueName);
                AppLog.Info("Removed Startup Selector's sign-in entry.");
            }
            catch (Exception ex)
            {
                AppLog.Error("Could not remove Startup Selector's sign-in entry.", ex);
            }
        }
    }
}
