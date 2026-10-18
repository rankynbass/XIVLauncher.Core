# Page.cs — Specification

**File:** `src/XIVLauncher.Core/Components/Page.cs` · 19 lines · 308 bytes
**Namespace:** `XIVLauncher.Core.Components` · **Declaration:** `public class Page : Component`
**Role:** Base type for the six fullscreen pages — holds the owning `LauncherApp`, a window-padding override, and the "page became visible" hook

---

## 1. Purpose

`Page` is the shared supertype of exactly the classes that `LauncherApp` can switch between (see the `CurrentPage` mapping in `LauncherApp.cs.md` §2): `MainPage`, `SettingsPage`, `LoadingPage`, `OtpEntryPage`, `FtsPage`, `SteamDeckPromptPage`. It contributes three things to all of them: a back-reference to the app, an optional `Padding`, and an `OnShow()` hook.

Usings: `System.Numerics` (for `Vector2`).

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `App` | `protected LauncherApp`, `get`-only accessor | assigned once by the protected ctor; **read from subclass code as `this.App`** |
| `Padding` | `public Vector2?`, `get` + `init` | initialised `null`; assignable only in initializers/constructors |
| `Page(LauncherApp app)` | **`protected` ctor** | `this.App = app`; the only way to populate `App` |
| `OnShow()` | `public virtual`, empty body | per-page "this page just became the current page" hook |

`Page` deliberately does **not** override `Draw()`; it inherits `Component`'s children loop, so a bare `Page` paints nothing itself — every concrete page overrides `Draw()`.

## 3. `App` — the back-reference

Declared as an accessor-style field (`protected LauncherApp App { get; }`) with no initializer and assigned in the ctor, so:

- Subclasses cannot see or clear it: `App` is a stable alias of the `LauncherApp` that constructed the page.
- It is *not* `public`: outside the class hierarchy the app must be reached through `LauncherApp` itself. Subclasses use it freely — `FtsPage` (`App.State = LauncherApp.LauncherState.Fts`), `LoadingPage` (`App.Settings.IsAutologin = false`), `MainPage` (`App.StartLoading`, `App.Accounts`, `App.Settings`), `OtpEntryPage` (`App.Settings.IsOtpServer`), `SettingsPage` (`this.App.State = LauncherApp.LauncherState.Main`).
- Because the type is `LauncherApp` (not an interface), a page may touch **app-level** services that the state machine normally drives: `MainPage` calls `App.AskForOtp()`/`App.WaitForOtpAsync()`/`App.ShowMessageBlocking` and sets `App.State`; that coupling is by design here, so a page cannot be reused as a sub-component of a different owner.
- The field name `App` is also the *parameter* name convention in page ctors (`SteamDeckPromptPage(LauncherApp app)`), and several pages shadow it with their own local `app` (`NewsFrame`, `SettingsPage`, `MainPage` ctor) — see the per-file specs; inside those ctors `App` and `app` are the same object but not the same identifier.

## 4. The `protected` constructor

`Page(LauncherApp app)` is `protected`, so it is callable only via `: base(app)` from a subclass (the syntax used by all six pages) or as `super(...)` — a non-subclass cannot write `new Page(app)`. This is what makes "a Page" unusable as a generic container: instantiation always produces a subclass with a page-specific `Draw()`/`OnShow()`.

Every subclass ctor takes `LauncherApp app` and forwards it; the app's own ctor (`LauncherApp.cs.md` §4 step 3) is the sole construction site: `new MainPage(this)`, `new SettingsPage(this)`, `new OtpEntryPage(this)`, `new LoadingPage(this)`, `new FtsPage(this)`, `new SteamDeckPromptPage(this)` — one instance each, constructed once, reused forever. Consequence: page construction cost (texture loads, news fetch kickoff, Steam handler registration) happens during app startup, not on state transitions.

## 5. `Padding`

```csharp
public Vector2? Padding { get; init; } = null;
```

- `init` means it can be assigned only during construction (initialiser or ctor body). `MainPage`'s ctor is the only writer in the codebase (`this.Padding = new Vector2(32f, 32f);`) — the other five pages therefore report `null`.
- Sole consumer: `LauncherApp.Draw()` → `ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, this.CurrentPage.Padding ?? ImGui.GetStyle().WindowPadding)` (`LauncherApp.cs.md` §8). So `Padding` is the per-page inset of the fullscreen content window, and only the main page has one.
- Units are ImGui window padding (unscaled device pixels); the value is not multiplied by `ImGuiHelpers.GlobalScale`, so 32f is a fixed inset at every DPI.
- It is `Vector2?` — a `Vector2(0,0)` from a page would force zero padding (suppressing the style default), which is different from `null` (inherit the style). Use `null` when a page wants defaults.
- It cannot be re-purposed as a mutable per-frame value; if a page needs a dynamic inset it must set it in its own ctor (as `MainPage` does) or use its own field, since `init` blocks later writes.

## 6. `OnShow()` contract

Declared empty, and *required* to be called by any override that adds behaviour: the project's convention is "override does its own work, then `base.OnShow()`", as in `SettingsPage.OnShow()`. The hook fires from `LauncherApp.State`'s setter only (`LauncherApp.cs.md` §2 table) — i.e. on a **state transition to this page**, not on construction and not per frame.

Two consequences for implementors:

- Setting `State` to the value it already has re-runs `OnShow()`; `OnShow()` bodies must therefore be idempotent-or-cheap (both `SettingsPage.OnShow()`'s "reload all settings from config" and `OtpEntryPage.Reset()` are re-entrant, `LoadingPage.Reset()` likewise).
- `OnShow()` runs on the *caller's* thread — the thread that assigned `State` — and that thread is often the login/patching task. A page's `OnShow()` must not block on the frame loop (no modal waits, no "wait until drawn" logic).

There is no `OnHide()`/`OnLeave()` counterpart: "the previous state was Settings" is handled by the `State` setter's own pre-switch side effect (`mainPage.ReloadNews()`), which is the only place page-exit logic currently lives.

## 7. Invariants and extension points

- **Six-page coupling:** the enum in `LauncherApp`, its `State`-setter switch, its `CurrentPage` switch, and this base type must stay in sync; adding a seventh page means editing all of them (`LauncherApp.cs.md` §10).
- `Page` provides no id, no window creation, no `ImGui.Begin`: pages paint inside the single `"XIVLauncher"` window that `LauncherApp.Draw()` opens, and create their own *child* windows (`ImGui.BeginChild`) when they need scrolling — the `###` id prefix convention and `ImGuiChildFlags` choices are documented in the individual page specs.
- `Padding` and `Margins` (`Component.cs.md` §4) are two different, partially-unused layout mechanisms; only `Padding` is live today.
- `App` is not `readonly`-enforced but is assigned exactly once; nothing in this file prevents a subclass from observing app re-entrancy (there is exactly one app instance — `Program` holds it in `launcherApp`).
- Because `App` is `protected` and `Padding` is `public`, subclass authors can reach the app, but *users* of a page can only read its padding. If a page ever needs to expose its app, that widens the API beyond the current intent.

## 8. Referenced / referenced-by

Base of: `FtsPage.cs.md`, `OtpEntryPage.cs.md`, `SteamDeckPromptPage.cs.md` (namespace `XIVLauncher.Core.Components`), `LoadingPage/LoadingPage.cs.md`, `MainPage/MainPage.cs.md`, `SettingsPage/SettingsPage.cs.md`.
Referenced: `Component` (`Component.cs.md`), `LauncherApp` (`LauncherApp.cs.md`), `System.Numerics.Vector2`.
