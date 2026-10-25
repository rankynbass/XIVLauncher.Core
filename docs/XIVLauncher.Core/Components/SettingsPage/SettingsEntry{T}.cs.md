# SettingsEntry{T}.cs

**File:** `src/XIVLauncher.Core/Components/SettingsPage/SettingsEntry{T}.cs` · 240 lines · 7,516 bytes

## Purpose

The typed settings row: one label, one input control, one description, plus validity and warning labels.
The value is read from and written to the launcher configuration through `load`/`save` delegates. The input
control is chosen once from `typeof(T)`, replacing the immediate-mode entry that switched on `typeof(T)`
every draw frame to pick an ImGui widget. Clicking the row name label focuses the row input, which makes
typeable rows (text, numeric, drop-down, check) reachable from the label as well as from the control itself.

## Declaration

`public class SettingsEntry<T> : SettingsEntry` (line 23), namespace `XIVLauncher.Core.Components.SettingsPage`.

## Members

| Member | Line | Notes |
| --- | --- | --- |
| `RowLabelWidth` | 27 | `const int` = 1000, used by every row label. GTK reports unwrapped label text as natural width, so labels need an explicit width to wrap inside the page. |
| `load` | 29 | `Func<T?>` supplied by the tab; reads the config value. |
| `save` | 30 | `Action<T?>` supplied by the tab; writes the config value. |
| `applyValue` | 31 | `Action`, defaults to no-op; pushes the current `InternalValue` into the input control. Subclasses replace it in `CreateInput()`. |
| `nameLabel` | 33, 60-65 | Label with the setting name, `WrapMode.Character`, width 1000. Primary mouse clicks focus `input` (69-76). Hidden for `bool` rows (line 157). |
| `input` | 34, 67 | The control created by `CreateInput()`. Focus target for `nameLabel` clicks. |
| `descriptionLabel` | 35, 78-84 | Grey (`EtoColors.Grey3`) description, wraps, width 1000. |
| `validityLabel` | 36, 86-92 | Red (`EtoColors.Red`), wraps, width 1000, starts hidden. |
| `warningLabel` | 37, 94-100 | Red, wraps, width 1000, starts hidden. |
| `Content` | 102-113 | Vertical `StackLayout` spacing 5: name, input, description, validity, warning. |
| `Name` | 39 | Set once in the constructor. |
| `Description` | 41 | Set once in the constructor. |
| `CheckValidity` | 43 | `Func<T?, string?>?`, init-only; `null` message means valid. |
| `CheckWarning` | 45 | `Func<T?, string?>?`, init-only; non-null message shows the warning. |
| `CheckVisibility` | 47 | `Func<bool>?`, init-only. |
| `IsVisible` | 49 | `CheckVisibility?.Invoke() ?? true`; evaluated on every read, never cached. |
| `Value` | 51 | `InternalValue == default ? default : (T)InternalValue`. |
| Constructor | 53-114 | Stores delegates and name/description, builds labels, calls `CreateInput()`, wires `nameLabel.MouseDown`, assembles the row layout. |

## Control factories (`CreateInput()`, 118-205)

- `typeof(T) == typeof(DirectoryInfo)` (122-135): `TextBox` with `MaxLength = 10000` and `Width = RowLabelWidth`.
  `TextChanged` stores `new DirectoryInfo(text)` when non-empty, `null` when empty, then `UpdateState()`.
  `applyValue` writes `FullName` (or empty string).
- `typeof(T) == typeof(string)` (137-150): `TextBox` with `MaxLength = 1000` and `Width = RowLabelWidth`.
  `TextChanged` stores the text, then `UpdateState()`. `applyValue` writes the string.
- `typeof(T) == typeof(bool)` (152-168): `CheckBox` whose `Text` is the setting name, so the separate
  `nameLabel` is hidden. `CheckedChanged` stores the bool, then `UpdateState()`. `applyValue` writes
  `Checked`.
- `type.IsEnum` (170-202): `DropDown`. `Enum.GetValues(type)` is enumerated; each item's text is
  `FriendlyName + " - " + Description` from `SettingsDescriptionAttribute`
  (via `XIVLauncher.Common.Util` `GetAttribute`, line 174, with fallback `FriendlyName = enum name,
  Description = ""`). A `Description` of `""` or `"dummy"` renders no suffix. `SelectedIndexChanged` parses the
  selected item's `Key` (the numeric value string), boxes it with `Enum.ToObject`, stores, `UpdateState()`.
  `applyValue` sets `SelectedIndex` to the enum's int value.
- Any other `T` throws `InvalidOperationException($"No settings input for {type}")` (line 204).

## State updates

`UpdateState()` (207-229):
- With `CheckValidity`: computes message; `IsValid` is true when the message is null/empty; validity label
  text and visibility update.
- Without `CheckValidity`: `IsValid = true`, validity label hidden.
- Warning: `CheckWarning?.Invoke(Value)`; the warning label shows the message when non-null.
- Finally `RaiseChanged()`, which notifies the settings page.

`LoadValues()` (231-237): `InternalValue = load()`, `applyValue()`, `UpdateState()`.

`Save()` (239): `save(Value)`.

## Consumers

Every settings tab constructs `SettingsEntry<T>` rows; `SettingsPage` subscribes each entry's `Changed`
event to `UpdateFinishButton` and rebuilds rows on search. Key consumers:

| Consumer | Lines | Usage |
| --- | --- | --- |
| `SettingsTabGame.cs` | 14-70 | `DirectoryInfo`, `string`, `ClientLanguage`, `DpiAwareness`, `bool` rows, game path validation, Unix-only config path, Steam row visibility |
| `SettingsTabPatching.cs` | 12-23 | patch path + validation, speed limit, keep patches |
| `SettingsTabWine.cs` | 33-69 | Wine install/version/sync/DXVK enums, WINEDEBUG string, DXVK overlay |
| `SettingsTabDalamud.cs` | 20-68 | enable Dalamud, load method enum, branch switcher, injection delay, manual injection, local path |
| `SettingsTabTroubleshooting.cs` | 20-53 | five `bool` fix rows |
| `SettingsTabDebug.cs` | 23 | `Entries` empty; debug tab uses header/footer labels/table, not settings rows |
| `NumericSettingsEntry.cs` | 19-51 | typed subclass: range fields, `NumericStepper` input, clamped `applyValue` |
| `DalamudBranchMetaSettingsEntry.cs` | 20-69 | typed subclass: async branch list, drop-down input, custom `applyValue` |
| `SettingsPage.cs` | 69-71, 126, 134-136, 150 | `Changed` subscriptions, search matching, visibility filtering, validity scan |

## Quirks

- **Virtual `CreateInput()` runs before derived constructors.** The base constructor calls
  `CreateInput()` (line 67), so derived classes see uninitialised fields. `NumericSettingsEntry` uses this to
  repair the stepper range in its constructor.
- **Label clicks focus the input** (`MouseDown`, 69-76) for all typed rows, including the numeric row, text
  row, enum row, and the hidden-label check row. The handler checks primary button only and marks the event
  handled, so clicking the label should not leave keyboard focus on the default Patch Path control.
- **`bool` rows hide the name label** (line 157): the `CheckBox` carries the name as `Text`, so the row's
  name label occupies no space.
- **`applyValue` fires the input's change handler.** Writing text/checked/selected index raises
  `TextChanged`/`CheckedChanged`/`SelectedIndexChanged`, which re-store the same value and call
  `UpdateState()`, so loading a value raises `Changed` immediately. `SettingsPage` reacts by refreshing the
  finish button, which is idempotent for identical values.
- **Enum `applyValue` assumes `SelectedIndex == int value`.** The factory assumes the enum has dense values
  `0..n-1` in the same order as `Enum.GetValues`, so `SelectedIndex` equals the enum's numeric value.
  `ClientLanguage` and `DpiAwareness` satisfy this. A sparse enum (e.g. `[Flags]`) would select the wrong item.
- **`descriptions[value]` indexes by enum number** (line 181): same dense-enum assumption; an out-of-range
  number throws `IndexOutOfRange` when building items.
- **`IsVisible` is evaluated on every read.** `CheckVisibility` runs in `SettingsPage`, tab layout, search
  rebuild, finish-button scan; the result is only acted on at rebuild time.
- **`Value` compares against `default`.** If `InternalValue` is `null`, `Value` returns `default(T)`; for
  reference types `T` this is `null`; for value types the zero value.
- **`DirectoryInfo` text boxes accept any text, not paths.** Free text is wrapped in `DirectoryInfo`
  immediately, so a bogus path is stored as the value and reported by `CheckValidity`.
- **Enum `Key` is a numeric string**, parsed with `int.Parse`; items must always be created in numeric order.
- **Labels wrap at 1000 px**, wider than the settings page area (the window is 1280 px with margins), so
  long names/descriptions wrap once near the right edge and rows keep full width.
- **The row `Panel` (`Component`) is the unit that search moves.** Re-parenting the row in search results
  moves the entire row, not the individual labels.
