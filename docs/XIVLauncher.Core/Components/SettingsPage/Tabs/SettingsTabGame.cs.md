# SettingsTabGame.cs — Specification

**File:** `src/XIVLauncher.Core/Components/SettingsPage/Tabs/SettingsTabGame.cs` · 80 lines · 3,699 bytes
**Namespace:** `XIVLauncher.Core.Components.SettingsPage.Tabs` · **Declaration:** `public class SettingsTabGame : SettingsTab` (line 17)
**Role:** Game tab — game path, config path, extra arguments, language, DPI, auth toggles, UID-cache reset action

---

## 1. Purpose

Replaces the immediate-mode game tab, which checked the UID-cache visibility flag every frame. Here the reset section is a panel whose `Visible` flag is recomputed in `RefreshDynamic()`, and each row persists through `Program.Config` on the settings page's Save pass.

Usings: `Eto.Drawing` (`Size`? not used here — `WrapMode`), `Eto.Forms`, `XIVLauncher.Common` (`ClientLanguage`, `DpiAwareness`, `CoreEnvironmentSettings`), `XIVLauncher.Core.Resources.Localization` (`Strings`).

## 2. Entries (8 rows, lines 21-54)

| # | Type | Label / Description | Load → Save | Extra behaviour |
| --- | --- | --- | --- | --- |
| 1 | `SettingsEntry<DirectoryInfo>` (23-35) | `Strings.GamePathSetting` / `...Description` (`Strings.Designer.cs:456, 462`) | `Program.Config.GamePath` (23) | `CheckValidity` (25-34): whitespace `FullName` → `Strings.GamePathSettingNotSetValidation` (28); `x.Name is "game" or "boot"` → `Strings.GamePathSettingInvalidValidationj` (31); else `null` (33) |
| 2 | `SettingsEntry<DirectoryInfo>` (37-43) | `Strings.GameConfigurationPathSetting` / `...Description` (480, 486) | `Program.Config.GameConfigPath` (37) | validity: empty → `Strings.GameConfigurationPathNotSetValidation` (39); visibility: `Environment.OSVersion.Platform == PlatformID.Unix` (42) |
| 3 | `SettingsEntry<string>` (45) | `Strings.AdditionalGameArgsSetting` / `...Description` (498, 504) | `Program.Config.AdditionalArgs` | no validity/warning/visibility hooks |
| 4 | `SettingsEntry<ClientLanguage>` (46) | `Strings.GameLanguageSetting` / `...Description` (510, 516) | `Program.Config.ClientLanguage ?? ClientLanguage.English` | `DropDown` built from `SettingsDescriptionAttribute` (`SettingsEntry{T}.cs.md` §5) |
| 5 | `SettingsEntry<DpiAwareness>` (47) | `Strings.GameDPIAwarenessSetting` / `...Description` (522, 528) | `Program.Config.DpiAwareness ?? DpiAwareness.Unaware` | enum drop-down |
| 6 | `SettingsEntry<bool>` (48) | `Strings.UseXLAuthMacrosSetting` / `...Description` (534, 540) | `Program.Config.IsOtpServer ?? false` | check box carries the label (`SettingsEntry{T}.cs.md` §4) |
| 7 | `SettingsEntry<bool>` (49-52) | `Strings.IgnoreSteamSetting` / `...Description` (546, 552) | `Program.Config.IsIgnoringSteam ?? false` | visibility: `!CoreEnvironmentSettings.IsSteamCompatTool` (51) |
| 8 | `SettingsEntry<bool>` (53) | `Strings.UseUIDCacheSetting` / `...Description` (558, 564) | `Program.Config.IsUidCacheEnabled ?? false` | toggles the footer panel via `RefreshDynamic` |

`Title` is `Strings.GameTitle` (`Strings.Designer.cs:570`) (line 56).

## 3. Reset section

Constructor (58-72):

- `var resetUidButton = new Button { Text = Strings.ResetUIDCacheButton }` (60) (`Strings.Designer.cs:582`)
- `resetUidButton.Click += (_, _) => Program.ResetUIDCache()` (61) → `Program.cs:422` (`launcherApp.UniqueIdCache.Reset()`)
- `this.resetUidPanel = new StackLayout { Spacing = 10, Items = { new Label { Text = Strings.ResetUIDCacheInfo, Wrap = WrapMode.Character }, resetUidButton } }` (63-71) (`Strings.Designer.cs:576`)

`FooterContent => [this.resetUidPanel]` (74) — the panel and its widgets are created once and reused across rebuilds.

`RefreshDynamic()` (76-79) sets `this.resetUidPanel.Visible = Program.Config.IsUidCacheEnabled == true` (78). `RefreshDynamic` is called by `SettingsTab.RefreshEntries()` (`SettingsTab.cs.md` §4), i.e. on page show / search rebuild, so the section appears only when the tab layout is rebuilt after the UID-cache checkbox changes.

## 4. Consumers

| Consumer | Line(s) | Use |
| --- | --- | --- |
| `SettingsPage.tabs` | `SettingsPage.cs:26` | first tab |
| `MainPage` login | `MainPage.cs:175, 262` | reads `App.Settings.GamePath`, `ClientLanguage` (`Components/MainPage/MainPage.cs.md` §3) |
| `MainPage` patching | `MainPage.cs:870-875, 943` | `App.Settings.PatchPath`, patch progress |
| `MainPage` launch | `MainPage.cs:205, 261` | `App.Settings.GamePath`, `IsUidCacheEnabled` |
| `Program` | `Program.cs:39` | `Program.Config` is the `ILauncherConfig` the entries read/write |

`App.Settings` and `Program.Config` are the same object: `LauncherApp.Settings` is `Program.Config` (`LauncherApp.cs:121`).

## 5. Quirks

- **Persistence is Save-time, not keystroke-time.** The `save` delegate runs only in `SettingsEntry<T>.Save()` (`SettingsEntry{T}.cs:222`), called by the finish button (`SettingsPage.cs:61-63`). Typing in the game-path box updates `InternalValue` and validity labels immediately, but `Program.Config.GamePath` is written only when the user presses Finish.
- `CheckValidity` for the game path reads `x.Name` (30) after the whitespace guard returns, so a `null` `DirectoryInfo` never reaches it.
- The config-path row is Unix-only (42); on Windows it never appears in the tab and never appears in search results (`IsVisible` gate at `SettingsPage.cs:136`).
- `CoreEnvironmentSettings.IsSteamCompatTool` (lib API, out of docs scope) is evaluated at rebuild time, so launching the launcher under a Steam compat tool and changing env mid-session does not re-show the row.
- The reset button calls `Program.ResetUIDCache()` with the default `tsbutton: false` (`Program.cs:422`).
- The footer panel is a `Panel` field typed as `Panel` (19) holding a `StackLayout`; hiding the panel hides both label and button.

## 6. Cross-references

Uses: `SettingsTab` (`SettingsTab.cs.md`), `SettingsEntry<T>` (`SettingsEntry{T}.cs.md`), `Strings` (`Resources/Localization/Strings.Designer.cs.md`), `Program` (`Program.cs.md`).
Referenced by: `SettingsPage.cs.md` §2.
