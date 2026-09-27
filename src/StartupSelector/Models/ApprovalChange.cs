namespace StartupSelector.Models
{
    /// <summary>One StartupApproved value to switch on or off. Serialized for the elevated helper process.</summary>
    public sealed class ApprovalChange
    {
        public ApprovedHive Hive { get; set; }

        public ApprovedKind Kind { get; set; }

        /// <summary>Run value name, or Startup folder file name.</summary>
        public string ValueName { get; set; } = string.Empty;

        public bool Enable { get; set; }

        /// <summary>The StartupEntry id this change belongs to (informational).</summary>
        public string EntryId { get; set; } = string.Empty;

        public bool RequiresElevation => Hive == ApprovedHive.LocalMachine;

        public static ApprovalChange For(StartupEntry entry, bool enable) => new()
        {
            Hive = entry.ApprovedHive,
            Kind = entry.ApprovedKind,
            ValueName = entry.Name,
            Enable = enable,
            EntryId = entry.Id,
        };

        public override string ToString() => $"{Hive}\\{Kind}\\{ValueName} -> {(Enable ? "enabled" : "disabled")}";
    }
}
