# MainPage.cs — Specification

**File:** `src/XIVLauncher.Core/Components/MainPage/MainPage.cs` · 1,155 lines · 46,979 bytes
**Namespace:** `XIVLauncher.Core.Components.MainPage` · **Declaration:** `public class MainPage : Page`
**Role:** The launcher's login → patch → launch → wait-for-game machine plus the main page UI composition

---

## 1. Purpose and the seam

`MainPage` is the concrete `Page` (`Components/Page.cs.md`) that `LauncherApp` hosts for `LauncherState.Main` (`LauncherApp.cs.md`): constructed at `LauncherApp.cs:146`, exposed as `CurrentPage` for `LauncherState.Main` (`LauncherApp.cs:112`), entered via `this.mainPage.OnShow()` (`LauncherApp.cs:80`), refreshed with `this.mainPage.ReloadNews()` when leaving settings (`LauncherApp.cs:69`), and asked to auto-login by `RunStartupTasks` (`LauncherApp.cs:282`).

Two halves in one file:

- **UI half:** the `TableLayout` composition built in the constructor (`MainPage.cs:84-105`) of `LoginFrame` (`LoginFrame.cs.md`), `NewsFrame` (`NewsFrame.cs.md`), `ActionButtons` (`ActionButtons.cs.md`), and the public `AccountSwitcher` (`AccountSwitcher.cs.md`).
- **Pipeline half:** `ProcessLogin` → `Login` → `TryLoginToGame` → `TryProcessLoginResult` → `StartGameAndAddon`, plus patching (`HandleBootCheck`, `InstallGamePatch`, `TryHandlePatchAsync`) and repair (`RepairGame`).

In HEAD the pipeline lived inside the immediate-mode draw loop with SDL quit events; in the Eto path the UI half became Eto controls and the quit push became `Ui.Post(() => Application.Instance.Quit())` (`MainPage.cs:181`, `Ui.cs.md`). `MainPage` has **no** `OnShow()` override, so `LauncherApp.cs:80` calls `Page.OnShow()`'s empty base (`Components/Page.cs.md` §3).

Usings: `System.Diagnostics` (`Debug.Assert`, `Process`), `System.Threading.Tasks`, `Eto.Drawing` (`Size`), `Eto.Forms` (`Application`), `Serilog`, the `XIVLauncher.Common` game/patch/compatibility namespaces (submodule `lib/FFXIVQuickLauncher`, out of docs scope), `XIVLauncher.Core.Accounts`, localization, `XIVLauncher.Core.Support`.

## 2. Surface

| Member | Kind | Line | Notes |
| --- | --- | --- | --- |
| `loginFrame`, `newsFrame`, `actionButtons` | `private readonly` | 39–41 | child components |
| `IsLoggingIn` | `public bool { get; private set; }` | 43 | pipeline gate |
| `AccountSwitcher` | `public AccountSwitcher { get; private set; }` | 45 | exposed so `LoginFrame` can open it (`LoginFrame.cs:117`) |
| `MainPage(LauncherApp app)` | ctor | 47–105 | |
| `DoAutoLoginIfApplicable()` | `public` | 108–114 | |
| `ReloadNews()` | `public` | 116 | forwards to `NewsFrame` |
| `SwitchAccount(XivAccount, bool)` | `private` | 118–133 | |
| `AccountSwitcherOnAccountChanged` | `private` | 135–138 | |
| `ProcessLogin(LoginAction)` | `private` | 140–195 | |
| `Login(username, password, isOtp, isSteam, isFreeTrial, doingAutoLogin, action)` | `public async Task<bool>` | 194–238 | |
| `TryLoginToGame(...)` | `private async Task<Launcher.LoginResult>` | 239–271 | |
| `TryProcessLoginResult(loginResult, isSteam, action)` | `private async Task<bool>` | 279–553 | |
| `StartGameAndAddon(loginResult, isSteam, forceNoDalamud, noPlugins, noThird)` | `public async Task<Process>` | 554–837 | |
| `PersistAccount(...)` | `private` | 838–864 | |
| `HandleBootCheck()` | `private async Task<bool>` | 866–904 | |
| `InstallGamePatch(loginResult)` | `private Task<bool>` | 905–914 | |
| `TryHandlePatchAsync(repository, pendingPatches, sid)` | `private async Task<bool>` | 915–1021 | |
| `PatcherOnFail(patch, context)` | `private` | 1022–1027 | |
| `InstallerOnFail()` | `private` | 1028–1033 | |
| `RepairGame(loginResult)` | `private async Task<bool>` | 1034–1142 | |
| `Hide()` | `private` | 1143–1146 | `Program.HideWindow()` |
| `Reactivate()` | `private` | 1148–1153 | `IsLoggingIn = false`, `State = Main`, `Program.ShowWindow()` |

`LoginAction` (7 members) lives in `Components/MainPage/LoginAction.cs.md`.

## 3. Constructor

Order matters, because some steps touch the UI thread during app startup (`Program.cs.md` §4 explains the GTK synchronization-context hazard):

| Step | Line(s) | Behaviour |
| --- | --- | --- |
| children | 50–53 | `new LoginFrame(this)`, `new NewsFrame(app)` (starts the news fetch + banner timer), `new ActionButtons()` |
| account switcher | 55–56 | `new AccountSwitcher(app.Accounts)`, `AccountChanged += AccountSwitcherOnAccountChanged` |
| login wiring | 58 | `loginFrame.OnLogin += ProcessLogin` |
| action buttons | 59–61 | settings → `App.State = Settings`; status → `AppUtil.OpenBrowser("https://is.xivup.com/")`; account → `AppUtil.OpenBrowser("https://sqex.to/Msp")` (`AppUtil.cs.md`) |
| padding | 63 | `new Padding(32, 32)` |
| saved account | 65–67 | `App.Accounts.CurrentAccount` → `SwitchAccount(savedAccount, false)`, so the login frame is prefilled without touching `CurrentAccount` |
| elevation warning | 69–70 | `PlatformHelpers.IsElevated()` → `App.ShowMessage(Strings.XLElevatedWarning, "XIVLauncher")` |
| troubleshooting | 77 | `Task.Run(Troubleshooting.LogTroubleshooting)` — deliberately off the UI thread; HEAD generated the payload synchronously inside the constructor, and `IntegrityCheck.CompareIntegrityAsync().Result` blocks the GTK main loop when run there (`Support/Troubleshooting.cs.md`) |
| layout | 84–105 | see §4 |

`Padding`, `Width`, `Content` are GTK widget writes ⇒ the ctor runs on the UI thread from `LauncherApp`'s construction inside `application.Initialized` (`Program.cs.md`).

## 4. Layout

```
this.Content = TableLayout(1, 2)            // 1 column, 2 rows, Spacing (0, 16)
  row 0: topRow (TableLayout 2,1, Spacing (16,0))   xscale: true, yscale: false
         column 0: loginFrame  (xscale false, Width = 400 at line 94)
         column 1: newsFrame    (xscale: true)
  row 1: actionButtons (xscale: false, yscale: true)   Rows[1].ScaleHeight = true (line 101)
```

Eto 2.13 scales columns/rows with boolean flags rather than proportions (`LauncherForm.cs.md` on the fixed 1280×800 window), so the split is expressed as: login frame with an explicit `Width = 400` in an unscaled column, news column scaled to take the remaining width (`NewsFrame.cs.md` §3 adds explicit child widths for the same reason).

`MainPage.cs:101` sets `Rows[1].ScaleHeight = true`, so the button row grows vertically; the login/news row keeps its natural height.

## 5. `ProcessLogin(LoginAction action)`

Lines 140–195 — the entry point from `LoginFrame` (`LoginFrame.cs.md` §6, `LoginFrame.cs:58`):

1. Gate: `if (this.IsLoggingIn) return;` (lines 142–144)
2. `App.StartLoading(Strings.LoggingIn, canDisableAutoLogin: true)` (line 145) — switches to `LoadingPage` (`Components/LoadingPage/LoadingPage.cs.md`); `canCancel` keeps the default `false`, so no cancel button appears.
3. `Task.Run(async () => ...)` (line 152):
   - `GameHelpers.CheckIsGameOpen() && action == Repair` → `App.ShowMessageBlocking(Strings.RepairOfficialLauncherOpenError, Strings.XIVLauncherError)` then `this.Reactivate()` and return (lines 154–160)
   - `Repository.Ffxiv.GetVer(GamePath) == Constants.BASE_GAME_VERSION && IsUidCacheEnabled == true` → `App.ShowMessageBlocking(Strings.ReinstallUIDCacheError, ...)` + `Reactivate()` + return (lines 162–171)
   - `this.IsLoggingIn = true` (line 172)
   - `App.Settings.IsAutologin = this.loginFrame.IsAutoLogin` (line 174)
   - `Login(loginFrame.Username, loginFrame.Password, loginFrame.IsOtp, loginFrame.IsSteam, loginFrame.IsFreeTrial, false, action)` (line 176)
   - `result == true` → `Ui.Post(() => Application.Instance.Quit())` (lines 178–183): HEAD pushed an SDL quit event, ending the render loop and exiting the app.
   - `result == false` → `Log.Verbose("Reactivated after Login() != true")` + `Reactivate()` (lines 184–188)
4. `.ContinueWith(t => { if (!App.HandleContinuationBlocking(t)) this.Reactivate(); })` (lines 191–194) — `LauncherApp.HandleContinuationBlocking` handles faulted/cancelled continuations, showing modals (`LauncherApp.cs.md` §6).

`ProcessLogin` is private; `RunLoginTest()` wrappers used during smoke-testing have been removed (`LauncherApp.cs.md`).

## 6. `Login(...)` (public)

Lines 194–238:

| Step | Line(s) |
| --- | --- |
| `LoginAction.Fake` | 196–208 — builds `WindowsGameRunner(null, false)` (line 201) or `UnixGameRunner(Program.CompatibilityTools, null, false)` (line 203), calls `App.Launcher.LaunchGame(gameRunner, "0", 1, 2, false, "", GamePath!, ClientLanguage.Japanese, true, DpiAwareness.Unaware)` (line 205), returns `false` (line 207) |
| `HandleBootCheck()` | 210, gate 212–213 |
| UID cache reset | 217–218 — `isOtp && HasValidCache(username) && IsUidCacheEnabled == false` → `Program.ResetUIDCache()` |
| OTP request | 220–227 — `isOtp && !HasValidCache` → `App.AskForOtp()` (line 222), `otp = await App.WaitForOtpAsync()` (line 223), then `App.State = LauncherState.Loading` (line 226) to restore the loading page |
| OTP cancel | 229–230 — `otp == null` → `false` (`OtpEntryPage.cs.md` §5: `GetResultAsync()` yields `null` on cancel) |
| `PersistAccount` | 232 |
| `TryLoginToGame` | 234 |
| `TryProcessLoginResult` | 236 |

`doingAutoLogin` is accepted but **never read** in this method (parameter line 194; grep finds no use). Auto-login is triggered from `DoAutoLoginIfApplicable` (lines 108–114) with `Debug.Assert(App.State == LauncherState.Main)` (line 110), gate on `App.Settings.IsAutologin ?? false` plus non-empty username/password (line 112), then `ProcessLogin(LoginAction.Game)` (line 113).

`Login` is public because `LauncherApp`'s smoke/continuation logic uses it; `ProcessLogin` stays private, so the login UI path is `LoginFrame.OnLogin` → `ProcessLogin`.

## 7. `TryLoginToGame(...)`

Lines 239–271. In non-`DEBUG` builds it first queries the login gate (`#if !DEBUG` block, lines 241–259): `App.Launcher.GetGateStatus(ClientLanguage)` (line 246), with `Log.Error(ex, "Could not obtain gate status")` on failure (lines 249–252); `gateStatus == null` → `App.ShowMessageBlocking("Login servers could not be reached or maintenance is in progress. ...")` and `return null!` (lines 254–258).

Then (lines 261–270): `enableUidCache = IsUidCacheEnabled ?? false`, `gamePath = GamePath!`, `language = ClientLanguage ?? English`; `action == Repair` → `App.Launcher.Login(username, password, otp, isSteam, false, gamePath, true, isFreeTrial, language)` (line 268) — the uid-cache flag is forced `false` and `repair` flag `true` for repair; otherwise `App.Launcher.Login(..., enableUidCache, gamePath, false, ...)` (line 270). `catch` logs `"Could not login to game"` and rethrows (lines 272–276).

`return null!` at line 256 is consumed by `TryProcessLoginResult`, which dereferences `loginResult.State` (`MainPage.cs:285`) — a `NullReferenceException` in the login gate failure path, surfaced through `ProcessLogin`'s `ContinueWith` → `App.HandleContinuationBlocking` (`LauncherApp.cs.md` §6).

## 8. `TryProcessLoginResult(...)`

Lines 279–553. Sequence:

| Branch | Line(s) |
| --- | --- |
| `LoginState.NoService` | 285–288 → `throw new OauthLoginException(preErrorMsg + Strings.LoginNoServiceAccountError + postErrorMsg)` |
| `LoginState.NoTerms` | 290–293 → `throw` with `Strings.LoginTermsNotAcceptedError` |
| `LoginState.NeedsPatchBoot` | 307–310 → `throw` with `"Boot conflict, need reinstall"` (the comment at lines 296–306 explains boot binaries changed outside the launcher) |
| `LoginAction.Repair` | 312–338 — `NeedsPatchGame` → `RepairGame(loginResult)` (318), `false` → `return false` (320), else `State = Ok`; non-`NeedsPatchGame` state → `throw` with `"Repair login state not NeedsPatchGame"` (line 325); the `catch` rethrows (lines 330–338, comment notes it should never be reached) |
| `LoginState.NeedsPatchGame` | 340–349 — `InstallGamePatch` false → `Log.Error("patchSuccess != true")`, `return false`; success → `State = Ok` |
| `LoginAction.GameNoLaunch` | 351–356 → `App.ShowMessageBlocking(Strings.UpdateCheckFinished, "XIVLauncher")`, `return false` |
| Gate check (`#if !DEBUG`) | 358–379 — `null` → `"Login servers could not be reached..."` + `false` (lines 374–376); `false` → `"Maintenance is in progress."` + `false` (lines 377–378) |
| `Debug.Assert(State == Ok)` | 382 |
| launch loop | 384–405 — `using var process = await StartGameAndAddon(loginResult, isSteam, action == GameNoDalamud, action == GameNoPlugins, action == GameNoThirdparty)` (line 388); `process is null` → `throw new InvalidOperationException("Could not obtain Process Handle")` (lines 390–391); `ExitCode != 0 && TreatNonZeroExitCodeAsFailure` → `throw new InvalidOperationException("Game exited with non-zero exit code")` (lines 393–396); success → `return true` (line 398); `catch` logs `"StartGameAndError resulted in an exception."` and rethrows (lines 400–404) |

The `while (true)` loop body has `continue` only in HEAD's commented-out exception-dialog block (lines 410–549), so in the current code every iteration either returns `true` (line 398) or throws. `preErrorMsg`/`postErrorMsg` (lines 282–283) wrap messages in the string format `OauthLoginException` expects (`window.external.user("login=auth,ng,err,...")`).

`MessageBoxResult` handling, `Process.GetProcessesByName("ffxiv_dx11"/"ffxiv")` multibox detection, and the process-kill loop are all commented out (lines 410–549) — HEAD's interactive error dialog is absent, so any `StartGameAndAddon` failure becomes a thrown exception that reaches `ProcessLogin`'s continuation handler.

## 9. `StartGameAndAddon(...)` (public)

Lines 554–837. Highlights:

| Step | Line(s) |
| --- | --- |
| Dalamud runner/compat check selection | 561–575 — `Win32NT` → `WindowsDalamudRunner(Program.DalamudUpdater.Runtime)` + `WindowsDalamudCompatibilityCheck`; `Unix` → `UnixDalamudRunner(Program.CompatibilityTools, Program.DotnetRuntime)` + `UnixDalamudCompatibilityCheck`; other platform → `throw new NotImplementedException()` (line 574) |
| troubleshooting payload | 582 — `Task.Run(Troubleshooting.LogTroubleshooting)` (same UI-thread hazard comment as the ctor, lines 578–581) |
| `DalamudLauncher` | 584–587 — `DalamudLoadMethod.GetValueOrDefault(DllInject)`, `Storage.Root`, `Storage.GetFolder("logs")`, `ClientLanguage`, `DalamudLoadDelay`, `noPlugins`, `noThird`, `Troubleshooting.GetTroubleshootingJson()` |
| compatibility check | 590–602 — `EnsureCompatibility()`, rethrow on `NoRedistsException` (line 594) / `ArchitectureNotSupportedException` (line 599) |
| Dalamud wait | 604–617 — `DalamudEnabled.GetValueOrDefault(true) && !forceNoDalamud` → `App.StartLoading(Strings.WaitingForDalamud, Strings.PleaseBePatient)` (line 608), `dalamudOk = dalamudLauncher.HoldForUpdate(GamePath) == DalamudInstallState.Ok` (line 609); `DalamudRunnerException` → log + rethrow (lines 611–616) |
| env hacks | 622–664 — `CoreEnvironmentSettings.IsSteamCompatTool` → merge `XL_PRELOAD` into `LD_PRELOAD` (622–628); `FixLocale` → `LC_ALL`/`LC_CTYPE` = `Program.CType` (632–637); `FixLDP` → strip `gameoverlayrenderer.so` (639–643); `FixIM` → `XMODIFIERS=@im=null` (646–649); `FixError127` → `DOTNET_SYSTEM_GLOBALIZATION_USENLS=true` (652–655); Unix + `DontUseSystemTz` → `TZ=""` (660–664) |
| `AdditionalArgs` parsing | 666–697 — split on `%command%` into `launchEnv`/`gameArgs` (666–682); single token starting with `-` is args, else env (671–677); env pairs skip entries without `=` (688), and `key+` means append to the existing variable (690–694), each pair applied with `Environment.SetEnvironmentVariable` (696) |
| runner selection | 699–766 — `Win32NT` → `WindowsGameRunner(dalamudLauncher, dalamudOk)` (line 701); `Unix` → custom-wine validation (`WineStartupType == Custom`: `WineBinaryPath` null / missing directory / no `wine64` → `InvalidOperationException`, lines 707–714; custom/managed/Dxvk version logging 716–724) + `UnixGameRunner(Program.CompatibilityTools, dalamudLauncher, dalamudOk)` (line 750); other platform → `throw new NotImplementedException()` (line 766) |
| compatibility tool ensure | 726–748 — `ManualResetEvent` (724) + `isFailed` flag (727) + `Task.Run(... Program.CompatibilityTools.EnsureTool(Program.HttpClient, tempPath) ...).ContinueWith(... signal.Set())` (729–740), `App.StartLoading(Strings.PreparingForCompatTool, Strings.PleaseBePatient)` (line 741), `signal.WaitOne()` (742), `signal.Dispose()` (743), `isFailed` → `return null!` (745–746), then `App.StartLoading(Strings.StartingGame, Strings.HaveFun)` (line 748) |
| `UserPath` | 756–761 — `Program.CompatibilityTools.UnixToWinePath(App.Settings.GameConfigPath!.FullName)` (756), appended as ` UserPath=...` unquoted when `IsEncryptArgs` is true (757–758) and quoted when false (759–760), then `gameArgs.Trim()` (761) |
| launch | 768–777 — `App.Launcher.LaunchGame(runner, loginResult.UniqueId, loginResult.OauthLogin.Region, loginResult.OauthLogin.MaxExpansion, isSteam, gameArgs, App.Settings.GamePath, App.Settings.ClientLanguage.GetValueOrDefault(English), IsEncryptArgs.GetValueOrDefault(true), DpiAwareness.GetValueOrDefault(Unaware))` |
| window state | 781–788 — `!Program.IsSteamDeckHardware \|\| CoreEnvironmentSettings.IsSteamCompatTool` → `this.Hide()` (line 783); else `App.State = LauncherState.SteamDeckPrompt` (line 787, `SteamDeckPromptPage.cs.md`) |
| null process | 791–796 — `Log.Information("GameProcess was null...")` (792), `IsLoggingIn = false` (794), `return null!` (795) |
| addons | 798–812 — `AddonManager` (798), `App.Settings.Addons ??= new List<AddonEntry>()` (802), enabled entries cast to `IAddon` (804), `addonMgr.RunAddons(launchedProcess.Id, addons)` (806); on exception `IsLoggingIn = false` (809), `StopAddons()` (810), rethrow (811) |
| game wait | 814–821 — `Log.Debug("Waiting for game to exit")` (814), `await Task.Run(() => launchedProcess!.WaitForExit())` (816), `Log.Verbose("Game has exited")` (818), `StopAddons()` when `addonMgr.IsRunning` (820–821) |
| Steam shutdown | 824–832 — `App.Steam?.IsValid == true` (825) → `App.Steam.Shutdown()` (827), `Log.Error(ex, "Could not shut down Steam")` on failure (831) |
| return | 835 — `return launchedProcess!` |

`Steam app id` behaviour is unchanged in HEAD semantics: `Program.UsesFallbackSteamAppId` and `EngcribeSteamAppId` live in `Program.cs.md`, and `ProcessLogin` retains HEAD's commented-out Steam app-id check (lines 148–150).

### Account persistence

`PersistAccount(username, password, isOtp, isSteam, isFreeTrial)` (838–864) is called from `Login` (line 232) before the login attempt:

| Step | Line(s) |
| --- | --- |
| password refresh | 841–844 — current account matches `username` (`StringComparison.Ordinal`), stored password differs, `SavePassword` true → `App.Accounts.UpdatePassword(CurrentAccount, password)` (844) |
| free-trial refresh | 846–849 — current account matches `username` (`OrdinalIgnoreCase`), `IsFreeTrial` differs → `App.Accounts.UpdateFreeTrial(CurrentAccount, isFreeTrial)` (849) |
| new account | 851–863 — no current account, or `CurrentAccount.Id != $"{username}-{isOtp}-{isSteam}"` → `new XivAccount(username) { Password, SavePassword = true, UseOtp, IsFreeTrial, UseSteamServiceAccount }` (854–861), `App.Accounts.AddAccount` (862), `App.Accounts.CurrentAccount = accountToSave` (863) |

`SwitchAccount(XivAccount account, bool saveAsCurrent)` (118–133) is called from `AccountSwitcherOnAccountChanged` (line 137, `saveAsCurrent: true`, `AccountSwitcher.cs.md` §4) and from the ctor with `saveAsCurrent: false` (line 67). It writes `loginFrame.Username` (120), `IsOtp` (121), `IsFreeTrial` (122), `IsSteam` (123), `IsAutoLogin = App.Settings.IsAutologin ?? false` (124), `Password` when `account.SavePassword` (126–127), and `App.Accounts.CurrentAccount = account` when `saveAsCurrent` (131). The account id format `"{username}-{isOtp}-{isSteam}"` (852) is the identity key: switching OTP/Steam flags on the same username creates a new saved account.

## 10. Patching

`HandleBootCheck` (866–904): `App.Settings.PatchPath is { Exists: false }` → `PatchPath = null` (870–873), `PatchPath ??= new DirectoryInfo(Path.Combine(Paths.RoamingPath, "patches"))` (875), `App.Launcher.CheckBootVersion(App.Settings.GamePath!)` (881), inner `catch` → `Log.Error(ex, "Unable to check boot version")` + `App.ShowMessage(Strings.CannotGetBootVerError, "XIVLauncher")` + `false` (883–888); `bootPatches.Length == 0` → `true` (891–892); patches → `TryHandlePatchAsync(Repository.Boot, bootPatches, "")` (894). Outer `catch` → `App.ShowExceptionBlocking(ex, "PatchBoot")` (898) + `Environment.Exit(0)` (899) + `false` (901).

`InstallGamePatch` (905–914): `Debug.Assert` on `NeedsPatchGame` (907–908) and `PendingPatches != null` (911), then `TryHandlePatchAsync(Repository.Ffxiv, loginResult.PendingPatches, loginResult.UniqueId)` (913).

`TryHandlePatchAsync` (915–1021):

| Step | Line(s) |
| --- | --- |
| `#if WIN32` mutex `"XivLauncherIsPatching"` | 917–928 — busy-wait `WaitOne(0, false)` false → `App.ShowMessageBlocking("XIVLauncher is already patching your game in another instance...", "XIVLauncher")` + `Environment.Exit(0)` (923–924); the `return false` at line 925 is unreachable by comment; HEAD's note at line 917 says the mutex does not disappear on Linux |
| game open check | 929–934 — `GameHelpers.CheckIsGameOpen()` → `App.ShowMessageBlocking(Strings.CannotPatchGameOpenError, "XIVLauncher")`, `false` |
| patcher setup | 936–941 — `PatchInstaller(GamePath, KeepPatches ?? false)`, `AriaPatchAcquisition(logs/aria2.log)`, `Program.Patcher = new PatchManager(...)`, `Program.Patcher.OnFail += PatcherOnFail`, `installer.OnFail += InstallerOnFail` |
| loading page | 943 — `App.StartLoading(string.Format(Strings.NowPatching, repository.ToString().ToLowerInvariant()), canCancel: false, isIndeterminate: false)` |
| status thread | 945–962 — `CancellationTokenSource`, `new Thread(UpdatePatchStatus)`, `statusThread.Start()`; local function (952–963) sleeps 30 ms (956) and writes `App.LoadingPage.Line2` (958), `Line3` (959–960), `Progress = CurrentInstallIndex / (float)Downloads.Count` (962) |
| patch run | 966–975 — `await Program.Patcher.PatchAsync(false)` (967), `finally { token.Cancel(); statusThread.Join(1000 ms); }` (970–973); success → `true` (976) |
| failures | 977–1013 — `PatchInstallerException` → `App.ShowMessageBlocking(string.Format(Strings.PatchInstallerStartFailError, ex.Message), Strings.XIVLauncherError)` (978); `NotEnoughSpaceException` per `SpaceKind` (`Patches` 986–990, `AllPatches` 992–996, `Game` 998–1002; `Debug.Assert(false, "HandlePatchAsync:Invalid NotEnoughSpaceException.SpaceKind value.")` on default 1005); generic `Exception` → `Log.Error(ex, "Error during patching")` + `App.ShowExceptionBlocking(ex, "HandlePatchAsync")` (1011–1012) |
| finally | 1014–1017 — `App.State = LauncherApp.LauncherState.Main` (1016) |
| return | 1019 — `false` for every failure path |

`Program.Patcher` is a static field on `Program` (`Program.cs.md`), so the patcher is process-global, not page-owned.

`PatcherOnFail` (1022–1027) and `InstallerOnFail` (1028–1033) both show a blocking modal then `Environment.Exit(0)` — the process terminates inside a modal handler, not through `Application.Quit`.

## 11. Repair

`RepairGame` (1034–1142): `#if WIN32` mutex block (comment 1038, mutex 1040, busy-wait `WaitOne(0, false)` 1042, `App.ShowMessageBlocking("XIVLauncher is already patching your game in another instance. Please check if XIVLauncher is still open.", "XIVLauncher")` 1044, `Environment.Exit(0)` 1045, unreachable `return false` 1046) as in patching; asserts `PendingPatches != null` (1050) and `Length != 0` (1051); `Log.Information("STARTING REPAIR")` (1053); `PatchVerifier(Program.HttpClient, Program.Config.GamePath!, Program.Config.PatchPath!, loginResult, 100 ms, MaxExpansion, false)` (1056).

`for (var doVerify = true; doVerify;)` (1058) wraps: `App.StartLoading(Strings.NowRepairingFiles, canCancel: false, isIndeterminate: false)` (1060), `verify.Start()` (1062), a `System.Threading.Timer` callback (1064–1088), `timer.Change(0, 250)` (1089), `await verify.WaitForCompletion()` (1091), `timer.Dispose()` (1092), `App.StopLoading()` (1093).

The timer callback writes, per `verify.State`:

- `DownloadMeta` (1069–1072): `Line2 = verify.CurrentFile` (1070), `Line3 = patchSet progress + bytes` (1071), `Progress = Progress/Total` (1072)
- `VerifyAndRepair` (1075–1078): `Line2 = CurrentFile` (1076), `Line3 = patch set + task + bytes` (1077), `Progress` (1078)
- default (1081–1084): `Line2 = ""` (1082), `Line3 = task count` (1083), `Progress = 1.0` when `Done` else `0` (1084)

`Components/LoadingPage/LoadingPage.cs.md` §4 is the relevant rule: `Line2`/`Line3` are plain auto-properties (no widget writes), while `Progress`'s setter posts `Refresh` through `Ui.Post` — so the timer callback is safe with respect to GTK: only `Progress` touches widgets, and it marshals.

Result handling (`switch (verify.State)`, 1095–1136): `Done` (1097) → message built from `NumBrokenFiles` (`Strings.RepairAllFilesValid` / `RepairFixedSingularFile` / `RepairFixedPluralFiles`, lines 1100–1104) plus moved-file text (`RepairSingleFileMoved`/`RepairPluralFilesMoved`, lines 1107–1111), `App.ShowMessageBlocking(mainText + additionalText)` (1114), stop looping (1116); `Error` (1119) → `doLogin = false`, `NoVersionReferenceException` → `Strings.RepairGameVerUnsupportedError` (1122–1124), else `verify.LastException + "\n\n" + Strings.RepairFailureError` (1128), stop looping (1131); `Cancelled` (1134) → `doLogin = doVerify = false` (1135). `return doLogin` (1140) — `RepairGame` returns `true` only when `doLogin` was never cleared, i.e. after a successful verify loop; `TryProcessLoginResult` then sets `State = Ok` and continues to the launch path (`MainPage.cs:320-322`).

## 12. `Hide()` / `Reactivate()`

- `Hide()` (1143–1146) → `Program.HideWindow()`, which minimizes the launcher window so the GTK application stays alive (`Program.cs.md` §5; the GTK `GApplication` exits when the last visible window closes).
- `Reactivate()` (1148–1153) → `IsLoggingIn = false`, `App.State = LauncherState.Main` (marshalled), `Program.ShowWindow()` (restores `WindowState.Normal`). Called from `ProcessLogin`'s failure paths (lines 157, 168, 186, 194) and as the login-failure recovery signal observed in smoke tests (`Log.Verbose("Reactivated after Login() != true")`, line 185).

## 13. Consumers

`MainPage` is used only by `LauncherApp` (`LauncherApp.cs.md`):

| Site | Use |
| --- | --- |
| `LauncherApp.cs:129` | `private readonly MainPage mainPage` field |
| `LauncherApp.cs:146` | `new MainPage(this)` |
| `LauncherApp.cs:112` | `LauncherState.Main => this.mainPage` (page for `CurrentPage`) |
| `LauncherApp.cs:80` | `this.mainPage.OnShow()` on entering `Main` |
| `LauncherApp.cs:69` | `this.mainPage.ReloadNews()` when leaving `Settings` |
| `LauncherApp.cs:282` | `this.mainPage.DoAutoLoginIfApplicable()` in `RunStartupTasks` |

`MainPage` consumes: `LoginFrame` (lines 50, 58, 94, 112, 120–127, 173, 175), `NewsFrame` (51, 92, 116), `ActionButtons` (41, 53, 59–61, 103), `AccountSwitcher` (45, 55, 56, 135–138), `App.*` (`StartLoading` 145/608/741/748/943/1060, `StopLoading` 1093, `ShowMessageBlocking`, `ShowMessage`, `HandleContinuationBlocking`, `AskForOtp` 222, `WaitForOtpAsync` 223, `Settings`, `Accounts`, `Launcher`, `UniqueIdCache`, `LoadingPage` 958–962/1070–1084, `Storage`, `Steam` 825–827), `Program.*` (`CompatibilityTools` 203/727, `ResetUIDCache` 218, `CType` 632, `HttpClient` 1056, `Patcher` 938–962, `SteamDeckHardware` 781, `HideWindow` 1145, `ShowWindow` 1152, `Config` 1056, `DalamudUpdater` 562/584, `DotnetRuntime` 567), `AppUtil.OpenBrowser` (60, 61), `Troubleshooting` (77, 582, 587), `Ui.Post` (181), `Strings`, `Support` exceptions.

## 14. Thread / UI rules

| Context | Rules |
| --- | --- |
| UI thread | ctor, layout, `SwitchAccount`, `DoAutoLoginIfApplicable`, `ProcessLogin` (up to `Task.Run`), `Ui.Post(() => Application.Instance.Quit())` |
| Background (login task) | `Login`, `TryLoginToGame`, `TryProcessLoginResult`, `StartGameAndAddon`, patching, repair, `PersistAccount`, `Reactivate`'s `State` write (marshalled via `Ui.Post` inside `LauncherApp.SetState`, `LauncherApp.cs.md` §4) |
| Blocking modals from background | `App.ShowMessageBlocking`, `App.ShowExceptionBlocking` pump a modal on the UI thread and release a `ManualResetEvent` (`LauncherApp.cs.md` §6), so background callers can block safely |
| Plain property writes from background | `App.LoadingPage.Line2/Line3/Progress` (958–962, 1070–1085): `Line*` are plain fields, `Progress` marshals `Refresh` |
| Direct widget writes from background | `App.StartLoading(...)` (145, 608, 741, 748, 943, 1060) writes `LoadingPage.Line1/Line2/Line3/IsIndeterminate/CanCancel/CanDisableAutoLogin` and calls `LoadingPage.Reset()`, whose `Refresh()` writes GTK labels/buttons/bar directly on the caller's thread (`LauncherApp.cs:259-270`) — see §15 |
| Environment mutation | `StartGameAndAddon` sets process-wide env vars (622–664, 686–697), which affects the launcher process itself, not only the child game |

## 15. Quirks

- **`StartLoading` writes widgets off the UI thread.** Every `StartLoading` call in this file happens inside the background login task (lines 145, 608, 741, 748, 943, 1060). `LauncherApp.StartLoading` (`LauncherApp.cs.md` §7) sets plain `LoadingPage` properties and then calls `Reset()` → `Refresh()`, writing GTK label/bar/button state on the caller's thread. `State`'s setter marshals, so page *switching* is safe; the label/bar writes are not. Eto's GTK handlers for `Label.Text`/`ProgressBar.Value` are not synchronized to `Gtk.Application`'s main thread.
- **`App.StopLoading` does not clear the loading page** (`LauncherApp.cs:272-273`): it only sets `State = Main`. `RepairGame` calls `App.StopLoading()` (line 1093) and patching's `finally` sets `State = Main` (line 1016), so a later `StartLoading` reuses stale `Progress`/`ProgressText` values until they are overwritten.
- **`RepairGame`'s `doLogin` is only true after a successful verify**, and `TryProcessLoginResult` treats `RepairGame == false` as a hard stop (`MainPage.cs:318-320`), so repair failure never reaches the launch path.
- `RepairGame` uses `Program.Config.GamePath!`/`Program.Config.PatchPath!` (line 1055), not `App.Settings.*`, so the patch verifier follows the startup config, not any settings-page change made after startup (`Program.cs.md` §6).
- `Environment.Exit(0)` inside modal handlers (899, 924, 1025, 1031) exits the process from inside a modal dialog handler, bypassing `Application.Quit` and any Eto cleanup.
- `Debug.Assert` calls (110, 382, 907–911, 1048–1049, 986) compile away in release: `Debug.Assert(loginResult.State == Ok)` (382) is a no-op in release, so the launch path runs with whatever `State` the server returned.
- `StartGameAndAddon` returns `null!` in three paths (746, 795, and implicitly via `Process is null` throw path) while its signature is non-nullable `Task<Process>`, so `TryProcessLoginResult` must null-check (390–391).
- The commented-out multibox/kill loop (lines 410–549) means no interactive error dialog exists for launch failures; all failures become exceptions, and `ProcessLogin`'s `ContinueWith` shows a modal via `App.HandleContinuationBlocking` (`LauncherApp.cs.md` §6).
- `doingAutoLogin` is an unused parameter (line 194), kept for API shape.
- `AddonManager` failures reset `IsLoggingIn` (810) but do not reset the loading page, so the UI can remain on `LoadingPage` while `IsLoggingIn` is false, permitting a new login while the old task's exception is being handled.
- `Program.Steam.Shutdown()` (827) is called after the game exits, in `StartGameAndAddon`; Steam state is process-global (`Program.cs.md`).
- `SwitchAccount` writes `Password` only when `account.SavePassword` (126–127), so switching to a saved account without a stored password leaves whatever is in the password box.
- No `OnShow()` override means page entry performs no refresh; `LauncherApp.cs:80`'s `OnShow()` is the empty base (`Components/Page.cs.md` §3), and news refresh comes only from `LauncherApp.cs:69` when leaving settings.

## 16. Referenced / referenced-by

Uses: `Page` (`Components/Page.cs.md`), `LauncherApp` (`LauncherApp.cs.md`), `LoginFrame` (`LoginFrame.cs.md`), `NewsFrame` (`NewsFrame.cs.md`), `ActionButtons` (`ActionButtons.cs.md`), `AccountSwitcher` (`AccountSwitcher.cs.md`), `OtpEntryPage` via `App.AskForOtp` (`Components/OtpEntryPage.cs.md`), `LoadingPage` (`Components/LoadingPage/LoadingPage.cs.md`), `SteamDeckPromptPage` (`Components/SteamDeckPromptPage.cs.md`), `Ui` (`Ui.cs.md`), `Program` (`Program.cs.md`), `AppUtil` (`AppUtil.cs.md`), `Troubleshooting` (`Support/Troubleshooting.cs.md`), `AccountManager`/`XivAccount` (`Accounts/AccountManager.cs.md`), `Strings` (`Resources/Localization/Strings.Designer.cs.md`), and the `XIVLauncher.Common` game/patch/Dalamud/compatibility types in `lib/FFXIVQuickLauncher` (submodule — out of docs scope).
Referenced by: `LauncherApp.cs` (lines 12, 69, 80, 112, 129, 146, 282).
