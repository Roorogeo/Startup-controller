using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using StartupSelector.Models;

namespace StartupSelector.Services
{
    /// <summary>Finds startup apps in the standard Run keys and Startup folders.</summary>
    public sealed class StartupScanner
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunKeyPath32 = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>Must be called on an STA thread (shortcut resolution uses COM).</summary>
        public List<StartupEntry> Scan()
        {
            var entries = new List<StartupEntry>();
            ScanRunKey(entries, RegistryHive.CurrentUser, RunKeyPath, StartupSource.CurrentUserRun);
            ScanRunKey(entries, RegistryHive.LocalMachine, RunKeyPath, StartupSource.LocalMachineRun);
            if (Environment.Is64BitOperatingSystem)
            {
                ScanRunKey(entries, RegistryHive.LocalMachine, RunKeyPath32, StartupSource.LocalMachineRun32);
            }

            ScanFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.Startup), StartupSource.UserStartupFolder);
            ScanFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), StartupSource.CommonStartupFolder);

            return entries
                .Where(e => !StartupController.IsSelf(e))
                .OrderBy(e => e.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static void ScanRunKey(List<StartupEntry> entries, RegistryHive hive, string path, StartupSource source)
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
                using var key = root.OpenSubKey(path, writable: false);
                if (key is null)
                {
                    return;
                }

                foreach (var valueName in key.GetValueNames())
                {
                    // The unnamed "(Default)" value is not a startup item.
                    if (string.IsNullOrEmpty(valueName) || key.GetValue(valueName) is not string command ||
                        string.IsNullOrWhiteSpace(command))
                    {
                        continue;
                    }

                    var (exe, args) = CommandLineParser.Split(command);
                    var entry = new StartupEntry
                    {
                        Id = StartupEntry.MakeId(source, valueName),
                        Source = source,
                        Name = valueName,
                        DisplayName = valueName,
                        Description = ReadDescription(exe),
                        Command = command,
                        ExecutablePath = string.IsNullOrWhiteSpace(exe) ? null : exe,
                        Arguments = args,
                    };
                    entry.IsEnabledInWindows = StartupApprovedRegistry.IsEnabled(entry.ApprovedHive, entry.ApprovedKind, valueName);
                    entry.Problem = CheckExecutable(entry.ExecutablePath);
                    entries.Add(entry);
                }
            }
            catch (Exception ex)
            {
                AppLog.Error($"Could not read {hive}\\{path}.", ex);
            }
        }

        private static void ScanFolder(List<StartupEntry> entries, string folder, StartupSource source)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return;
            }

            try
            {
                foreach (var file in new DirectoryInfo(folder).EnumerateFiles())
                {
                    if (file.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) ||
                        file.Attributes.HasFlag(FileAttributes.Hidden))
                    {
                        continue;
                    }

                    string? target = file.FullName;
                    var args = string.Empty;
                    string? problem = null;
                    if (file.Extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                    {
                        (target, args) = ShortcutResolver.Resolve(file.FullName);
                        // A null target is normal for MSI-advertised and Store app shortcuts: the shell
                        // can still launch them, so only a concrete missing path counts as broken.
                        problem = target is null ? null : CheckExecutable(target);
                    }

                    var entry = new StartupEntry
                    {
                        Id = StartupEntry.MakeId(source, file.Name),
                        Source = source,
                        Name = file.Name,
                        DisplayName = Path.GetFileNameWithoutExtension(file.Name),
                        Description = ReadDescription(target),
                        Command = file.FullName,
                        ExecutablePath = target,
                        Arguments = args,
                        FilePath = file.FullName,
                        Problem = problem,
                    };
                    entry.IsEnabledInWindows = StartupApprovedRegistry.IsEnabled(entry.ApprovedHive, entry.ApprovedKind, file.Name);
                    entries.Add(entry);
                }
            }
            catch (Exception ex)
            {
                AppLog.Error($"Could not read startup folder '{folder}'.", ex);
            }
        }

        private static string? CheckExecutable(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "The command line could not be parsed.";
            }

            if (!Path.IsPathRooted(path))
            {
                return $"'{path}' was not found on this PC.";
            }

            return File.Exists(path) || Directory.Exists(path) ? null : $"File not found: {path}";
        }

        private static string? ReadDescription(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path) ||
                !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                var description = FileVersionInfo.GetVersionInfo(path).FileDescription?.Trim();
                return string.IsNullOrEmpty(description) ? null : description;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
