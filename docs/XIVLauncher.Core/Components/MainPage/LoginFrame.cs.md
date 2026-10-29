# LoginFrame.cs — Specification

**File:** `src/XIVLauncher.Core/Components/MainPage/LoginFrame.cs` · 176 lines · 7,398 bytes
**Namespace:** `XIVLauncher.Core.Components.MainPage` · **Declaration:** `public class LoginFrame : Component`
**Role:** The login column: username, password, the four option checkboxes, the login button with its login-action menu, the caret and friends buttons, and the secrets-provider warning

---

## 1. Purpose

Replaces the immediate-mode login child window. The right-click login-action popup becomes an Eto `ContextMenu` attached to the login button, so the caret icon button shows the same menu, and the friends icon button opens `AccountSwitcher` (`AccountSwitcher.cs.md`). State is exposed as properties (`Username`, `Password`, `IsOtp`, `IsSteam`, `IsAutoLogin`, `IsFreeTrial`) that forward to the underlying Eto controls, and `MainPage` reads/persists them (`MainPage.cs.md`).

Usings: `System.Numerics` (`Vector2` input spacing), `Eto.Forms`, `XIVLauncher.Core.Accounts.Secrets.Providers` (`DummySecretProvider` test for the warning label), `XIVLauncher.Core.Resources.Localization`.

Two size constants drive the login column geometry: `LoginInputWidth = 300 - 32` (line 27; each entry fills the 300 px login column, inset 32 px on the left — the right side is flush because the login column is pinned to the window's right edge) and `ExtraButtonSize = 32` (line 30; HEAD's `new Vector2(45) * GlobalScale` caret and account buttons).

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `LoginInputWidth` | `private const int` (line 27) | 268 = 300 − 32, the login column inner width |
| `ExtraButtonSize` | `private const int` (line 30) | 32, square caret/account buttons |
| `mainPage` | `private readonly MainPage` (line 32) | used to reach `AccountSwitcher` |
| `loginInput`, `passwordInput` | `private readonly Common.Input` (lines 34–35) | `Components/Common/Input.cs.md` |
| `oneTimePasswordCheckbox`, `useSteamServiceCheckbox`, `freeTrialCheckbox`, `autoLoginCheckbox` | `private readonly Common.Checkbox` (lines 36–39) | `Components/Common/Checkbox.cs.md` |
| `loginButton` | `private readonly Common.Button` (line 40) | |
| `secretsWarningLabel` | `private readonly Label` (line 41) | |
| `Username` | `public string` (lines 43–47) | forwards `loginInput.Value` |
| `Password` | `public string` (lines 49–53) | forwards `passwordInput.Value` |
| `IsOtp` | `public bool` (lines 55–59) | forwards `oneTimePasswordCheckbox.Value` |
| `IsSteam` | `public bool` (lines 61–65) | forwards `useSteamServiceCheckbox.Value` |
| `IsAutoLogin` | `public bool` (lines 67–71) | forwards `autoLoginCheckbox.Value` |
| `IsFreeTrial` | `public bool` (lines 73–77) | forwards `freeTrialCheckbox.Value` |
| `OnLogin` | `public event Action<LoginAction>?` (line 80) | the requested login action |
| `LoginFrame(MainPage mainPage)` | ctor (lines 82–168) | builds all controls + layout |
| `MenuItem(string text, Action clicked)` | `private static ButtonMenuItem` (lines 170–175) | menu-item factory |

All six state properties forward directly to widgets, so every read and write is a GTK UI-thread operation.

## 3. Controls

| Control | Construction | Configuration |
| --- | --- | --- |
| `loginInput` | `LoginFrame.cs:88` | label `Strings.UsernameInput`, hint `Strings.UsernameInputHint`, spacing `(12,0)`, `MaxLength = 128`, no flags; `InputWidth = LoginInputWidth` (line 89); `Enter += TriggerLogin` (line 90) |
| `passwordInput` | `LoginFrame.cs:92` | `Strings.PasswordInput`, `Strings.PasswordInputHint`, spacing `(12,0)`, `MaxLength = 128`, `Password \| EnterReturnsTrue`; `InputWidth = LoginInputWidth` (line 93); `Enter += TriggerLogin` (line 94) |
| `oneTimePasswordCheckbox` | `LoginFrame.cs:96` | `Strings.UseOneTimePasswordCheckbox` |
| `useSteamServiceCheckbox` | `LoginFrame.cs:97` | `Strings.UseSteamServiceAccount` |
| `freeTrialCheckbox` | `LoginFrame.cs:98` | `Strings.FreeTrialAccountCheckbox` |
| `autoLoginCheckbox` | `LoginFrame.cs:99` | `Strings.LogInAutomaticCheckbox` |
| `loginButton` | `LoginFrame.cs:119` | `Strings.LoginButton`; `Click += TriggerLogin` (line 120); `ContextMenu = loginActionMenu` (line 121) |
| `caretButton` | `LoginFrame.cs:124-126` | empty label, `Size = new Size(ExtraButtonSize, ExtraButtonSize)` (32x32), `ShowIcon(FontAwesomeIcon.CaretDown, 10f)`, `Click += () => loginActionMenu.Show(this.loginButton)` |
| `friendsButton` | `LoginFrame.cs:128-130` | empty label, `Size = new Size(ExtraButtonSize, ExtraButtonSize)` (32x32), `ShowIcon(FontAwesomeIcon.UserFriends, 10f)`, `Click += () => this.mainPage.AccountSwitcher.Open(this.loginButton)` |
| `secretsWarningLabel` | `LoginFrame.cs:133-141` | `Strings.NoSecretsProviderWarning`, `TextColor = EtoColors.Red`, `Wrap = WrapMode.Word`, `Width = 300`, `Visible = Program.Secrets is DummySecretProvider` |

None of the four checkboxes has an `OnChange` handler: the values are read by `MainPage` when the login action fires (`MainPage.cs:189`, `MainPage.cs:126-133`).

`secretsWarningLabel.Width = 300` is deliberate: without an explicit width, GTK sizes the wrapped label to the message's natural width, and that natural width becomes the login column's width, pushing the news column off-screen (`EtoColors.cs.md`, `Components/LoadingPage/NewsFrame.cs.md` §4 on the same effect). The explicit 300 matches `loginFrame.Width` (`MainPage.cs:92`), and `MinimumSize (200, 0)` (`MainPage.cs:91`) keeps the column from shrinking below 200 px.

## 4. Login-action menu

`loginActionMenu` is built once in the constructor (line 101) with `SeparatorMenuItem` between entries (lines 103–111):

| Item (lines) | Action raised |
| --- | --- |
| `Strings.LaunchWithoutDalamudButton` (line 103) | `LoginAction.GameNoDalamud` |
| `Strings.LaunchWithoutPluginsButton` (line 105) | `LoginAction.GameNoPlugins` |
| `Strings.LaunchWithNoCustomPluginsButton` (line 107) | `LoginAction.GameNoThirdparty` |
| `Strings.PatchWithoutLaunchingButton` (line 109) | `LoginAction.GameNoLaunch` |
| `Strings.RepairGameFilesButton` (line 111) | `LoginAction.Repair` |
| `"Fake Login"` (lines 115–116, only when `LauncherApp.IsDebug`) | `LoginAction.Fake` |

The menu is used in three ways: as `loginButton.ContextMenu` (right-click, line 121), shown programmatically by `caretButton` (`loginActionMenu.Show(this.loginButton)`, line 126), and not at all by the friends button. `TriggerLogin` (line 86) raises `OnLogin(LoginAction.Game)` — the plain login path used by the login button and by Enter in either input.

`MenuItem` (lines None–174) builds `ButtonMenuItem` and wires `item.Click += (_, _) => clicked()`; Eto menu items use `EventHandler<EventArgs>`, so the wrapper adapter converts them to the launcher's `Action` style. `MainMenu.cs.md` uses `ButtonMenuItem` directly.

## 5. Layout

`this.Content = new StackLayout { Spacing = 10, Items = { ... } }` (lines 143–168), in order:

1. `loginInput`
2. `passwordInput`
3. `oneTimePasswordCheckbox`
4. `useSteamServiceCheckbox`
5. `freeTrialCheckbox`
6. `autoLoginCheckbox`
7. `loginButton`
8. nested horizontal `StackLayout` with `caretButton`, `friendsButton` (lines 155–161)
9. `secretsWarningLabel` (line 161)
10. `new StackLayoutItem(new Panel(), VerticalAlignment.Stretch)` (line 165) — an empty expander that takes the leftover column height

The frame's width is set by `MainPage`: `this.loginFrame.Width = 300` with `MinimumSize = new Size(200, 0)` (`MainPage.cs:91-92`), and the page layout adds it as the right column, fixed width (`xscale: false`) stretched vertically (`yscale: true`), pinned to the window's right edge (`MainPage.cs:110`, `MainPage.cs.md` §4). Eto.Gtk's `StackLayout` packs items at the top with no expand flag, so items top-pack; the trailing stretch item absorbs the leftover column height (the news column has the same trailing expander, `Components/MainPage/NewsFrame.cs.md` §5).

## 6. Consumers

`MainPage` is the only consumer (`MainPage.cs.md`):

| Site | Use |
| --- | --- |
| `MainPage.cs:50` | `new LoginFrame(this)` |
| `MainPage.cs:58` | `this.loginFrame.OnLogin += this.ProcessLogin` |
| `MainPage.cs:91-92` | `loginFrame.MinimumSize = new Size(200, 0)` + `loginFrame.Width = 300` |
| `MainPage.cs:118` | auto-login gate: `(App.Settings.IsAutologin ?? false) && !IsNullOrEmpty(loginFrame.Username) && !IsNullOrEmpty(loginFrame.Password)` |
| `MainPage.cs:126-133` | `SwitchAccount` writes `Username`, `IsOtp`, `IsFreeTrial`, `IsSteam`, `IsAutoLogin`, `Password` (password only when `account.SavePassword`) |
| `MainPage.cs:189` | `App.Settings.IsAutologin = isAutoLogin` (value from the login snapshot, `MainPage.cs:161`) |
| `MainPage.cs:191` | `Login(username, password, isOtp, isSteam, isFreeTrial, false, action)` — snapshot values captured on the UI thread at `MainPage.cs:156-161`, consumed inside `Task.Run` |

`ProcessLogin` is guarded by `MainPage.IsLoggingIn` (`MainPage.cs:146-148`), so repeated Enter presses in the inputs cannot start two login tasks.

## 7. Thread / UI rules

Construction, property reads/writes, `ShowIcon`, `ContextMenu.Show`, and click handlers are UI-thread operations on GTK widgets. `OnLogin` fires on the UI thread (button click, Enter key, menu click), and `MainPage.ProcessLogin` calls `App.StartLoading` on the UI thread, then runs the login task in the background (`MainPage.cs.md` §5; `LauncherApp.cs.md` §7 notes that `StartLoading` marshals only `State`, so later `StartLoading` calls from the login task write loading-page widgets off the UI thread). The `secretsWarningLabel` `Visible` flag is computed once at construction from `Program.Secrets` (`Program.cs.md`), so a later secrets-provider change is not reflected.

## 8. Quirks

- Checkbox state is **not** persisted by the frame: only `IsAutoLogin` is written back to `App.Settings`, at `MainPage.cs:189`, inside the login path. `IsOtp`/`IsSteam`/`IsFreeTrial` reach `App.Settings` only through the persisted account (`MainPage.PersistAccount`, `MainPage.cs.md`).
- `Password` is read on the UI thread and then captured by the background login task's string argument, so the password string is copied at call time (no live binding to the widget).
- `ShowIcon` replaces the button label, so `caretButton`/`friendsButton` have no text — accessibility sees only the glyph (`Components/Common/Button.cs.md` §3).
- `launcherActionMenu` is attached to `loginButton`, so a right-click on the login button shows the menu, and GTK shows it at the pointer; `caretButton` shows it anchored to the login button instead.
- The "Fake Login" item is present only when `LauncherApp.IsDebug` is true at construction, i.e. the menu is built at startup and does not react to a later `IsDebug` change.
- The secrets warning is `Visible`-gated at construction, not re-evaluated in `OnShow()` — the frame has no `OnShow()` override.
- No keyboard traversal special-casing: HEAD tracked `focusedButton` + Enter for the login button; here Enter in an input calls `TriggerLogin` via `InputFlags.EnterReturnsTrue` and Enter on a focused button is GTK's own default-button behaviour.

## 9. Referenced / referenced-by

Uses: `Component` (`Component.cs.md`), `Common.Input` (`Components/Common/Input.cs.md`), `Common.Checkbox` (`Components/Common/Checkbox.cs.md`), `Common.Button` (`Components/Common/Button.cs.md`), `EtoColors.Red` (`EtoColors.cs.md`), `Program.Secrets`/`Program.Steam` (`Program.cs.md`), `Strings`, `LoginAction` (`MainPage.cs.md`), `FontAwesomeIcon`, `MainMenu`/`ButtonMenuItem`.
Referenced by: `MainPage.cs` (lines 50, 58, 88–89, 107, 118, 126–133, 189, 191).
