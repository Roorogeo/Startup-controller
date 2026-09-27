using System.Collections.Generic;

namespace StartupSelector.Models
{
    /// <summary>Outcome of applying a batch of StartupApproved changes.</summary>
    public sealed class ApplyResult
    {
        public List<ApprovalChange> Succeeded { get; } = new();

        public List<ApprovalChange> Failed { get; } = new();

        /// <summary>The user dismissed the UAC prompt, so elevated changes were not attempted.</summary>
        public bool ElevationCancelled { get; set; }

        public bool AllSucceeded => Failed.Count == 0 && !ElevationCancelled;
    }
}
