# DalamudBranchMetaSettingsEntry.cs — Specification

**File:** `src/XIVLauncher.Core/Components/SettingsPage/DalamudBranchMetaSettingsEntry.cs` · 57 lines · 2137 bytes
**Namespace:** `XIVLauncher.Core.Components.SettingsPage` · **Declaration:** `public class DalamudBranchMetaSettingsEntry : SettingsEntry<string>`
**Role:** A hand-written row: switch the Dalamud "beta branch" by picking a live branch list fetched over HTTP, and trigger the updater on save

---

## 1. Purpose

`T = string` would make this a plain text field (`SettingsEntry{T}.cs.md` §3, `string` branch → `InputText`, max 1000). It is not: the class overrides `Draw()` to replace that widget with an `ImGui.Combo` of the branch list published by `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Dalamud/DalamudBranchMeta.cs`, and overrides `Save()` to do the extra work picking a branch implies (write two config fields, run the updater). It is the only entry class in the package that is neither generic nor a thin numeric wrapper — the template for "a row whose value is chosen from a live remote list and whose save has side effects".

## 2. Surface

| Member | Anchor | Form | Notes |
| --- | --- | --- | --- |
| `SelectedBranch` | `:9` | `private DalamudBranchMeta.Branch? { get; set; }` | set only by a combo click; read only by `Save()` |
| `BranchTask` | `:10` | `private Task<IEnumerable<Branch>> { get; init; }` | assigned in the ctor (`:16`); the readiness gate |
| `Branches` | `:11` | `private List<Branch> { get; set; } = []` | flattened result of the fetch, initialised to empty list |
| ctor | `:13-:17` | `(name, description, Func<string> load, Action<string?> save) : base(...)` | note `Func<string>` (non-nullable return) vs `SettingsEntry<string>`'s `Func<string?>` |
| `LoadBranchesAsync()` | `:19-:24` | `private async Task<IEnumerable<Branch>>` | `await DalamudBranchMeta.FetchBranchesAsync(Program.HttpClient)`, then `this.Branches = [.. branches]` |
| `Draw()` | `:26-:48` | `override` | §2 — **does not** call `base.Draw()` |
| `Save()` | `:50-:56` | `override` | §3 |
| inherited | — | `Load()`, `Value`, `Name`, `Description`, `InternalValue`, `IsValid`, `Id` | from `SettingsEntry{T}.cs.md` / `SettingsEntry.cs.md` / `Component.cs.md` |

## 3. Construction — the fetch is one-shot per process

```csharp
public DalamudBranchMetaSettingsEntry(string name, …) : base(name, description, load, save)
{
    this.BranchTask = this.LoadBranchesAsync();   // :16
}
```

`BranchTask` is an `init` accessor (`SettingsEntry{T}.cs.md` §`Check*` restriction is the same mechanism) and the ctor *starts* the async fetch without awaiting. Where construction happens decides when the fetch happens: the entry is created in `SettingsTabDalamud.cs.md` §ctor (the `Entries` array literal), which is constructed by `SettingsPage.cs.md` §tabs (`private readonly SettingsTab[] tabs = { … }`), which is constructed at `LauncherApp.cs:135 this.setPage = new SettingsPage(this);`. So:

- **one fetch per launcher process**, at `LauncherApp` construction — `SettingsPage.cs.md` §OnShow calling `tab.Load()` does **not** re-fetch (the entry's `Load()` is the inherited generic one, which only reads `this.load()`), and there is no retry path. A branch list can therefore never become fresher within a session; a fresh list needs a process restart.
- **The task is never awaited**, only inspected via `IsCompletedSuccessfully` (`:29`). A *faulted* fetch therefore leaves `BranchTask.IsCompletedSuccessfully == false` forever and the exception is never surfaced (no `await`, no `ContinueWith` — contrast `MainPage.cs.md` §ProcessLogin, where `ContinueWith` + `App.HandleContinuationBlocking` is the reporting channel). Observable symptom: the combo stays disabled and `Branches` stays `[]` with nothing logged. If error reporting is ever added, add it *inside* `LoadBranchesAsync()`'s caller chain (a `ContinueWith` on the assignment) rather than in `Draw()`, which runs every frame.
- `Branches` is pre-initialised to `[]` (`:11`), which is what makes the pre-combo `Find` (`:30`) safe while the fetch is still in flight — keep the initializer, since `Draw()` reads `Branches` before ever reading `BranchTask`'s result.

## 4. `Draw()`

```csharp
ImGuiHelpers.TextWrapped(this.Name);
ImGui.BeginDisabled(!this.BranchTask.IsCompletedSuccessfully);            // :29
var currentBranch = this.Branches.Find(b => b.Track == this.InternalValue as string);
if (ImGui.BeginCombo($"###{this.Id}", currentBranch?.DisplayName))         // :31
{
    foreach (var branch in this.Branches)
    {
        if (branch.Hidden) continue;                                      // :35
        if (ImGui.Selectable(branch.DisplayName, branch.Track == currentBranch?.Track))
        {
            this.SelectedBranch = branch;                                  // :38
            this.InternalValue = branch.Track;                             // :39
        }
    }
    ImGui.EndCombo();
}
ImGui.EndDisabled();                                                      // :44
ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
ImGuiHelpers.TextWrapped(this.Description);
ImGui.PopStyleColor();
```

- **The combo replaces the text field**, so the value can only be a `Track` that is currently published — the free-text escape hatch of `SettingsEntry{T}.cs.md` §`string` branch is deliberately gone. `Program.Config.DalamudBetaKind` (the `load` lambda in `Tabs/SettingsTabDalamud.cs.md`, see that spec's row) can therefore hold a value the UI will never offer again; the `Find` above still resolves it to a `Branch` when the list contains it, and to `null` when it doesn't (combo display = `currentBranch?.DisplayName` → `null`, no `Selectable` marked) — i.e. a stale persisted branch shows as "nothing selected", not as an error row.
- **`BeginDisabled`/`EndDisabled` are balanced across branches** (`:29` before the `if`, `:44` after it, both unconditional), so the disabled state wraps the whole combo. Contrast the unbalanced-looking pair in `Tabs/SettingsTabWine.cs.md` §Draw (conditional `EndDisabled` guarded by the same condition) — if you add a nesting level here, keep this pair unconditional, because ImGui's disabled stack is positional.
- **`Hidden` branches are unselectable but still resolvable**: `Find` runs over the full `Branches`, so a persisted hidden `Track` is still recognized as "current". That is what keeps a user on a hidden branch from appearing to have lost their setting, and it is why the filter is on `Selectable` only, not on the list.
- **`this.Id` is used here** (`Component.cs.md` §`Id`, protected accessor), formatted with the default `Guid.ToString()` form — unlike `SettingsEntry{T}.cs.md` §id note (`Id.ToString()`) this class writes `$"###{this.Id}"` without `.ToString()`, and unlike `NumericSettingsEntry.cs.md` (own `id`, `:N` format). All three are per-instance and unique; do not treat the textual form as an addressable key.
- **`IsValid` is never recomputed in `Draw()`** because `base.Draw()` (`SettingsEntry{T}.cs.md` §3 tail) is not called: the row's validity is whatever `Load()` left (`SettingsEntry{T}.cs.md` §4: `true` when no `CheckValidity` is supplied, and `Tabs/SettingsTabDalamud.cs.md` supplies none). Consequence: this row can never block `SettingsPage.cs.md` §finish button. If a validity check is ever wanted for the branch switcher, it needs its own `IsValid` assignment *and* its own red text (the class re-implements the description block at `:45-:47` instead of inheriting it — that duplication is the cost of skipping `base.Draw()`).

## 5. `Save()` — the row's side effect

```csharp
public override void Save()
{
    if (this.SelectedBranch is null) return;
    Program.Config.DalamudBetaKind = SelectedBranch.Track;
    Program.Config.DalamudBetaKey = SelectedBranch.Key;
    Program.DalamudUpdater.Run(Program.Config.DalamudBetaKind, Program.Config.DalamudBetaKey);
}
```

- **Two truths, one write path**: `SelectedBranch` (UI-selected branch) and `InternalValue` (the `Track` string, `:39`) are written together at `:38-:39` and are the only writers; `Save()` reads `SelectedBranch`, `Load()` reads/writes `InternalValue`. Therefore **`Save()` is a no-op unless the user clicked a branch in this session** — including the case where the correct branch was already selected: the combo's *default* selected item does not fire `ImGui.Selectable`'s return, so "leave the setting as it is" never runs the updater. That is the row's contract (updater runs only on an explicit switch), and it is why the call site passes an **empty save lambda** (`Tabs/SettingsTabDalamud.cs.md` §`new DalamudBranchMetaSettingsEntry(…, () => Program.Config.DalamudBetaKind ?? "release", s => {})` — the `s => {}` exists only to satisfy the ctor's `Action<string?> save` parameter, since `Save()` is overridden and never forwards to `this.save`). Keep that shape if you touch either side.
- **`Program.DalamudUpdater.Run(kind, key)` is the download/switch trigger** (`docs/XIVLauncher.Core/Program.cs.md` §`DalamudUpdater`, `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Dalamud/DalamudUpdater.cs`), i.e. switching a branch *during* Save re-acquires the runner. The same call also happens during process start — `docs/XIVLauncher.Core/Program.cs.md:261 DalamudUpdater.Run(Config.DalamudBetaKind, Config.DalamudBetaKey)` — so a persisted branch keeps working across sessions without the UI doing anything; only an explicit switch adds a second run. Everything downstream consumes the same config pair at launch time; the launcher-side wiring is recorded in `MainPage.cs.md` §9 (`DalamudLauncher` ctor arguments) and `Tabs/SettingsTabDalamud.cs.md` §cross-entry notes.
- **`Program.Config.DalamudBetaKind`/`BetaKey` are written from a settings row** — same persistence channel as every other row here (the `Program.Config` lambdas), so the value round-trips across sessions without this class doing anything; only the updater run is session-gated (previous bullet). If a "re-run the updater for the persisted branch" behaviour is wanted, do it where `Program.Config` is read (launcher side), not by making `SelectedBranch` default to the persisted branch — that would let a page visit silently re-download.

## 6. Referenced / referenced-by

Derives from: `SettingsEntry{T}.cs.md` (`Load()` inherited, ctor delegation, `InternalValue`/`Value`/`IsValid`/`Id`), which implements `SettingsEntry.cs.md`.
Instantiated by: `Tabs/SettingsTabDalamud.cs.md` §entries (one instance in the package; its save lambda is `s => {}`).
Uses: `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Dalamud/DalamudBranchMeta.cs` (`Branch`, `Track`, `DisplayName`, `Key`, `Hidden`, `FetchBranchesAsync`), `docs/XIVLauncher.Core/Program.cs.md` (`Config`, `HttpClient`, `DalamudUpdater`), `docs/XIVLauncher.Core/ImGuiHelpers.cs.md` §`TextWrapped`, `docs/XIVLauncher.Core/ImGuiColors.cs.md` §`DalamudGrey`, `Component.cs.md` §`Id`.
Behaviour mirrored by: `MainPage.cs.md` §9 (what the beta kind/key and load method feed at launch).
