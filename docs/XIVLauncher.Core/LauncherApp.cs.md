# LauncherApp.cs — Specification

**File:** `src/XIVLauncher.Core/LauncherApp.cs` · 284 lines · 9,241 bytes
**Namespace:** `XIVLauncher.Core` · **Declaration:** `public class LauncherApp`
**Role:** The launcher shell — state machine that swaps pages, modal-dialog helpers, and the shared launcher services (login client, accounts, storage, news/OTP/FTS pages)

---

## 1. Purpose

`LauncherApp` is the object every page component is built around (`Components/Page.cs.md` stores it as `App`). It owns:

- the `LauncherState` state machine (`Main`, `Settings`, `Loading`, `OtpEntry`, `Fts`, `SteamDeckPrompt`),
- the six page instances and the `PageShown` event that the window host subscribes to,
- the modal system (single-modal counter + `ManualResetEvent` + `ModalDialog`),
- the shared services: `Launcher` (login client), `Accounts`, `UniqueIdCache`, `Storage`, `Settings`,
- the startup behaviour: kill-switch check, `RunStartupTasks()` (FTS on Flatpak, auto-login).

The file previously drove an immediate-mode loop: it created the ImGui windows, drew `CurrentPage.Draw()` each frame, and drew a modal with `DrawModal()`. All of that is gone: pages are Eto controls hosted by `LauncherForm.ShowPage`, and modals are real Eto `Dialog`s.

## 2. State machine

`public enum LauncherState { Main, Settings, Loading, OtpEntry, Fts, SteamDeckPrompt }` (line 47).

```csharp
public LauncherState State
{
    get => this.state;
    set => Ui.Post(() => this.SetState(value));   // line 62
}
```

**The setter marshals to the UI thread** (line 62). Login, patching, Dalamud, and startup code run on background tasks and set `State` directly (`MainPage.cs:226`, `MainPage.cs:59`, `Program.cs:364`, `MainPage.cs:1151`), so posting is what makes page swaps safe under Eto's single-threaded UI (`Ui.cs.md`).

`SetState(LauncherState value)` (line 65) — private, UI-thread only:

1. If the outgoing state is `Settings`, calls `this.mainPage.ReloadNews()` (line 68) — the client language may have changed in settings.
2. Assigns `this.state = value`.
3. Calls `OnShow()` on the **new** page (`setPage`, `mainPage`, `LoadingPage`, `otpEntryPage`, `ftsPage`, `steamDeckPromptPage`), `default:` → `ArgumentOutOfRangeException` (line 97).
4. Raises `this.PageShown?.Invoke(this.CurrentPage)` (line 104).

`public event Action<Page>? PageShown` (line 107) is the only integration point with the window: `LauncherForm.ShowPage` replaces `pageHost.Content` with the page (`LauncherForm.cs.md`).

`public Page CurrentPage` (line 109) maps each state to its page, `default` → `ArgumentOutOfRangeException` naming `this.State` (line 117). Note the exception text references `this.State`, not the local `state` field.

**Ordering rule:** `OnShow()` runs before `PageShown` is raised, so a page's `OnShow()` sees the *old* host content. `MainPage.OnShow()` (Steam Deck prompts) and `FtsPage.OnShow()` set `State` again from inside `OnShow()` — that is legal because the setter posts to the UI thread, so the nested change is queued after the current `SetState` completes.

## 3. Collaborator surface

| Member | Kind | Notes |
| --- | --- | --- |
| `Settings` | `public ILauncherConfig` → `Program.Config` (line 121) | settings tabs write it through `App.Settings` |
| `Launcher` | `public Launcher` (line 122) | login client, built in ctor (line 144) |
| `Steam` | `public ISteam?` → `Program.Steam` (line 123) | `MainPage.cs:147`, `FtsPage.cs:87`, `Components/Common/Input.cs:92` |
| `Storage` | `public Storage` (line 124) | `App.Storage.Root`, `App.Storage.GetFolder("logs")` (`MainPage.cs:586`) |
| `LoadingPage` | `public LoadingPage { get; }` (line 125) | the *page* object; also the name of the type — see §7 |
| `Accounts` | `public AccountManager` **field** (line 126) | `MainPage.cs:65,131` |
| `UniqueIdCache` | `public CommonUniqueIdCache` **field** (line 127) | `MainPage.cs:217,220`, `Program.cs:422` |
| `mainPage`/`setPage`/`otpEntryPage`/`ftsPage`/`steamDeckPromptPage` | `private readonly` (lines 129–134) | created once in ctor |
| `form` | `private LauncherForm?` (line 135) | set by `AttachForm`; modal owner |
| `openModals` | `private int` (line 42) | modal counter guard |
| `modalWaitHandle` | `private readonly ManualResetEvent` (line 43) | modal blocking |
| `IsDebug` | `public static bool` = `Debugger.IsAttached`, `#if FLATPAK`? no — `#if DEBUG` sets `true` (line 171) | |

`Settings`/`Steam` are pass-throughs to `Program`; the shell keeps the names so pages keep the `App.X` idiom.

## 4. Constructor contract

`public LauncherApp(Storage storage, string frontierUrl, string? cutOffBootver)` (line 137), created inside `application.Initialized` (`Program.cs:275`).

Order (lines 139–144): `Storage` (139) → `AccountManager(storage.GetFile("accounts.json"))` (141) → `CommonUniqueIdCache(storage.GetFile("uidCache.json"))` (142) → `Launcher(Program.Steam, UniqueIdCache, frontierUrl, Program.Config.AcceptLanguage ?? ApiHelpers.GenerateAcceptLanguage())` (144).

Then pages are constructed in order `mainPage`, `setPage`, `otpEntryPage`, `LoadingPage`, `ftsPage`, `steamDeckPromptPage` (lines 146–151). **All page construction runs on the UI thread inside `Initialized`** — Eto controls must be created there, so any page that does network work in its constructor blocks first paint (`MainPage`'s constructor triggers `NewsFrame.ReloadNews()`, `MainPage.cs.md`).

Kill switch (lines 153–166): when `!EnvironmentSettings.IsNoKillswitch && !string.IsNullOrEmpty(cutOffBootver)`, compares `SeVersion.Parse(Repository.Boot.GetVer(Program.Config.GamePath))` with `SeVersion.Parse(cutOffBootver)`; if the local boot version is newer, `ShowMessage(..., "Close Launcher", () => Environment.Exit(0))` is posted and the ctor **returns** — so `RunStartupTasks()` is skipped, the pages exist, and the UI starts normally with the modal displayed on top.

`this.RunStartupTasks()` (line 168) then `#if DEBUG IsDebug = true` (lines 170–172).

**Ordering hazard:** the kill-switch modal is posted *before* `AttachForm` runs (`Program.cs:279`), so `this.form` is `null` and `ShowMessage` takes the `dialog.ShowModal()` branch (`LauncherApp.cs:206`) — the dialog is not parented to the main window, and it is shown before any page has been hosted. Its action calls `Environment.Exit(0)` directly, not `Application.Instance.Quit()`, so no Eto shutdown path runs.

## 5. `AttachForm`

```csharp
public void AttachForm(LauncherForm form)
{
    this.form = form;
    this.PageShown += form.ShowPage;
    Ui.Post(() => this.SetState(LauncherState.Main));
}
```

- Called by `Program` immediately after construction (`Program.cs:279`), i.e. **before** `launcherForm.Show()`; the posted `SetState(Main)` lands on the UI queue, so the first page is hosted when the GTK loop starts pumping.
- `form` is stored so `ShowMessage` can parent dialogs to the main window (`dialog.ShowModal(this.form)`, line 204).
- `PageShown` is a single-subscriber contract in practice: `LauncherForm` is the only subscriber; subscribing twice would host the page in two places.

## 6. Modal system

`ShowMessage(string text, string title = "XIVLauncher", string modalButtonText = "OK", Action? modalPressedAction = default)` (line 184):

1. `if (this.openModals > 0) throw new InvalidOperationException("Cannot open modal while another modal is open")` (lines 186–187) — checked on the **caller's** thread, before posting.
2. `this.modalWaitHandle.Reset()` (line 189).
3. `var pressedAction = modalPressedAction ?? new Action(() => { })` (line 191) — the `new Action(...)` form is required: `() => { }` alone is not convertible to `Action` on the right-hand side of `??`.
4. Posts a UI block that increments `openModals`, builds a `ModalDialog(text, title, modalButtonText, callback)`, shows it **modally** (`dialog.ShowModal(this.form)` when a form is attached, line 204; `dialog.ShowModal()` otherwise, line 206) and decrements `openModals` after the modal returns.

**`ShowModal` pumps the GTK loop**, so `openModals` stays `> 0` for the entire time the modal is visible, and `ShowMessage` does not return until the modal is closed (it returns on the UI thread). The button callback sets `modalWaitHandle` and runs `pressedAction` (`ModalDialog.cs.md`).

`ShowMessageBlocking(string text, string title = "XIVLauncher", bool canContinue = true)` (line 212):

- Guard: `if (!this.modalWaitHandle.WaitOne(0) && this.openModals > 0) throw ...` (line 214) — i.e. the "modal already open" rule is relaxed when the handle is already set (a blocking modal in flight).
- `Reset()`, `ShowMessage(text, title)`, then `this.modalWaitHandle.WaitOne()` (lines 217–220) — the caller blocks on a `ManualResetEvent` that is set by the modal button, so background login/patch code waits for user acknowledgement.

`ShowExceptionBlocking(Exception ex, string context)` (line 223) formats `context + " failed with exception\nPlease report this error to us.\n\n" + ex.Message` with title `"XIVLauncher has encountered an error."`.

`HandleContinuationBlocking(Task t)` (line 228): `IsFaulted` → `Log.Error(t.Exception, "Task failed")` + blocking modal with `t.Exception?.InnerException?.Message ?? "Unknown error - please check logs."`; `IsCanceled` → blocking modal `"Task was canceled."`; returns `false` in both cases, `true` otherwise. Consumers: `MainPage.cs:189`, `MainPage.cs` patch/login continuations.

**Defaults are now HEAD-equivalent.** An earlier pass attempted richer exception-specific text for `SeBreakException`, `OauthLoginException`, and auth-char offline/online errors; those types and `Strings.LoginAuthCharOfflineError`/`LoginAuthCharOnlineError` do not exist in this project, so `HandleContinuationBlocking` is the generic fault/cancel form.

**Threading rules:**
- `ShowMessage` is safe from any thread (it posts). `ShowMessageBlocking` is intended for background threads; calling it from the UI thread deadlocks, because `ShowModal` needs the UI loop and `WaitOne()` blocks that same loop.
- The `openModals` guard runs on the caller thread while the counter is mutated on the UI thread, so the guard is advisory: a race can post two modals that each see `openModals == 0` at check time. The counter is decremented after `ShowModal` returns, so a second `ShowMessage` posted while a modal is open throws on the UI thread.
- `modalWaitHandle` is shared across all modals: `ShowMessage` resets it unconditionally, so an outstanding blocking caller can be released by a later modal's button.

## 7. OTP and loading flows

- `AskForOtp()` (line 246): `this.otpEntryPage.Reset()` then `State = LauncherState.OtpEntry` (`MainPage.cs:222`).
- `WaitForOtpAsync()` (line 252): awaits `otpEntryPage.GetResultAsync()` with `ConfigureAwait(false)`, then `otpEntryPage.StopOtpListener()` and returns the code (`MainPage.cs:223`).
- `StartLoading(string line1, string line2 = "", string line3 = "", bool isIndeterminate = true, bool canCancel = false, bool canDisableAutoLogin = false)` (line 259): sets `State = Loading` first, then writes `Line1`/`Line2`/`Line3`/`IsIndeterminate`/`CanCancel`/`CanDisableAutoLogin` on `LoadingPage`, then `LoadingPage.Reset()`. Called from `MainPage.cs:147` (login), `MainPage.cs:620` (Dalamud wait), `MainPage.cs:753`/`760` (compat-tool prepare and game start), `MainPage.cs:955` (patching), `MainPage.cs:1072` (file repair). Every one of these calls happens on a background thread. The `State` write is marshalled (`Ui.Post`, `LauncherApp.cs.md` §4), and so is every widget write that follows: each `LoadingPage` setter stores the value and posts `ApplyState`, and `Reset()` posts it too (`Components/LoadingPage/LoadingPage.cs.md` §3). Pre-fix, the property writes and `Reset()` were unmarshalled — `Reset() → Refresh() → Label.Text` ran GTK size negotiation on the caller's worker thread, which is the login crash; the marshalling is the fix.
- `StopLoading()` (line 272): `State = LauncherState.Main`.
- **Naming collision:** the member `LoadingPage` and the type `LoadingPage` share a name; `this.LoadingPage.Line1` is the page instance, and `Components/LoadingPage/LoadingPage.cs` is the type. Consumers use `App.LoadingPage.Line2`/`Line3`/`Progress` for patch progress (`MainPage.cs:970–974`).

## 8. `RunStartupTasks`

```csharp
public void RunStartupTasks()
{
#if FLATPAK
    this.ftsPage.OpenFtsIfNeeded();
#endif
    this.mainPage.DoAutoLoginIfApplicable();
}
```

- FTS (first-time setup) is only checked under `FLATPAK` — the sandboxed build, where the Wine prefix cannot be assumed. Non-Flatpak builds reach FTS through `MainPage.OnShow()` (`MainPage.cs`, `FtsPage.cs.md`).
- `DoAutoLoginIfApplicable()` (`MainPage.cs:110–114`) starts the login pipeline when `App.Settings.IsAutologin` is true with non-empty username/password (`MainPage.cs.md`).
- Runs inside the constructor, i.e. on the UI thread, so auto-login starts from the UI thread and then `Task.Run`s the login (`MainPage.cs` `ProcessLogin`).
- A temporary `RunLoginTest()` wrapper (public `MainPage.RunLoginTest` + `LauncherApp.RunLoginTest` + `XL_LOGIN_TEST` hook in `Program.cs`) was used to exercise this pipeline from the command line and has been removed; `ProcessLogin` is `private` in `MainPage`, so any future smoke test needs a wrapper again.

## 9. Invariants and hazards

- **Every UI mutation from this file is posted.** `State`, `AttachForm`'s `SetState`, and `ShowMessage`'s dialog creation all go through `Ui.Post`. Any new page mutation added here must follow the same rule; `SetState` is the single place that touches pages, and it is private. `StartLoading`'s property writes are posted by `LoadingPage`'s own setters, not by `StartLoading` — the marshalling lives in the page.
- **Single-modal invariant**: `openModals` is a plain `int` with no lock; the intended discipline is one modal at a time. Long modal chains (login errors, patch errors, gate status) are sequential because each `ShowMessageBlocking` waits for the button.
- **Pages are constructed once and never disposed** — Eto controls are re-hosted, not recreated; `LauncherForm.ShowPage` swaps `pageHost.Content` (`LauncherForm.cs.md`). Leaving a page for a new one does not detach the previous page from its parent, so the old page is dropped by the host.
- **`Settings`/`Steam` are live views onto `Program`**; they can be `null` for `Steam` when Steam fails to initialize (`Program.cs:245`), and pages must null-check (`FtsPage.cs:87`, `Input.cs:92`).
- `State`'s setter is fire-and-forget: a caller that reads `App.State` right after setting it sees the **old** value until the UI thread drains the queue. `MainPage` does not rely on this, and `Debug.Assert(App.State == LauncherState.Main)` (`MainPage.cs:110`) is asserted on the UI thread.

## 10. Referenced / referenced-by

Referenced by: `Program.cs` (construction, `AttachForm`, `ClearSettings` `launcherApp.State`), `Components/Page.cs` (`App`), `MainPage.cs`, `SettingsPage.cs`, `LoadingPage.cs`, `OtpEntryPage.cs`, `FtsPage.cs`, `SteamDeckPromptPage.cs`, `LauncherForm.cs` (`PageShown` subscription, `ModalOwner`).
Uses: `Ui`, `ModalDialog`, `LauncherForm`, `Program.Config`, `Program.Steam`, `Eto.Forms.Dialog` (via `ModalDialog`), `Launcher`, `AccountManager`, `CommonUniqueIdCache`, `Storage`, `SeVersion`, `Repository`, `ApiHelpers`, `EnvironmentSettings`, `Strings`, `Log`, `Debugger`.
