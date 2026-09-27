using System.IO;

namespace StartupSelector.Models
{
    /// <summary>One startup item found in a Run key or a Startup folder.</summary>
    public sealed class StartupEntry
    {
        /// <summary>Stable identifier: "{Source}|{Name}". Used by presets and the managed list.</summary>
        public required string Id { get; init; }

        public required StartupSource Source { get; init; }

        /// <summary>Registry value name, or file name (with extension) for Startup folder items.
        /// This is also the value name used under StartupApproved.</summary>
        public required string Name { get; init; }

        public required string DisplayName { get; init; }

        /// <summary>File description from the executable's version resource, if any.</summary>
        public string? Description { get; init; }

        /// <summary>Raw registry command line, or the full path of the Startup folder file.</summary>
        public required string Command { get; init; }

        /// <summary>Resolved executable (may be null when it cannot be determined, e.g. MSI advertised shortcuts).</summary>
        public string? ExecutablePath { get; init; }

        public string Arguments { get; init; } = string.Empty;

        /// <summary>Full path of the file inside a Startup folder (null for registry entries).</summary>
        public string? FilePath { get; init; }

        /// <summary>True when Windows itself will start this item at sign-in (StartupApproved says enabled).</summary>
        public bool IsEnabledInWindows { get; set; }

        /// <summary>Description of a problem (missing executable, unreadable shortcut...), or null if healthy.</summary>
        public string? Problem { get; set; }

        public bool IsFolderEntry => Source is StartupSource.UserStartupFolder or StartupSource.CommonStartupFolder;

        /// <summary>Changing the StartupApproved state of HKLM / All Users items requires administrator rights.</summary>
        public bool RequiresElevation => Source is StartupSource.LocalMachineRun
            or StartupSource.LocalMachineRun32
            or StartupSource.CommonStartupFolder;

        public ApprovedHive ApprovedHive => Source is StartupSource.CurrentUserRun or StartupSource.UserStartupFolder
            ? ApprovedHive.CurrentUser
            : ApprovedHive.LocalMachine;

        public ApprovedKind ApprovedKind => Source switch
        {
            StartupSource.LocalMachineRun32 => ApprovedKind.Run32,
            StartupSource.UserStartupFolder or StartupSource.CommonStartupFolder => ApprovedKind.StartupFolder,
            _ => ApprovedKind.Run,
        };

        /// <summary>Path shown under the name in the list.</summary>
        public string DisplayPath => ExecutablePath ?? FilePath ?? Command;

        /// <summary>Folder used as working directory when launching a registry entry.</summary>
        public string? WorkingDirectory
        {
            get
            {
                if (string.IsNullOrEmpty(ExecutablePath) || !Path.IsPathRooted(ExecutablePath))
                {
                    return null;
                }

                return Path.GetDirectoryName(ExecutablePath);
            }
        }

        public string SourceLabel => Source switch
        {
            StartupSource.CurrentUserRun => "Registry (current user)",
            StartupSource.LocalMachineRun => "Registry (all users)",
            StartupSource.LocalMachineRun32 => "Registry (all users, 32-bit)",
            StartupSource.UserStartupFolder => "Startup folder",
            StartupSource.CommonStartupFolder => "Startup folder (all users)",
            _ => Source.ToString(),
        };

        public string SourceLocation => Source switch
        {
            StartupSource.CurrentUserRun => @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run",
            StartupSource.LocalMachineRun => @"HKLM\Software\Microsoft\Windows\CurrentVersion\Run",
            StartupSource.LocalMachineRun32 => @"HKLM\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run",
            StartupSource.UserStartupFolder => FilePath is null ? "shell:startup" : Path.GetDirectoryName(FilePath) ?? "shell:startup",
            StartupSource.CommonStartupFolder => FilePath is null ? "shell:common startup" : Path.GetDirectoryName(FilePath) ?? "shell:common startup",
            _ => string.Empty,
        };

        public static string MakeId(StartupSource source, string name) => $"{source}|{name}";
    }
}
