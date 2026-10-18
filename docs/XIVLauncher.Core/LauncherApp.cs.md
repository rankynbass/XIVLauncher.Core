# LauncherApp.cs — Specification

**File:** `src/XIVLauncher.Core/LauncherApp.cs` · 332 lines · 10,910 bytes
**Namespace:** `XIVLauncher.Core` · **Declaration:** `public class LauncherApp : Component`
**Role:** Root UI component: page state machine, blocking modal dialogs, draw orchestration

---

## 1. Purpose

`LauncherApp` is the single "root component" instantiated by `Program.Main` and drawn every frame. It owns:

- the **page state machine** (`LauncherState` ⇄ page instances),
- the **modal dialog system** (including a blocking variant that halts the calling thread until the user clicks),
- the **frame draw order** (fullscreen background → fullscreen page window → popup),
- the **startup policy** (killswitch check, auto-login trigger),
- shared handles to accounts, unique-id cache, and the `Launcher`.

It derives from `Component` (the project's component base class) so it participates in the component draw convention, calling `base.Draw()` (C# 13 `base` keyword ⇒ the inherited `Component.Draw()`).

## 2. State machine

### `enum LauncherState`

`Main`, `Settings`, `Loading`, `OtpEntry`, `Fts`, `SteamDeckPrompt` — public nested enum, referenced from outside this file as `LauncherApp.LauncherState.Settings` (e.g. `Program.ClearSettings`).

### `public LauncherState State { get; set }`

The setter is **effectful**, and that effect is the contract every other unit relies on:

| Transition | Side effect |
| --- | --- |
| *leaving* `Settings` (any new value) | `mainPage.ReloadNews()` — because `ClientLanguage` may have changed while on the settings page |
| → `Settings` | `setPage.OnShow()` |
| → `Main` | `mainPage.OnShow()` |
| → `Loading` | `LoadingPage.OnShow()` |
| → `OtpEntry` | `otpEntryPage.OnShow()` |
| → `Fts` | `ftsPage.OnShow()` |
| → `SteamDeckPrompt` | `steamDeckPromptPage.OnShow()` |
| → anything else | `throw ArgumentOutOfRangeException` |

Assigning the *same* state still re-runs that page's `OnShow()` (no identity guard) — do not set `State` to the current value casually, as `OnShow()` implementations may reset page-local state.

`OnShow()` is the page's "this page became visible" hook; `Draw()` is per-frame. Pages are constructed once and reused; `OnShow` is not a construction event.

### `public Page CurrentPage`

Get-only accessor mapping `state` → the page instance (same six-way mapping; `_` arm throws). This is what `Draw()` draws and what the `Padding` read comes from.

## 3. Collaborator surface

| Member | Kind | Notes |
| --- | --- | --- |
| `Settings` | accessor `=> Program.Config` | the `ILauncherConfig` view (aliased, not copied) |
| `Steam` | accessor `=> Program.Steam` (`ISteam?`) | global handle, may be `null` |
| `Storage` | field, ctor-assigned | the launcher-wide `Storage` (same object as `Program.storage`) |
| `Launcher` | `get`/`private set` | `new Launcher(Program.Steam, UniqueIdCache, frontierUrl, AcceptLanguage ?? ApiHelpers.GenerateAcceptLanguage())` |
| `Accounts` | `public AccountManager` | `Storage.GetFile("accounts.json")` |
| `UniqueIdCache` | `public CommonUniqueIdCache` | `Storage.GetFile("uidCache.json")` — also the target of `Program.ResetUIDCache` |
| `LoadingPage` | `get` accessor | the `LoadingPage` instance |
| `mainPage`, `setPage`, `otpEntryPage`, `ftsPage`, `steamDeckPromptPage` | `private readonly` | constructed in ctor |
| `background` | `private readonly Background = new()` | the fullscreen backdrop component |
| `IsDebug` | `public static bool` (`get`, `private set`) `= Debugger.IsAttached`, forced `true` under `#if DEBUG` in the ctor | global "debug UI" flag read by other units |

`Accounts`/`UniqueIdCache`/`Launcher` are declared **without initializers** (definite assignment via the ctor) while `Storage`/`LoadingPage`/`Launcher` are accessors/fields with `null!`-style declarations — mixed but all assigned before first use.

## 4. Constructor contract

`LauncherApp(Storage storage, string frontierUrl, string? cutOffBootver)` — supplied by `Program.Main` from `LauncherClientConfig.GetAsync()` (so these two values are network-derived: the Frontier base URL and the "cut off bootver" killswitch threshold).

1. `Storage = storage`; `Accounts = AccountManager(accounts.json)`; `UniqueIdCache = CommonUniqueIdCache(uidCache.json)`.
2. `Launcher = Launcher(Program.Steam, UniqueIdCache, frontierUrl, AcceptLanguage ?? ApiHelpers.GenerateAcceptLanguage())`.
3. Construct all six pages **plus** `LoadingPage` (order: `mainPage`, `setPage`, `otpEntryPage`, `LoadingPage`, `ftsPage`, `steamDeckPromptPage`) — every page holds a reference back to `this`, so page constructors must not touch `LauncherApp` state that is assigned later.
4. **Killswitch gate:** if `!EnvironmentSettings.IsNoKillswitch && !string.IsNullOrEmpty(cutOffBootver)`, compare `SeVersion.Parse(Repository.Boot.GetVer(Config.GamePath))` against `SeVersion.Parse(cutOffBootver)`. If the game's current boot version exceeds the cutoff, show the "XIVLauncher is unavailable at this time…" modal with button `"Close Launcher"` and action `() => Environment.Exit(0)`, then **`return`** — startup tasks are skipped, and the launcher keeps drawing the modal until the user presses the button, at which point the process exits.
5. `RunStartupTasks()`; then `#if DEBUG IsDebug = true;`.

The killswitch path is the reason `Program.Main` blocks on `GetAsync().GetAwaiter().GetResult()` before constructing the app: `frontierUrl` and `cutOffBootver` must exist synchronously at construction time.

## 5. Modal system

Private state (`#region Modal State`):

| Field | Meaning |
| --- | --- |
| `isModalDrawing` | a modal is currently on screen |
| `modalOnNextFrame` | a modal was *requested* this frame and must be opened at the start of the next frame |
| `modalText`, `modalTitle`, `modalButtonText` | content |
| `modalButtonPressAction` (`Action`) | what runs when the button is pressed |
| `modalWaitHandle` (`ManualResetEvent`, initially `false`) | used by the blocking variants to park a thread |

### `ShowMessage(text, title = "XIVLauncher", modalButtonText = "OK", modalPressedAction = default)`

- Throws `InvalidOperationException("Cannot open modal while another modal is open")` when `isModalDrawing` — the UI supports **exactly one** modal.
- `modalPressedAction` uses the C# 13 **`= default`** parameter default (⇒ `null` for the `Action` type). When `null`, the default action is installed: `CloseCurrentPopup()`, `isModalDrawing = false`, `modalWaitHandle.Set()`. A caller-provided action **replaces** it and is responsible for closing the popup itself (e.g. the killswitch action exits the process without closing).
- Sets `isModalDrawing = true` and `modalOnNextFrame = true`; it does **not** open the popup immediately.

### Why the "next frame" deferral exists

`ShowMessage` may be called from anywhere (including during the current frame's `Draw()` of a page). Opening a popup mid-frame would nest it inside the page's window. `DrawModal()` runs at the very end of `Draw()`; it first calls `ImGui.OpenPopup("###xl_modal")` when `modalOnNextFrame`, clears the flag, and sets `isModalDrawing = true` — so the modal actually becomes interactive on the following frame. The visible effect for callers is that a message appears one frame later.

### `ShowMessageBlocking(text, title = "XIVLauncher", canContinue = true)`

1. Guard: `if (!modalWaitHandle.WaitOne(0) && isModalDrawing) throw InvalidOperationException(…)`. `WaitOne(0)` is a non-blocking "is the handle set?" test, so the throw only fires while an *unfinished* modal is on screen — i.e. blocking calls nest legally after the previous one completed.
2. `modalWaitHandle.Reset()` (re-arm), then `ShowMessage(...)` — note the default button label `"OK"` and no custom action, so pressing the button installs the default action which sets the handle.
3. `modalWaitHandle.WaitOne()` — **blocks the calling thread until the frame loop renders the modal and the user presses the button**.

Consequence: `ShowMessageBlocking` must never be called from the draw path itself (`Draw`, page draws, or `OnShow`), only from startup/logic code, otherwise the loop that would render the modal is the blocked thread — deadlock. `Program.Main`'s frame loop is the only thing that can satisfy the wait.

### `ShowExceptionBlocking(Exception, string context)`

`ShowMessageBlocking($"An error occurred ({context}).\n\n{exception}", Strings.XIVLauncherError)` — formats the exception with `ToString()` and uses the localized `Strings.XIVLauncherError` title (from `Resources/Localization/Strings.resx`, accessor spec'ed in `Resources/Localization/Strings.Designer.cs.md`; the `text` argument here is **not** catalogued, so an error modal is half-translated by construction).

### `HandleContinuationBlocking(Task task)` → `bool`

| Task state | Action | Return |
| --- | --- | --- |
| `IsFaulted` | `Log.Error(task.Exception, "Task failed")` + blocking modal with `task.Exception?.InnerException?.Message ?? "Unknown error - please check logs."` titled `"Error"` | `false` |
| `IsCanceled` | blocking modal `"Task was canceled."` titled `"Error"` | `false` |
| else | none | `true` |

Note it inspects `InnerException` (unwrapping one level, e.g. `AsyncTaskResultWrapper` style nesting) rather than the outermost message, and it does not rethrow.

## 6. OTP and loading flows

| Member | Contract |
| --- | --- |
| `AskForOtp()` | `otpEntryPage.Reset()` then `State = OtpEntry` (fires `OnShow`) |
| `async Task<string?> WaitForOtpAsync()` | `await otpEntryPage.GetResultAsync().ConfigureAwait(false)` then **always** `otpEntryPage.StopOtpListener()` — the listener is torn down even when the result is `null` |
| `StartLoading(line1, line2 = "", line3 = "", isIndeterminate = true, canCancel = false, canDisableAutoLogin = false)` | `State = Loading` (fires `OnShow`), sets `Line1..3`, `IsIndeterminate`, `CanCancel`, `CanDisableAutoLogin`, then `LoadingPage.Reset()` — order matters: flags are set before the reset |
| `StopLoading()` | `State = Main` (also triggers `ReloadNews` if the previous state was `Settings`) |

`WaitForOtpAsync` returns whatever the OTP page produced (a code or `null`), leaving it to the caller to distinguish "no code" from failure.

## 7. `RunStartupTasks`

```
#if FLATPAK
    this.ftsPage.OpenFtsIfNeeded();
#endif
    this.mainPage.DoAutoLoginIfApplicable();
```

`FLATPAK` is a build-time `DefineConstants` switch (the same mechanism `XIVLauncher.Core.csproj` uses for `WIN32`/`OSX`/`LINUX`), so the Flatpak build proactively offers the free trial. `DoAutoLoginIfApplicable` consults `Config.IsAutologin` (and account state) inside `MainPage`.

## 8. `Draw()` contract (override of `Component.Draw`)

Two fullscreen windows plus the popup, in this exact order:

1. **Background window** `"Background"`:
   `PushStyleVar(WindowRounding, 0)`, `PushStyleVar(WindowPadding, Vector2(0))`, pos `(0,0)`, size `ImGuiHelpers.ViewportSize`, flags `NoTitleBar | NoBackground | NoResize | NoFocusOnAppearing | NoNavFocus | NoNavInputs | NoScrollbar | NoScrollWithMouse`, body `background.Draw()`, then `PopStyleVar(2)`.
2. **Content window** `"XIVLauncher"`:
   `PushStyleVar(WindowRounding, 0)`, `PushStyleVar(WindowPadding, CurrentPage.Padding ?? GetStyle().WindowPadding)` — the page's `Padding` (a `Vector2`, see `Margins.cs.md` note) drives window padding; pos `(0,0)`, size `ViewportSize`, `SetNextWindowBgAlpha(0.75f)`, `PushStyleColor(ImGuiCol.WindowBg, ImGuiColors.BlueShade0)`, flags `NoTitleBar | NoResize | NoScrollbar | NoScrollWithMouse`, body **`CurrentPage.Draw()` then `base.Draw()`** (children of the root component), `End()`, `PopStyleVar(2)`, `PopStyleColor()`.
3. **`DrawModal()`** last, so the modal always paints above the page.

Push/Pop balance: 2 pushes before each window, `PopStyleVar(2)` after each; one `PushStyleColor`/`PopStyleColor` pair. Any early `return` inside a page's draw must not break this balance (the balance is held entirely in this method).

Content alpha is 0.75 with `BlueShade0` (`ImGuiColors.cs.md` §2.4), letting the background bleed through — the reason the background window must have `NoBackground`.

## 9. `DrawModal()` detail

- `SetNextWindowSize(450, 300)` then `BeginPopupModal(modalTitle + "###xl_modal", ref isModalDrawing, AlwaysAutoResize | NoScrollbar | NoTitleBar)`.
  - The popup's ImGui **id** is the concatenated string; `"###xl_modal"` is the project's uniqueness marker, appended so the popup id cannot collide with a page's window id while the *displayed* title stays `modalTitle` (ImGui hides text after its own `###` id-splitting convention).
  - The `ref isModalDrawing` argument is ImGui's "open flag": ImGui drives it to `false` when the popup closes itself (e.g. via `CloseCurrentPopup`), keeping the field authoritative.
- Inside: `BeginChild("###xl_modal_scrolling", new Vector2(0, -GetTextLineHeightWithSpacing() * 2))` then `TextWrapped(modalText)` — a scrollable child window with **negative bottom padding of two text lines** (the message can scroll, and the reserved two lines are where the button sits).
- Button: `BUTTON_WIDTH = 120f` (`const float`), cursor X = `(GetWindowWidth() - 120) / 2` (centred), `ImGui.Button(modalButtonText, Vector2(120, 40))`; on press → `modalButtonPressAction()`.
- `AlwaysAutoResize` + a fixed `SetNextWindowSize` ⇒ the 450×300 hint is a starting size, not a bound.
- Closing: `ImGui.EndPopup()`; then (outside the `if`) the `modalOnNextFrame` open-popup block described in §5.

## 10. Invariants and hazards

- **Single modal only.** `ShowMessage` throws while one is open; `ShowExceptionBlocking`/`HandleContinuationBlocking` are therefore unusable as "toast" spam. There is no queue.
- **`modalWaitHandle` is the only synchronisation**, and it is armed by blocking callers; the non-blocking path (`ShowMessage`) with a caller-supplied action that never calls `modalWaitHandle.Set()` leaves a blocking caller parked forever. Only the default action sets it.
- **`isModalDrawing` vs `modalOnNextFrame` interlock:** a `ShowMessage` immediately followed by another `ShowMessage` in the same frame throws (first call sets `isModalDrawing`), even though the popup has not opened yet.
- `IsDebug` is `static` mutable state derived from `Debugger.IsAttached` at first touch (and unconditionally `true` in Debug builds); a debug build therefore always shows debug UI.
- `State` setter's default arm and `CurrentPage`'s `_` arm mean the enum and the page list must stay in sync when adding a page — three places to change: enum, setter switch, `CurrentPage` switch (plus the field and the ctor).
- `WaitForOtpAsync` disables the OTP listener after awaiting; two concurrent waits are not supported.
- `Steam` may be `null` (Steam failed to initialise) and is passed straight to `Launcher` in step 2 of the ctor — `Launcher` must tolerate a null Steam handle (contract of that unit, out of scope here).
- `UniqueIdCache` is the lib cache type (`CommonUniqueIdCache`, `:115`), constructed from `uidCache.json` at `:130`; the fork's same-named `XIVLauncher.Core.Configuration.UniqueIdCache` (`Configuration/UniqueIdCache.cs.md`) is a dead null-object stub and is never constructed here — do not treat `App.UniqueIdCache` as that class.
- `#if FLATPAK` and `#if DEBUG` are preprocessor switches; the csproj injects `WIN32`/`OSX`/`LINUX` and a configurable `ExtraDefineConstants`, which is the documented mechanism for `FLATPAK`.

## 11. Referenced units

`Component` (base), `MainPage`, `SettingsPage`, `OtpEntryPage`, `LoadingPage`, `FtsPage`, `SteamDeckPromptPage`, `Background`, `Page`, `AccountManager`, `CommonUniqueIdCache` (`lib/…/XIVLauncher.Common/CommonUniqueIdCache.cs`, implements `lib/…/PlatformAbstractions/IUniqueIdCache.cs`; the fork's dead stub counterpart is spec'd in `docs/XIVLauncher.Core/Configuration/UniqueIdCache.cs.md`), `Launcher`, `SeVersion`, `Repository.Boot`, `EnvironmentSettings.IsNoKillswitch`, `ApiHelpers.GenerateAcceptLanguage`, `Strings.XIVLauncherError` (`Resources/Localization/Strings.Designer.cs.md` §7, the only `Strings.*` read in this file), `Program` (`Config`, `Steam`), `ImGuiColors.BlueShade0`, `ImGuiHelpers.ViewportSize`, `CommonSettings` (`docs/XIVLauncher.Core/Configuration/CommonSettings.cs.md`, constructed at `:49`, unused), `Settings` alias of `Program.Config` (`:107`, schema in `docs/XIVLauncher.Core/Configuration/ILauncherConfig.cs.md`).
