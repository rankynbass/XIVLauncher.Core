# SettingsTabDalamud.cs

**File:** `src/XIVLauncher.Core/Components/SettingsPage/Tabs/SettingsTabDalamud.cs` · 70 lines · 3,625 bytes

## Purpose

The Dalamud settings tab: enable Dalamud, load method, branch switcher, injection delay, manual injection
enable, and the local injection path.

## Declaration

`public class SettingsTabDalamud : SettingsTab` (line 10), namespace
`XIVLauncher.Core.Components.SettingsPage.Tabs`.

## Members

| Member | Line | Behavior |
| --- | --- | --- |
| `Title` | 12 | `Strings.DalamudTitle` (`Strings.Designer.cs:594`). |
| `Entries` | 14, 20-68 | Six rows, built once in the constructor. |
| `enableManualInjection` | 16, 27 | The enable-manual-injection row, kept in a field so the local-path row can read its value. |

Entries, in order:

1. **Enable Dalamud** (line 22): `SettingsEntry<bool>`,
   `Strings.EnableDalamudSetting`/`...Description` (`Strings.Designer.cs:600`/`606`);
   load `Program.Config.DalamudEnabled ?? true`; save writes `Program.Config.DalamudEnabled`.
2. **Load Method** (line 23): `SettingsEntry<DalamudLoadMethod>`,
   `Strings.LoadMethodSetting`/`...Description` (`Strings.Designer.cs:612`/`618`);
   load `Program.Config.DalamudLoadMethod ?? DalamudLoadMethod.DllInject`; save writes the enum. The base
   enum factory builds the drop-down from `DalamudLoadMethod` values.
3. **Dalamud Branch** (line 24): `DalamudBranchMetaSettingsEntry`,
   `Strings.DalamudBranchSwitcher`/`...Description` (`Strings.Designer.cs:672`/`678`);
   load `Program.Config.DalamudBetaKind ?? "release"`; the `save` delegate is empty
   (`s => { }`), saving is performed by the entry's own `Save()` side effects.
4. **Injection Delay (ms)** (line 25): `NumericSettingsEntry`,
   `Strings.DalamudInjectionDelaySetting`/`...Description` (`Strings.Designer.cs:624`/`630`);
   load `Program.Config.DalamudLoadDelay`; save writes it; range `0` to `int.MaxValue`, step `1000`.
5. **Use Local Dalamud** (line 27): `SettingsEntry<bool>`,
   `Strings.DalamudManualInjectionEnableSetting`/`...Description` (`Strings.Designer.cs:636`/`642`);
   load `Program.Config.DalamudManualInjectionEnabled ?? false`. Save writes the flag and, as side effects:
   - when disabled, clears `Program.DalamudUpdater.RunnerOverride` and returns (lines 31-35);
   - when enabled, if `Program.Config.DalamudManualInjectPath` exists and contains
     `Program.DALAMUD_INJECTOR_NAME` (`Program.cs:144` = `"Dalamud.Injector.exe"`), sets
     `Program.DalamudUpdater.RunnerOverride` to that file (lines 37-42).
6. **Local injection path** (lines 45-67): `SettingsEntry<DirectoryInfo>`,
   `Strings.DalamudLocalInjectionPathSetting`/`...Description` (`Strings.Designer.cs:648`/`654`);
   load `Program.Config.DalamudManualInjectPath`. Save skips when manual injection is off (`this.enableManualInjection.Value == false`,
   line 47) or input is null (line 48), then stores the directory and sets `Program.DalamudUpdater.RunnerOverride`
   (lines 49-50).
   `CheckVisibility = () => this.enableManualInjection.Value` (line 53): the row appears only when manual
   injection is enabled.
   `CheckValidity` (lines 54-66): `null`/missing directory →
   `Strings.DalamudLocalInjectionPathSettingNoDirValidation` (`Strings.Designer.cs:660`); directory without
   the injector file → `Strings.DalamudLocalInjectionPathSettingNoInjValidation`
   (`Strings.Designer.cs:666`); otherwise valid.

## Consumers

`SettingsPage` constructs the tab (`SettingsPage.cs:29`), loads values on show, saves on finish, shows it as
the Dalamud tab when visible (the tab is not Unix-exclusive), includes its entries in search results, and
scans its rows for finish-button validity.

`Program`/launcher-side consumers of the saved config values:
- `Program.Config.DalamudEnabled`, `DalamudLoadMethod`, `DalamudLoadDelay`, `DalamudBetaKind`,
  `DalamudManualInjectionEnabled`, `DalamudManualInjectPath` are read during game launch (`MainPage.StartGameAndAddon`
  uses `App.Settings.DalamudLoadDelay`, `App.Settings.DalamudManualInjectPath`, `Program.Config.DalamudLoadMethod`)
  and by `Program.CreateDalamudUpdater` (`Program.cs:153-164`).
- `Program.DalamudUpdater.RunnerOverride` (`lib/FFXIVQuickLauncher` Dalamud updater) is mutated by rows 5 and 6.

## Quirks

- **`Entries` is assigned in the constructor.** The field initializer `Entries { get; }` (line 14) is
  populated once (lines 20-68), so entries outlive tab rebuilds; `RefreshEntries()` re-parents them.
- **Manual injection side effects happen at save time**, not at load. Enabling the checkbox and saving sets
  `RunnerOverride`; disabling and saving clears it. A path row save is skipped when the enable row's current
  value is false, so unchecking and not saving leaves `RunnerOverride` unchanged.
- **Save order matters.** Finish `Save()` iterates all tabs; within this tab, entries are saved in array order,
  so the enable row (index 4) is saved before the path row (index 5). A path typed while the enable row is
  unchecked never writes `RunnerOverride`.
- **Validation checks the file system on each value change.** `CheckValidity` calls `input.Exists` and
  `GetFiles()`, so typing a path performs disk enumeration on the UI thread.
- **Visibility is checked eagerly by readers.** `CheckVisibility` reads the enable checkbox's current value;
  toggling it does not rebuild the page immediately, so the path row appears only on the next rebuild
  (search change, page show).
- **The branch row's `load` fallback is `"release"`**; a configured `DalamudBetaKind` value that is not in the
  fetched branch list leaves the drop-down unselected.
- **The numeric row uses the repaired `NumericSettingsEntry`**, so the base zero-range stepper problem is
  addressed for this row as for the patch speed row.
- **`Program.DALAMUD_INJECTOR_NAME` is a Windows executable name** (`"Dalamud.Injector.exe"`) on all
  platforms; the local-path validation looks for exactly that file.
