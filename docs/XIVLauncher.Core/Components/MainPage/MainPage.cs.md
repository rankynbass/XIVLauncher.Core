# MainPage.cs — Specification

**File:** `src/XIVLauncher.Core/Components/MainPage/MainPage.cs` · 1,171 lines · 48,332 bytes
**Namespace:** `XIVLauncher.Core.Components.MainPage` · **Declaration:** `public class MainPage : Page`
**Role:** The launcher's login → patch → launch → wait-for-game machine plus the main page UI composition

---

## 1. Purpose and the seam

`MainPage` is the concrete `Page` (`Components/Page.cs.md`) that `LauncherApp` hosts for `LauncherState.Main` (`LauncherApp.cs.md`): constructed at `LauncherApp.cs:146`, exposed as `CurrentPage` for `LauncherState.Main` (`LauncherApp.cs:112`), entered via `this.mainPage.OnShow()` (`LauncherApp.cs:80`), refreshed with `this.mainPage.ReloadNews()` when leaving settings (`LauncherApp.cs:69`), and asked to auto-login by `RunStartupTasks` (`LauncherApp.cs:282`).

Two halves in one file:

- **UI half:** the `DynamicLayout` composition built in the constructor (`MainPage.cs:87-111`) of `LoginFrame` (`LoginFrame.cs.md`), `NewsFrame` (`NewsFrame.cs.md`), `ActionButtons` (`ActionButtons.cs.md`), and the public `AccountSwitcher` (`AccountSwitcher.cs.md`).
- **Pipeline half:** `ProcessLogin` → `Login` → `TryLoginToGame` → `TryProcessLoginResult` → `StartGameAndAddon`, plus patching (`HandleBootCheck`, `InstallGamePatch`, `TryHandlePatchAsync`) and repair (`RepairGame`).

In HEAD the pipeline lived inside the immediate-mode draw loop with SDL quit events; in the Eto path the UI half became Eto controls and the quit push became `Ui.Post(() => Application.Instance.Quit())` (`MainPage.cs:196`, `Ui.cs.md`). `MainPage` has **no** `OnShow()` override, so `LauncherApp.cs:80` calls `Page.OnShow()`'s empty base (`Components/Page.cs.md` §3).

Usings: `System.Diagnostics` (`Debug.Assert`, `Process`), `System.Threading.Tasks`, `Eto.Drawing` (`Size`), `Eto.Forms` (`Application`), `Serilog`, the `XIVLauncher.Common` game/patch/compatibility namespaces (submodule `lib/FFXIVQuickLauncher`, out of docs scope), `XIVLauncher.Core.Accounts`, localization, `XIVLauncher.Core.Support`.

## 2. Surface

| Member | Kind | Line | Notes |
| --- | --- | --- | --- |
| `loginFrame`, `newsFrame`, `actionButtons` | `private readonly` | 39–41 | child components |
| `IsLoggingIn` | `public bool { get; private set; }` | 43 | pipeline gate |
| `AccountSwitcher` | `public AccountSwitcher { get; private set; }` | 45 | exposed so `LoginFrame` can open it (`LoginFrame.cs:118`) |
| `MainPage(LauncherApp app)` | ctor | 50–112 | |
| `DoAutoLoginIfApplicable()` | `public` | 114–120 | |
| `ReloadNews()` | `public` | 121 | forwards to `NewsFrame` |
| `SwitchAccount(XivAccount, bool)` | `private` | 124–139 | |
| `AccountSwitcherOnAccountChanged` | `private` | 141–144 | |
| `ProcessLogin(LoginAction)` | `private` | 146–209 | |
| `Login(username, password, isOtp, isSteam, isFreeTrial, doingAutoLogin, action)` | `public async Task<bool>` | 210–253 | |
| `TryLoginToGame(...)` | `private async Task<Launcher.LoginResult>` | 255–293 | |
| `TryProcessLoginResult(loginResult, isSteam, action)` | `private async Task<bool>` | 295–568 | |
| `StartGameAndAddon(loginResult, isSteam, forceNoDalamud, noPlugins, noThird)` | `public async Task<Process>` | 570–852 | |
| `PersistAccount(...)` | `private` | 854–880 | |
| `HandleBootCheck()` | `private async Task<bool>` | 882–919 | |
| `InstallGamePatch(loginResult)` | `private Task<bool>` | 921–929 | |
| `TryHandlePatchAsync(repository, pendingPatches, sid)` | `private async Task<bool>` | 931–1036 | |
| `PatcherOnFail(patch, context)` | `private` | 1038–1042 | |
| `InstallerOnFail()` | `private` | 1044–1048 | |
| `RepairGame(loginResult)` | `private async Task<bool>` | 1050–1157 | |
| `Hide()` | `private` | 1159–1162 | `Program.HideWindow()` |
| `Reactivate()` | `private` | 1164–1169 | `IsLoggingIn = false`, `State = Main`, `Program.ShowWindow()` |

`LoginAction` (7 members) lives in `Components/MainPage/LoginAction.cs.md`.

## 3. Constructor

Order matters, because some steps touch the UI thread during app startup (`Program.cs.md` §4 explains the GTK synchronization-context hazard):

| Step | Line(s) | Behaviour |
| --- | --- | --- |
| children | 50–53 | `new LoginFrame(this)`, `new NewsFrame(app)` (starts the news fetch + banner timer), `new ActionButtons()` |
| account switcher | 55–56 | `new AccountSwitcher(app.Accounts)`, `AccountChanged += AccountSwitcherOnAccountChanged` |
| login wiring | 58 | `loginFrame.OnLogin += ProcessLogin` |
| action buttons | 59–61 | settings → `App.State = Settings`; status → `AppUtil.OpenBrowser("https://is.xivup.com/")`; account → `AppUtil.OpenBrowser("https://sqex.to/Msp")` (`AppUtil.cs.md`) |
| padding | 65 | `new Padding(10, 32, 0, 10)` — zero right padding pins the login column to the window right edge |
| saved account | 67–69 | `App.Accounts.CurrentAccount` → `SwitchAccount(savedAccount, false)`, so the login frame is prefilled without touching `CurrentAccount` |
| elevation warning | 71–72 | `PlatformHelpers.IsElevated()` → `App.ShowMessage(Strings.XLElevatedWarning, "XIVLauncher")` |
| troubleshooting | 79 | `Task.Run(Troubleshooting.LogTroubleshooting)` — deliberately off the UI thread; HEAD generated the payload synchronously inside the constructor, and `IntegrityCheck.CompareIntegrityAsync().Result` blocks the GTK main loop when run there (`Support/Troubleshooting.cs.md`) |
| layout | 87–111 | MinimumSize floors for both frames (87–88, comment 86), `DynamicLayout` build 91–111, see §4 |

`Padding`, `Width`, `Content` are GTK widget writes ⇒ the ctor runs on the UI thread from `LauncherApp`'s construction inside `application.Initialized` (`Program.cs.md`).

## 4. Layout

```
this.Content = DynamicLayout(Spacing 16x16, line 91)
  BeginHorizontal()                                        (93)
    BeginVertical(xscale: true, yscale: true)              (98)   <- news column section
      BeginScrollable(BorderType.None, Padding 10, xscale/yscale true) (99)
        newsFrame (xscale: true, yscale: true)             (100)
      EndScrollable                                        (101)
      Add(actionButtons, xscale: false, yscale: false)     (102)   <- buttons at column bottom
    EndVertical                                            (103)
    Add(loginFrame, xscale: false, yscale: true)           (107)   <- login column, Width=240 pinned, flush right
  EndHorizontal                                            (109)
  this.Content = layout                                    (111)
```

Eto `DynamicLayout` scales with boolean flags (`LauncherForm.cs.md` on the resizable window, `MinimumSize = new Size(640, 480)`), so the split is expressed as: the news column section is scaled (`BeginVertical(xscale: true, yscale: true)`, line 98) and takes the remaining width and height, the login column is added `xscale: false` (line 107) with the login frame width-pinned `Width = 240` (line 89), zero right padding (line 65), and MinimumSize floors on both frames (lines 87–88, 200 px each) so the columns cannot overlap at the window minimum.

The action buttons are added inside the news column's `BeginVertical` below the scrollable news frame with `xscale: false, yscale: false` (line 102), so they keep natural size at the column bottom, 10 px above the window bottom edge. `newsFrame` is wrapped in `BeginScrollable` (lines 99–101) so it can shrink independently; the login column has no scrollable wrapper because DynamicScrollable cells expand to the row size, and a scrollable login frame would cover the news column and the bottom buttons (comment at lines 105–106).

> **Line-number note:** all line references in this document are HEAD-verified (1,171 lines). The ctor layout rewrite (84–107 → 91–111), the login snapshot (142–205 → 146–209), and the runner/compat block growth shift everything below: Login 210, TryLoginToGame 255, TryProcessLoginResult 295, StartGameAndAddon 570, patching 882+, repair 1050+, Hide/Reactivate 1159/1164.

## 5. `ProcessLogin(LoginAction action)`

Lines 146–209 — the entry point from `LoginFrame` (`LoginFrame.cs.md` §6, `LoginFrame.cs:59`):

1. Gate: `if (this.IsLoggingIn) return;` (lines 148–149)
2. `App.StartLoading(Strings.LoggingIn, canDisableAutoLogin: true)` (line 151) — switches to `LoadingPage` (`Components/LoadingPage/LoadingPage.cs.md`); `canCancel` keeps the default `false`, so no cancel button appears.
3. **Login widget snapshot** (lines 156–161): `username`, `password`, `isOtp`, `isSteam`, `isFreeTrial`, `isAutoLogin` are read from `loginFrame` on the UI thread, *before* `Task.Run`. Eto reads are UI-object accesses, not just writes: reading `Checkbox.Value`/`Input.Value` calls `Control.Handler` → `Application.EnsureUIThread`, and the pre-fix code did it inside `Task.Run` on a `.NET TP Worker` thread, so GTK read label/checkbox handlers off-thread (`Ui.cs.md` §2 documents the stack Eto reports). The snapshot hands plain values to the background task, so nothing in the task touches a widget.
4. `Task.Run(async () => ...)` (line 166):
   - `GameHelpers.CheckIsGameOpen() && action == Repair` → `App.ShowMessageBlocking(Strings.RepairOfficialLauncherOpenError, Strings.XIVLauncherError)` then `this.Reactivate()` and return (lines 168–174)
   - `Repository.Ffxiv.GetVer(GamePath) == Constants.BASE_GAME_VERSION && IsUidCacheEnabled == true` → `App.ShowMessageBlocking(Strings.ReinstallUIDCacheError, ...)` + `Reactivate()` + return (lines 176–185)
   - `this.IsLoggingIn = true` (line 191)
   - `App.Settings.IsAutologin = isAutoLogin` (line 189) — the snapshot, not the checkbox
   - `Login(username, password, isOtp, isSteam, isFreeTrial, false, action)` (line 191)
   - `result == true` → `Ui.Post(() => Application.Instance.Quit())` (lines 193–198): HEAD pushed an SDL quit event, ending the render loop and exiting the app.
   - `result == false` → `Log.Verbose("Reactivated after Login() != true")` + `Reactivate()` (lines 200–204)
5. `.ContinueWith(t => { if (!App.HandleContinuationBlocking(t)) this.Reactivate(); })` (lines 205–208) — `LauncherApp.HandleContinuationBlocking` handles faulted/cancelled continuations, showing modals (`LauncherApp.cs.md` §6).

`ProcessLogin` is private; `RunLoginTest()` wrappers used during smoke-testing have been removed (`LauncherApp.cs.md`).

## 6. `Login(...)` (public)

Lines 210–253:

| Step | Line(s) |
| --- | --- |
| `LoginAction.Fake` | 212–224 — builds `WindowsGameRunner(null, false)` (line 217) or `UnixGameRunner(Program.CompatibilityTools, null, false)` (line 219), calls `App.Launcher.LaunchGame(gameRunner, "0", 1, 2, false, "", GamePath!, ClientLanguage.Japanese, true, DpiAwareness.Unaware)` (line 221), returns `false` (line 223) |
| `HandleBootCheck()` | 226, gate 228–229 |
| UID cache reset | 233–234 — `isOtp && HasValidCache(username) && IsUidCacheEnabled == false` → `Program.ResetUIDCache()` |
| OTP request | 236–243 — `isOtp && !HasValidCache` → `App.AskForOtp()` (line 238), `otp = await App.WaitForOtpAsync()` (line 239), then `App.State = LauncherState.Loading` (line 242) to restore the loading page |
| OTP cancel | 245–246 — `otp == null` → `false` (`OtpEntryPage.cs.md` §5: `GetResultAsync()` yields `null` on cancel) |
| `PersistAccount` | 248 |
| `TryLoginToGame` | 250 |
| `TryProcessLoginResult` | 252 |

`doingAutoLogin` is accepted but **never read** in this method (parameter line 210; grep finds no use). Auto-login is triggered from `DoAutoLoginIfApplicable` (lines 114–120) with `Debug.Assert(App.State == LauncherApp.LauncherState.Main)` (line 116), gate on `App.Settings.IsAutologin ?? false` plus non-empty username/password (line 118), then `ProcessLogin(LoginAction.Game)` (line 119).

`Login` is public because `LauncherApp`'s smoke/continuation logic uses it; `ProcessLogin` stays private, so the login UI path is `LoginFrame.OnLogin` → `ProcessLogin`.

## 7. `TryLoginToGame(...)`

Lines 255–293. In non-`DEBUG` builds it first queries the login gate (`#if !DEBUG` block, lines 257–275): `App.Launcher.GetGateStatus(ClientLanguage)` (line 262), with `Log.Error(ex, "Could not obtain gate status")` on failure (lines 265–268); `gateStatus == null` → `App.ShowMessageBlocking("Login servers could not be reached or maintenance is in progress. ...")` and `return null!` (lines 270–274).

Then (lines 277–286): `enableUidCache = IsUidCacheEnabled ?? false`, `gamePath = GamePath!`, `language = ClientLanguage ?? English`; `action == Repair` → `App.Launcher.Login(username, password, otp, isSteam, false, gamePath, true, isFreeTrial, language)` (line 284) — the uid-cache flag is forced `false` and `repair` flag `true` for repair; otherwise `App.Launcher.Login(..., enableUidCache, gamePath, false, ...)` (line 286). `catch` logs `"Could not login to game"` and rethrows (lines 288–292).

`return null!` at line 273 is consumed by `TryProcessLoginResult`, which dereferences `loginResult.State` (`MainPage.cs:301`) — a `NullReferenceException` in the login gate failure path, surfaced through `ProcessLogin`'s `ContinueWith` → `App.HandleContinuationBlocking` (`LauncherApp.cs.md` §6).

## 8. `TryProcessLoginResult(...)`

Lines 295–568. Sequence:

| Branch | Line(s) |
| --- | --- |
| `LoginState.NoService` | 301–304 → `throw new OauthLoginException(preErrorMsg + Strings.LoginNoServiceAccountError + postErrorMsg)` |
| `LoginState.NoTerms` | 306–309 → `throw` with `Strings.LoginTermsNotAcceptedError` |
| `LoginState.NeedsPatchBoot` | 323–326 → `throw` with `"Boot conflict, need reinstall"` (the comment at lines 312–322 explains boot binaries changed outside the launcher) |
| `LoginAction.Repair` | 328–354 — `NeedsPatchGame` → `RepairGame(loginResult)` (334), `false` → `return false` (336), else `State = Ok`; non-`NeedsPatchGame` state → `throw` with `"Repair login state not NeedsPatchGame"` (line 341); the `catch` rethrows (lines 346–354, comment notes it should never be reached) |
| `LoginState.NeedsPatchGame` | 356–365 — `InstallGamePatch` false → `Log.Error("patchSuccess != true")`, `return false`; success → `State = Ok` |
| `LoginAction.GameNoLaunch` | 367–372 → `App.ShowMessageBlocking(Strings.UpdateCheckFinished, "XIVLauncher")`, `return false` |
| Gate check (`#if !DEBUG`) | 374–395 — `null` → `"Login servers could not be reached..."` + `false` (lines 390–392); `false` → `"Maintenance is in progress."` + `false` (lines 393–394) |
| `Debug.Assert(State == Ok)` | 398 |
| launch loop | 400–421 — `using var process = await StartGameAndAddon(loginResult, isSteam, action == GameNoDalamud, action == GameNoPlugins, action == GameNoThirdparty)` (line 404); `process is null` → `throw new InvalidOperationException("Could not obtain Process Handle")` (lines 406–407); `ExitCode != 0 && TreatNonZeroExitCodeAsFailure` → `throw new InvalidOperationException("Game exited with non-zero exit code")` (lines 410–413); success → `return true` (line 414); `catch` logs `"StartGameAndError resulted in an exception."` and rethrows (lines 416–420) |

The `while (true)` loop body has `continue` only in HEAD's commented-out exception-dialog block (lines 423–566), so in the current code every iteration either returns `true` (line 414) or throws. `preErrorMsg`/`postErrorMsg` (lines 298–299) wrap messages in the string format `OauthLoginException` expects (`window.external.user("login=auth,ng,err,...")`).

`MessageBoxResult` handling, `Process.GetProcessesByName("ffxiv_dx11"/"ffxiv")` multibox detection, and the process-kill loop are all commented out (lines 423–566) — HEAD's interactive error dialog is absent, so any `StartGameAndAddon` failure becomes a thrown exception that reaches `ProcessLogin`'s continuation handler.

## 9. `StartGameAndAddon(...)` (public)

Lines 570–852. Highlights:

| Step | Line(s) |
| --- | --- |
| Dalamud runner/compat check selection | 576–589 — `Win32NT` → `WindowsDalamudRunner(Program.DalamudUpdater.Runtime)` + `WindowsDalamudCompatibilityCheck`; `Unix` → `UnixDalamudRunner(Program.CompatibilityTools, Program.DotnetRuntime)` + `UnixDalamudCompatibilityCheck`; other platform → `throw new NotImplementedException()` (line 588) |
| troubleshooting payload | 598 — `Task.Run(Troubleshooting.LogTroubleshooting)` (same UI-thread hazard comment as the ctor, lines 594–597) |
| `DalamudLauncher` | 600–603 — `DalamudLoadMethod.GetValueOrDefault(DllInject)`, `Storage.Root`, `Storage.GetFolder("logs")`, `ClientLanguage`, `DalamudLoadDelay`, `noPlugins`, `noThird`, `Troubleshooting.GetTroubleshootingJson()` |
| compatibility check | 606–618 — `EnsureCompatibility()`, rethrow on `NoRedistsException` (line 612) / `ArchitectureNotSupportedException` (line 617) |
| Dalamud wait | 620–633 — `DalamudEnabled.GetValueOrDefault(true) && !forceNoDalamud` → `App.StartLoading(Strings.WaitingForDalamud, Strings.PleaseBePatient)` (line 624), `dalamudOk = dalamudLauncher.HoldForUpdate(GamePath) == DalamudInstallState.Ok` (line 625); `DalamudRunnerException` → log + rethrow (lines 627–632) |
| env hacks | 636–679 — `CoreEnvironmentSettings.IsSteamCompatTool` → merge `XL_PRELOAD` into `LD_PRELOAD` (638–644); `FixLocale` → `LC_ALL`/`LC_CTYPE` = `Program.CType` (650–651); `FixLDP` → strip `gameoverlayrenderer.so` (655–658); `FixIM` → `XMODIFIERS=@im=null` (662–664); `FixError127` → `DOTNET_SYSTEM_GLOBALIZATION_USENLS=true` (668–670); Unix + `DontUseSystemTz` → `TZ=""` (676–678) |
| `AdditionalArgs` parsing | 681–713 — split on `%command%` into `launchEnv`/`gameArgs` (681–697); single token starting with `-` is args, else env (686–692); env pairs skip entries without `=` (704), and `key+` means append to the existing variable (706–710), each pair applied with `Environment.SetEnvironmentVariable` (711) |
| runner selection | 715–780 — `Win32NT` → `WindowsGameRunner(dalamudLauncher, dalamudOk)` (line 717); `Unix` → custom-wine validation (`WineStartupType == Custom`: `WineBinaryPath` null / missing directory / no `wine64` → `InvalidOperationException`, lines 721–730; custom/managed/Dxvk version logging 732–738) + `UnixGameRunner(Program.CompatibilityTools, dalamudLauncher, dalamudOk)` (line 766); other platform → `throw new NotImplementedException()` (line 780) |
| compatibility tool ensure | 740–764 — `ManualResetEvent` (740) + `isFailed` flag (741) + `Task.Run(... Program.CompatibilityTools.EnsureTool(Program.HttpClient, tempPath) ...).ContinueWith(... signal.Set())` (743–755), `App.StartLoading(Strings.PreparingForCompatTool, Strings.PleaseBePatient)` (line 757), `signal.WaitOne()` (758), `signal.Dispose()` (759), `isFailed` → `return null!` (762), then `App.StartLoading(Strings.StartingGame, Strings.HaveFun)` (line 764) |
| `UserPath` | 770–775 — `Program.CompatibilityTools.UnixToWinePath(App.Settings.GameConfigPath!.FullName)` (770), appended as ` UserPath=...` unquoted when `IsEncryptArgs` is true (771–772) and quoted when false (773–774), then `gameArgs.Trim()` (776) |
| launch | 784–793 — `App.Launcher.LaunchGame(runner, loginResult.UniqueId, loginResult.OauthLogin.Region, loginResult.OauthLogin.MaxExpansion, isSteam, gameArgs, App.Settings.GamePath, App.Settings.ClientLanguage.GetValueOrDefault(English), IsEncryptArgs.GetValueOrDefault(true), DpiAwareness.GetValueOrDefault(Unaware))` |
| window state | 797–803 — `!Program.IsSteamDeckHardware \|\| CoreEnvironmentSettings.IsSteamCompatTool` → `this.Hide()` (line 799); else `App.State = LauncherApp.LauncherState.SteamDeckPrompt` (line 803, `SteamDeckPromptPage.cs.md`) |
| null process | 806–811 — `Log.Information("GameProcess was null...")` (808), `IsLoggingIn = false` (809), `return null!` (810) |
| addons | 813–827 — `AddonManager` (813), `App.Settings.Addons ??= new List<AddonEntry>()` (817), enabled entries cast to `IAddon` (819), `addonMgr.RunAddons(launchedProcess.Id, addons)` (821); on exception `IsLoggingIn = false` (825), `StopAddons()` (826), `throw` (827) |
| game wait | 830–837 — `Log.Debug("Waiting for game to exit")` (830), `await Task.Run(() => launchedProcess!.WaitForExit()).ConfigureAwait(false)` (832), `Log.Verbose("Game has exited")` (834), `StopAddons()` when `addonMgr.IsRunning` (836–837) |
| Steam shutdown | 841–848 — `App.Steam?.IsValid == true` (841) → `App.Steam.Shutdown()` (843), `Log.Error(ex, "Could not shut down Steam")` on failure (847) |
| return | 851 — `return launchedProcess!` |

`Steam app id` behaviour is unchanged in HEAD semantics: `Program.UsesFallbackSteamAppId` and `EngcribeSteamAppId` live in `Program.cs.md`, and `ProcessLogin` retains HEAD's commented-out Steam app-id check (lines 162–164).

### Account persistence

`PersistAccount(username, password, isOtp, isSteam, isFreeTrial)` (854–880) is called from `Login` (line 248) before the login attempt:

| Step | Line(s) |
| --- | --- |
| password refresh | 857–860 — current account matches `username` (`StringComparison.Ordinal`), stored password differs, `SavePassword` true → `App.Accounts.UpdatePassword(CurrentAccount, password)` (860) |
| free-trial refresh | 862–865 — current account matches `username` (`OrdinalIgnoreCase`), `IsFreeTrial` differs → `App.Accounts.UpdateFreeTrial(CurrentAccount, isFreeTrial)` (865) |
| new account | 867–879 — no current account, or `CurrentAccount.Id != $"{username}-{isOtp}-{isSteam}"` → `new XivAccount(username) { Password, SavePassword = true, UseOtp, IsFreeTrial, UseSteamServiceAccount }` (869–876), `App.Accounts.AddAccount` (877), `App.Accounts.CurrentAccount = accountToSave` (878) |

`SwitchAccount(XivAccount account, bool saveAsCurrent)` (124–139) is called from `AccountSwitcherOnAccountChanged` (line 141, `saveAsCurrent: true`, `AccountSwitcher.cs.md` §4) and from the ctor with `saveAsCurrent: false` (line 69). It writes `loginFrame.Username` (126), `IsOtp` (127), `IsFreeTrial` (128), `IsSteam` (129), `IsAutoLogin = App.Settings.IsAutologin ?? false` (130), `Password` when `account.SavePassword` (131–132), and `App.Accounts.CurrentAccount = account` when `saveAsCurrent` (137). The account id format `"{username}-{isOtp}-{isSteam}"` (867) is the identity key: switching OTP/Steam flags on the same username creates a new saved account.

## 10. Patching

`HandleBootCheck` (882–920): `App.Settings.PatchPath is { Exists: false }` → `PatchPath = null` (886–889), `PatchPath ??= new DirectoryInfo(Path.Combine(Paths.RoamingPath, "patches"))` (891), `App.Launcher.CheckBootVersion(App.Settings.GamePath!)` (897), inner `catch` → `Log.Error(ex, "Unable to check boot version")` + `App.ShowMessage(Strings.CannotGetBootVerError, "XIVLauncher")` + `false` (899–904); `bootPatches.Length == 0` → `true` (907–908); patches → `TryHandlePatchAsync(Repository.Boot, bootPatches, "")` (910). Outer `catch` → `App.ShowExceptionBlocking(ex, "PatchBoot")` (914) + `Environment.Exit(0)` (915) + `false` (917).

`InstallGamePatch` (921–930): `Debug.Assert` on `NeedsPatchGame` (923–924) and `PendingPatches != null` (926), then `TryHandlePatchAsync(Repository.Ffxiv, loginResult.PendingPatches, loginResult.UniqueId)` (928).

`TryHandlePatchAsync` (931–1037):

| Step | Line(s) |
| --- | --- |
| `#if WIN32` mutex `"XivLauncherIsPatching"` | 933–944 — busy-wait `WaitOne(0, false)` false → `App.ShowMessageBlocking("XIVLauncher is already patching your game in another instance...", "XIVLauncher")` + `Environment.Exit(0)` (939–940); the `return false` at line 941 is unreachable by comment; HEAD's note at line 933 says the mutex does not disappear on Linux |
| game open check | 945–950 — `GameHelpers.CheckIsGameOpen()` → `App.ShowMessageBlocking(Strings.CannotPatchGameOpenError, "XIVLauncher")`, `false` |
| patcher setup | 952–957 — `PatchInstaller(GamePath, KeepPatches ?? false)`, `AriaPatchAcquisition(logs/aria2.log)`, `Program.Patcher = new PatchManager(...)`, `Program.Patcher.OnFail += PatcherOnFail`, `installer.OnFail += InstallerOnFail` |
| loading page | 959 — `App.StartLoading(string.Format(Strings.NowPatching, repository.ToString().ToLowerInvariant()), canCancel: false, isIndeterminate: false)` |
| status thread | 961–978 — `CancellationTokenSource`, `new Thread(UpdatePatchStatus)`, `statusThread.Start()`; local function (968–979) sleeps 30 ms (972) and writes `App.LoadingPage.Line2` (974), `Line3` (975–976), `Progress = CurrentInstallIndex / (float)Downloads.Count` (978) |
| patch run | 982–991 — `await Program.Patcher.PatchAsync(false)` (984), `finally { token.Cancel(); statusThread.Join(1002 ms); }` (986–989); success → `true` (991) |
| failures | 993–1029 — `PatchInstallerException` → `App.ShowMessageBlocking(string.Format(Strings.PatchInstallerStartFailError, ex.Message), Strings.XIVLauncherError)` (994); `NotEnoughSpaceException` per `SpaceKind` (`Patches` 1002–1006, `AllPatches` 1008–1012, `Game` 1014–1018; `Debug.Assert(false, "HandlePatchAsync:Invalid NotEnoughSpaceException.SpaceKind value.")` on default 1021); generic `Exception` → `Log.Error(ex, "Error during patching")` + `App.ShowExceptionBlocking(ex, "HandlePatchAsync")` (1027–1028) |
| finally | 1030–1033 — `App.State = LauncherApp.LauncherState.Main` (1032) |
| return | 1035 — `false` for every failure path |

`Program.Patcher` is a static field on `Program` (`Program.cs.md`), so the patcher is process-global, not page-owned.

`PatcherOnFail` (1038–1043) and `InstallerOnFail` (1044–1049) both show a blocking modal then `Environment.Exit(0)` — the process terminates inside a modal handler, not through `Application.Quit`.

## 11. Repair

`RepairGame` (1050–1158): `#if WIN32` mutex block (comment 1052, mutex 1056, busy-wait `WaitOne(0, false)` 1058, `App.ShowMessageBlocking("XIVLauncher is already patching your game in another instance. Please check if XIVLauncher is still open.", "XIVLauncher")` 1060, `Environment.Exit(0)` 1061, unreachable `return false` 1062) as in patching; asserts `PendingPatches != null` (1066) and `Length != 0` (1067); `Log.Information("STARTING REPAIR")` (1069); `PatchVerifier(Program.HttpClient, Program.Config.GamePath!, Program.Config.PatchPath!, loginResult, 100 ms, MaxExpansion, false)` (1072).

`for (var doVerify = true; doVerify;)` (1074) wraps: `App.StartLoading(Strings.NowRepairingFiles, canCancel: false, isIndeterminate: false)` (1076), `verify.Start()` (1078), a `System.Threading.Timer` callback (1080–1104), `timer.Change(0, 252)` (1105), `await verify.WaitForCompletion()` (1107), `timer.Dispose()` (1108), `App.StopLoading()` (1109).

The timer callback writes, per `verify.State`:

- `DownloadMeta` (1085–1088): `Line2 = verify.CurrentFile` (1086), `Line3 = patchSet progress + bytes` (1087), `Progress = Progress/Total` (1088)
- `VerifyAndRepair` (1091–1094): `Line2 = CurrentFile` (1092), `Line3 = patch set + task + bytes` (1093), `Progress` (1094)
- default (1097–1100): `Line2 = ""` (1098), `Line3 = task count` (1099), `Progress = 1.0` when `Done` else `0` (1100)

`Components/LoadingPage/LoadingPage.cs.md` §3 is the relevant rule: every `LoadingPage` property setter stores the value and posts `ApplyState` through `Ui.Post`, so the timer callback runs the widget writes on the UI thread. The repair timer writes (`Line2`/`Line3`/`Progress`) are safe by construction; the pre-fix version marshalled only `Progress`, and `App.StartLoading(..., isIndeterminate: false)` at line 1076 drove `Reset() → Refresh() → Label.Text` on the timer thread.

Result handling (`switch (verify.State)`, 1111–1153): `Done` (1113) → message built from `NumBrokenFiles` (`Strings.RepairAllFilesValid` / `RepairFixedSingularFile` / `RepairFixedPluralFiles`, lines 1116–1120) plus moved-file text (`RepairSingleFileMoved`/`RepairPluralFilesMoved`, lines 1123–1127), `App.ShowMessageBlocking(mainText + additionalText)` (1130), stop looping (1132); `Error` (1135) → `doLogin = false`, `NoVersionReferenceException` → `Strings.RepairGameVerUnsupportedError` (1138–1140), else `verify.LastException + "\n\n" + Strings.RepairFailureError` (1144), stop looping (1147); `Cancelled` (1150) → `doLogin = doVerify = false` (1151). `return doLogin` (1156) — `RepairGame` returns `true` only when `doLogin` was never cleared, i.e. after a successful verify loop; `TryProcessLoginResult` then sets `State = Ok` and continues to the launch path (`MainPage.cs:341-343`).

## 12. `Hide()` / `Reactivate()`

- `Hide()` (1159–1162) → `Program.HideWindow()` (line 1161), which hides (unmaps) the launcher window while the game runs: the GTK `GApplication` is held across the hidden period so the loop stays alive (`Program.cs.md` §5; `LauncherForm.cs.md` §3).
- `Reactivate()` (1164–1170): `IsLoggingIn = false` (1166), `App.State = LauncherApp.LauncherState.Main` (1167, marshalled), `Program.ShowWindow()` (1169) — restores visibility (`Visible = true`, `WindowState.Normal`) and releases the `GApplication` hold. Called from `ProcessLogin`'s failure paths (lines 172, 183, 201, 206) and as the login-failure recovery signal observed in smoke tests (`Log.Verbose("Reactivated after Login() != true")`, line 200).

## 13. Consumers

`MainPage` is used only by `LauncherApp` (`LauncherApp.cs.md`):

| Site | Use |
| --- | --- |
| `LauncherApp.cs:131` | `private readonly MainPage mainPage` field |
| `LauncherApp.cs:148` | `new MainPage(this)` |
| `LauncherApp.cs:114` | `LauncherState.Main => this.mainPage` (page for `CurrentPage`) |
| `LauncherApp.cs:80` | `this.mainPage.OnShow()` on entering `Main` |
| `LauncherApp.cs:69` | `this.mainPage.ReloadNews()` when leaving `Settings` |
| `LauncherApp.cs:284` | `this.mainPage.DoAutoLoginIfApplicable()` in `RunStartupTasks` |

`MainPage` consumes: `LoginFrame` (lines 58, 88–89, 118, 126–133, 156–163), `NewsFrame` (59, 87, 122), `ActionButtons` (41, 59–61, 104), `AccountSwitcher` (45, 55, 56, 143–146), `App.*` (`StartLoading` 151/624/757/764/959/1076, `StopLoading` 1109, `ShowMessageBlocking`, `ShowMessage`, `HandleContinuationBlocking`, `AskForOtp` 238, `WaitForOtpAsync` 239, `Settings`, `Accounts`, `Launcher`, `UniqueIdCache`, `LoadingPage` 974–978/1086–1100, `Storage`, `Steam` 843–845), `Program.*` (`CompatibilityTools` 219/746, `ResetUIDCache` 234, `CType` 648, `HttpClient` 1072, `Patcher` 954–978, `SteamDeckHardware` 797, `HideWindow` 1161, `ShowWindow` 1169, `Config` 1072, `DalamudUpdater` 580/600, `DotnetRuntime` 585), `AppUtil.OpenBrowser` (60, 61), `Troubleshooting` (79, 598, 603), `Ui.Post` (196), `Strings`, `Support` exceptions.

## 14. Thread / UI rules

| Context | Rules |
| --- | --- |
| UI thread | ctor, layout, `SwitchAccount`, `DoAutoLoginIfApplicable`, `ProcessLogin` (up to `Task.Run`), `Ui.Post(() => Application.Instance.Quit())` |
| Background (login task) | `Login`, `TryLoginToGame`, `TryProcessLoginResult`, `StartGameAndAddon`, patching, repair, `PersistAccount`, `Reactivate`'s `State` write (marshalled via `Ui.Post` inside `LauncherApp.SetState`, `LauncherApp.cs.md` §4) |
| Blocking modals from background | `App.ShowMessageBlocking`, `App.ShowExceptionBlocking` pump a modal on the UI thread and release a `ManualResetEvent` (`LauncherApp.cs.md` §6), so background callers can block safely |
| Plain property writes from background | `App.LoadingPage.Line2/Line3/Progress` (974–978, 1086–1100): `Line*` are plain fields, `Progress` marshals `Refresh` |
| Direct widget writes from background | `App.StartLoading(...)` (151, 624, 757, 764, 959, 1076) writes `LoadingPage.Line1/Line2/Line3/IsIndeterminate/CanCancel/CanDisableAutoLogin` and calls `LoadingPage.Reset()`, whose `Refresh()` writes GTK labels/buttons/bar directly on the caller's thread (`LauncherApp.cs:261-272`) — see §15 |
| Environment mutation | `StartGameAndAddon` sets process-wide env vars (636–679, 681–713), which affects the launcher process itself, not only the child game |

## 15. Quirks

- **`StartLoading` writes widgets off the UI thread.** Every `StartLoading` call in this file happens inside the background login task (lines 151, 624, 757, 764, 959, 1076). `LauncherApp.StartLoading` (`LauncherApp.cs.md` §7) sets plain `LoadingPage` properties and then calls `Reset()` → `Refresh()`, writing GTK label/bar/button state on the caller's thread. `State`'s setter marshals, so page *switching* is safe; the label/bar writes are not. Eto's GTK handlers for `Label.Text`/`ProgressBar.Value` are not synchronized to `Gtk.Application`'s main thread.
- **`App.StopLoading` does not clear the loading page** (`LauncherApp.cs:274-275`): it only sets `State = Main`. `RepairGame` calls `App.StopLoading()` (line 1095) and patching's `finally` sets `State = Main` (line 1032), so a later `StartLoading` reuses stale `Progress`/`ProgressText` values until they are overwritten.
- **`RepairGame`'s `doLogin` is only true after a successful verify**, and `TryProcessLoginResult` treats `RepairGame == false` as a hard stop (`MainPage.cs:341-343`), so repair failure never reaches the launch path.
- `RepairGame` uses `Program.Config.GamePath!`/`Program.Config.PatchPath!` (line 1072), not `App.Settings.*`, so the patch verifier follows the startup config, not any settings-page change made after startup (`Program.cs.md` §6).
- `Environment.Exit(0)` inside modal handlers (915, 940, 1041, 1047) exits the process from inside a modal dialog handler, bypassing `Application.Quit` and any Eto cleanup.
- `Debug.Assert` calls (116, 398, 923–927, 1066–1067, 1021) compile away in release: `Debug.Assert(loginResult.State == Ok)` (398) is a no-op in release, so the launch path runs with whatever `State` the server returned.
- `StartGameAndAddon` returns `null!` in three paths (762, 810, and implicitly via `Process is null` throw path) while its signature is non-nullable `Task<Process>`, so `TryProcessLoginResult` must null-check (406–407).
- The commented-out multibox/kill loop (lines 423–566) means no interactive error dialog exists for launch failures; all failures become exceptions, and `ProcessLogin`'s `ContinueWith` shows a modal via `App.HandleContinuationBlocking` (`LauncherApp.cs.md` §6).
- `doingAutoLogin` is an unused parameter (line 210), kept for API shape.
- `AddonManager` failures reset `IsLoggingIn` (825) but do not reset the loading page, so the UI can remain on `LoadingPage` while `IsLoggingIn` is false, permitting a new login while the old task's exception is being handled.
- `App.Steam.Shutdown()` (843) is called after the game exits, in `StartGameAndAddon`; Steam state is process-global (`Program.cs.md`).
- `SwitchAccount` writes `Password` only when `account.SavePassword` (131–132), so switching to a saved account without a stored password leaves whatever is in the password box.
- No `OnShow()` override means page entry performs no refresh; `LauncherApp.cs:80`'s `OnShow()` is the empty base (`Components/Page.cs.md` §3), and news refresh comes only from `LauncherApp.cs:69` when leaving settings.

## 16. Referenced / referenced-by

Uses: `Page` (`Components/Page.cs.md`), `LauncherApp` (`LauncherApp.cs.md`), `LoginFrame` (`LoginFrame.cs.md`), `NewsFrame` (`NewsFrame.cs.md`), `ActionButtons` (`ActionButtons.cs.md`), `AccountSwitcher` (`AccountSwitcher.cs.md`), `OtpEntryPage` via `App.AskForOtp` (`Components/OtpEntryPage.cs.md`), `LoadingPage` (`Components/LoadingPage/LoadingPage.cs.md`), `SteamDeckPromptPage` (`Components/SteamDeckPromptPage.cs.md`), `Ui` (`Ui.cs.md`), `Program` (`Program.cs.md`), `AppUtil` (`AppUtil.cs.md`), `Troubleshooting` (`Support/Troubleshooting.cs.md`), `AccountManager`/`XivAccount` (`Accounts/AccountManager.cs.md`), `Strings` (`Resources/Localization/Strings.Designer.cs.md`), and the `XIVLauncher.Common` game/patch/Dalamud/compatibility types in `lib/FFXIVQuickLauncher` (submodule — out of docs scope).
Referenced by: `LauncherApp.cs` (lines 12, 69, 80, 114, 131, 148, 284).
