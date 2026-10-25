# Component.cs — Specification

**File:** `src/XIVLauncher.Core/Components/Component.cs` · 21 lines · 890 bytes
**Namespace:** `XIVLauncher.Core.Components` · **Declaration:** `public abstract class Component : Panel`
**Role:** Root of the launcher UI tree — an Eto `Panel` base with a per-instance id and the `OnShow()` hook

---

## 1. Purpose

`Component` is the abstract base every visible thing in the launcher derives from: `Page` (and thus all pages), `Common/Button`, `Common/Checkbox`, `Common/Input`, `MainPage/ActionButtons`, `MainPage/AccountSwitcher`, `MainPage/LoginFrame`, `MainPage/NewsFrame`, `SettingsPage/SettingsEntry`, `SettingsPage/SettingsTab`.

The previous version was an immediate-mode base: a `Children` collection, an `Enabled` flag, a `Margins` struct, and a `Draw()` that walked children every frame. Eto widgets are retained: a component is constructed once (in its constructor), added to a layout, and shown/hidden/enabled/refreshed through the platform. `Control.Enabled` propagates to child widgets in GTK, which replaces the old `Enabled` + `Children` draw recursion.

Usings: `Eto.Forms` only.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `Component` | `public abstract class Component : Panel` | inherits Eto `Panel` → `Content`, `Padding`, `Controls`, `ContextMenu`, `Enabled`, `Visible`, `Size`, `BackgroundColor` |
| `Id` | `protected Guid { get; } = Guid.NewGuid()` (line 17) | per-instance, generated at construction |
| `OnShow()` | `public virtual void OnShow() { }` (line 20) | page-shown hook, default no-op |

`Enabled`, `Margins` and `Children` from the previous base are **gone**: `Margins.cs` and the `BlockingCollection<Component>` child collection were deleted, and enablement is now Eto's `Control.Enabled`.

## 3. `OnShow()` — page-shown hook

`LauncherApp.SetState` calls `OnShow()` on the page it is entering, before raising `PageShown` (`LauncherApp.cs.md` §2):

| Override | Site | Behaviour |
| --- | --- | --- |
| `LoadingPage.OnShow()` | `LoadingPage.cs:123` | `=> this.Refresh()` — recomputes control visibility from the current flags |
| `SettingsPage.OnShow()` | `SettingsPage.cs:85`, `base.OnShow()` at 95 | loads tab values, resets search, rebuilds pages |
| `FtsPage.OnShow()` | `FtsPage.cs:105` | first-run / Steam Deck checks, may switch state |
| `Page.OnShow()` | `Page.cs:22` | empty body |
| `MainPage`, `OtpEntryPage`, `SteamDeckPromptPage` | — | no override; inherit the base no-op |

Because `OnShow()` runs on the UI thread (it is invoked from `SetState`, which is reached via `Ui.Post`), implementations may mutate Eto controls directly without marshalling.

`Component.OnShow()` is `public virtual`, not abstract: leaf components (`Button`, `Input`, `Checkbox`, `LoginFrame`, …) inherit the empty body, and nothing outside the page state machine calls it.

## 4. `Id` — declared, not consumed

`Id` is `protected` + get-only, `Guid.NewGuid()` per instance. In the immediate-mode build it supplied ImGui id strings (`$"###{Id}"` in `Checkbox`, `Input`, `SettingsEntry{T}`), which is why it was needed. **No code in `src/XIVLauncher.Core` reads `Id` any more** (grep: the only `Id` hits outside this file are `XivAccount.Id`, `AccountManager` account lookups, and `process.Id`), because Eto controls are persistent objects and GTK does not need per-item ids. It remains part of the base API surface, so removing it is a cross-package change.

`Guid.NewGuid()` is per process run, not stable across runs — fine for ids, unsuitable for anything persisted.

## 5. Invariants and extension points

- **Construction is UI-thread only.** A `Component` is an Eto `Panel`, so it must be created after `Application` exists (`Program.cs` builds everything inside `application.Initialized`). Constructing one before the platform is initialized throws.
- **Enablement semantics change with inheritance:** `Component.Enabled` is Eto's `Enabled`, which GTK propagates to children. The launcher wrappers (`Common/Button.cs.md`, `Common/Checkbox.cs.md`, `Common/Input.cs.md`) expose `IsEnabled` and forward it to the wrapped native control, so `IsEnabled` on a wrapper and `Enabled` on a `Component` are the same mechanism through different names; `public new` is used in the wrappers to suppress hiding warnings.
- `Panel` gives every component a `Content` slot; pages set `Content` once in their constructor, and `LauncherForm.ShowPage` swaps pages by replacing `pageHost.Content` (`LauncherForm.cs.md`). Nothing here implements layout — layout comes from Eto (`StackLayout`, `TableLayout`, `PixelLayout`, `Scrollable`).
- No thread-safety contract is stated. The project's rule is "UI mutation on the UI thread" (`Ui.cs.md`); `OnShow()` satisfies it by construction.
- The class declares no abstract members: the extension point is convention (override `OnShow()`, set `Content` in the ctor).

## 6. Referenced / referenced-by

Uses: `Eto.Forms.Panel`.
Referenced by: `Page.cs.md` (base of pages), `Common/Button.cs.md`, `Common/Checkbox.cs.md`, `Common/Input.cs.md`, `MainPage/ActionButtons.cs.md`, `MainPage/AccountSwitcher.cs.md`, `MainPage/LoginFrame.cs.md`, `MainPage/NewsFrame.cs.md`, `SettingsPage/SettingsEntry.cs.md`, `SettingsPage/SettingsTab.cs.md` (and the tab/entry subclasses that inherit through them).
