using System;
using System.IO;
using System.Linq;

namespace StartupSelector.Services
{
    /// <summary>Splits a Run-key command line into executable and arguments.</summary>
    public static class CommandLineParser
    {
        public static (string Executable, string Arguments) Split(string commandLine)
        {
            var command = Environment.ExpandEnvironmentVariables(commandLine ?? string.Empty).Trim();
            if (command.Length == 0)
            {
                return (string.Empty, string.Empty);
            }

            if (command[0] == '"')
            {
                var closing = command.IndexOf('"', 1);
                if (closing > 0)
                {
                    return (Resolve(command[1..closing]), command[(closing + 1)..].Trim());
                }

                return (Resolve(command.Trim('"')), string.Empty);
            }

            // Unquoted: paths may contain spaces ("C:\Program Files\App\app.exe -min"), so try the
            // shortest prefix ending at a space that names an existing file.
            var parts = command.Split(' ');
            for (var i = 1; i <= parts.Length; i++)
            {
                var candidate = string.Join(' ', parts.Take(i));
                var found = ExistingFile(candidate);
                if (found is not null)
                {
                    return (found, string.Join(' ', parts.Skip(i)).Trim());
                }
            }

            return (Resolve(parts[0]), string.Join(' ', parts.Skip(1)).Trim());
        }

        private static string? ExistingFile(string candidate)
        {
            if (!Path.IsPathRooted(candidate))
            {
                var onPath = SearchPath(candidate);
                return onPath;
            }

            if (File.Exists(candidate))
            {
                return candidate;
            }

            if (!Path.HasExtension(candidate) && File.Exists(candidate + ".exe"))
            {
                return candidate + ".exe";
            }

            return null;
        }

        /// <summary>Returns a full path for bare file names (e.g. "rundll32.exe"), or the input unchanged.</summary>
        public static string Resolve(string executable)
        {
            if (string.IsNullOrWhiteSpace(executable) || Path.IsPathRooted(executable))
            {
                return executable;
            }

            return SearchPath(executable) ?? executable;
        }

        private static string? SearchPath(string fileName)
        {
            if (fileName.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || fileName.Contains('\\') || fileName.Contains('/'))
            {
                return null;
            }

            var names = Path.HasExtension(fileName) ? new[] { fileName } : new[] { fileName + ".exe", fileName };
            var directories = new[] { Environment.SystemDirectory, Environment.GetFolderPath(Environment.SpecialFolder.Windows) }
                .Concat((Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                    .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            foreach (var directory in directories)
            {
                foreach (var name in names)
                {
                    try
                    {
                        var full = Path.Combine(Environment.ExpandEnvironmentVariables(directory), name);
                        if (File.Exists(full))
                        {
                            return full;
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Malformed PATH entry; skip it.
                    }
                }
            }

            return null;
        }
    }
}
