# SettingsEntry{T}.cs

**File:** `src/XIVLauncher.Core/Components/SettingsPage/SettingsEntry{T}.cs` · 277 lines · 8,821 bytes

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
| `nameLabel` | 35, 62-66 | Label with the setting name, `WrapMode.Character`, no explicit width (stretched by the row layout). Primary mouse clicks focus `input` (70-77). Hidden for `bool` rows (line 168). |
| `input` | 36, 68 | The control created by `CreateInput()`. Focus target for `nameLabel` clicks. |
| `descriptionLabel` | 37, 79-84 | Grey (`EtoColors.Grey3`) description, wraps. |
| `validityLabel` | 38, 86-91 | Red (`EtoColors.Red`), wraps, starts hidden. |
| `warningLabel` | 39, 93-98 | Red, wraps, starts hidden. |
| `Content` | 100-111 | `DynamicLayout` with `Spacing = new Size(5, 5)` (100-103), name, input, description, validity, warning added in order (105-109), `this.Content = rowLayout` (111). |
| `Name` | 41 | Set once in the constructor. |
| `Description` | 43 | Set once in the constructor. |
| `CheckValidity` | 45 | `Func<T?, string?>?`, init-only; `null` message means valid. |
| `CheckWarning` | 47 | `Func<T?, string?>?`, init-only; non-null message shows the warning. |
| `CheckVisibility` | 49 | `Func<bool>?`, init-only. |
| `IsVisible` | 51 | `CheckVisibility?.Invoke() ?? true`; evaluated on every read, never cached. |
| `Value` | 53 | `InternalValue == default ? default : (T)InternalValue`. |
| Constructor | 55-112 | Stores delegates and name/description, builds labels, calls `CreateInput()`, wires `nameLabel.MouseDown`, assembles the row layout. |

## Control factories (`CreateInput()`, 116-242)

- `typeof(T) == typeof(DirectoryInfo)` (120-166): `FilePicker` with `FileAction = FileAction.SelectFolder`
  (line 126), a native folder chooser (GtkFileChooserAction.SelectFolders on GTK, Vista folder dialog on
  WinForms). A grey `pathLabel` (130-134) mirrors the full path as `Path: <path>` below the chooser
  button, which shows only the folder's name and is not editable on GTK; `ShowPath` (136-140) sets its text
  and hides it when empty.
  `FilePathChanged` (142-151) shows the path, stores `new DirectoryInfo(path)` when non-empty, `null` when
  empty, then `UpdateState()`. `applyValue` (155-160) seeds `FilePath` with `FullName` and mirrors it. The
  picker and label are stacked in a `DynamicLayout` with `Spacing = new Size(0, 2)` (162-164).
- `typeof(T) == typeof(string)` (169-182): `TextBox` with `MaxLength = 1000`, no explicit width.
  `TextChanged` stores the text, then `UpdateState()`. `applyValue` writes the string.
- `typeof(T) == typeof(bool)` (184-200): `CheckBox` whose `Text` is the setting name, so the separate
  `nameLabel` is hidden. `CheckedChanged` stores the bool, then `UpdateState()`. `applyValue` writes
  `Checked`.
- `type.IsEnum` (202-238): read-only `ComboBox` (`ReadOnly = true`, line 211 — typing is blocked, the
  drop-down list is the only way to change it). `Enum.GetValues(type)` is enumerated; items are the plain
  strings `FriendlyName + " - " + Description` from `SettingsDescriptionAttribute`
  (via `XIVLauncher.Common.Util` `GetAttribute`, line 206, with fallback `FriendlyName = enum name,
  Description = ""`). A `Description` of `""` or `"dummy"` renders no suffix. `keys` (213, `int[]`) keeps
  each item's numeric value at the same index. `SelectedIndexChanged` (223-230) boxes `keys[SelectedIndex]`
  with `Enum.ToObject`, stores, `UpdateState()`. `applyValue` (232-236) sets `SelectedIndex` to the enum's
  int value.
- Any other `T` throws `InvalidOperationException($"No settings input for {type}")` (line 241).

## State updates

`UpdateState()` (244-266):
- With `CheckValidity`: computes message; `IsValid` is true when the message is null/empty; validity label
  text and visibility update.
- Without `CheckValidity`: `IsValid = true`, validity label hidden.
- Warning: `CheckWarning?.Invoke(Value)`; the warning label shows the message when non-null.
- Finally `RaiseChanged()`, which notifies the settings page.

`LoadValues()` (268-274): `InternalValue = load()`, `applyValue()`, `UpdateState()`.

`Save()` (276): `save(Value)`.

## Consumers

Every settings tab constructs `SettingsEntry<T>` rows; `SettingsPage` subscribes each entry's `Changed`
event to `UpdateFinishButton` and rebuilds rows on search. Key consumers:

| Consumer | Lines | Usage |
| --- | --- | --- |
| `SettingsTabGame.cs` | 17-66 | `DirectoryInfo`, `string`, `ClientLanguage`, `DpiAwareness`, `bool` rows, game path validation, Unix-only config path, Steam row visibility |
| `SettingsTabPatching.cs` | 12-23 | patch path + validation, speed limit, keep patches |
| `SettingsTabWine.cs` | 33-69 | Wine install/version/sync/DXVK enums, WINEDEBUG string, DXVK overlay |
| `SettingsTabDalamud.cs` | 20-68 | enable Dalamud, load method enum, branch switcher, injection delay, manual injection, local path |
| `SettingsTabTroubleshooting.cs` | 20-53 | five `bool` fix rows |
| `SettingsTabDebug.cs` | 23 | `Entries` empty; debug tab uses header/footer labels/table, not settings rows |
| `NumericSettingsEntry.cs` | 19-51 | typed subclass: range fields, `NumericStepper` input, clamped `applyValue` |
| `DalamudBranchMetaSettingsEntry.cs` | 20-69 | typed subclass: async branch list, drop-down input, custom `applyValue` |
| `SettingsPage.cs` | 90-92, 189, 199, 239 | `Changed` subscriptions, search matching, visibility filtering, validity scan |

## Quirks

- **Virtual `CreateInput()` runs before derived constructors.** The base constructor calls
  `CreateInput()` (line 68), so derived classes see uninitialised fields. `NumericSettingsEntry` uses this to
  repair the stepper range in its constructor.
- **Label clicks focus the input** (`MouseDown`, 70-77) for all typed rows, including the numeric row, text
  row, enum row, and the hidden-label check row. The handler checks primary button only and marks the event
  handled, so clicking the label should not leave keyboard focus on the default Patch Path control.
- **`bool` rows hide the name label** (line 189): the `CheckBox` carries the name as `Text`, so the row's
  name label occupies no space.
- **`applyValue` fires the input's change handler.** Writing text/checked/selected index raises
  `TextChanged`/`CheckedChanged`/`SelectedIndexChanged`, which re-store the same value and call
  `UpdateState()`, so loading a value raises `Changed` immediately. `SettingsPage` reacts by refreshing the
  finish button, which is idempotent for identical values.
- **Enum `applyValue` assumes `SelectedIndex == int value`.** The factory assumes the enum has dense values
  `0..n-1` in the same order as `Enum.GetValues`, so `SelectedIndex` equals the enum's numeric value.
  `ClientLanguage` and `DpiAwareness` satisfy this. A sparse enum (e.g. `[Flags]`) would select the wrong item.
- **`descriptions[value]` indexes by enum number** (line 213): same dense-enum assumption; an out-of-range
  number throws `IndexOutOfRange` when building items.
- **`IsVisible` is evaluated on every read.** `CheckVisibility` runs in `SettingsPage`, tab layout, search
  rebuild, finish-button scan; the result is only acted on at rebuild time.
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
