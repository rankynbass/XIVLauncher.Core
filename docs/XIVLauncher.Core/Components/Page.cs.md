# Page.cs — Specification

**File:** `src/XIVLauncher.Core/Components/Page.cs` · 23 lines · 653 bytes
**Namespace:** `XIVLauncher.Core.Components` · **Declaration:** `public class Page : Component`
**Role:** Base for launcher pages — a full-window `Component` that carries the launcher shell reference (`App`)

---

## 1. Purpose

`Page` is the base for every screen the launcher can show. It adds exactly two things to `Component`:

- `protected LauncherApp App { get; }` (line 15) — the launcher shell,
- `public override void OnShow() { }` (line 22) — an explicit, overridable page-shown hook (the base `Component.OnShow()` is empty; `Page` overrides it with the same empty body so subclasses see `override` in their own file).

Usings: `Eto.Drawing` (for the XML-doc `Padding` reference; not otherwise used).

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `App` | `protected LauncherApp { get; }` (line 15) | set once in the constructor, never reassigned |
| `Page(LauncherApp app)` | `protected` ctor (lines 17–20) | every page subclass calls `: base(app)` |
| `OnShow()` | `public override void OnShow() { }` (line 22) | base no-op; overridden by `LoadingPage` (123), `SettingsPage` (85), `FtsPage` (105) |

Inherited from `Component`/`Panel`: `Content`, `Padding`, `Enabled`, `Visible`, `Size`, `BackgroundColor`, `ContextMenu`, `Id`, `Controls`.

## 3. Subclasses

| Page | File | What it hosts |
| --- | --- | --- |
| `MainPage` | `Components/MainPage/MainPage.cs` | login frame, news frame, action buttons, account switcher |
| `SettingsPage` | `Components/SettingsPage/SettingsPage.cs` | tab control, search box, finish button |
| `LoadingPage` | `Components/LoadingPage/LoadingPage.cs` | spinner, progress bar, status lines, cancel/disable buttons |
| `OtpEntryPage` | `Components/OtpEntryPage.cs` | centered OTP box, paste, OK, cancel |
| `FtsPage` | `Components/FtsPage.cs` | first-time-setup artwork with fixed click areas |
| `SteamDeckPromptPage` | `Components/SteamDeckPromptPage.cs` | full-window "switch to Steam" artwork |

`LauncherApp.CurrentPage` maps `LauncherState` → page instance, and `LauncherApp.PageShown` raises the instance so `LauncherForm.ShowPage(Page page)` can host it (`LauncherApp.cs.md`, `LauncherForm.cs.md`).

## 4. `App` usage

`App` is the page's link to the shell: settings reads/writes, state transitions, loading-page updates, modal dialogs. Measured consumers in page subclasses:

- `App.Settings` — `LoadingPage.cs:91` (`IsAutologin = false`), `LoadingPage.cs:136`, `FtsPage.cs:81,90,102` (`CompletedFts`, `IsIgnoringSteam`), `OtpEntryPage.cs:128,135` (`IsOtpServer`); `MainPage` uses it far more heavily (`MainPage.cs.md`).
- `App.State` — `FtsPage.cs:74,83,92,99` (`Fts`/`Main`), `MainPage.cs:226` (`Loading`), `MainPage.cs:59` (`Settings`), `MainPage.cs:1151` (`Main`).
- `App.StartLoading` / `App.AskForOtp` / `App.WaitForOtpAsync` / `App.ShowMessage*` — `MainPage.cs.md`, `OtpEntryPage.cs.md`.

`App` is `protected`, so a page cannot hand the shell to a non-page collaborator; `MainPage` passes `this` to `LoginFrame`/`NewsFrame`, which reach `App` through their own field.

## 5. Invariants and quirks

- **Pages are built once.** `LauncherApp` constructs all six pages in its constructor, on the UI thread, and never recreates them (`LauncherApp.cs.md` §4). A page's constructor therefore runs once, before the page is ever shown.
- **Page size is convention, not enforcement.** Pages are placed in `pageHost`, sized `1280x800` by `LauncherForm` (`LauncherForm.cs.md`). A page that sets a smaller `Size` shrinks its host slot; nothing in `Page` enforces full-window size, and `PixelLayout`-based pages (`FtsPage`, `SteamDeckPromptPage`) place children at fixed coordinates assuming 1280x800.
- **`Padding` semantics changed.** The old pages exposed a `Padding` vector read by the frame loop; Eto pages set `Panel.Padding` in their constructor, so a page's padding is applied by the layout, not by a draw loop.
- `Page` is `public class`, not abstract: it can be instantiated directly, producing an empty page. `CurrentPage` never returns a bare `Page`.
- `OnShow()` is called on the **entering** page, before the page is hosted, so an implementation must not assume it is already visible (`LauncherApp.cs.md` §2).

## 6. Referenced / referenced-by

Uses: `Component` (`Component.cs.md`), `LauncherApp` (`LauncherApp.cs.md`).
Referenced by: the six page classes above, `LauncherApp.CurrentPage`/`PageShown`, `LauncherForm.ShowPage`.
