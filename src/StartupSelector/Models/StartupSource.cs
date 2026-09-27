namespace StartupSelector.Models
{
    /// <summary>The standard Windows locations Startup Selector scans for startup apps.</summary>
    public enum StartupSource
    {
        /// <summary>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</summary>
        CurrentUserRun,

        /// <summary>HKLM\Software\Microsoft\Windows\CurrentVersion\Run (64-bit view)</summary>
        LocalMachineRun,

        /// <summary>HKLM\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run (32-bit apps)</summary>
        LocalMachineRun32,

        /// <summary>shell:startup</summary>
        UserStartupFolder,

        /// <summary>shell:common startup</summary>
        CommonStartupFolder,
    }

    /// <summary>Which StartupApproved hive a change targets.</summary>
    public enum ApprovedHive
    {
        CurrentUser,
        LocalMachine,
    }

    /// <summary>Which StartupApproved sub key a change targets.</summary>
    public enum ApprovedKind
    {
        Run,
        Run32,
        StartupFolder,
    }
}
