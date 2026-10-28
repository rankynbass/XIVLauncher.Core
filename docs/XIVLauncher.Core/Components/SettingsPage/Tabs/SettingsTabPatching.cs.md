# SettingsTabPatching.cs — Specification

**File:** `src/XIVLauncher.Core/Components/SettingsPage/Tabs/SettingsTabPatching.cs` · 29 lines · 1,206 bytes
**Namespace:** `XIVLauncher.Core.Components.SettingsPage.Tabs` · **Declaration:** `public class SettingsTabPatching : SettingsTab` (line 8)
**Role:** Patching tab — patch path, download speed limit, patch retention

---

## 1. Purpose

Replaces the immediate-mode patching tab. All three rows are plain entries with no tab-specific widgets, so the tab has no ctor, no header/footer, and no `RefreshDynamic` override.

Using: `XIVLauncher.Core.Resources.Localization` (`Strings`).

## 2. Entries (3 rows, lines 10-26)

| # | Type | Label / Description | Load → Save | Extra behaviour |
| --- | --- | --- | --- | --- |
| 1 | `SettingsEntry<DirectoryInfo>` (12-21) | `Strings.PatchPathSetting` / `...Description` (`Strings.Designer.cs:396, 402`) | `Program.Config.PatchPath` (12) | `CheckValidity` (14-20): whitespace `FullName` → `Strings.PatchPathSettingNotSetValidation` (17); else `null` (19) |
| 2 | `NumericSettingsEntry` (23-24) | `Strings.MaximumSpeedSetting` / `...Description` (`Strings.Designer.cs:426, 432`) | `() => (int)Program.Config.PatchSpeedLimit` / `x => Program.Config.PatchSpeedLimit = x` | range `0 .. int.MaxValue`, step `1000` (`NumericSettingsEntry.cs.md`) |
| 3 | `SettingsEntry<bool>` (25) | `Strings.KeepPatchesSetting` / `...Description` (`Strings.Designer.cs:438, 444`) | `Program.Config.KeepPatches ?? false` | check box carries the label |

`Title` is `Strings.PatchingTitle` (`Strings.Designer.cs:450`) (line 28).

## 3. Consumers

| Consumer | Line(s) | Use |
| --- | --- | --- |
| `SettingsPage.tabs` | `SettingsPage.cs:38` | second tab |
| `MainPage` boot patch | `MainPage.cs:870-875` | `App.Settings.PatchPath` existence + default `Paths.RoamingPath/patches` (`Components/MainPage/MainPage.cs.md` §6) |
| `MainPage` patching | `MainPage.cs:936-939` | `PatchInstaller(gamePath, keepPatches)`, `PatchManager(..., speedLimitBps, ..., patchStore)` |
| `MainPage` repair | `MainPage.cs:1056` | `Program.Config.PatchPath` passed to `PatchVerifier` |

`App.Settings` is `Program.Config` (`LauncherApp.cs:121`), so tab changes take effect for patching only after the finish button's `Save()` pass (`SettingsPage.cs:82-84`).

## 4. Quirks

- **Speed-limit row construction order**: `NumericSettingsEntry` creates its stepper before the range fields exist, so the row depends on the range repair in `NumericSettingsEntry.cs.md` §3; the `step: 1000` argument is what makes the arrows jump by 1000 B/s.
- `PatchSpeedLimit` is a lib-side config field (`ILauncherConfig`, `lib/FFXIVQuickLauncher`, out of docs scope); the cast `(int)` at line 23 narrows it, and the save writes `int` back.
- No `CheckVisibility` hooks: all three rows are visible on every platform, including the speed limit row on Windows.
- Patch-path validation only checks for emptiness, not existence; a non-existent directory is accepted (`CheckValidity` at 14-20), matching HEAD.

## 5. Cross-references

Uses: `SettingsTab` (`SettingsTab.cs.md`), `SettingsEntry<T>` (`SettingsEntry{T}.cs.md`), `NumericSettingsEntry` (`NumericSettingsEntry.cs.md`), `Strings` (`Resources/Localization/Strings.Designer.cs.md`), `Program` (`Program.cs.md`).
Referenced by: `SettingsPage.cs.md` §2.
