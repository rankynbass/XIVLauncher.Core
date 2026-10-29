# SettingsEntry{T}.cs

**File:** `src/XIVLauncher.Core/Components/SettingsPage/SettingsEntry{T}.cs` · 291 lines · 9,595 bytes

## Purpose

The typed settings row: one label, one input control, one description, plus validity and warning labels.
The value is read from and written to the launcher configuration through `load`/`save` delegates. The input
control is chosen once from `typeof(T)`, replacing the immediate-mode entry that switched on `typeof(T)`
every draw frame to pick an ImGui widget. Clicking the row name label focuses the row input, which makes
typeable rows (text, numeric, drop-down, check) reachable from the label as well as from the control itself.

## Declaration

`public class SettingsEntry<T> : SettingsEntry` (line 26), namespace `XIVLauncher.Core.Components.SettingsPage`.

## Members

| Member | Line | Notes |
| --- | --- | --- |
| label-width comment | 28-29 | doc comment: `DynamicLayout` stretches each row's controls to the tab width, so wrapped labels wrap at the column width; no explicit label widths are needed (the old `RowLabelWidth = 1000` const is gone) |
| `load` | 31 | `Func<T?>` supplied by the tab; reads the config value. |
| `save` | 32 | `Action<T?>` supplied by the tab; writes the config value. |
| `applyValue` | 33 | `Action`, defaults to no-op; pushes the current `InternalValue` into the input control. Subclasses replace it in `CreateInput()`. |
| `nameLabel` | 35, 71-75 | Label with the setting name, `WrapMode.Character`, no explicit width (stretched by the row layout). Primary mouse clicks focus `input` (79-86). Hidden for `bool` rows (line 198). |
| `input` | 36, 77 | The control created by `CreateInput()`. Focus target for `nameLabel` clicks. |
| `descriptionLabel` | 37, 88-93 | Grey (`EtoColors.Grey3`) description, wraps. |
| `validityLabel` | 38, 95-100 | Red (`EtoColors.Red`), wraps, starts hidden. |
| `warningLabel` | 39, 102-107 | Red, wraps, starts hidden. |
| `Content` | 109-120 | `DynamicLayout` with `Spacing = new Size(5, 5)` (111), name, input, description, validity, warning added in order (114-118), `this.Content = rowLayout` (120). |
| `Name` | 68 | Set once in the constructor. |
| `Description` | 69 | Set once in the constructor. |
| `CheckValidity` | 45 | `Func<T?, string?>?`, init-only; `null` message means valid. |
| `CheckWarning` | 47 | `Func<T?, string?>?`, init-only; non-null message shows the warning. |
| `CheckEnabled` | 49 | `Func<bool>?`, init-only; conditional gate — row shows disabled when false, re-evaluated live by `SettingsPage.RefreshAllStates`. |
| `CheckVisibility` | 53 | `Func<bool>?`, init-only; platform gate — row hidden at page build when false (doc comment lines 51-52). |
| `IsEnabled` | 55 | `CheckEnabled?.Invoke() ?? true`; evaluated on every read, never cached. |
| `IsVisible` | 57 | `CheckVisibility?.Invoke() ?? true`; read at page build (tab layout, search). |
| `RefreshState()` | 60 | `UpdateState(notifyChanged: false)` — gate re-evaluation without raising `Changed`. |
| `Value` | 62 | `InternalValue == default ? default : (T)InternalValue`. |
| Constructor | 64-120 | Stores delegates and name/description, builds labels, calls `CreateInput()`, wires `nameLabel.MouseDown`, assembles the row layout. |

## Control factories (`CreateInput()`, 125-250)

- `typeof(T) == typeof(DirectoryInfo)` (132-175): `FilePicker` with `FileAction = FileAction.SelectFolder`
  (line 135), a native folder chooser (GtkFileChooserAction.SelectFolders on GTK, Vista folder dialog on
  WinForms). A grey `pathLabel` (139-143) mirrors the full path as `Path: <path>` below the chooser
  button, which shows only the folder's name and is not editable on GTK; `ShowPath` (145-149) sets its text
  and hides it when empty.
  `FilePathChanged` (151-161) shows the path, stores `new DirectoryInfo(path)` when non-empty (159), `null` when
  empty, then `UpdateState()` (161). `applyValue` (164-169) seeds `FilePath` with `FullName` and mirrors it (168). The
  picker and label are stacked in a `DynamicLayout` with `Spacing = new Size(0, 2)` (171-173).
- `typeof(T) == typeof(string)` (178-190): `TextBox` with `MaxLength = 1000` (180), no explicit width.
  `TextChanged` (182-187) stores the text, then `UpdateState()` (185). `applyValue` (188) writes the string; `return textBox` (190).
- `typeof(T) == typeof(bool)` (193-208): `CheckBox` (`Text = this.Name`, 195) so the separate
  `nameLabel` is hidden (198). `CheckedChanged` (200-205) stores the bool, then `UpdateState()` (203). `applyValue` (206) writes
  `Checked`; `return checkBox` (208).
- `type.IsEnum` (211-248): read-only `ComboBox` (`ReadOnly = true`, line 220 — typing is blocked, the
  drop-down list is the only way to change it). `Enum.GetValues(type)` (213) is enumerated; items are the plain
  strings `FriendlyName + " - " + Description` from `SettingsDescriptionAttribute`
  (via `XIVLauncher.Common.Util` `GetAttribute`, line 215, with fallback `FriendlyName = enum name,
  Description = ""`). A `Description` of `""` or `"dummy"` renders no suffix. `keys` (223, `int[]`) keeps
  each item's numeric value at the same index. `SelectedIndexChanged` (232-239) boxes `keys[SelectedIndex]`
  with `Enum.ToObject` (237), stores, `UpdateState()` (238). `applyValue` (241-245) sets `SelectedIndex` to the enum's
  int value; `return comboBox` (247).
- Any other `T` throws `InvalidOperationException($"No settings input for {type}")` (line 250).

## State updates

`UpdateState(bool notifyChanged = true)` (253-279):
- First `this.Enabled = this.IsEnabled` (257): the row is never hidden by a conditional gate; a gated row
  renders disabled, cascading to its child controls (GTK container sensitivity, WinForms `Panel.Enabled`).
- With `CheckValidity`: computes message; `IsValid` is true when the message is null/empty; validity label
  text and visibility update.
- Without `CheckValidity`: `IsValid = true`, validity label hidden.
- Warning: `CheckWarning?.Invoke(Value)`; the warning label shows the message when non-null (275-276).
- `Changed` fires only when `notifyChanged` (278-279), so `RefreshState()` gate refreshes do not re-raise
  it — `SettingsPage.RefreshAllStates` runs it on every entry change.

`RefreshState()` (60) overrides the base: gate re-evaluation via `UpdateState(notifyChanged: false)`.

`LoadValues()` (282-288): `InternalValue = load()`, `applyValue()`, `UpdateState()` (287).

`Save()` (290): `save(Value)`.

## Consumers

Every settings tab constructs `SettingsEntry<T>` rows; `SettingsPage` subscribes each entry's `Changed`
event to `UpdateFinishButton` and `RefreshAllStates`, and rebuilds rows on search. Key consumers:

| Consumer | Lines | Usage |
| --- | --- | --- |
| `SettingsTabGame.cs` | 17-66 | `DirectoryInfo`, `string`, `ClientLanguage`, `DpiAwareness`, `bool` rows, game path validation, Unix-only config path (hidden on Windows), Steam row gate |
| `SettingsTabPatching.cs` | 12-23 | patch path + validation, speed limit, keep patches |
| `SettingsTabWine.cs` | 33-80 | Wine install/version/sync/DXVK enums (combo gates), WINEDEBUG string, DXVK overlay, Linux GameMode gate |
| `SettingsTabDalamud.cs` | 20-68 | enable Dalamud, load method enum, branch switcher, injection delay, manual injection, local path (gate 53) |
| `SettingsTabTroubleshooting.cs` | 20-53 | five `bool` fix rows (Linux-only tab, gates always true) |
| `SettingsTabDebug.cs` | 23 | `Entries` empty; debug tab uses header/footer labels/table, not settings rows |
| `NumericSettingsEntry.cs` | 19-51 | typed subclass: range fields, `NumericStepper` input, clamped `applyValue` |
| `DalamudBranchMetaSettingsEntry.cs` | 20-69 | typed subclass: async branch list, drop-down input, custom `applyValue` |
| `SettingsPage.cs` | 91-98, 205, 254 | `Changed` subscriptions (finish button + state refresh), search visibility filter, validity scan |

## Quirks

- `FilePicker` (the `DirectoryInfo` rows: Game path, Game config path, Patch path, Dalamud manual inject path)
  emits a GTK console warning on Linux: Eto 2.13.0's GTK `FilePickerHandler` mirrors the chooser's filename
  into its internal `Gtk.Entry` on every `selection-changed` emission
  (`src/Eto.Gtk/Forms/Controls/FilePickerHandler.cs:51` in the Eto repo), and `filebutton.Filename` is NULL
  until a folder is selected, so `gtk_entry_set_text` fires its `text != NULL` assertion. Observed counts
  (measured with `G_DEBUG=fatal-criticals`): ~1 critical when Settings opens, ~1 per tab tabulation. Seeding
  `picker.FilePath = string.Empty` makes it **worse** (`SetFilename("")` emits a selection-changed that the
  handler processes with a NULL filename: 4 extra criticals, one per picker row). The accepted fix is in
  `Program.LoadConfig`: `Config.DalamudManualInjectPath` defaults to
  `Path.Combine(storage.Root.FullName, "dalamud", "Hooks", "dev")` — all four `DirectoryInfo` rows then
  start with a non-empty path, and the measured critical count drops to **zero** (open + full tab cycle).
  No Eto release fixes the handler itself (bug present through 2.13.0).

- **Virtual `CreateInput()` runs before derived constructors.** The base constructor calls
  `CreateInput()` (line 77), so derived classes see uninitialised fields. `NumericSettingsEntry` uses this to
  repair the stepper range in its constructor.
- **Label clicks focus the input** (`MouseDown`, 79-86) for all typed rows, including the numeric row, text
  row, enum row, and the hidden-label check row. The handler checks primary button only and marks the event
  handled, so clicking the label should not leave keyboard focus on the default Patch Path control.
- **`bool` rows hide the name label** (line 198): the `CheckBox` carries the name as `Text`, so the row's
  name label occupies no space.
- **`applyValue` fires the input's change handler.** Writing text/checked/selected index raises
  `TextChanged`/`CheckedChanged`/`SelectedIndexChanged`, which re-store the same value and call
  `UpdateState()`, so loading a value raises `Changed` immediately. `SettingsPage` reacts by refreshing the
  finish button, which is idempotent for identical values.
- **Enum `applyValue` assumes `SelectedIndex == int value`.** The factory assumes the enum has dense values
  `0..n-1` in the same order as `Enum.GetValues`, so `SelectedIndex` equals the enum's numeric value.
  `ClientLanguage` and `DpiAwareness` satisfy this. A sparse enum (e.g. `[Flags]`) would select the wrong item.
- **`descriptions[value]` indexes by enum number** (line 227): same dense-enum assumption; an out-of-range
  number throws `IndexOutOfRange` when building items.
- **Two gates, two lifetimes.** `CheckVisibility` (platform, never-usable rows) is evaluated at page build
  only — the row is absent until a rebuild. `CheckEnabled` (conditional) is re-evaluated live: every entry
  `Changed` runs `SettingsPage.RefreshAllStates()`, which calls `RefreshState()` → `UpdateState(notifyChanged:
  false)` on every entry, so gates that read other entries' values flip `Enabled` without a page rebuild.
- **`Value` compares against `default`.** If `InternalValue` is `null`, `Value` returns `default(T)`; for
  reference types `T` this is `null`; for value types the zero value.
- **`DirectoryInfo` rows store the picked folder verbatim.** The folder chooser yields a path; it is wrapped
  in `DirectoryInfo` immediately, so a non-existent folder is stored as the value and reported by
  `CheckValidity`. The GTK chooser button displays only the folder's name, never the full path, and cannot be
  typed into; the grey `pathLabel` below the picker mirrors the full path, and typed paths work on Windows
  (WinForms/WPF text box + Browse) only.
- **Enum items are plain strings:** `ComboBox.Items` holds strings; the parallel `keys` array (int) maps
  `SelectedIndex` back to the enum value, boxed with `Enum.ToObject(type, int)`. Items must always be created
  in numeric order (index == enum value).
- **`ReadOnly = true` on `ComboBox`:** blocks typing in the text portion; selection via the list still works.
  `applyValue`'s `SelectedIndex` write fires `SelectedIndexChanged` and the store, for enums.
- **Labels have no explicit width.** The row `DynamicLayout` stretches every control to the tab width, so
  `WrapMode.Character` labels wrap at the column width; labels report no natural width when unwrapped, which
  is what kept the old explicit `RowLabelWidth = 1000` — the stretching layout replaces it. Rows follow the
  window width as the settings page shrinks (floor: the tab's 500 px `MinimumSize` floor, `SettingsTab.cs.md` §4).
- **The row `Panel` (`Component`) is the unit that search moves.** Re-parenting the row in search results
  moves the entire row, not the individual labels.
