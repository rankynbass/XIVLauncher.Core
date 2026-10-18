# Component.cs — Specification

**File:** `src/XIVLauncher.Core/Components/Component.cs` · 23 lines · 495 bytes
**Namespace:** `XIVLauncher.Core` · **Declaration:** `public abstract class Component`
**Role:** Root of the UI tree — enable flag, margins, child collection, per-instance id, and the per-frame `Draw()` convention

---

## 1. Purpose

`Component` is the abstract base every visible thing in the launcher derives from: `Background`, `Page`, `Button`, `Checkbox`, `Input`, `Spinner`, `SettingsEntry`, `SettingsTab`, `SettingsEntry<T>` and `LauncherApp` itself (`LauncherApp.cs.md` §"Role: Root UI component"). It is an **immediate-mode** base: instances are constructed once and `Draw()` is re-invoked every frame by the frame loop; there is no retained scene graph, no layout pass, no caching.

Usings: `System.Collections.Concurrent` (for `BlockingCollection`).

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `Enabled` | `public bool`, `get`/`set` | initialised `true`; read **only** by the parent's `Draw()` loop |
| `Margins` | `public Margins`, `get`/`set` | initialised `new()` ⇒ all four insets `0` (see `Margins.cs.md`) |
| `Children` | `public BlockingCollection<Component>`, `get` only | initialised `new()`; never added to anywhere in `src/XIVLauncher.Core` |
| `Id` | `protected Guid`, `get` only | `Guid.NewGuid()` per instance — written by the field initialiser |
| `Draw()` | `public virtual`, default body | iterates `Children`, skipping those with `Enabled == false` |

`Children` is declared as a `get`-only accessor with an initializer, so it is a fixed reference to one `BlockingCollection` instance — it cannot be swapped, only added to (`Add`) or drained.

## 3. `Enabled` — a parent-side flag

```csharp
foreach (var child in Children)
    if (child.Enabled)
        child.Draw();
```

The gate lives in the **consumer**, not in `Draw()`. Consequences:

- A component whose own `Draw()` is called directly (which is how every page, `LauncherApp`, and every settings tab calls things: `this.loginFrame.Draw()`, `settingsTab.Draw()`, `settingsEntry.Draw()`) is **not** filtered by `Enabled` at all — `Enabled` only takes effect for objects registered in a `Children` collection.
- Since nothing in the repo ever calls `Children.Add(...)`, `Enabled` is currently inert in `XIVLauncher.Core`. Grep confirms the only read of `Enabled` is this loop.
- `Enabled` is inherited, so derived classes that want their own notion of "shown" must not confuse it with `Component.Enabled` — this is exactly why `Button`, `Checkbox` and `Input` declare a **separate** `IsEnabled` field (see `Common/Button.cs.md`, `Common/Checkbox.cs.md`, `Common/Input.cs.md`). Do not "fix" that by reusing `Enabled`: the two flags have different consumers.

## 4. `Margins` — declared, not consumed

No code in `src/` reads a `.Margins` member (grep: zero hits outside this file and `Margins.cs` itself). The field is therefore a per-component layout slot reserved for future use, and its presence is the reason `Margins.cs.md` documents the LTRB convention. `LauncherApp.Draw()` uses `CurrentPage.Padding` (a `Vector2?` from `Page.cs.md`) instead — the two mechanisms coexist and only the latter is live.

## 5. `Id` — the per-instance ImGui id

`Id` is `protected` + `get`-only, generated once at construction. Its purpose in practice is to make ImGui **id strings** unique per instance, since the project's id convention is to embed the id in the item label after `###`:

- `Checkbox.Draw()`: `ImGui.Checkbox($"###{Id}", ref inputBacking)`
- `Input.Draw()`: `ImGui.InputTextWithHint($"###{Id}", …)`
- `SettingsEntry{T}.Draw()`: `ImGui.InputText($"###{Id.ToString()}", …)`, `ImGui.BeginCombo($"###{Id}", …)`
- `AccountSwitcher.Draw()`: `ImGui.Button(name + $"###{account.Id}", …)` (there the id comes from `XivAccount`, not from `Component`)
- `SettingsPage.Draw()`: `$"###settings_scrolling_{settingsTab.Title}"` (title-based, not id-based)

Two consequences:

1. `Guid.NewGuid()` is **per process run**, not stable across runs. That is only safe because ImGui ids need uniqueness *within the current frame's window*, not persistence — anything that ever persists a component id (a save file, a URL) would be broken by this.
2. `Id` is unique per *instance*, so two live instances of the same page element cannot collide — but a component constructed inside a draw loop would get a fresh id every frame, which silently breaks "same item across frames" state (ImGui remembers focus/active state per id). Do not construct components per frame.

## 6. `Draw()` and the `base.Draw()` convention

Every concrete `Draw()` override in this package ends with `base.Draw()` (C# 13's `base` keyword ⇒ the overridden implementation), so the child loop always runs last in the chain. Order matters in immediate-mode UI: children paint over siblings drawn earlier. Two exceptions are deliberate:

Not every override ends with `base.Draw()`, and the ones that skip it are the leaf controls: `Button`, `Checkbox` and `Input` in `Common/` have no `base.Draw()` call at all, so anything registered in their `Children` would never be painted. `Spinner` (also a leaf) does call it. In `SettingsPage/`, the tab overrides are split: `SettingsTabGame`, `SettingsTabAbout`, `SettingsTabDebug` and `SettingsTabTroubleshooting` end with `base.Draw()` (extra buttons *after* the entries), while `SettingsTabWine.Draw()` calls `base.Draw()` **first** (entries, then its four WINE launcher buttons) and never calls it again — see that file's §on draw order. `SettingsTabPatching` and `SettingsTabDalamud` do not override `Draw()` at all, so they get this default body.

The default `Draw()` body is itself the "draw all children" primitive; `Page` and its subclasses rely on inheriting it, and `Page` does not override `Draw()` at all (see `Page.cs.md`): a bare `Page` draws only its children.

### Iteration hazards on `BlockingCollection`

- Iteration uses the concurrent-collection iterator protocol: it yields a snapshot-style traversal and tolerates concurrent mutation (mutation during iteration surfaces as "the old pattern, plus the new value"), so an `Add` during a frame is not a crash risk but is also not deterministic in *when* it appears.
- Unlike `BlockingCollection`'s `Take`/`RemoveAny` methods, plain iteration does **not** block on an empty collection, so the default `Draw()` on a childless component is cheap and safe.
- `Children` has **no defined order**. Any component that later relies on child draw order must not use `Children`.
- `BlockingCollection` is not a `Component`-typed container of "owned" children: it holds references; nothing removes or releases them, and there is no `Add`-helper, no `parent` back-pointer, and no `Enabled`-propagating removal.

## 7. Invariants and extension points

- The class is `abstract` but declares no abstract members: the extension point is convention, not enforcement. Subclasses are expected to override `Draw()` (and finish with `base.Draw()`) and/or declare `Title`-style data.
- There is no `Add(Component)` / `Remove(Component)` helper, no hit-testing, no layout, no event plumbing (clicks are handled by each concrete component) — adding any of that belongs in this file, since it is the only shared point.
- No thread-safety contract is stated for `Enabled`/`Margins`, though `Children`'s backing type is thread-safe: the project's model is "one draw thread, logic threads never touch UI state".
- Because `Enabled`, `Margins` and `Children` are `public`, they are part of the API surface of *every* UI type. Renaming or narrowing them is a cross-package change (`SettingsTab`, `SettingsEntry`, `LauncherApp` and all pages inherit them).
- `Draw()` returns `void` and cannot signal "I drew nothing", so a component cannot ask its parent to skip padding.

## 8. Referenced / referenced-by

Defined here: `Component` (abstract base).
Referenced: `Margins` (`Margins.cs.md`), `BlockingCollection` (stdlib).
Referenced by: `Background.cs.md`, `Page.cs.md`, `Common/Button.cs.md`, `Common/Checkbox.cs.md`, `Common/Input.cs.md`, `LoadingPage/Spinner.cs.md`, `MainPage/AccountSwitcher.cs.md`, `MainPage/ActionButtons.cs.md`, `MainPage/LoginFrame.cs.md`, `MainPage/NewsFrame.cs.md`, `SettingsPage/SettingsEntry.cs.md`, `SettingsPage/SettingsTab.cs.md`, `LoadingPage/DalamudOverlayInfoProxy.cs.md` (indirectly, via `IDalamudLoadingOverlay`), `LauncherApp.cs.md`.
