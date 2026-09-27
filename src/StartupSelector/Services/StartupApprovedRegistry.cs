using System;
using Microsoft.Win32;
using StartupSelector.Models;

namespace StartupSelector.Services
{
    /// <summary>
    /// Reads and writes the StartupApproved values Task Manager uses to enable / disable startup items.
    /// Each value is a 12-byte REG_BINARY: an odd first byte (0x03) means disabled and bytes 4-11 hold the
    /// FILETIME it was disabled; an even first byte (0x02) or a missing value means enabled.
    /// The original Run values and shortcut files are never touched.
    /// </summary>
    public static class StartupApprovedRegistry
    {
        public const string BaseKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

        public static string SubKeyPath(ApprovedKind kind) => $@"{BaseKeyPath}\{kind}";

        public static RegistryKey OpenBaseKey(ApprovedHive hive) => RegistryKey.OpenBaseKey(
            hive == ApprovedHive.CurrentUser ? RegistryHive.CurrentUser : RegistryHive.LocalMachine,
            RegistryView.Registry64);

        public static bool IsEnabled(ApprovedHive hive, ApprovedKind kind, string valueName)
        {
            try
            {
                using var root = OpenBaseKey(hive);
                using var key = root.OpenSubKey(SubKeyPath(kind), writable: false);
                if (key?.GetValue(valueName) is byte[] data && data.Length > 0)
                {
                    return (data[0] & 0x01) == 0;
                }
            }
            catch (Exception ex)
            {
                AppLog.Error($"Could not read StartupApproved state for {hive}\\{kind}\\{valueName}.", ex);
            }

            return true;
        }

        /// <summary>Writes the value. Throws on failure (e.g. UnauthorizedAccessException for HKLM without admin).</summary>
        public static void SetEnabled(ApprovedHive hive, ApprovedKind kind, string valueName, bool enabled)
        {
            using var root = OpenBaseKey(hive);
            using var key = root.CreateSubKey(SubKeyPath(kind), writable: true)
                ?? throw new InvalidOperationException($"Could not open {hive}\\{SubKeyPath(kind)}.");
            key.SetValue(valueName, enabled ? EnabledValue() : DisabledValue(DateTime.UtcNow), RegistryValueKind.Binary);
        }

        public static void Apply(ApprovalChange change) =>
            SetEnabled(change.Hive, change.Kind, change.ValueName, change.Enable);

        public static bool IsApplied(ApprovalChange change) =>
            IsEnabled(change.Hive, change.Kind, change.ValueName) == change.Enable;

        public static void DeleteValue(ApprovedHive hive, ApprovedKind kind, string valueName)
        {
            using var root = OpenBaseKey(hive);
            using var key = root.OpenSubKey(SubKeyPath(kind), writable: true);
            key?.DeleteValue(valueName, throwOnMissingValue: false);
        }

        private static byte[] EnabledValue() => new byte[] { 0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        private static byte[] DisabledValue(DateTime utc)
        {
            var data = new byte[12];
            data[0] = 0x03;
            BitConverter.GetBytes(utc.ToFileTimeUtc()).CopyTo(data, 4);
            return data;
        }
    }
}
