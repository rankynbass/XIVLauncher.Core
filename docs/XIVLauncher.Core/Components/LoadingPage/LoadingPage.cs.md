# LoadingPage.cs — Specification

**File:** `src/XIVLauncher.Core/Components/LoadingPage/LoadingPage.cs` · 150 lines · 5,504 bytes
**Namespace:** `XIVLauncher.Core.Components.LoadingPage` · **Declaration:** `public class LoadingPage : Page`
**Role:** The loading page: three status lines, cancel / disable-auto-login buttons, spinner or progress bar, and a progress label

---

## 1. Purpose

`LoadingPage` is the page `LauncherApp` shows for `LauncherState.Loading`. Patching, file verification, login, and Dalamud setup all write into its properties, and it is the page most written to from background threads — the property setters push the resulting UI update to the UI thread through `Ui.Post` (`Ui.cs.md`).

The ImGui version read `Line1/Line2/Line3/Progress/CanCancel/CanDisableAutoLogin` on every frame. The Eto version keeps the same property surface and pushes state to the controls, so background patching threads can keep writing to the same properties.

Usings: `Eto.Forms`, `XIVLauncher.Core.Resources.Localization`.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `SPINNER_RADIUS` | `private const int = 15` (line 19) | spinner size (`Spinner.cs.md`) |
| `spinner` | `private readonly Spinner` (line 21) | built at line 65 with thickness 5, `EtoColors.Blue` |
| `progressBar` | `private readonly ProgressBar` (line 22) | `MinValue = 0`, `MaxValue = 1000`, `Width = 400` (lines 68–73) |
| `progressLabel` | `private readonly Label` (line 23) | `TextAlignment = Center` (line 75) |
| `line1Label`, `line2Label`, `line3Label` | `private readonly Label` (lines 24–26) | each `Wrap = WrapMode.Character` (lines 77–79) |
| `autoLoginLabel` | `private readonly Label` (line 27) | text `Strings.AutoLoginDisabledNextStart` (line 80) |
| `cancelButton`, `disableAutoLoginButton` | `private readonly Common.Button` (line 28) | `Width = 320` each (lines 82–89) |
| `hasDisabledAutoLogin` | `private bool` (line 31) | per-session flag, reset by `Reset()` |
| `IsIndeterminate` | `public bool` (line 33) | spinner vs progress bar |
| `CanCancel` | `public bool` (line 35), default `true` | cancel button visibility |
| `CanDisableAutoLogin` | `public bool` (line 37) | disable-auto-login button visibility gate |
| `Line1` | `public string` (line 39), default `"Loading text line 1"` | main status line |
| `Line2`, `Line3` | `public string?` (lines 41, 43) | secondary lines, hidden when empty |
| `ProgressText` | `public string?` (line 45) | label under the progress bar |
| `Progress` | `public float` (lines 47–55) | setter posts `Refresh` via `Ui.Post` |
| `progress` | `private float` (line 57) | backing field |
| `Cancelled` | `public event Action?` (line 60) | raised by the cancel button |
| `LoadingPage(LauncherApp app)` | ctor (lines 62–116) | builds controls + stack |
| `Reset()` | `public` (lines 118–122) | clears `hasDisabledAutoLogin`, refreshes |
| `OnShow()` | `public override` (line 124) | `=> this.Refresh()` |
| `Refresh()` | `public` (lines 127–149) | pushes state onto the controls |

## 3. Progress scaling

`Progress` is a `float` in `0..1`; `Refresh()` writes `progressBar.Value = (int)Math.Round(this.Progress * 1000)` (line 145) because Eto's `ProgressBar.Value` is an integer and `MaxValue` is set to 1000. The permille scaling is the only numeric transformation in the file.

`Progress`'s setter is the **only** setter that marshals (`Ui.Post(this.Refresh)`, line 52); `Line1/Line2/Line3/ProgressText/IsIndeterminate/CanCancel/CanDisableAutoLogin` are plain auto-properties, so writing them from a background thread does **not** refresh the UI immediately. The refresh happens later via `Refresh()`: `OnShow()` calls it, and `Progress`'s setter (which most patching loops write last) pulls the whole state onto the controls. `StartLoading` never writes `Progress` or `ProgressText` (`LauncherApp.cs:259-270`), so patching/verification loops are responsible for the first `Progress` write that flushes the state.

**Thread safety:** `Ui.Post` is `Application.Instance.AsyncInvoke` (`Ui.cs.md`), so `Progress = x` from any thread is safe. Writing `Line2`/`Line3` from a background thread mutates plain fields; the eventual `Refresh()` reads them on the UI thread. `Reset()` and `OnShow()` call `Refresh()` **directly**, so a caller that invokes them off the UI thread performs GTK widget writes off the UI thread — `LauncherApp.StartLoading` does exactly that for login, Dalamud, compat-tool, patching, and repair (`MainPage.cs:145, 608, 741, 748, 943, 1060`; `LauncherApp.cs.md` §7).

## 4. Control visibility rules

`Refresh()` (lines 127–149) is the single place that maps state to widgets:

| Condition | Effect |
| --- | --- |
| `Line1` | `line1Label.Text = Line1` (line 129) — always visible |
| `Line2` non-empty | `line2Label.Text`, `Visible = !IsNullOrEmpty` (lines 131–132) |
| `Line3` non-empty | `line3Label.Text`, `Visible = !IsNullOrEmpty` (lines 134–135) |
| `showDisableAutoLogin = CanDisableAutoLogin && (App.Settings.IsAutologin ?? false)` (line 137) | gates the disable button |
| `CanCancel` | `cancelButton.Visible` (line 139) |
| `showDisableAutoLogin` | `disableAutoLoginButton.Visible` (line 140) |
| `hasDisabledAutoLogin && !showDisableAutoLogin` | `autoLoginLabel.Visible` (line 141) |
| `IsIndeterminate` | `spinner.Visible` (line 143); `progressBar.Visible = !IsIndeterminate` (line 144) |
| `Progress * 1000` rounded | `progressBar.Value` (line 145) |
| `!IsIndeterminate && !IsNullOrEmpty(ProgressText)` | `progressLabel` text + visibility (lines 147–148) |

The disable-auto-login button (lines 86–89) sets `hasDisabledAutoLogin = true`, writes `App.Settings.IsAutologin = false` (Config.Net-backed persistence), then calls `Refresh()`. `Reset()` clears the flag; `LauncherApp.StartLoading` calls `Reset()` (`LauncherApp.cs.md` §7).

The "auto-login disabled next start" label appears only after the user pressed disable *and* the disable button is not currently shown.

## 5. Stack order

`this.Content = new StackLayout` (lines 97–115) with `Spacing = 8` (line 99), items in this order, all `HorizontalAlignment.Center`, with expanding `Panel()` spacers first and last (lines 102, 112):

1. `line1Label` (line 103)
2. `line2Label`
3. `line3Label`
4. `cancelButton`
5. `disableAutoLoginButton`
6. `autoLoginLabel`
7. `spinner` (line 109)
8. `progressBar`
9. `progressLabel`

This reproduces HEAD's immediate-mode draw order (lines, cancel/disable buttons, then the spinner or progress bar), with the spacers reproducing the vertical padding the ImGui layout produced. The user-verified layout on GTK: the centered stack has visible spacing between lines, buttons, bar, and label, so `Spacing = 8` replaces the immediate-mode spacing the ImGui layout produced.

## 6. Consumers

| Consumer | Site | Use |
| --- | --- | --- |
| `LauncherApp.StartLoading` | `LauncherApp.cs:259` | sets `State = Loading`, `Line1` (line 262), `Line2`, `Line3`, `IsIndeterminate` (line 265), `CanCancel` (line 266), `CanDisableAutoLogin` (line 267), then `LoadingPage.Reset()` (line 269) |
| `LauncherApp.StopLoading` | `LauncherApp.cs:272` | only sets `State = Main`; it does not touch `LoadingPage` |
| `LauncherApp.SetState` | `LauncherApp.cs:89` | `this.LoadingPage.OnShow()` on entering `LauncherState.Loading` |
| `MainPage` login | `MainPage.cs:145` | `StartLoading(Strings.LoggingIn, canDisableAutoLogin: true)` |
| `MainPage` Dalamud / compat tool / game start | `MainPage.cs:608`, `741`, `748` | `StartLoading` with fixed lines, indeterminate |
| `MainPage` patching | `MainPage.cs:943`, `958`, `959`, `962` | `Line2` (working-on status), `Line3` (left-to-download), `Progress` (install index ratio) |
| `MainPage` file verification | `MainPage.cs:1060`, `1070-1072`, `1076-1078`, `1082-1084` | `Line2`/`Line3`/`Progress` per verifier state |
| `LauncherApp` ctor | `LauncherApp.cs:148` | page built once on the UI thread |

`App.StartLoading(..., canDisableAutoLogin: true)` at `MainPage.cs:145` is the login path that shows the disable-auto-login button.

## 7. Quirks

- **`Cancelled` has no consumer.** The cancel button raises it (`cancelButton.Click += () => this.Cancelled?.Invoke()`, line 85), and nothing in `src/XIVLauncher.Core` subscribes (`LauncherApp.StartLoading` has no `onCancel` parameter, `LauncherApp.cs:259`; `StopLoading` only sets `State = Main`, `LauncherApp.cs:272`). The cancel button is therefore visible whenever `CanCancel` is true and clicking it does nothing; `StartLoading`'s `canCancel` defaults to `false` (`LauncherApp.cs:259`) and every `MainPage` call site leaves it at that default, so in practice `CanCancel` stays false (the field's own default is `true`, `LoadingPage.cs:35`).
- `progressBar.Width = 400` is fixed; the spinner's size comes from `Spinner`'s own formula (`Spinner.cs.md`), and the buttons are `Width = 320`.
- `Line1`'s default value is the literal `"Loading text line 1"` — visible if the page is shown before `LauncherApp.StartLoading` sets real text.
- `App.Settings.IsAutologin` is read as `bool?` (`?? false`), so a null setting means "not auto-logging in", hiding the disable button.
- The `Wrap = WrapMode.Character` on the three status labels lets long patch messages wrap character-by-character, matching HEAD's text wrapping; labels have no explicit width, so GTK natural width governs line breaks (`Components/MainPage/NewsFrame.cs.md` §4 explains the same width behaviour).
- `Refresh()` is public, so external code can force a re-push; `MainPage` does not call it directly (it relies on `Progress`'s setter and `LauncherApp`'s state change).

## 8. Referenced / referenced-by

Uses: `Page` (`Page.cs.md`), `Spinner` (`Spinner.cs.md`), `Common.Button` (`Components/Common/Button.cs.md`), `EtoColors` (`EtoColors.cs.md`), `Ui` (`Ui.cs.md`), `App.Settings`, `Strings`, `Eto.Forms.ProgressBar`/`Label`/`StackLayout`.
Referenced by: `LauncherApp` (construction, `StartLoading`, `StopLoading`, `SetState`), `MainPage` (patching + verification progress).
