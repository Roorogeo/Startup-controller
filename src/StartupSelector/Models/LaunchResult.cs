namespace StartupSelector.Models
{
    public sealed class LaunchResult
    {
        public LaunchResult(StartupEntry entry, bool success, string? error)
        {
            Entry = entry;
            Success = success;
            Error = error;
        }

        public StartupEntry Entry { get; }

        public bool Success { get; }

        public string? Error { get; }
    }
}
