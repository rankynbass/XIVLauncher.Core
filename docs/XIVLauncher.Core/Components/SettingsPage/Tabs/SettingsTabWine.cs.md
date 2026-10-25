# SettingsTabWine.cs

**File:** `src/XIVLauncher.Core/Components/SettingsPage/Tabs/SettingsTabWine.cs` · 167 lines · 7,227 bytes

## Purpose

The Wine/DXVK settings tab and the compatibility-tool action buttons. The action buttons and the
"not set up" note are enabled only when the compatibility tool has been downloaded, mirroring the
immediate-mode tab's `BeginDisabled` group.

## Declaration

`public class SettingsTabWine : SettingsTab` (line 21), namespace `XIVLauncher.Core.Components.SettingsPage.Tabs`.

## Members

| Member | Line | Behavior |
| --- | --- | --- |
| `startupTypeSetting` | 23, 34-35 | Wine startup row, kept in a field for visibility checks. |
| `dxvkVersionSetting` | 25, 66 | DXVK version row, kept for visibility checks. |
| `toolActionPanel` | 27, 104-117 | Stack of "not setup" label, three-button row, kill button. |
| `notSetupLabel` | 122, 125, 86-90 | The wrapped note label, reused for rebuilds. |
| `toolButtons` | 121, 127 | The four action buttons, enabled/disabled together. |
| `Entries` | 129, 31-84 | Nine rows, built once. |
| `IsUnixExclusive` | 131 | `true`: the tab is hidden on non-Unix systems. |
| `Title` | 133 | `Strings.WineTitle` (`Strings.Designer.cs:198`). |
| `feralGameModeFound` / `FeralGameModeFound` | 135-148 | Cached Linux-only probe for `libgamemodeauto.so.0`. |
| `FooterContent` | 150 | Returns the single `toolActionPanel`. |
| `RefreshDynamic()` | 152-160 | Shows/hides the note, enables/disables the four buttons from `Program.CompatibilityTools.IsToolDownloaded`. |
| `Save()` | 162-166 | Saves entries, then `Program.CreateCompatToolsInstance()`. |

Entries, in order:

1. **WINE Install** (lines 34-35): `SettingsEntry<WineStartupType>`,
   `Strings.WineInstallSetting`/`...Description` (`Strings.Designer.cs:90`/`96`);
   load `Program.Config.WineStartupType ?? WineStartupType.Managed`; save writes it.
2. **WINE Version** (lines 36-40): `SettingsEntry<WineManagedVersion>`,
   `Strings.WineVersionSetting`/`...Description` (`Strings.Designer.cs:102`/`108`);
   load `Program.Config.WineManagedVersion ?? WineManagedVersion.Stable`; save writes it; visible only when
   startup type is `Managed` (line 39).
3. **Custom Wine binary path** (lines 41-46): `SettingsEntry<string>`,
   `Strings.WineBinaryPathSetting`/`Strings.WineBinarySettingDescription`
   (`Strings.Designer.cs:114`/`120`); load `Program.Config.WineBinaryPath`; save writes it; visible only when
   startup type is `Custom` (line 45).
4. **WINE Sync Method** (lines 47-62): `SettingsEntry<WineSyncType>`,
   `Strings.WineSyncMethodSetting`/`...Description` (`Strings.Designer.cs:1050`/`1056`);
   load `Program.Config.WineSyncType ?? WineSyncType.FSync`; save writes it.
   `CheckValidity` calls `WineUtility.SystemFsyncSupport()`:
   `UnsupportedPlatform` → `Strings.EnableFsyncSettingUnsupportedPlatformValidation`
   (`Strings.Designer.cs:1062`), `OutdatedKernel` → `Strings.EnableFSyncSettingMinKernelValidation`
   (`Strings.Designer.cs:168`), `Supported`/default → valid.
5. **WINEDEBUG Variables** (line 63): `SettingsEntry<string>`,
   `Strings.WineDebugAdditionalVarSetting`/`...Description` (`Strings.Designer.cs:186`/`192`);
   load `Program.Config.WineDebugVars ?? string.Empty`; save writes it.
6. **DXVK Version** (line 66): `SettingsEntry<DxvkVersion>`,
   `Strings.DXVKVersionSetting`/`...Description` (`Strings.Designer.cs:126`/`132`);
   load `Program.Config.DxvkVersion ?? DxvkVersion.Stable`; save writes it.
7. **DXVK Overlay** (line 67): `SettingsEntry<DxvkHudType>`,
   `Strings.EnableDXVKOverlaySetting`/`...Description` (`Strings.Designer.cs:174`/`180`);
   load `Program.Config.DxvkHudType`; save writes it.
8. **DXVK Async** (lines 68-71): `SettingsEntry<bool>`,
   `Strings.DXVKEnableAsyncSetting`/`...Description` (`Strings.Designer.cs:138`/`144`);
   load `Program.Config.DxvkAsyncEnabled ?? true`; save writes it; visible only when DXVK version is not
   `Disabled` (line 70).
9. **Feral GameMode** (lines 74-83): `SettingsEntry<bool>`,
   `Strings.EnableFeralGameModeSetting`/`...Description` (`Strings.Designer.cs:150`/`156`);
   load `Program.Config.GameModeEnabled ?? true`; save writes it.
   `CheckVisibility` = Linux only (line 76). `CheckValidity`: enabled + GameMode library missing →
   `Strings.EnableFeralGameModeNotFoundValidation` (`Strings.Designer.cs:162`).

Footer/action buttons (86-122):
- `notSetupLabel` text `Strings.CompatibilityToolNotSetup` (`Strings.Designer.cs:204`), wraps.
- `openPrefixButton` (`Strings.OpenWINEPrefix`, `Strings.Designer.cs:210`) opens
  `Program.CompatibilityTools.Settings.Prefix.FullName` through `PlatformHelpers.OpenBrowser` (line 93).
- `openConfigurationButton` (`Strings.OpenWINEConfiguration`, line 216) runs `winecfg` in the prefix (line 96).
- `openExplorerButton` (`Strings.OpenWINEExplorer`, line 222) runs `explorer` in the prefix (line 99).
- `killButton` (`Strings.KillAllWINEProcesses`, line 228) calls `Program.CompatibilityTools.Kill()` (line 102).
- The buttons are placed in a horizontal `StackLayout` inside the vertical footer panel.

## Consumers

`SettingsPage` constructs the tab (`SettingsPage.cs:28`) and includes it only on Unix
(`SettingsPage.cs:102`). `Program.CompatibilityTools` (`Program.cs:43`, created at `Program.cs:307-316`)
supplies prefix paths, `RunInPrefix`, `Kill`, `IsToolDownloaded`. `Program.CreateCompatToolsInstance()`
(`Program.cs:307`) is called after save (line 165) and during startup (`Program.cs:253`) and UID cache resets
(`Program.cs:400`).

`MainPage.StartGameAndAddon` uses `Program.CompatibilityTools` and the saved Wine/DXVK/GameMode settings when
launching on Unix.

## Quirks

- **The footer panel is reused, not recreated.** `FooterContent` returns the single `toolActionPanel` field,
  so `RefreshEntries()` re-parents the same widget tree; the label and buttons keep state.
- **Group enablement is per button.** `RefreshDynamic()` disables each of the four buttons individually; the
  surrounding `StackLayout` is never disabled, because a wrapping panel does not propagate `Enabled` to
  children in this Eto/GTK path.
- **`notSetupLabel` is assigned after construction.** `this.notSetupLabel = null!` is a field initializer that
  runs before the constructor body sets the real label (line 122); during base construction
  (`SettingsTab` field initialization order) the field is null, so any base member that touched
  `FooterContent` before line 122 would see `null!`. In practice `FooterContent` is read only after the
  constructor completes.
- **`FeralGameModeFound` caches a native-library probe.** The first call loads
  `libgamemodeauto.so.0` with `NativeLibrary.TryLoad`, frees the handle, and caches the result in
  `feralGameModeFound`; later calls reuse the cached value.
- **GameMode validation runs on the row's value changes**, not just when shown, so typing/loading the row
  triggers `NativeLibrary` probing only through the cached property (the probe is cached, not repeated).
- **Fsync validation calls `WineUtility.SystemFsyncSupport()` on each `CheckValidity`**, which is invoked on
  every value change through `UpdateState()`; the result is not cached.
- **`Save()` rebuilds the compatibility tools instance** (`Program.CreateCompatToolsInstance()`), which
  constructs a new `CompatibilityTools` with the just-saved `DxvkVersion`, `DxvkHudType`, `GameModeEnabled`,
  `DxvkAsyncEnabled` values.
- **The Wine tab is Unix-exclusive** (`IsUnixExclusive => true`), so Windows builds save its rows through the
  finish button (all tabs save) even though the tab is not visible.
- **Buttons are raw Eto `Button`s**, not the launcher's `Common.Button`, so they use text labels, not
  FontAwesome glyphs.
- **Prefix/configuration/explorer/kill actions run on the UI thread.** `RunInPrefix("winecfg")` and
  `RunInPrefix("explorer")` launch Wine processes directly from the button handler; `Kill()` terminates Wine
  processes from the UI thread.
