# DalamudBranchMetaSettingsEntry.cs

**File:** `src/XIVLauncher.Core/Components/SettingsPage/DalamudBranchMetaSettingsEntry.cs` · 82 lines · 2,913 bytes

## Purpose

A settings row listing the Dalamud branches. The branch list arrives from the network, so the drop-down is
created disabled and is populated/enabled when the fetch finishes. Replaces the ImGui branch combo, which
checked the load task's completion state every frame and disabled itself until it finished.

## Declaration

`public class DalamudBranchMetaSettingsEntry : SettingsEntry<string>` (line 15), namespace
`XIVLauncher.Core.Components.SettingsPage`. Constructor
`(string name, string description, Func<string> load, Action<string?> save)` (lines 20-21).

## Members

| Member | Line | Behavior |
| --- | --- | --- |
| `dropDown` | 17 | Eto `DropDown`, created once as a field initializer. |
| `Branches` | 18 | `List<DalamudBranchMeta.Branch>`, starts empty, replaced with fetched branches. |
| Constructor | 20-41 | `dropDown.Enabled = false` (line 23); `Task.Run` fetches branches (line 25-27); `Ui.Post` populates items, selects current track, enables drop-down (29-39). |
| `CreateInput()` | 43-69 | Supplies `applyValue` (47-51), wires `SelectedIndexChanged` (53-66), returns `dropDown` (68). |
| `Save()` | 71-81 | Looks up the selected branch, writes `Program.Config.DalamudBetaKind`/`DalamudBetaKey`, then `Program.DalamudUpdater.Run(track, key)`. |

Constructor details:
- The base constructor runs first, calling the overridden `CreateInput()`; that override is where
  `applyValue` is assigned, because `SettingsEntry<T>.LoadValues()` calls `applyValue` unconditionally
  (comment lines 45-46).
- `applyValue` finds the index of the first non-hidden branch whose `Track` equals `InternalValue` and
  assigns `dropDown.SelectedIndex`. Before the fetch completes, `Branches` is empty, `FindIndex` returns `-1`
  (no selection).
- `SelectedIndexChanged` reads the selected item's `Key`, finds the matching branch, stores `branch.Track` as
  `InternalValue`, and calls `UpdateState()`.
- `Ui.Post` callback: items are added in `Branches` order, filtered by `!Hidden`, using `branch.DisplayName`
  as text and `branch.Track` as key; selection is applied; then `Enabled = true`.

`Save()` runs the Dalamud updater immediately for the selected branch: `Program.DalamudUpdater.Run(...)`
(line 80, `Program.DalamudUpdater` defined at `Program.cs:41`, created at `Program.cs:153-164`).

## Consumers

`SettingsTabDalamud.cs:24` constructs the entry:

```csharp
new DalamudBranchMetaSettingsEntry(Strings.DalamudBranchSwitcher, Strings.DalamudBranchSwitcherDescription,
    () => Program.Config.DalamudBetaKind ?? "release", s => { })
```
`Strings.DalamudBranchSwitcher` at `Strings.Designer.cs:672`,
`Strings.DalamudBranchSwitcherDescription` at `Strings.Designer.cs:678`. The `save` delegate is empty:
saving happens through `Save()`'s side effects. `SettingsPage.OnShow()` calls `LoadValues()` on it
(`SettingsPage.cs:140`), the page saves it through `tab.Save()` (finish button, `SettingsPage.cs:82-84`).

Base behavior: `SettingsEntry<string>`, `SettingsEntry`, `Component`, `ListItem`, `Ui.Post`, `Program.HttpClient`,
`DalamudBranchMeta.FetchBranchesAsync`, `DalamudBranchMeta.Branch` (`DisplayName`, `Track`, `Key`, `Hidden`
in `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Dalamud/DalamudBranchMeta.cs:12-28`).

## Quirks

- **The drop-down is a field, not created inside `CreateInput()`.** `dropDown` is initialized as a field
  (line 17) and returned by `CreateInput()`, so its `Enabled = false` and event wiring order depends on the
  base constructor running `CreateInput()` before the constructor body sets `Enabled = false` (line 23).
  Field initializers run before the base constructor body, so `CreateInput()` receives the enabled-by-default
  control; line 23 disables it immediately after.
- **`applyValue` selects `-1` before branches arrive.** `LoadValues()` runs during `SettingsPage.OnShow()`;
  if the fetch has not finished, `FindIndex` returns `-1` and `SelectedIndex = -1` clears selection.
- **The fetch is unbounded and unobserved.** `Task.Run` has no `await`, no exception handling; a failed fetch
  leaves the drop-down permanently disabled and the row unusable (HEAD checked the task state per frame).
- **Hidden branches are filtered from items but not from lookup.** `Save()`'s `Find` searches all branches
  (including hidden ones), so a value loaded from config that points at a hidden branch can still be saved
  and run.
- **`SelectedIndexChanged` fires when `applyValue` sets `SelectedIndex`.** Setting the selection raises the
  handler, which re-stores the same `Track` and calls `UpdateState()`, raising `Changed` for the page.
- **Selection matching uses `Track` equality.** Items store `Key = branch.Track` (line 34), so item keys and
  `InternalValue` are the same string.
- **`Save()` triggers a network/update side effect at save time** (`Program.DalamudUpdater.Run`), not just
  config writes. The updater runs on the finish button's UI thread.
- **The `save` delegate is a no-op** (`s => { }` at `SettingsTabDalamud.cs:24`): `SettingsEntry.Save()` calls
  it, and this override performs the real work.
