# Startup Selector

A Windows desktop app (C# / .NET 8 / WPF) that replaces Windows' "start everything at sign-in" behaviour with a choice at every sign-in.

When you sign in, Startup Selector shows your startup apps with a checkbox next to each one and a 10-second countdown. Do nothing and your default preset launches. Click anywhere to pause, pick what you want, and press **Launch Selected**.

## How it works

1. **Detection.** It scans every standard startup location:
   - `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
   - `HKLM\Software\Microsoft\Windows\CurrentVersion\Run`
   - `HKLM\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run`
   - the user Startup folder (`shell:startup`) and the All Users Startup folder (`shell:common startup`)

   Each entry shows its icon, name, path, source location and current state.
2. **Taking control ("Manage").** A managed app is disabled in Windows through the `StartupApproved` keys (`...\Explorer\StartupApproved\Run`, `Run32` and `StartupFolder`, in HKCU or HKLM). This is the same non-destructive switch Task Manager's Startup tab uses. The original Run values and shortcuts are **never** deleted or edited. Startup Selector then launches the checked managed apps itself.
3. **Sign-in flow.** Startup Selector registers itself in `HKCU\...\Run` with `--startup`. At sign-in it shows the countdown. If you do nothing, it launches the default preset (or your last selection if there is no default preset). Launches are staggered (1 s by default) and the window then hides to the tray.
4. **Elevation only when needed.** Reading HKLM needs no admin rights, but changing an HKLM or All Users entry does. Those rows show the UAC shield. When you change one, Startup Selector re-launches itself elevated as a short-lived helper (`--apply-elevated <request.json>`). The helper has no UI, only writes whitelisted HKLM `StartupApproved` values, then exits. The main app always runs as a normal user.
5. **Release.** *Settings → Release all apps back to Windows* returns every managed entry to the state it had before it was managed and removes Startup Selector's own sign-in entry, so you can uninstall cleanly.

Startup Selector never lists itself, so **All Off** can never disable it.

## Project structure

```
Startup-controller/
├── StartupSelector.sln
├── README.md
├── .gitignore
├── .github/workflows/ci.yml          # Windows build + single-file publish, uploads the exe
├── tools/
│   └── make_icon.py                  # regenerates Resources/app.ico (pure Python, no dependencies)
└── src/StartupSelector/
    ├── StartupSelector.csproj
    ├── app.manifest                  # asInvoker + PerMonitorV2 DPI + Win10/11 compatibility
    ├── App.xaml / App.xaml.cs        # composition root, single instance, tray wiring, elevated-helper mode
    ├── Properties/PublishProfiles/
    │   └── SingleFile-win-x64.pubxml # one self-contained .exe
    ├── Resources/app.ico
    ├── Fonts/                        # Inter (SIL OFL), used by the Noir theme
    ├── Themes/Dark.xaml              # Noir palette + templates: buttons, checkbox, combo, scrollbars, menus, tooltips...
    ├── Converters/BoolToVisibilityConverter.cs
    ├── Models/
    │   ├── AppSettings.cs            # everything persisted to settings.json
    │   ├── ApplyResult.cs
    │   ├── ApprovalChange.cs         # one StartupApproved on/off change
    │   ├── LaunchResult.cs
    │   ├── ManagedEntry.cs
    │   ├── Preset.cs
    │   ├── StartupEntry.cs
    │   └── StartupSource.cs          # StartupSource / ApprovedHive / ApprovedKind enums
    ├── Services/
    │   ├── StartupScanner.cs         # reads Run keys + Startup folders
    │   ├── StartupController.cs      # manage / release via StartupApproved, self-registration
    │   ├── SettingsStore.cs          # JSON load/save, corruption backup + last-good restore
    │   ├── AppLauncher.cs            # staggered launching
    │   ├── StartupApprovedRegistry.cs
    │   ├── ElevationService.cs       # UAC helper process
    │   ├── CommandLineParser.cs      # splits Run commands into exe + args
    │   ├── ShortcutResolver.cs       # IShellLink COM interop for .lnk files
    │   ├── IconExtractor.cs
    │   ├── TrayIconService.cs        # NotifyIcon + dark tray menu
    │   ├── SingleInstance.cs
    │   ├── IDialogService.cs
    │   ├── AppLog.cs
    │   └── AppPaths.cs
    ├── ViewModels/
    │   ├── ObservableObject.cs
    │   ├── RelayCommand.cs           # RelayCommand + AsyncRelayCommand
    │   ├── MainViewModel.cs
    │   ├── StartupItemViewModel.cs
    │   ├── PresetViewModel.cs
    │   ├── InfoBarViewModel.cs
    │   └── SettingsViewModel.cs
    └── Views/
        ├── MainWindow.xaml(.cs)
        ├── SettingsWindow.xaml(.cs)
        ├── DialogWindow.xaml(.cs)    # dark MessageBox / confirm / text prompt
        ├── DialogService.cs
        └── WindowTheming.cs          # dark title bar (DWMWA_USE_IMMERSIVE_DARK_MODE), black caption on Windows 11
```

No NuGet packages are used. Everything is plain .NET 8 (WPF, plus WinForms for the tray icon only).

## Build and run

### Requirements
- Windows 10 1809+ or Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), or Visual Studio 2022 17.8+ with the **.NET desktop development** workload

### Visual Studio 2022
1. Open `StartupSelector.sln`.
2. Select the **Debug** configuration and press **F5**.
3. To test the sign-in flow, open *Project → StartupSelector Properties → Debug → Open debug launch profiles UI* and set **Command line arguments** to `--startup`.

### dotnet CLI
```powershell
cd Startup-controller
dotnet build -c Release
dotnet run --project src/StartupSelector                 # normal start
dotnet run --project src/StartupSelector -- --startup    # simulate sign-in (countdown)
```

The first run registers the built exe at sign-in. Once you're happy with a published build, run that exe once so the registration points to it (the path is refreshed on every start), or untick *Settings → Start Startup Selector when I sign in*.

## Publish a single self-contained .exe

```powershell
dotnet publish src/StartupSelector -p:PublishProfile=SingleFile-win-x64
```

Output: `src/StartupSelector/bin/publish/win-x64/StartupSelector.exe` (about 65 MB, no .NET install needed).

CI (`.github/workflows/ci.yml`) builds and publishes on `windows-latest` for every pull request and every push to `main`. It uploads the exe as the **StartupSelector-win-x64** artifact on the workflow run page.

Equivalent explicit command:

```powershell
dotnet publish src/StartupSelector -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

In Visual Studio: right-click the project → **Publish…** → choose the **SingleFile-win-x64** profile → **Publish**.

Copy the exe somewhere permanent (for example `%LocalAppData%\Programs\StartupSelector\`) and run it once.

## Files

| Path | Contents |
|---|---|
| `%AppData%\StartupSelector\settings.json` | settings, presets, managed apps, default preset, last selection, known apps |
| `%AppData%\StartupSelector\settings.bak.json` | last successfully written copy (used to recover from corruption) |
| `%AppData%\StartupSelector\settings.corrupt-*.json` | a damaged settings file, moved aside |
| `%AppData%\StartupSelector\log.txt` | errors and actions (rotated at 1 MB to `log.old.txt`) |

## Uninstall

1. *Settings → Release all apps back to Windows* (accept the UAC prompt if any all-users apps are managed).
2. Choose **Exit** (or right-click the tray icon → **Exit**).
3. Delete `StartupSelector.exe` and `%AppData%\StartupSelector`.

## Manual test checklist

Setup: publish the exe and run it once. For a broken-entry test, add a fake entry:
`reg add HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v FakeApp /d "C:\Nope\fake.exe"`

1. **Detection.** The list shows your HKCU/HKLM Run entries and Startup folder shortcuts with icons, paths and sources. HKLM / All Users rows show a shield. Startup Selector itself is not listed. The first run shows the Welcome info bar and two presets, **★ Everything** (default) and **Minimal**.
2. **Broken entry.** `FakeApp` shows an amber warning icon with "File not found", and `log.txt` contains a matching `WARN` line.
3. **Manage (HKCU).** Click **Manage** on a current-user app. It moves to *Managed by Startup Selector* and is checked if it used to start. Task Manager → Startup now shows it as **Disabled**, and its Run value still exists (`reg query HKCU\...\Run`).
4. **Manage (HKLM).** Click **Manage** on a shielded app. A UAC prompt appears. Accept it and the app becomes managed. Cancel it and an info bar explains that nothing changed.
5. **All On / All Off.** Check or uncheck every managed app. The footer count and the **Launch Selected (n)** button update.
6. **Presets.** Click **Save as…** and enter "Work". Change the checkboxes and "modified" appears. Test ⋯ → **Update**, **Rename…**, **Set as default** (★ moves) and **Delete**. Duplicate or empty names are rejected.
7. **Persistence.** Exit from the tray, then reopen. Presets, the default preset, managed apps and settings are all kept.
8. **Sign-in flow.** Sign out and back in (or run `StartupSelector.exe --startup`). The countdown banner appears. Leave it and the default preset launches with about 1 s between apps, then the window hides to the tray. Run it again, click anywhere in the window, and the countdown pauses. **Resume** continues it and **Launch now** launches immediately.
9. **Tray.** Left-click opens the window. The right-click menu offers Open, Launch Preset ▸ (lists presets), Settings and Exit, all in dark style. Closing the window with ✕ hides it to the tray (a one-time balloon explains this).
10. **New apps.** While Startup Selector is closed, add a Run entry (`reg add HKCU\...\Run /v NewThing /d notepad.exe`). On the next start it is highlighted **NEW** with an info bar. **Mark as seen** (✕ on the badge) or **Manage** clears it.
11. **Re-enabled by an app.** Re-enable a managed HKCU app in Task Manager, then click **Refresh**. Startup Selector silently disables it again. An HKLM app gets a "Disable again" info bar instead.
12. **Settings.** Change the countdown (0 disables auto-launch) and the launch delay. Invalid values are rejected. The **Start with Windows** toggle adds or removes `HKCU\...\Run\StartupSelector`.
13. **Corrupted settings.** Exit, put garbage into `settings.json`, then start. A warning info bar appears, the bad file is moved to `settings.corrupt-*.json`, and the last good settings are restored.
14. **Release all.** *Settings → Release all apps back to Windows*. Every managed app returns to its previous Task Manager state, the `StartupSelector` Run value is removed, and you are offered to exit.
15. **Single instance.** Launch the exe a second time. The existing window comes to the front instead.
16. **Noir theme.** Everything sits on pure black with white text and controls and soft translucent borders: title bars, list, checkboxes, dropdown, scrollbars, tooltips, dialogs and tray menu. The primary button is white with a faint halo, and text uses Inter.

## Design notes

- **Theme.** The look follows the "Noir" design system: black `background`, `surface` and `surface-raised` panels, white `ink` for text and primary controls, translucent white borders, 6/10/16 px radii and the Inter font. All colors live in the palette block at the top of `Themes/Dark.xaml` (the tray menu mirrors them in `TrayIconService.cs`). Amber warnings and red destructive actions are the only colors, kept so problems and irreversible actions still stand out.

- **All On / All Off and presets apply to managed apps.** Apps still controlled by Windows are shown greyed out, since Windows starts them regardless. Click **Manage** (or **Manage all…**) to put them under your control.
- **Releasing restores the previous state.** An app that was already disabled in Task Manager before you managed it goes back to disabled, not enabled, so releasing never turns on something you had switched off.
- The **Everything** example preset means "every managed app, including ones managed later". Updating it with ⋯ → **Update** turns it into a fixed list.
- Managed entries whose Run value or shortcut disappears (the app was uninstalled) are forgotten automatically.
