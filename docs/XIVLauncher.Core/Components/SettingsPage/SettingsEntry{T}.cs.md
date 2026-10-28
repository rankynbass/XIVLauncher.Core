# SettingsEntry{T}.cs

**File:** `src/XIVLauncher.Core/Components/SettingsPage/SettingsEntry{T}.cs` · 235 lines · 7,312 bytes

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
| label-width comment | 25-26 | doc comment: `DynamicLayout` stretches each row's controls to the tab width, so wrapped labels wrap at the column width; no explicit label widths are needed (the old `RowLabelWidth = 1000` const is gone) |
| `load` | 28 | `Func<T?>` supplied by the tab; reads the config value. |
| `save` | 29 | `Action<T?>` supplied by the tab; writes the config value. |
| `applyValue` | 30 | `Action`, defaults to no-op; pushes the current `InternalValue` into the input control. Subclasses replace it in `CreateInput()`. |
| `nameLabel` | 32, 59-63 | Label with the setting name, `WrapMode.Character`, no explicit width (stretched by the row layout). Primary mouse clicks focus `input` (67-74). Hidden for `bool` rows (line 152). |
| `input` | 33, 65 | The control created by `CreateInput()`. Focus target for `nameLabel` clicks. |
| `descriptionLabel` | 34, 76-81 | Grey (`EtoColors.Grey3`) description, wraps. |
| `validityLabel` | 35, 83-88 | Red (`EtoColors.Red`), wraps, starts hidden. |
| `warningLabel` | 36, 90-95 | Red, wraps, starts hidden. |
| `Content` | 97-108 | `DynamicLayout` with `Spacing = new Size(5, 5)` (97-100), name, input, description, validity, warning added in order (102-106), `this.Content = rowLayout` (108). |
| `Name` | 38 | Set once in the constructor. |
| `Description` | 40 | Set once in the constructor. |
| `CheckValidity` | 42 | `Func<T?, string?>?`, init-only; `null` message means valid. |
| `CheckWarning` | 44 | `Func<T?, string?>?`, init-only; non-null message shows the warning. |
| `CheckVisibility` | 46 | `Func<bool>?`, init-only. |
| `IsVisible` | 48 | `CheckVisibility?.Invoke() ?? true`; evaluated on every read, never cached. |
| `Value` | 50 | `InternalValue == default ? default : (T)InternalValue`. |
| Constructor | 52-109 | Stores delegates and name/description, builds labels, calls `CreateInput()`, wires `nameLabel.MouseDown`, assembles the row layout. |

## Control factories (`CreateInput()`, 113-200)

- `typeof(T) == typeof(DirectoryInfo)` (117-130): `TextBox` with `MaxLength = 10000`, no explicit width (the row layout stretches it).
  `TextChanged` stores `new DirectoryInfo(text)` when non-empty, `null` when empty, then `UpdateState()`.
  `applyValue` writes `FullName` (or empty string).
- `typeof(T) == typeof(string)` (132-145): `TextBox` with `MaxLength = 1000`, no explicit width.
  `TextChanged` stores the text, then `UpdateState()`. `applyValue` writes the string.
- `typeof(T) == typeof(bool)` (147-163): `CheckBox` whose `Text` is the setting name, so the separate
  `nameLabel` is hidden. `CheckedChanged` stores the bool, then `UpdateState()`. `applyValue` writes
  `Checked`.
- `type.IsEnum` (165-197): `DropDown`. `Enum.GetValues(type)` is enumerated; each item's text is
  `FriendlyName + " - " + Description` from `SettingsDescriptionAttribute`
  (via `XIVLauncher.Common.Util` `GetAttribute`, line 169, with fallback `FriendlyName = enum name,
  Description = ""`). A `Description` of `""` or `"dummy"` renders no suffix. `SelectedIndexChanged` parses the
  selected item's `Key` (the numeric value string), boxes it with `Enum.ToObject`, stores, `UpdateState()`.
  `applyValue` sets `SelectedIndex` to the enum's int value.
- Any other `T` throws `InvalidOperationException($"No settings input for {type}")` (line 199).

## State updates

`UpdateState()` (202-224):
- With `CheckValidity`: computes message; `IsValid` is true when the message is null/empty; validity label
  text and visibility update.
- Without `CheckValidity`: `IsValid = true`, validity label hidden.
- Warning: `CheckWarning?.Invoke(Value)`; the warning label shows the message when non-null.
- Finally `RaiseChanged()`, which notifies the settings page.

`LoadValues()` (226-232): `InternalValue = load()`, `applyValue()`, `UpdateState()`.

`Save()` (234): `save(Value)`.

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
  `CreateInput()` (line 65), so derived classes see uninitialised fields. `NumericSettingsEntry` uses this to
  repair the stepper range in its constructor.
- **Label clicks focus the input** (`MouseDown`, 67-74) for all typed rows, including the numeric row, text
  row, enum row, and the hidden-label check row. The handler checks primary button only and marks the event
  handled, so clicking the label should not leave keyboard focus on the default Patch Path control.
- **`bool` rows hide the name label** (line 152): the `CheckBox` carries the name as `Text`, so the row's
  name label occupies no space.
- **`applyValue` fires the input's change handler.** Writing text/checked/selected index raises
  `TextChanged`/`CheckedChanged`/`SelectedIndexChanged`, which re-store the same value and call
  `UpdateState()`, so loading a value raises `Changed` immediately. `SettingsPage` reacts by refreshing the
  finish button, which is idempotent for identical values.
- **Enum `applyValue` assumes `SelectedIndex == int value`.** The factory assumes the enum has dense values
  `0..n-1` in the same order as `Enum.GetValues`, so `SelectedIndex` equals the enum's numeric value.
  `ClientLanguage` and `DpiAwareness` satisfy this. A sparse enum (e.g. `[Flags]`) would select the wrong item.
- **`descriptions[value]` indexes by enum number** (line 176): same dense-enum assumption; an out-of-range
  number throws `IndexOutOfRange` when building items.
- **`IsVisible` is evaluated on every read.** `CheckVisibility` runs in `SettingsPage`, tab layout, search
  rebuild, finish-button scan; the result is only acted on at rebuild time.
- **`Value` compares against `default`.** If `InternalValue` is `null`, `Value` returns `default(T)`; for
  reference types `T` this is `null`; for value types the zero value.
- **`DirectoryInfo` text boxes accept any text, not paths.** Free text is wrapped in `DirectoryInfo`
  immediately, so a bogus path is stored as the value and reported by `CheckValidity`.
- **Enum `Key` is a numeric string**, parsed with `int.Parse`; items must always be created in numeric order.
- **Labels have no explicit width.** The row `DynamicLayout` stretches every control to the tab width, so
  `WrapMode.Character` labels wrap at the column width; labels report no natural width when unwrapped, which
  is what kept the old explicit `RowLabelWidth = 1000` — the stretching layout replaces it. Rows follow the
  window width as the settings page shrinks (floor: the tab's 500 px `MinimumSize` floor, `SettingsTab.cs.md` §4).
- **The row `Panel` (`Component`) is the unit that search moves.** Re-parenting the row in search results
  moves the entire row, not the individual labels.
