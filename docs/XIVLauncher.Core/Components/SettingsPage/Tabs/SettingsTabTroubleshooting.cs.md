# SettingsTabTroubleshooting.cs

**File:** `src/XIVLauncher.Core/Components/SettingsPage/Tabs/SettingsTabTroubleshooting.cs` · 141 lines · 6,463 bytes

## Purpose

The troubleshooting tab: Unix "fix" checkboxes, the log-pack action, and the destructive cleanup/reset
actions. Replaces the immediate-mode tab where every action was an ImGui button inside one draw pass. Each
action is a `Button`; explanatory text is grey (`EtoColors.Grey3`, the old disabled-text style) and the
destructive warning is red (`EtoColors.Red`).

## Declaration

`public class SettingsTabTroubleshooting : SettingsTab` (line 18), namespace
`XIVLauncher.Core.Components.SettingsPage.Tabs`.

## Members

| Member | Line | Behavior |
| --- | --- | --- |
| `Entries` | 20-53 | Five `bool` hack rows, built in the field initializer. |
| `IsUnixExclusive` | 55 | `false`: the tab is visible on all platforms, even though its rows are Unix-only. |
| `Title` | 57 | `Strings.TroubleshootingTitle` (`Strings.Designer.cs:306`). |
| Constructor | 59-117 | Builds header items and footer items (buttons, labels, action rows). |
| `headerItems` | 119, 61 | Single grey `"Fixes"` label, created once. |
| `footerItems` | 120, 88-116 | Grey `Logs` label, TSPack button row, grey explanation, grey `Cleanup` label, red warning label, six `ActionRow`s. Created once. |
| `HeaderContent` | 122 | Returns `headerItems` (field-backed, reused). |
| `FooterContent` | 124 | Returns `footerItems` (field-backed, reused). |
| `ActionRow` | 127-140 | `StackLayout` (horizontal): button, grey wrapping description label. |

Entries, in order (all Unix-only visibility, lines 24/28/32/38/51):

1. **Disable gameoverlayrenderer.so** (line 22): `Strings.DisableGameoverlayRendererHack`/`...Description`
   (`Strings.Designer.cs:234`/`240`); load `Program.Config.FixLDP ?? false`; save writes `FixLDP`.
2. **XMODIFIERS="@im=none"** (line 26): `Strings.XModifiersHack`/`...Description`
   (`Strings.Designer.cs:246`/`252`); load `Program.Config.FixIM ?? false`; save writes `FixIM`.
3. **libicuuc Dalamud error** (line 30): `Strings.FixLibICUUCErrorHack`/`...Description`
   (`Strings.Designer.cs:258`/`264`); load `Program.Config.FixError127 ?? false`; save writes `FixError127`.
4. **Force locale to C.UTF8** (line 34): name is
   `string.Format(Strings.ForceLocaleHack, Program.CType or Strings.ForceLocaleHackCUTF8)`
   (`Strings.Designer.cs:270`/`276`); description is
   `string.Format(Strings.ForceLocaleHackDescription, Program.CType)` when a UTF-8 locale was found, otherwise a
   literal `"Hack Disabled. Could not find a UTF-8 C locale. You may have to set LC_ALL manually if LANG is not a UTF-8 type."`
   (`Strings.Designer.cs:282`); load `Program.Config.FixLocale ?? false`; save writes `FixLocale`.
   `CheckWarning` (lines 39-47): `CoreEnvironmentSettings.GetCleanEnvironmentVariable("LANG")`
   containing `UTF` and the box checked → `string.Format(Strings.ForceLocaleHackUTFValidation, lang)`
   (`Strings.Designer.cs:288`); otherwise no warning.
5. **Don't use system timezone** (line 49): `Strings.ForceDontUseSystemTZ`/`Strings.ForceDontUseSystemDescription`
   (`Strings.Designer.cs:294`/`300`); load `Program.Config.DontUseSystemTz ?? true`; save writes `DontUseSystemTz`.

Footer actions:
- **Generate TSPack** button (`Strings.GenerateTSPackTroubleshootingButton`, `Strings.Designer.cs:384`) calls
  `PackGenerator.SavePack(Program.storage)` and opens the logs folder in a browser (lines 64-68).
- **Clear WINE prefix** (`Strings.ClearWINEPrefixTroubleshootingButton`, line 318) → `Program.ClearPrefix()`
  (`Program.cs:369`, deletes/recreates `wineprefix`).
- **Clear managed compatibility tools** (line 330) → `Program.ClearTools(true)` (`Program.cs:395`, deletes
  `compatibilitytool`, recreates wine/dxvk folders, `CreateCompatToolsInstance()`).
- **Clear Dalamud** (line 342) → `Program.ClearPlugins(true)` (`Program.cs:375`, deletes dalamud assets/runtime,
  UI/config files, recreates folders, `DalamudLoadInfo = new DalamudOverlayInfoProxy()`,
  `DalamudUpdater = CreateDalamudUpdater()`, `DalamudUpdater.Run(Config.DalamudBetaKind, Config.DalamudBetaKey)`).
- **Clear all logs** (line 354) → `Program.ClearLogs(true)` (`Program.cs:452`, deletes/recreates `logs`,
  deletes four dalamud log files, `SetupLogging(mainArgs)`).
- **Reset settings** (line 366) → `Program.ClearSettings(true)` (`Program.cs:408`, deletes `launcher.ini`,
  reloads config, opens Settings state).
- **Reset all** (line 390) → `Program.ClearAll(true)` (`Program.cs:414`, destructive: settings, plugins,
  tools, prefix, logs).
- The descriptions for cleanup actions are `Strings.*Troubleshooting` (`Strings.Designer.cs:312`, `324`, `336`,
  `348`, `360`, `372`), and the TSPack explanation is `Strings.GenerateTSPackTroubleshooting`
  (`Strings.Designer.cs:378`); `Strings.TroubleshootingDestructiveActionWarning` (`Strings.Designer.cs:912`)
  is the red warning.

## Consumers

`SettingsPage` constructs the tab (`SettingsPage.cs:32`), loads row values on show, saves rows on finish,
shows the tab on all platforms, includes rows in search results (they match Unix visibility checks at rebuild),
and scans validity. Rows' saved values are read during game launch through `MainPage.StartGameAndAddon` env
manipulation (`FixLDP`, `FixIM`, `FixError127`, `FixLocale`, `DontUseSystemTz`).

Cleanup buttons call `Program.Clear*` functions, which mutate storage folders, config, the Dalamud updater,
compatibility tools, and logging state. `Reset settings` opens Settings state directly.

`AppUtil.OpenBrowser` is used by TSPack's open-logs action via `PlatformHelpers.OpenBrowser`
(`Strings.Designer.cs` consumers via `XIVLauncher.Common.Util`).

## Quirks

- **Header/footer items are field-backed and reused**, unlike About/Debug. They are re-parented to a fresh
  `StackLayout` in `SettingsTab.RefreshEntries()` on every rebuild, so buttons, labels, and action rows keep
  state (button enablement, label text) across rebuilds.
- **The tab is not Unix-exclusive** (`IsUnixExclusive => false`), so Windows/macOS show the tab, the "Fixes"
  header, the log/cleanup footer actions, and the red warning. Its five hack rows are Unix-only, so on non-Unix
  the tab has no visible entries, and `Entries` is scanned by `UpdateFinishButton` via `IsVisible`
  (`Environment.OSVersion.Platform == PlatformID.Unix` → false).
- **The locale row's name and description are computed once at construction.** `Program.CType`
  (`Program.cs:60`) is read when the tab is constructed, so the row text reflects the launcher process's
  detected locale at startup; environment changes later do not update the text.
- **`CheckWarning` runs `GetCleanEnvironmentVariable("LANG")` on every value change**, not cached. The warning
  label updates through `UpdateState()`, so typing/loading the locale row can show it.
- **`Entries` is a field initializer**, not constructor code. `SettingsTabTroubleshooting`'s base constructor
  (`Component`/`Panel`) runs before the field initializer? In C# derived field initializers run before the base
  constructor body, so `Entries` is assigned first, and `SettingsEntry<T>` rows are created before the tab
  constructor body builds header/footer. In practice, no base member consumes `Entries` during construction.
- **Cleanup actions are immediate UI-thread calls.** Each destructive button deletes folders/files synchronously
  on the UI thread; `ClearAll(true)`/`ClearPlugins(true)` restart logging/updaters/compat instances, and
  `ClearSettings(true)` switches the launcher to the Settings state.
- **The TSPack button generates a pack and opens a folder on the UI thread** (`PackGenerator.SavePack`,
  `PlatformHelpers.OpenBrowser`).
- **Red warning label wraps**; it is added before the cleanup action rows, so the warning sits above the
  destructive buttons, matching HEAD ordering.
- **Action rows are horizontal StackLayouts with no width on labels**: the description labels wrap at GTK
  natural width, so long descriptions can push the row width beyond the tab area.
