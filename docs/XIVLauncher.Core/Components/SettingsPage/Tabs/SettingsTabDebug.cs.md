# SettingsTabDebug.cs

**File:** `src/XIVLauncher.Core/Components/SettingsPage/Tabs/SettingsTabDebug.cs` · 69 lines · 2,491 bytes

## Purpose

The debug tab: generic launcher information (OS, runtime, Steam Deck detection, Flatpak note) and an
environment-variable table. Replaces the ImGui table that was rebuilt inside every draw pass; here the table
is rebuilt when the tab is refreshed.

## Declaration

`public class SettingsTabDebug : SettingsTab` (line 18), namespace `XIVLauncher.Core.Components.SettingsPage.Tabs`.

## Members

| Member | Line | Behavior |
| --- | --- | --- |
| `steamDeckLabel` | 20 | Field label `"Steam Deck Hardware Detected"`, hidden by default. |
| `envVarsTable` | 21, 29-36 | `TableLayout` created with the `Key`/`Value` header row. |
| `Entries` | 23 | Empty array: no settings rows, so nothing appears in search results from this tab. |
| `Title` | 25 | `Strings.DebugTitle` (`Strings.Designer.cs:588`). |
| `HeaderContent` | 38-55 | Grey "Generic Information" label, OS label, runtime label, Steam Deck label, optional Flatpak label under `#if FLATPAK`, grey "Environment Information" label, inline `Scrollable` wrapping `this.envVarsTable` (lines 48-54): `Border = BorderType.None`, `ExpandContentHeight = true`, `ExpandContentWidth = false`. |
| `RefreshDynamic()` | 57-68 | Sets Steam Deck visibility, clears the table, adds the header row, adds a row per environment variable sorted by key. |

Header content details:
- `Environment.OSVersion` and `Environment.Version` are read when the header list is built, i.e. on each
  `RefreshEntries()` rebuild, not at construction.
- `#if FLATPAK` adds `"Running as a Flatpak"` only when compiled with the `FLATPAK` symbol
  (`XIVLauncher.Core.csproj`).
- The `Scrollable` wrapper is created inline in `HeaderContent` (lines 48-54); the wrapper is recreated per
  rebuild, so scroll position resets on both platforms. It expands content height but not width
  (`ExpandContentHeight = true`, `ExpandContentWidth = false`).

Table rebuild:
- `envVarsTable.Rows.Clear()` (line 63) removes all rows, including the previous header.
- A fresh `TableRow(new Label { Text = "Key" }, new Label { Text = "Value" })` header is added (line 64).
- `Environment.GetEnvironmentVariables()` is cast to `DictionaryEntry` items, ordered by key string
  (`OrderBy(entry => entry.Key.ToString())`), and added as two-label rows; `null` key/value render as
  `"null"` (line 61).
- `Program.IsSteamDeckHardware` (`Program.cs:63`, from `CoreEnvironmentSettings.IsDeck`) drives the Steam Deck
  label (line 59).

## Consumers

`SettingsPage` constructs the tab (`SettingsPage.cs:46` in `BuildTabs()`), loads values on show (no entries),
saves (no entries), adds the tab, includes it in the validity scan (no rows), and exposes the header on
rebuild. The Steam Deck label and Flatpak label depend on launcher state: `Program.IsSteamDeckHardware` and the
`FLATPAK` compilation symbol.

Consumes `EtoColors.Grey3`, `Strings.DebugTitle`, `Program.IsSteamDeckHardware`, `Environment`,
`TableLayout`, `TableRow`, `Scrollable`, `BorderType`, `Label`.

## Quirks

- **The tab contributes nothing to settings.** `Entries` is empty, so `LoadValues`, `Save`, search, and finish
  validity see no rows; the tab is informational only.
- **The header list allocates fresh labels on every rebuild.** `HeaderContent` is an expression property
  evaluated per `RefreshEntries()`, so all labels except `this.steamDeckLabel` are recreated. The Steam Deck
  label is reused (field), and its visibility is refreshed each time.
- **Environment variables are snapshotted at rebuild time.** The table reflects `Environment.GetEnvironmentVariables()`
  when the page is shown or when the search text changes; the launcher mutates environment variables during
  game launch (`MainPage.StartGameAndAddon`), so a table built before launch shows the pre-launch state.
- **The `Scrollable` wrapper is recreated per rebuild**, so a scrolled table returns to the top whenever pages
  rebuild. Since the wrapper is now an inline Eto `Scrollable` on both platforms, the table scrolls on Windows
  as well (the old `SettingsTab.WrapScrollable` pass-through on Windows is gone).
- **No column widths are set.** The table's natural widths come from labels; long environment values (for
  example, `DEBUGINFOD_URLS`) expand the row width as the toolkit requests, and rows can extend beyond the
  visible area inside the `Scrollable` on both platforms (the tab's outer `Scrollable` in `SettingsPage` does
  not scroll content width, so horizontal overflow stays in the inner table's scroll).
- **Sorting is string-based, ordinal via `OrderBy`.** Keys are ordered by `Key.ToString()` using LINQ's default
  string comparer (culture-sensitive).
- **`#if FLATPAK` is a compile-time symbol**, not runtime detection; the Flatpak label appears only in Flatpak
  builds.
