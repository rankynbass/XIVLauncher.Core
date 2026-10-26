# LoadingPage.cs — Specification

**File:** `src/XIVLauncher.Core/Components/LoadingPage/LoadingPage.cs` · 198 lines · 7,117 bytes
**Namespace:** `XIVLauncher.Core.Components.LoadingPage` · **Declaration:** `public class LoadingPage : Page`
**Role:** The loading page: three status lines, cancel / disable-auto-login buttons, spinner or progress bar, and a progress label

---

## 1. Purpose

`LoadingPage` is the page `LauncherApp` shows for `LauncherState.Loading`. Patching, file verification, login, and Dalamud setup all write into its properties from worker threads. Every property setter marshals the resulting widget update with `Ui.Post` (`Ui.cs.md`), so no widget is touched off the UI thread.

The ImGui version read `Line1/Line2/Line3/Progress/CanCancel/CanDisableAutoLogin` on every frame. The Eto version keeps the same property surface and pushes state to the controls, so background patching threads can keep writing to the same properties.

Usings: `Eto.Forms`, `XIVLauncher.Core.Resources.Localization`.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `SPINNER_RADIUS` | `private const int = 15` (line 18) | spinner size (`Spinner.cs.md`) |
| `spinner` | `private readonly Spinner` (line 20) | built at line 108 with thickness 5, `EtoColors.Blue` |
| `progressBar` | `private readonly ProgressBar` (line 21) | `MinValue = 0`, `MaxValue = 1000`, `Width = 400` (lines 111–116) |
| `progressLabel` | `private readonly Label` (line 22) | `TextAlignment = Center` (line 118) |
| `line1Label`, `line2Label`, `line3Label` | `private readonly Label` (lines 23–25) | each `Wrap = WrapMode.Character` (lines 120–122) |
| `autoLoginLabel` | `private readonly Label` (line 26) | text `Strings.AutoLoginDisabledNextStart` (line 123) |
| `cancelButton`, `disableAutoLoginButton` | `private readonly Common.Button` (lines 27–28) | `Width = 320` each (lines 126, 130) |
| `hasDisabledAutoLogin` | `private bool` (line 30) | per-session flag, reset by `Reset()` |
| backing fields | `private` (lines 32–39) | `isIndeterminate`, `canCancel` (default `true`), `canDisableAutoLogin`, `line1` (default `"Loading text line 1"`), `line2`, `line3`, `progressText`, `progress` |
| `IsIndeterminate` | `public bool` (lines 41–45) | spinner vs progress bar |
| `CanCancel` | `public bool` (lines 47–51) | cancel button visibility |
| `CanDisableAutoLogin` | `public bool` (lines 53–57) | disable-auto-login button visibility gate |
| `Line1` | `public string` (lines 59–63) | main status line |
| `Line2`, `Line3` | `public string?` (lines 65–69, 71–75) | secondary lines, hidden when empty |
| `ProgressText` | `public string?` (lines 77–81) | label under the progress bar |
| `Progress` | `public float` (lines 83–87) | progress bar fill, `0..1` |
| `SetStateField<T>` | `private` (lines 93–100) | shared setter: store + `Ui.Post(this.ApplyState)` on change |
| `Cancelled` | `public event Action?` (line 103) | raised by the cancel button |
| `LoadingPage(LauncherApp app)` | ctor (lines 104–158) | builds controls + stack |
| `Reset()` | `public` (lines 161–165) | clears `hasDisabledAutoLogin`, posts `ApplyState` |
| `OnShow()` | `public override` (line 167) | `=> Ui.Post(this.ApplyState)` |
| `Refresh()` | `public` (line 170) | `=> Ui.Post(this.ApplyState)` |
| `ApplyState()` | `private` (lines 173–197) | writes the controls; UI thread only |

## 3. State marshalling

All eight properties share one setter path, `SetStateField<T>` (lines 93–100): store the value, and on change post `ApplyState` with `Ui.Post` (`Application.Instance.AsyncInvoke`, `Ui.cs.md`). `Reset()` (line 164), `OnShow()` (line 167), and `Refresh()` (line 170) post `ApplyState` the same way, and `ApplyState` asserts UI-thread ownership with `Ui.AssertUIThread("LoadingPage.ApplyState")` (line 175; Debug-only, `Ui.cs.md`).

This is the fix for the login crash. `LauncherApp.StartLoading` (`LauncherApp.cs:259-269`) is called from login, Dalamud, compat-tool, patching, and repair threads (`MainPage.cs:147, 620, 753, 760, 955, 1072`), and the pre-fix path `StartLoading → LoadingPage.Reset() → Refresh() → Label.Text` ran GTK size negotiation on a worker thread. Eto's own check (`Application.EnsureUIThread`, reached through `Control.Handler`) logged 32 warnings on that path in a selftest run; the same writes on a worker thread in a standalone Eto/GTK repro segfaulted inside pango (`pango_shape_item`/`pango_layout_get_extents` with a NULL layout), which is the crash the launcher core dumps showed. With the marshalled setters, the same selftest run logs zero warnings.

`SetStateField` skips equal values, so a repeated write (patching loops write `Line2`/`Line3`/`Progress` every iteration) posts one queued `ApplyState` per change, not per iteration; GTK coalesces the queued updates.

## 4. Progress scaling

`Progress` is a `float` in `0..1`; `ApplyState` writes `progressBar.Value = (int)Math.Round(this.Progress * 1000)` (line 193) because Eto's `ProgressBar.Value` is an integer and `MaxValue` is set to 1000. The permille scaling is the only numeric transformation in the file.

`StartLoading` never writes `Progress` or `ProgressText` (`LauncherApp.cs:259-270`), so patching/verification loops are responsible for the first `Progress` write that fills the bar.

## 5. Control visibility rules

`ApplyState` (lines 173–197) is the single place that maps state to widgets:

| Condition | Effect |
| --- | --- |
| `Line1` | `line1Label.Text = Line1` (line 177) — always visible |
| `Line2` non-empty | `line2Label.Text`, `Visible = !IsNullOrEmpty` (lines 179–180) |
| `Line3` non-empty | `line3Label.Text`, `Visible = !IsNullOrEmpty` (lines 182–183) |
| `showDisableAutoLogin = CanDisableAutoLogin && (App.Settings.IsAutologin ?? false)` (line 185) | gates the disable button |
| `CanCancel` | `cancelButton.Visible` (line 187) |
| `showDisableAutoLogin` | `disableAutoLoginButton.Visible` (line 188) |
| `hasDisabledAutoLogin && !showDisableAutoLogin` | `autoLoginLabel.Visible` (line 189) |
| `IsIndeterminate` | `spinner.Visible` (line 191); `progressBar.Visible = !IsIndeterminate` (line 192) |
| `Progress * 1000` rounded | `progressBar.Value` (line 193) |
| `!IsIndeterminate && !IsNullOrEmpty(ProgressText)` | `progressLabel` text + visibility (lines 195–196) |

The disable-auto-login button (lines 131–135) sets `hasDisabledAutoLogin = true`, writes `App.Settings.IsAutologin = false` (Config.Net-backed persistence), then calls `Refresh()`. `Reset()` clears the flag; `LauncherApp.StartLoading` calls `Reset()` (`LauncherApp.cs.md` §7).

The "auto-login disabled next start" label appears only after the user pressed disable *and* the disable button is not currently shown.

## 6. Stack order

`this.Content = new StackLayout` (lines 140–158) with `Spacing = 8` (line 142), items in this order, all `HorizontalAlignment.Center`, with expanding `Panel()` spacers first and last (lines 145, 155):

1. `line1Label` (line 146)
2. `line2Label`
3. `line3Label`
4. `cancelButton`
5. `disableAutoLoginButton`
6. `autoLoginLabel`
7. `spinner` (line 152)
8. `progressBar`
9. `progressLabel`

This reproduces HEAD's immediate-mode draw order (lines, cancel/disable buttons, then the spinner or progress bar), with the spacers reproducing the vertical padding the ImGui layout produced. The user-verified layout on GTK: the centered stack has visible spacing between lines, buttons, bar, and label, so `Spacing = 8` replaces the immediate-mode spacing the ImGui layout produced.

## 7. Consumers

| Consumer | Site | Use |
| --- | --- | --- |
| `LauncherApp.StartLoading` | `LauncherApp.cs:259` | sets `State = Loading`, `Line1` (line 262), `Line2`, `Line3`, `IsIndeterminate` (line 265), `CanCancel` (line 266), `CanDisableAutoLogin` (line 267), then `LoadingPage.Reset()` (line 269) |
| `LauncherApp.StopLoading` | `LauncherApp.cs:272` | only sets `State = Main`; it does not touch `LoadingPage` |
| `LauncherApp.SetState` | `LauncherApp.cs:84` | `this.LoadingPage.OnShow()` on entering `LauncherState.Loading` |
| `MainPage` login | `MainPage.cs:147` | `StartLoading(Strings.LoggingIn, canDisableAutoLogin: true)` |
| `MainPage` Dalamud / compat tool / game start | `MainPage.cs:620`, `753`, `760` | `StartLoading` with fixed lines, indeterminate |
| `MainPage` patching | `MainPage.cs:955`, `970–974` | `Line2` (working-on status), `Line3` (left-to-download), `Progress` (install index ratio) |
| `MainPage` file verification | `MainPage.cs:1072`, `1082–1084`, `1088–1090`, `1094–1096` | `Line2`/`Line3`/`Progress` per verifier state |
| `Program` selftest | `Program.cs:295-312` | Debug-only, `XLCORE_SELFTEST=1`: drives `StartLoading` + patching/repair `Line2`/`Line3`/`Progress` writes from a worker thread |
| `LauncherApp` ctor | `LauncherApp.cs:149` | page built once on the UI thread |

`App.StartLoading(..., canDisableAutoLogin: true)` at `MainPage.cs:147` is the login path that shows the disable-auto-login button.

## 8. Quirks

- **`Cancelled` has no consumer.** The cancel button raises it (`cancelButton.Click += () => this.Cancelled?.Invoke()`, line 127), and nothing in `src/XIVLauncher.Core` subscribes (`LauncherApp.StartLoading` has no `onCancel` parameter, `LauncherApp.cs:259`; `StopLoading` only sets `State = Main`, `LauncherApp.cs:272`). The cancel button is therefore visible whenever `CanCancel` is true and clicking it does nothing; `StartLoading`'s `canCancel` defaults to `false` (`LauncherApp.cs:259`) and every `MainPage` call site leaves it at that default, so in practice `CanCancel` stays false (the backing field's default is `true`, `LoadingPage.cs:33`).
- `progressBar.Width = 400` is fixed; the spinner's size comes from `Spinner`'s own formula (`Spinner.cs.md`), and the buttons are `Width = 320`.
- `Line1`'s default value is the literal `"Loading text line 1"` — visible if the page is shown before `LauncherApp.StartLoading` sets real text.
- `App.Settings.IsAutologin` is read as `bool?` (`?? false`), so a null setting means "not auto-logging in", hiding the disable button.
- The `Wrap = WrapMode.Character` on the three status labels lets long patch messages wrap character-by-character, matching HEAD's text wrapping; labels have no explicit width, so GTK natural width governs line breaks (`Components/MainPage/NewsFrame.cs.md` §4 explains the same width behaviour).
- `Refresh()` is public and only posts, so external code can force a re-push from any thread; `MainPage` does not call it directly.
- `App.Settings.IsAutologin` is read on the UI thread inside `ApplyState` (line 185), so a worker-thread write of `IsAutologin` (`MainPage.cs:173`) never races the label decision.

## 9. Referenced / referenced-by

Uses: `Page` (`Page.cs.md`), `Spinner` (`Spinner.cs.md`), `Common.Button` (`Components/Common/Button.cs.md`), `EtoColors` (`EtoColors.cs.md`), `Ui` (`Ui.cs.md`), `App.Settings`, `Strings`, `Eto.Forms.ProgressBar`/`Label`/`StackLayout`.
Referenced by: `LauncherApp` (construction, `StartLoading`, `StopLoading`, `SetState`), `MainPage` (patching + verification progress), `Program` (Debug-only selftest).
