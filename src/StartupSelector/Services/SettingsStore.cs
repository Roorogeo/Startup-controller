using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using StartupSelector.Models;

namespace StartupSelector.Services
{
    /// <summary>Loads and saves <see cref="AppSettings"/> as JSON, recovering from missing or corrupted files.</summary>
    public sealed class SettingsStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = { new JsonStringEnumConverter() },
        };

        private readonly string _path;

        public SettingsStore()
            : this(AppPaths.SettingsFile)
        {
        }

        public SettingsStore(string path)
        {
            _path = path;
        }

        /// <summary>Set when the last <see cref="Load"/> found a corrupted file; holds where it was moved to.</summary>
        public string? CorruptBackupPath { get; private set; }

        /// <summary>True when the last <see cref="Load"/> recovered from the automatic last-good copy.</summary>
        public bool RestoredFromLastGood { get; private set; }

        /// <summary>Copy of the previous successfully written settings, used to recover from corruption.</summary>
        private string LastGoodPath => Path.Combine(Path.GetDirectoryName(_path)!, "settings.bak.json");

        public AppSettings Load()
        {
            CorruptBackupPath = null;
            RestoredFromLastGood = false;
            if (!File.Exists(_path))
            {
                AppLog.Info("No settings file found; using defaults.");
                return AppSettings.CreateDefault();
            }

            try
            {
                return Read(_path);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException)
            {
                AppLog.Error("Settings file is corrupted.", ex);
                CorruptBackupPath = BackUpCorruptFile();
                return LoadLastGoodOrDefault();
            }
            catch (IOException ex)
            {
                AppLog.Error("Settings file could not be read; using defaults for this session.", ex);
                return AppSettings.CreateDefault();
            }
            catch (UnauthorizedAccessException ex)
            {
                AppLog.Error("Settings file could not be read; using defaults for this session.", ex);
                return AppSettings.CreateDefault();
            }
        }

        public void Save(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                var json = JsonSerializer.Serialize(settings, JsonOptions);
                var temp = _path + ".tmp";
                File.WriteAllText(temp, json);
                File.Move(temp, _path, overwrite: true);
                File.Copy(_path, LastGoodPath, overwrite: true);
            }
            catch (Exception ex)
            {
                AppLog.Error("Could not save settings.", ex);
            }
        }

        private static AppSettings Read(string path)
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions)
                ?? throw new JsonException("Settings file is empty.");
            Normalize(settings);
            return settings;
        }

        /// <summary>
        /// Losing the managed-app list would leave apps disabled in Windows with nothing launching them,
        /// so the last successfully written copy is tried before falling back to defaults.
        /// </summary>
        private AppSettings LoadLastGoodOrDefault()
        {
            if (File.Exists(LastGoodPath))
            {
                try
                {
                    var settings = Read(LastGoodPath);
                    RestoredFromLastGood = true;
                    AppLog.Warn("Restored settings from the last good copy.");
                    return settings;
                }
                catch (Exception ex)
                {
                    AppLog.Error("The last good settings copy is unusable too.", ex);
                }
            }

            AppLog.Warn("Falling back to default settings.");
            return AppSettings.CreateDefault();
        }

        private string? BackUpCorruptFile()
        {
            try
            {
                var backup = Path.Combine(
                    Path.GetDirectoryName(_path)!,
                    $"settings.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                File.Move(_path, backup, overwrite: true);
                AppLog.Warn($"Corrupted settings backed up to {backup}");
                return backup;
            }
            catch (Exception ex)
            {
                AppLog.Error("Could not back up the corrupted settings file.", ex);
                return null;
            }
        }

        /// <summary>Repairs values a hand-edited or older file may contain.</summary>
        private static void Normalize(AppSettings s)
        {
            s.Presets ??= new List<Preset>();
            s.LastUsedSelection ??= new List<string>();
            s.ManagedEntries ??= new List<ManagedEntry>();
            s.KnownEntryIds ??= new List<string>();

            s.CountdownSeconds = Math.Clamp(s.CountdownSeconds, 0, 300);
            s.LaunchDelayMilliseconds = Math.Clamp(s.LaunchDelayMilliseconds, 0, 30000);

            foreach (var preset in s.Presets)
            {
                preset.Name = (preset.Name ?? string.Empty).Trim();
                preset.CheckedIds ??= new List<string>();
            }

            // Drop unnamed and duplicate presets (case-insensitive).
            s.Presets = s.Presets
                .Where(p => p.Name.Length > 0)
                .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            s.ManagedEntries = s.ManagedEntries
                .Where(m => !string.IsNullOrWhiteSpace(m.Id))
                .GroupBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            if (s.DefaultPresetName is not null &&
                !s.Presets.Any(p => string.Equals(p.Name, s.DefaultPresetName, StringComparison.OrdinalIgnoreCase)))
            {
                s.DefaultPresetName = s.Presets.FirstOrDefault()?.Name;
            }

            s.SchemaVersion = AppSettings.CurrentSchemaVersion;
        }
    }
}
