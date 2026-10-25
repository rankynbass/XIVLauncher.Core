# LoginFrame.cs — Specification

**File:** `src/XIVLauncher.Core/Components/MainPage/LoginFrame.cs` · 158 lines · 6,218 bytes
**Namespace:** `XIVLauncher.Core.Components.MainPage` · **Declaration:** `public class LoginFrame : Component`
**Role:** The login column: username, password, the four option checkboxes, the login button with its login-action menu, the caret and friends buttons, and the secrets-provider warning

---

## 1. Purpose

Replaces the immediate-mode login child window. The right-click login-action popup becomes an Eto `ContextMenu` attached to the login button, so the caret icon button shows the same menu, and the friends icon button opens `AccountSwitcher` (`AccountSwitcher.cs.md`). State is exposed as properties (`Username`, `Password`, `IsOtp`, `IsSteam`, `IsAutoLogin`, `IsFreeTrial`) that forward to the underlying Eto controls, and `MainPage` reads/persists them (`MainPage.cs.md`).

Usings: `System.Numerics` (`Vector2` input spacing), `Eto.Forms`, `XIVLauncher.Core.Accounts.Secrets.Providers` (`DummySecretProvider` test for the warning label), `XIVLauncher.Core.Resources.Localization`.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `mainPage` | `private readonly MainPage` (line 22) | used to reach `AccountSwitcher` |
| `loginInput`, `passwordInput` | `private readonly Common.Input` (lines 24–25) | `Components/Common/Input.cs.md` |
| `oneTimePasswordCheckbox`, `useSteamServiceCheckbox`, `freeTrialCheckbox`, `autoLoginCheckbox` | `private readonly Common.Checkbox` (lines 26–29) | `Components/Common/Checkbox.cs.md` |
| `loginButton` | `private readonly Common.Button` (line 30) | |
| `secretsWarningLabel` | `private readonly Label` (line 31) | |
| `Username` | `public string` (lines 33–37) | forwards `loginInput.Value` |
| `Password` | `public string` (lines 39–43) | forwards `passwordInput.Value` |
| `IsOtp` | `public bool` (lines 45–49) | forwards `oneTimePasswordCheckbox.Value` |
| `IsSteam` | `public bool` (lines 51–55) | forwards `useSteamServiceCheckbox.Value` |
| `IsAutoLogin` | `public bool` (lines 57–61) | forwards `autoLoginCheckbox.Value` |
| `IsFreeTrial` | `public bool` (lines 63–67) | forwards `freeTrialCheckbox.Value` |
| `OnLogin` | `public event Action<LoginAction>?` (line 70) | the requested login action |
| `LoginFrame(MainPage mainPage)` | ctor (lines 72–150) | builds all controls + layout |
| `MenuItem(string text, Action clicked)` | `private static ButtonMenuItem` (lines 152–157) | menu-item factory |

All six state properties forward directly to widgets, so every read and write is a GTK UI-thread operation.

## 3. Controls

| Control | Construction | Configuration |
| --- | --- | --- |
| `loginInput` | `LoginFrame.cs:78` | label `Strings.UsernameInput`, hint `Strings.UsernameInputHint`, spacing `(12,0)`, `MaxLength = 128`, no flags; `Enter += TriggerLogin` (line 79) |
| `passwordInput` | `LoginFrame.cs:81` | `Strings.PasswordInput`, `Strings.PasswordInputHint`, spacing `(12,0)`, `MaxLength = 128`, `Password \| EnterReturnsTrue`; `Enter += TriggerLogin` (line 82) |
| `oneTimePasswordCheckbox` | `LoginFrame.cs:84` | `Strings.UseOneTimePasswordCheckbox` |
| `useSteamServiceCheckbox` | `LoginFrame.cs:85` | `Strings.UseSteamServiceAccount` |
| `freeTrialCheckbox` | `LoginFrame.cs:86` | `Strings.FreeTrialAccountCheckbox` |
| `autoLoginCheckbox` | `LoginFrame.cs:87` | `Strings.LogInAutomaticCheckbox` |
| `loginButton` | `LoginFrame.cs:107` | `Strings.LoginButton`; `Click += TriggerLogin` (line 108); `ContextMenu = loginActionMenu` (line 109) |
| `caretButton` | `LoginFrame.cs:111-113` | empty label, `Width = 45`, `ShowIcon(FontAwesomeIcon.CaretDown)`, `Click += () => loginActionMenu.Show(this.loginButton)` |
| `friendsButton` | `LoginFrame.cs:115-117` | empty label, `Width = 45`, `ShowIcon(FontAwesomeIcon.UserFriends)`, `Click += () => this.mainPage.AccountSwitcher.Open(this.loginButton)` |
| `secretsWarningLabel` | `LoginFrame.cs:119-128` | `Strings.NoSecretsProviderWarning`, `TextColor = EtoColors.Red`, `Wrap = WrapMode.Word`, `Width = 400`, `Visible = Program.Secrets is DummySecretProvider` |

None of the four checkboxes has an `OnChange` handler: the values are read by `MainPage` when the login action fires (`MainPage.cs:175`, `MainPage.cs:120-124`).

`secretsWarningLabel.Width = 400` is deliberate: GTK reports the unwrapped text width as the label's natural width, which would push the login column wide enough to clip the news column (`EtoColors.cs.md`, `Components/LoadingPage/NewsFrame.cs.md` §4 on the same effect).

## 4. Login-action menu

`loginActionMenu` is built once in the constructor (line 89) with `SeparatorMenuItem` between entries (lines 91–100):

| Item (lines) | Action raised |
| --- | --- |
| `Strings.LaunchWithoutDalamudButton` (line 91) | `LoginAction.GameNoDalamud` |
| `Strings.LaunchWithoutPluginsButton` (line 93) | `LoginAction.GameNoPlugins` |
| `Strings.LaunchWithNoCustomPluginsButton` (line 95) | `LoginAction.GameNoThirdparty` |
| `Strings.PatchWithoutLaunchingButton` (line 97) | `LoginAction.GameNoLaunch` |
| `Strings.RepairGameFilesButton` (line 99) | `LoginAction.Repair` |
| `"Fake Login"` (lines 101–105, only when `LauncherApp.IsDebug`) | `LoginAction.Fake` |

The menu is used in three ways: as `loginButton.ContextMenu` (right-click, line 109), shown programmatically by `caretButton` (`loginActionMenu.Show(this.loginButton)`, line 113), and not at all by the friends button. `TriggerLogin` (line 76) raises `OnLogin(LoginAction.Game)` — the plain login path used by the login button and by Enter in either input.

`MenuItem` (lines 152–157) builds `ButtonMenuItem` and wires `item.Click += (_, _) => clicked()`; Eto menu items use `EventHandler<EventArgs>`, so the wrapper adapter converts them to the launcher's `Action` style. `MainMenu.cs.md` uses `ButtonMenuItem` directly.

## 5. Layout

`this.Content = new StackLayout { Spacing = 10, Items = { ... } }` (lines 130–150), in order:

1. `loginInput`
2. `passwordInput`
3. `oneTimePasswordCheckbox`
4. `useSteamServiceCheckbox`
5. `freeTrialCheckbox`
6. `autoLoginCheckbox`
7. `loginButton`
8. nested horizontal `StackLayout` with `caretButton`, `friendsButton` (lines 142-147)
9. `secretsWarningLabel`

The frame's width is set by `MainPage`: `this.loginFrame.Width = 400` (`MainPage.cs:94`), and the login/news row places it in a non-scaled cell (`MainPage.cs:91`, `MainPage.cs.md` §4).

## 6. Consumers

`MainPage` is the only consumer (`MainPage.cs.md`):

| Site | Use |
| --- | --- |
| `MainPage.cs:50` | `new LoginFrame(this)` |
| `MainPage.cs:58` | `this.loginFrame.OnLogin += this.ProcessLogin` |
| `MainPage.cs:94` | `this.loginFrame.Width = 400` |
| `MainPage.cs:112` | auto-login gate: `(App.Settings.IsAutologin ?? false) && !IsNullOrEmpty(loginFrame.Username) && !IsNullOrEmpty(loginFrame.Password)` |
| `MainPage.cs:120-127` | `SwitchAccount` writes `Username`, `IsOtp`, `IsFreeTrial`, `IsSteam`, `IsAutoLogin`, `Password` (password only when `account.SavePassword`) |
| `MainPage.cs:173` | `App.Settings.IsAutologin = this.loginFrame.IsAutoLogin` |
| `MainPage.cs:175` | `Login(this.loginFrame.Username, this.loginFrame.Password, this.loginFrame.IsOtp, this.loginFrame.IsSteam, this.loginFrame.IsFreeTrial, false, action)` |

`ProcessLogin` is guarded by `MainPage.IsLoggingIn` (`MainPage.cs:142-144`), so repeated Enter presses in the inputs cannot start two login tasks.

## 7. Thread / UI rules

Construction, property reads/writes, `ShowIcon`, `ContextMenu.Show`, and click handlers are UI-thread operations on GTK widgets. `OnLogin` fires on the UI thread (button click, Enter key, menu click), and `MainPage.ProcessLogin` calls `App.StartLoading` on the UI thread, then runs the login task in the background (`MainPage.cs.md` §5; `LauncherApp.cs.md` §7 notes that `StartLoading` marshals only `State`, so later `StartLoading` calls from the login task write loading-page widgets off the UI thread). The `secretsWarningLabel` `Visible` flag is computed once at construction from `Program.Secrets` (`Program.cs.md`), so a later secrets-provider change is not reflected.

## 8. Quirks

- Checkbox state is **not** persisted by the frame: only `IsAutoLogin` is written back to `App.Settings`, at `MainPage.cs:173`, inside the login path. `IsOtp`/`IsSteam`/`IsFreeTrial` reach `App.Settings` only through the persisted account (`MainPage.PersistAccount`, `MainPage.cs.md`).
- `Password` is read on the UI thread and then captured by the background login task's string argument, so the password string is copied at call time (no live binding to the widget).
- `ShowIcon` replaces the button label, so `caretButton`/`friendsButton` have no text — accessibility sees only the glyph (`Components/Common/Button.cs.md` §3).
- `launcherActionMenu` is attached to `loginButton`, so a right-click on the login button shows the menu, and GTK shows it at the pointer; `caretButton` shows it anchored to the login button instead.
- The "Fake Login" item is present only when `LauncherApp.IsDebug` is true at construction, i.e. the menu is built at startup and does not react to a later `IsDebug` change.
- The secrets warning is `Visible`-gated at construction, not re-evaluated in `OnShow()` — the frame has no `OnShow()` override.
- No keyboard traversal special-casing: HEAD tracked `focusedButton` + Enter for the login button; here Enter in an input calls `TriggerLogin` via `InputFlags.EnterReturnsTrue` and Enter on a focused button is GTK's own default-button behaviour.

## 9. Referenced / referenced-by

Uses: `Component` (`Component.cs.md`), `Common.Input` (`Components/Common/Input.cs.md`), `Common.Checkbox` (`Components/Common/Checkbox.cs.md`), `Common.Button` (`Components/Common/Button.cs.md`), `EtoColors.Red` (`EtoColors.cs.md`), `Program.Secrets`/`Program.Steam` (`Program.cs.md`), `Strings`, `LoginAction` (`MainPage.cs.md`), `FontAwesomeIcon`, `MainMenu`/`ButtonMenuItem`.
Referenced by: `MainPage.cs` (lines 50, 58, 94, 112, 120–127, 173, 175).
