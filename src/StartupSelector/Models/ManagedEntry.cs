using System;

namespace StartupSelector.Models
{
    /// <summary>A startup item that Startup Selector has taken control of (disabled in Windows via StartupApproved).</summary>
    public sealed class ManagedEntry
    {
        public string Id { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Windows' own state before Startup Selector took control; restored on release.</summary>
        public bool WasEnabledInWindows { get; set; } = true;

        public DateTime ManagedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
