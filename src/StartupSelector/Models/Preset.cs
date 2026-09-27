using System.Collections.Generic;

namespace StartupSelector.Models
{
    /// <summary>A named checkbox selection.</summary>
    public sealed class Preset
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>Ids of the managed entries that are checked in this preset.</summary>
        public List<string> CheckedIds { get; set; } = new();

        /// <summary>When true the preset always means "every managed app", including ones managed later.</summary>
        public bool IncludeAllManaged { get; set; }
    }
}
