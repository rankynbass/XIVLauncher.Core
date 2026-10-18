# LoadingPage.cs — Specification

**File:** `src/XIVLauncher.Core/Components/LoadingPage/LoadingPage.cs` · 113 lines · 3122 bytes
**Namespace:** `XIVLauncher.Core.Components.LoadingPage` · **Declaration:** `public class LoadingPage : Page`
**Role:** The "working on something" screen: up to three centred lines, an optional spinner *or* progress bar, and optional cancel / disable-auto-login buttons

---

## 1. Purpose

`LoadingPage` is the one page that is not user-navigable: `LauncherApp` switches into it while a task runs (`LauncherApp.cs.md` §"the `State` setter" → `LauncherState.Loading`) and switches out when the task finishes. Everything on screen is a view of fields that the task writes from outside the draw path.

It is constructed once in `LauncherApp`'s ctor (`LauncherApp.cs.md` §ctor: `this.loadingPage = new LoadingPage(this);`) and reached through the `LoadingPage` accessor (get-only, `LauncherApp.cs.md` §surface). Usings: `System.Numerics`, `Hexa.NET.ImGui`, `XIVLauncher.Core.Components.Common` (for `Button`/`Spinner`), `XIVLauncher.Core.Resources.Localization` (`Strings`).

## 2. Surface — every field is a public per-frame knob

| Member | Kind | Default | Written by |
| --- | --- | --- | --- |
| `IsIndeterminate` | `bool get/set` | `false` | `LauncherApp.StartLoading`'s `isIndeterminate` arg |
| `CanCancel` | `bool get/set` | `true` | `StartLoading`'s `canCancel` |
| `CanDisableAutoLogin` | `bool get/set` | `false` | `StartLoading`'s `canDisableAutoLogin` |
| `Progress` | `float get/set` | `0` | `MainPage/MainPage.cs.md` patch/repair progress loops (`MainPage.cs:955`, `:1065`, `:1071`, `:1077`) — see §5 |
| `Line1` | `string get/set` | `"Loading text line 1"` | `StartLoading` (`line1` is required) |
| `Line2`, `Line3` | `string? get/set` | `null` | `StartLoading` optional args; overwritten mid-run by `MainPage/MainPage.cs.md` patch/repair progress loops (`MainPage.cs:951/952`, `:1063-1077`) |
| `ProgressText` | `string? get/set` | `null` | nobody — see §6 |
| `spinner` | `private Spinner` | ctor: `new Spinner(SPINNER_RADIUS, 5, GetColorU32(ButtonActive))` | — |
| `cancelButton` | `private Button = new(Strings.CancelLabel)` | field initializer | — |
| `disableAutoLoginButton` | `private Button = new(Strings.DisableAutoLoginButton)` | field initializer | — |
| `hasDisabledAutoLogin` | `private bool` | `false` | button-click closure; cleared by `Reset()` |
| `Cancelled` | `public event Action?` | — | fired by `cancelButton.Click` |
| `SPINNER_RADIUS` | `private const int = 15` | — | ctor + `Draw()` centring |

Field-initializer construction of the two buttons (`private Button cancelButton = new(Strings.CancelLabel);` — C# 13 target-typed `new`) means their `Label` strings are captured **once at page construction**. `Strings.CancelLabel` is a generated accessor in `src/XIVLauncher.Core/Resources/Localization/Strings.Designer.cs` that resolves through `System.Resources.ResourceManager` at call time, so the *resolved text* is fixed at startup — there is no `SetLanguage` API in this fork (localization follows the process locale, and cannot change mid-session), so this is not a bug: labels are per-run constants. If a language switch is ever added, it must happen before `LauncherApp` construction (`LauncherApp.cs:115-135`) for these labels to pick it up.

## 3. Constructor: the two click closures

```csharp
this.spinner = new Spinner(SPINNER_RADIUS, 5, ImGui.GetColorU32(ImGuiCol.ButtonActive));
this.cancelButton.Click += () => this.Cancelled?.Invoke();
this.disableAutoLoginButton.Click += () => { this.hasDisabledAutoLogin = true; App.Settings.IsAutologin = false; };
```

- **The cancel button fires `Cancelled`, and nothing subscribes to it.** Grep across `src/XIVLauncher.Core` finds the single `+=` on `cancelButton.Click` and zero `Cancelled +=` sites, so clicking cancel currently ends with an event that has no listeners: the loading task keeps running and the page stays on screen. This is the honest current behaviour of `CanCancel`, and the fix belongs here (subscribe `Cancelled` to something that can abort the task, or drive a page-side flag the task polls) — see `LauncherApp.cs.md` §task-await translation for the shape used elsewhere. `LoadingPage.cs.md`'s `Cancelled` must be read as "cancel was requested", not "cancel happened".
- **The disable-auto-login closure writes persisted config:** `App.Settings.IsAutologin = false` (`App.Settings` is `Program.Config`, see `LauncherApp.cs.md` §"Settings" and `Program.cs.md` §persistence) — a settings write from inside a draw-time closure. It is a *deliberate* side effect (the same value is re-read every frame by the visibility test, §5) and it is why `hasDisabledAutoLogin` exists: after the setting flips, `isDrawDisableAutoLogin` becomes false, and only `hasDisabledAutoLogin` keeps a confirmation line on screen for the rest of the run (`Strings.AutoLoginDisabledNextStart`).
- The write is **not** reverted when the run ends; only `MainPage/MainPage.cs.md`'s auto-login machinery (`DoAutoLoginIfApplicable`) and the launcher settings UI reflect it on the next launch. Do not "balance" this with a restore in `Reset()` — `Reset()` clears only `hasDisabledAutoLogin` (§4) because the persisted choice is meant to persist.
- Closure ordering note: both handlers are `+=`-composed on `Common/Button.cs.md`'s `event Action? Click`; a single click fires exactly one handler, once.

## 4. `Reset()`

```csharp
public void Reset() { this.hasDisabledAutoLogin = false; }
```

Called **only** by `LauncherApp.StartLoading(...)` (`LauncherApp.cs:243`, after the six flag/text assignments — order matters only for `hasDisabledAutoLogin`, which the assignments do not touch). The `State` setter's `LauncherState.Loading` branch calls `this.LoadingPage.OnShow()` (`LauncherApp.cs:75`), which for this class is `Page`'s empty hook (`Page.cs.md` §"OnShow contract"), so entering the loading state without going through `StartLoading` performs no reset at all. Callers must go through `StartLoading` — which is what `MainPage/MainPage.cs.md` §login (`App.StartLoading(Strings.LoggingIn, canDisableAutoLogin: true)`) and `LauncherApp.cs.md` §auto-login do.

The reset is deliberately minimal: `Line1..3`, `Progress`, `IsIndeterminate`, `CanCancel`, `CanDisableAutoLogin` are **not** cleared here — `StartLoading` always assigns them, so `Reset()` alone (i.e. the `State`-setter path without `StartLoading`) leaves stale text on screen. Callers must go through `StartLoading`, which is what `MainPage/MainPage.cs.md` §login (`App.StartLoading(Strings.LoggingIn, canDisableAutoLogin: true)`) and `LauncherApp.cs.md` §auto-login do.

## 5. `Draw()` — layout and gating, top to bottom

```
vp = ImGuiHelpers.ViewportSize
SetCursorPosY(vp.Y / 2 - 100)
CenteredText(Line1)
if Line2 non-empty → Dummy(2,2); CenteredText(Line2)
if Line3 non-empty → Dummy(2,2); CenteredText(Line3)
isDrawDisableAutoLogin = CanDisableAutoLogin && (App.Settings.IsAutologin ?? false)
if (CanCancel || isDrawDisableAutoLogin) → Dummy(20)
if CanCancel → Width = vp.X/4; CenterCursorFor(Width.Value); cancelButton.Draw()
if isDrawDisableAutoLogin → same, disableAutoLoginButton
else if hasDisabledAutoLogin → CenteredText(Strings.AutoLoginDisabledNextStart)
Dummy(20)
if IsIndeterminate → CenterCursorFor(SPINNER_RADIUS*2); spinner.Draw()
else → width = vp.X/3; CenterCursorFor(width); ImGui.ProgressBar(Progress, (width,20), ProgressText)
base.Draw()
```

- **Vertical layout is viewport-relative and hand-placed** (`vp.Y / 2 - 100` then implicit flow), not flexed: three text lines + buttons + gauge must fit under that offset, and on a short window the gauge can overflow the window bottom. `ImGuiHelpers.ViewportSize` is a live accessor (`ImGuiHelpers.cs.md`), so this reflows on resize mid-frame.
- **Empty-optional handling** uses `string.IsNullOrEmpty` on `Line2`/`Line3`: an empty (not just null) line is skipped, and the `Dummy(2,2)` gap is only emitted for lines that render. `Line1` has no such guard, so a `""` `Line1` still consumes a centred-text slot (`CenteredText("")`) — `StartLoading`'s `line1` is required, so this is an unreachable-but-not-defensive path.
- **Button widths are recomputed per frame** (`vp.X / 4`) and centred with `ImGuiHelpers.CenterCursorFor(Width.Value)`. The `.Value` unwrap is safe *only* because the assignment immediately precedes it; see `Common/Button.cs.md` §usage contract for that rule.
- **`if (CanCancel || isDrawDisableAutoLogin) Dummy(20)`** reserves the button row whenever either button may appear, so the gauge never collides with buttons — but that gap is present even when a cancel click would do nothing (see §3), i.e. the layout cost is paid for an inert feature.
- **`isDrawDisableAutoLogin` reads persisted config every frame** (`App.Settings.IsAutologin ?? false`) — a settings read in the draw path. `Config.Net`-backed accessors are cheap but this is the only place in the package where the frame loop reads config directly; note the `?? false` treats "unset" as "auto-login off", whereas `MainPage/MainPage.cs.md` §`DoAutoLoginIfApplicable` treats null the same way (`App.Settings.IsAutologin` guarded) — keep the two in step if the default ever changes.
- **`else if (hasDisabledAutoLogin)`** prints the confirmation line *in the button slot*, so after a successful disable the row stays occupied by text instead of a button.
- **Gauge choice:** `IsIndeterminate ? Spinner : ImGui.ProgressBar(Progress, Vector2(vp.X/3, 20), ProgressText)`. `Progress` **is** written — but only from `MainPage/MainPage.cs.md` while patching (`MainPage.cs:955`, in the status thread described in `MainPage.cs.md` §patching) and while repairing (`MainPage.cs:1065`/`:1071`/`:1077`), and only when `StartLoading(..., isIndeterminate: false)` is used, which is exactly those two call sites (`MainPage.cs.md` §patching cites the `canCancel: false, isIndeterminate: false` shape). Everywhere else (`MainPage.cs.md` §login, `LauncherApp.cs.md` §auto-login, the Dalamud wait) the spinner branch runs and the gauge is never seen. `ProgressText` is the genuinely unwritten field: the bar has no caption today, which is the gap the `LoadingPage/DalamudOverlayInfoProxy.cs.md` §"ReportProgress is a stub" bridge should fill — that bridge would also be the third `Progress` writer. `LauncherApp.StartLoading` has no progress argument (`LauncherApp.cs.md` §start-loading signature), so progress reaches this page by direct field assignment from the task, not through the API.
- `Spinner` details (`Spinner.cs.md`): the constructor captures `ImGui.GetColorU32(ImGuiCol.ButtonActive)` **once at startup**, so the spinner's colour does not follow the loading-page theme pushed by `LauncherApp.Draw()` (`LauncherApp.cs.md` §8) — that contrast is intentional in this file and is why the spinner looks slightly different in tone from the gauge.
- `base.Draw()` runs last (`Page` inherits `Component.Draw`, `Page.cs.md`/`Component.cs.md`): children would paint below the gauge. None exist.

## 6. Invariants, dead plumbing and intended fixes

- **Dead today:** `ProgressText` (never written), `Cancelled` (no subscribers). **Live:** all three `Can*` flags, `Line1..3`, `IsIndeterminate`, `Progress` (written only by the patch/repair loops in `MainPage/MainPage.cs.md`, see §5). Do not delete the dead ones as "cleanup": they are the API `LauncherApp.StartLoading` already exposes (`LauncherApp.cs.md` §start-loading) and the natural home for the Dalamud progress bridge (`LoadingPage/DalamudOverlayInfoProxy.cs.md` §4).
- **No `OnShow()` override:** the class relies on `Page`'s empty hook (`Page.cs.md` §"OnShow contract"); a future "clear the lines on entry" belongs in an override that calls `base.OnShow()`.
- **No window creation:** the page paints in the content window that `LauncherApp.Draw()` opens (`LauncherApp.cs.md` §8), unlike `MainPage/MainPage.cs.md` which opens child windows — so there is no child-window id to namespace here and `ImGui.IsWindowAppearing()`-style tricks are unavailable.
- **The three text lines are not localized-looked-up here**: `Line1..3` arrive as already-resolved `Strings.*` values from callers (`MainPage/MainPage.cs.md`, `LauncherApp.cs.md`), which is the project convention (callers resolve strings; pages display them). Keep it that way: localizing in this file would double-translate.
- **No cancel state machine:** there is no "cancel requested → task aborts → show a cancellation message" path in this file. `LauncherApp.cs.md` §"task-await translation" lists `IsCanceled` as the *task*-side signal; the UI-side counterpart is the un-subscribed `Cancelled` event. If implementing, subscribe in `StartLoading`'s sibling path rather than adding per-page flags, and keep `hasDisabledAutoLogin`-style "confirmation" lines separate from "cancelled" lines.
- **Button visibility vs. hitbox:** both buttons are `Common/Button.cs.md` instances, whose `IsEnabled` is unread there (`Common/Button.cs.md` §dead flag); visibility here is controlled by the `Can*` flags, which is the correct lever — do not set `Button.IsEnabled` expecting a visual change.
- **`SPINNER_RADIUS` duplication:** `15` is used by the ctor argument *and* the `CenterCursorFor(SPINNER_RADIUS * 2)` call; `Spinner.cs.md` §"contract for callers" notes the value is not exposed by `Spinner`, so this file is the single source of the number for both uses. Changing it changes both consistently — changing only the ctor argument does not.

## 7. Referenced / referenced-by

Constructed / driven by: `LauncherApp.cs.md` (ctor, `StartLoading`, `State` setter, `CurrentPage`), `MainPage/MainPage.cs.md` (`App.StartLoading(Strings.LoggingIn, canDisableAutoLogin: true)`), `LauncherApp.cs.md` §auto-login.
Uses: `Page` (`Page.cs.md`), `Component` (`Component.cs.md`), `Common/Button.cs.md`, `Spinner.cs.md`, `ImGuiHelpers.cs.md` (`CenteredText`, `CenterCursorFor`, `ViewportSize`), `Strings` (`src/XIVLauncher.Core/Resources/Localization/Strings.Designer.cs`), `App.Settings` (`Program.cs.md`), `ImGui.ProgressBar` (Hexa binding).
Sibling in the same folder, unrelated to drawing: `LoadingPage/DalamudOverlayInfoProxy.cs.md`.
