using System.Collections.Generic;

namespace StartupSelector.Models
{
    /// <summary>Everything persisted to %AppData%\StartupSelector\settings.json.</summary>
    public sealed class AppSettings
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        /// <summary>False until the first run has finished; suppresses "New" badges and the login countdown on first launch.</summary>
        public bool FirstRunCompleted { get; set; }

        /// <summary>Seconds the login countdown runs before the default preset launches. 0 disables auto-launch.</summary>
        public int CountdownSeconds { get; set; } = 10;

        /// <summary>Delay between two launches, to reduce sign-in slowdown.</summary>
        public int LaunchDelayMilliseconds { get; set; } = 1000;

        public bool StartWithWindows { get; set; } = true;

        public bool MinimizeToTrayOnClose { get; set; } = true;

        /// <summary>The "still running in the tray" balloon is shown only once.</summary>
        public bool TrayHintShown { get; set; }

        public List<Preset> Presets { get; set; } = new();

        public string? DefaultPresetName { get; set; }

        /// <summary>The checked ids the last time apps were launched.</summary>
        public List<string> LastUsedSelection { get; set; } = new();

        public List<ManagedEntry> ManagedEntries { get; set; } = new();

        /// <summary>Every entry id the user has already seen. Anything else is flagged as "New".</summary>
        public List<string> KnownEntryIds { get; set; } = new();

        public static AppSettings CreateDefault()
        {
            var settings = new AppSettings();
            settings.Presets.Add(new Preset { Name = "Everything", IncludeAllManaged = true });
            settings.Presets.Add(new Preset { Name = "Minimal" });
            settings.DefaultPresetName = "Everything";
            return settings;
        }
    }
}
