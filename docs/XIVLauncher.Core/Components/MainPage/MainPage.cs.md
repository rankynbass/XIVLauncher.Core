# MainPage.cs — Specification

**File:** `src/XIVLauncher.Core/Components/MainPage/MainPage.cs` · 1,157 lines · 47,178 bytes
**Namespace:** `XIVLauncher.Core.Components.MainPage` · **Declaration:** `public class MainPage : Page`
**Role:** The launcher's login → patch → launch → wait-for-game machine plus the main page UI composition

---

## 1. Purpose and the seam

`MainPage` is the concrete `Page` (`Components/Page.cs.md`) that `LauncherApp` hosts for `LauncherState.Main` (`LauncherApp.cs.md`): constructed at `LauncherApp.cs:146`, exposed as `CurrentPage` for `LauncherState.Main` (`LauncherApp.cs:112`), entered via `this.mainPage.OnShow()` (`LauncherApp.cs:80`), refreshed with `this.mainPage.ReloadNews()` when leaving settings (`LauncherApp.cs:69`), and asked to auto-login by `RunStartupTasks` (`LauncherApp.cs:282`).

Two halves in one file:

- **UI half:** the `TableLayout` composition built in the constructor (`MainPage.cs:84-107`) of `LoginFrame` (`LoginFrame.cs.md`), `NewsFrame` (`NewsFrame.cs.md`), `ActionButtons` (`ActionButtons.cs.md`), and the public `AccountSwitcher` (`AccountSwitcher.cs.md`).
- **Pipeline half:** `ProcessLogin` → `Login` → `TryLoginToGame` → `TryProcessLoginResult` → `StartGameAndAddon`, plus patching (`HandleBootCheck`, `InstallGamePatch`, `TryHandlePatchAsync`) and repair (`RepairGame`).

In HEAD the pipeline lived inside the immediate-mode draw loop with SDL quit events; in the Eto path the UI half became Eto controls and the quit push became `Ui.Post(() => Application.Instance.Quit())` (`MainPage.cs:182`, `Ui.cs.md`). `MainPage` has **no** `OnShow()` override, so `LauncherApp.cs:80` calls `Page.OnShow()`'s empty base (`Components/Page.cs.md` §3).

Usings: `System.Diagnostics` (`Debug.Assert`, `Process`), `System.Threading.Tasks`, `Eto.Drawing` (`Size`), `Eto.Forms` (`Application`), `Serilog`, the `XIVLauncher.Common` game/patch/compatibility namespaces (submodule `lib/FFXIVQuickLauncher`, out of docs scope), `XIVLauncher.Core.Accounts`, localization, `XIVLauncher.Core.Support`.

## 2. Surface

| Member | Kind | Line | Notes |
| --- | --- | --- | --- |
| `loginFrame`, `newsFrame`, `actionButtons` | `private readonly` | 39–41 | child components |
| `IsLoggingIn` | `public bool { get; private set; }` | 43 | pipeline gate |
| `AccountSwitcher` | `public AccountSwitcher { get; private set; }` | 45 | exposed so `LoginFrame` can open it (`LoginFrame.cs:117`) |
| `MainPage(LauncherApp app)` | ctor | 47–108 | |
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
| layout | 84–107 | see §4 |

`Padding`, `Width`, `Content` are GTK widget writes ⇒ the ctor runs on the UI thread from `LauncherApp`'s construction inside `application.Initialized` (`Program.cs.md`).

## 4. Layout

```
this.Content = TableLayout(1, 2)            // 1 column, 2 rows, Spacing (0, 16)
  row 0: topRow (TableLayout 2,1, Spacing (16,0))   xscale: true, yscale: false
         column 0: loginFrame  (xscale false, Width = 400 at line 94)
         column 1: newsFrame    (xscale: true)
  row 1: actionButtons (xscale: false, yscale: true)   Rows[0].ScaleHeight = true (line 103)
```

Eto 2.13 scales columns/rows with boolean flags rather than proportions (`LauncherForm.cs.md` on the fixed 1280×800 window), so the split is expressed as: login frame with an explicit `Width = 400` in an unscaled column, news column scaled to take the remaining width (`NewsFrame.cs.md` §3 adds explicit child widths for the same reason).

`MainPage.cs:103` sets `Rows[0].ScaleHeight = true`, so the login/news row grows vertically and the button row keeps its natural height directly under the columns. Scaling `Rows[1]` floats the buttons midway between the column bottom and the window bottom, because Eto.Gtk's `StackLayout` packs items at the top with no expand flag; `LoginFrame` and `NewsFrame` each end with a `VerticalAlignment.Stretch` spacer so the columns fill their scaled rows (`LoginFrame.cs.md` §5, `NewsFrame.cs.md` §3).

## 5. `ProcessLogin(LoginAction action)`

Lines 142–197 — the entry point from `LoginFrame` (`LoginFrame.cs.md` §6, `LoginFrame.cs:58`):

1. Gate: `if (this.IsLoggingIn) return;` (lines 144–146)
2. `App.StartLoading(Strings.LoggingIn, canDisableAutoLogin: true)` (line 147) — switches to `LoadingPage` (`Components/LoadingPage/LoadingPage.cs.md`); `canCancel` keeps the default `false`, so no cancel button appears.
3. `Task.Run(async () => ...)` (line 154):
   - `GameHelpers.CheckIsGameOpen() && action == Repair` → `App.ShowMessageBlocking(Strings.RepairOfficialLauncherOpenError, Strings.XIVLauncherError)` then `this.Reactivate()` and return (lines 156–162)
   - `Repository.Ffxiv.GetVer(GamePath) == Constants.BASE_GAME_VERSION && IsUidCacheEnabled == true` → `App.ShowMessageBlocking(Strings.ReinstallUIDCacheError, ...)` + `Reactivate()` + return (lines 164–173)
   - `this.IsLoggingIn = true` (line 174)
   - `App.Settings.IsAutologin = this.loginFrame.IsAutoLogin` (line 176)
   - `Login(loginFrame.Username, loginFrame.Password, loginFrame.IsOtp, loginFrame.IsSteam, loginFrame.IsFreeTrial, false, action)` (line 178)
   - `result == true` → `Ui.Post(() => Application.Instance.Quit())` (lines 180–185): HEAD pushed an SDL quit event, ending the render loop and exiting the app.
   - `result == false` → `Log.Verbose("Reactivated after Login() != true")` + `Reactivate()` (lines 186–190)
4. `.ContinueWith(t => { if (!App.HandleContinuationBlocking(t)) this.Reactivate(); })` (lines 193–196) — `LauncherApp.HandleContinuationBlocking` handles faulted/cancelled continuations, showing modals (`LauncherApp.cs.md` §6).

`ProcessLogin` is private; `RunLoginTest()` wrappers used during smoke-testing have been removed (`LauncherApp.cs.md`).

## 6. `Login(...)` (public)

Lines 196–240:

| Step | Line(s) |
| --- | --- |
| `LoginAction.Fake` | 198–210 — builds `WindowsGameRunner(null, false)` (line 203) or `UnixGameRunner(Program.CompatibilityTools, null, false)` (line 205), calls `App.Launcher.LaunchGame(gameRunner, "0", 1, 2, false, "", GamePath!, ClientLanguage.Japanese, true, DpiAwareness.Unaware)` (line 207), returns `false` (line 209) |
| `HandleBootCheck()` | 212, gate 214–215 |
| UID cache reset | 219–220 — `isOtp && HasValidCache(username) && IsUidCacheEnabled == false` → `Program.ResetUIDCache()` |
| OTP request | 222–229 — `isOtp && !HasValidCache` → `App.AskForOtp()` (line 224), `otp = await App.WaitForOtpAsync()` (line 225), then `App.State = LauncherState.Loading` (line 228) to restore the loading page |
| OTP cancel | 231–232 — `otp == null` → `false` (`OtpEntryPage.cs.md` §5: `GetResultAsync()` yields `null` on cancel) |
| `PersistAccount` | 234 |
| `TryLoginToGame` | 236 |
| `TryProcessLoginResult` | 238 |

`doingAutoLogin` is accepted but **never read** in this method (parameter line 196; grep finds no use). Auto-login is triggered from `DoAutoLoginIfApplicable` (lines 108–116) with `Debug.Assert(App.State == LauncherState.Main)` (line 112), gate on `App.Settings.IsAutologin ?? false` plus non-empty username/password (line 114), then `ProcessLogin(LoginAction.Game)` (line 115).

`Login` is public because `LauncherApp`'s smoke/continuation logic uses it; `ProcessLogin` stays private, so the login UI path is `LoginFrame.OnLogin` → `ProcessLogin`.

## 7. `TryLoginToGame(...)`

Lines 241–273. In non-`DEBUG` builds it first queries the login gate (`#if !DEBUG` block, lines 243–261): `App.Launcher.GetGateStatus(ClientLanguage)` (line 248), with `Log.Error(ex, "Could not obtain gate status")` on failure (lines 251–254); `gateStatus == null` → `App.ShowMessageBlocking("Login servers could not be reached or maintenance is in progress. ...")` and `return null!` (lines 256–260).

Then (lines 263–272): `enableUidCache = IsUidCacheEnabled ?? false`, `gamePath = GamePath!`, `language = ClientLanguage ?? English`; `action == Repair` → `App.Launcher.Login(username, password, otp, isSteam, false, gamePath, true, isFreeTrial, language)` (line 270) — the uid-cache flag is forced `false` and `repair` flag `true` for repair; otherwise `App.Launcher.Login(..., enableUidCache, gamePath, false, ...)` (line 272). `catch` logs `"Could not login to game"` and rethrows (lines 274–278).

`return null!` at line 258 is consumed by `TryProcessLoginResult`, which dereferences `loginResult.State` (`MainPage.cs:287`) — a `NullReferenceException` in the login gate failure path, surfaced through `ProcessLogin`'s `ContinueWith` → `App.HandleContinuationBlocking` (`LauncherApp.cs.md` §6).

## 8. `TryProcessLoginResult(...)`

Lines 281–555. Sequence:

| Branch | Line(s) |
| --- | --- |
| `LoginState.NoService` | 287–290 → `throw new OauthLoginException(preErrorMsg + Strings.LoginNoServiceAccountError + postErrorMsg)` |
| `LoginState.NoTerms` | 292–295 → `throw` with `Strings.LoginTermsNotAcceptedError` |
| `LoginState.NeedsPatchBoot` | 309–312 → `throw` with `"Boot conflict, need reinstall"` (the comment at lines 298–308 explains boot binaries changed outside the launcher) |
| `LoginAction.Repair` | 314–340 — `NeedsPatchGame` → `RepairGame(loginResult)` (320), `false` → `return false` (322), else `State = Ok`; non-`NeedsPatchGame` state → `throw` with `"Repair login state not NeedsPatchGame"` (line 327); the `catch` rethrows (lines 332–340, comment notes it should never be reached) |
| `LoginState.NeedsPatchGame` | 342–351 — `InstallGamePatch` false → `Log.Error("patchSuccess != true")`, `return false`; success → `State = Ok` |
| `LoginAction.GameNoLaunch` | 353–358 → `App.ShowMessageBlocking(Strings.UpdateCheckFinished, "XIVLauncher")`, `return false` |
| Gate check (`#if !DEBUG`) | 360–381 — `null` → `"Login servers could not be reached..."` + `false` (lines 376–378); `false` → `"Maintenance is in progress."` + `false` (lines 379–380) |
| `Debug.Assert(State == Ok)` | 384 |
| launch loop | 386–407 — `using var process = await StartGameAndAddon(loginResult, isSteam, action == GameNoDalamud, action == GameNoPlugins, action == GameNoThirdparty)` (line 390); `process is null` → `throw new InvalidOperationException("Could not obtain Process Handle")` (lines 392–393); `ExitCode != 0 && TreatNonZeroExitCodeAsFailure` → `throw new InvalidOperationException("Game exited with non-zero exit code")` (lines 395–398); success → `return true` (line 400); `catch` logs `"StartGameAndError resulted in an exception."` and rethrows (lines 402–406) |

The `while (true)` loop body has `continue` only in HEAD's commented-out exception-dialog block (lines 412–551), so in the current code every iteration either returns `true` (line 400) or throws. `preErrorMsg`/`postErrorMsg` (lines 284–285) wrap messages in the string format `OauthLoginException` expects (`window.external.user("login=auth,ng,err,...")`).

`MessageBoxResult` handling, `Process.GetProcessesByName("ffxiv_dx11"/"ffxiv")` multibox detection, and the process-kill loop are all commented out (lines 412–551) — HEAD's interactive error dialog is absent, so any `StartGameAndAddon` failure becomes a thrown exception that reaches `ProcessLogin`'s continuation handler.

## 9. `StartGameAndAddon(...)` (public)

Lines 556–839. Highlights:

| Step | Line(s) |
| --- | --- |
| Dalamud runner/compat check selection | 563–577 — `Win32NT` → `WindowsDalamudRunner(Program.DalamudUpdater.Runtime)` + `WindowsDalamudCompatibilityCheck`; `Unix` → `UnixDalamudRunner(Program.CompatibilityTools, Program.DotnetRuntime)` + `UnixDalamudCompatibilityCheck`; other platform → `throw new NotImplementedException()` (line 576) |
| troubleshooting payload | 584 — `Task.Run(Troubleshooting.LogTroubleshooting)` (same UI-thread hazard comment as the ctor, lines 580–583) |
| `DalamudLauncher` | 586–589 — `DalamudLoadMethod.GetValueOrDefault(DllInject)`, `Storage.Root`, `Storage.GetFolder("logs")`, `ClientLanguage`, `DalamudLoadDelay`, `noPlugins`, `noThird`, `Troubleshooting.GetTroubleshootingJson()` |
| compatibility check | 592–604 — `EnsureCompatibility()`, rethrow on `NoRedistsException` (line 596) / `ArchitectureNotSupportedException` (line 601) |
| Dalamud wait | 606–619 — `DalamudEnabled.GetValueOrDefault(true) && !forceNoDalamud` → `App.StartLoading(Strings.WaitingForDalamud, Strings.PleaseBePatient)` (line 610), `dalamudOk = dalamudLauncher.HoldForUpdate(GamePath) == DalamudInstallState.Ok` (line 611); `DalamudRunnerException` → log + rethrow (lines 613–618) |
| env hacks | 624–666 — `CoreEnvironmentSettings.IsSteamCompatTool` → merge `XL_PRELOAD` into `LD_PRELOAD` (624–630); `FixLocale` → `LC_ALL`/`LC_CTYPE` = `Program.CType` (634–639); `FixLDP` → strip `gameoverlayrenderer.so` (641–645); `FixIM` → `XMODIFIERS=@im=null` (648–651); `FixError129` → `DOTNET_SYSTEM_GLOBALIZATION_USENLS=true` (654–657); Unix + `DontUseSystemTz` → `TZ=""` (662–666) |
| `AdditionalArgs` parsing | 668–699 — split on `%command%` into `launchEnv`/`gameArgs` (668–684); single token starting with `-` is args, else env (673–679); env pairs skip entries without `=` (690), and `key+` means append to the existing variable (692–696), each pair applied with `Environment.SetEnvironmentVariable` (698) |
| runner selection | 701–768 — `Win32NT` → `WindowsGameRunner(dalamudLauncher, dalamudOk)` (line 703); `Unix` → custom-wine validation (`WineStartupType == Custom`: `WineBinaryPath` null / missing directory / no `wine64` → `InvalidOperationException`, lines 709–716; custom/managed/Dxvk version logging 718–726) + `UnixGameRunner(Program.CompatibilityTools, dalamudLauncher, dalamudOk)` (line 752); other platform → `throw new NotImplementedException()` (line 768) |
| compatibility tool ensure | 728–750 — `ManualResetEvent` (726) + `isFailed` flag (729) + `Task.Run(... Program.CompatibilityTools.EnsureTool(Program.HttpClient, tempPath) ...).ContinueWith(... signal.Set())` (731–742), `App.StartLoading(Strings.PreparingForCompatTool, Strings.PleaseBePatient)` (line 743), `signal.WaitOne()` (744), `signal.Dispose()` (745), `isFailed` → `return null!` (747–748), then `App.StartLoading(Strings.StartingGame, Strings.HaveFun)` (line 750) |
| `UserPath` | 758–763 — `Program.CompatibilityTools.UnixToWinePath(App.Settings.GameConfigPath!.FullName)` (758), appended as ` UserPath=...` unquoted when `IsEncryptArgs` is true (759–760) and quoted when false (761–762), then `gameArgs.Trim()` (763) |
| launch | 770–779 — `App.Launcher.LaunchGame(runner, loginResult.UniqueId, loginResult.OauthLogin.Region, loginResult.OauthLogin.MaxExpansion, isSteam, gameArgs, App.Settings.GamePath, App.Settings.ClientLanguage.GetValueOrDefault(English), IsEncryptArgs.GetValueOrDefault(true), DpiAwareness.GetValueOrDefault(Unaware))` |
| window state | 783–790 — `!Program.IsSteamDeckHardware \|\| CoreEnvironmentSettings.IsSteamCompatTool` → `this.Hide()` (line 785); else `App.State = LauncherState.SteamDeckPrompt` (line 789, `SteamDeckPromptPage.cs.md`) |
| null process | 793–798 — `Log.Information("GameProcess was null...")` (794), `IsLoggingIn = false` (796), `return null!` (797) |
| addons | 800–814 — `AddonManager` (800), `App.Settings.Addons ??= new List<AddonEntry>()` (804), enabled entries cast to `IAddon` (806), `addonMgr.RunAddons(launchedProcess.Id, addons)` (808); on exception `IsLoggingIn = false` (811), `StopAddons()` (812), rethrow (813) |
| game wait | 816–823 — `Log.Debug("Waiting for game to exit")` (816), `await Task.Run(() => launchedProcess!.WaitForExit())` (818), `Log.Verbose("Game has exited")` (820), `StopAddons()` when `addonMgr.IsRunning` (822–823) |
| Steam shutdown | 826–834 — `App.Steam?.IsValid == true` (827) → `App.Steam.Shutdown()` (829), `Log.Error(ex, "Could not shut down Steam")` on failure (833) |
| return | 837 — `return launchedProcess!` |

`Steam app id` behaviour is unchanged in HEAD semantics: `Program.UsesFallbackSteamAppId` and `EngcribeSteamAppId` live in `Program.cs.md`, and `ProcessLogin` retains HEAD's commented-out Steam app-id check (lines 150–152).

### Account persistence

`PersistAccount(username, password, isOtp, isSteam, isFreeTrial)` (840–866) is called from `Login` (line 234) before the login attempt:

| Step | Line(s) |
| --- | --- |
| password refresh | 843–846 — current account matches `username` (`StringComparison.Ordinal`), stored password differs, `SavePassword` true → `App.Accounts.UpdatePassword(CurrentAccount, password)` (846) |
| free-trial refresh | 848–851 — current account matches `username` (`OrdinalIgnoreCase`), `IsFreeTrial` differs → `App.Accounts.UpdateFreeTrial(CurrentAccount, isFreeTrial)` (851) |
| new account | 853–865 — no current account, or `CurrentAccount.Id != $"{username}-{isOtp}-{isSteam}"` → `new XivAccount(username) { Password, SavePassword = true, UseOtp, IsFreeTrial, UseSteamServiceAccount }` (856–863), `App.Accounts.AddAccount` (864), `App.Accounts.CurrentAccount = accountToSave` (865) |

`SwitchAccount(XivAccount account, bool saveAsCurrent)` (120–135) is called from `AccountSwitcherOnAccountChanged` (line 139, `saveAsCurrent: true`, `AccountSwitcher.cs.md` §4) and from the ctor with `saveAsCurrent: false` (line 67). It writes `loginFrame.Username` (122), `IsOtp` (123), `IsFreeTrial` (124), `IsSteam` (125), `IsAutoLogin = App.Settings.IsAutologin ?? false` (126), `Password` when `account.SavePassword` (128–129), and `App.Accounts.CurrentAccount = account` when `saveAsCurrent` (133). The account id format `"{username}-{isOtp}-{isSteam}"` (854) is the identity key: switching OTP/Steam flags on the same username creates a new saved account.

## 10. Patching

`HandleBootCheck` (868–906): `App.Settings.PatchPath is { Exists: false }` → `PatchPath = null` (872–875), `PatchPath ??= new DirectoryInfo(Path.Combine(Paths.RoamingPath, "patches"))` (877), `App.Launcher.CheckBootVersion(App.Settings.GamePath!)` (883), inner `catch` → `Log.Error(ex, "Unable to check boot version")` + `App.ShowMessage(Strings.CannotGetBootVerError, "XIVLauncher")` + `false` (885–890); `bootPatches.Length == 0` → `true` (893–894); patches → `TryHandlePatchAsync(Repository.Boot, bootPatches, "")` (896). Outer `catch` → `App.ShowExceptionBlocking(ex, "PatchBoot")` (900) + `Environment.Exit(0)` (901) + `false` (903).

`InstallGamePatch` (907–916): `Debug.Assert` on `NeedsPatchGame` (909–910) and `PendingPatches != null` (913), then `TryHandlePatchAsync(Repository.Ffxiv, loginResult.PendingPatches, loginResult.UniqueId)` (915).

`TryHandlePatchAsync` (917–1023):

| Step | Line(s) |
| --- | --- |
| `#if WIN32` mutex `"XivLauncherIsPatching"` | 919–930 — busy-wait `WaitOne(0, false)` false → `App.ShowMessageBlocking("XIVLauncher is already patching your game in another instance...", "XIVLauncher")` + `Environment.Exit(0)` (925–926); the `return false` at line 927 is unreachable by comment; HEAD's note at line 919 says the mutex does not disappear on Linux |
| game open check | 931–936 — `GameHelpers.CheckIsGameOpen()` → `App.ShowMessageBlocking(Strings.CannotPatchGameOpenError, "XIVLauncher")`, `false` |
| patcher setup | 938–943 — `PatchInstaller(GamePath, KeepPatches ?? false)`, `AriaPatchAcquisition(logs/aria2.log)`, `Program.Patcher = new PatchManager(...)`, `Program.Patcher.OnFail += PatcherOnFail`, `installer.OnFail += InstallerOnFail` |
| loading page | 945 — `App.StartLoading(string.Format(Strings.NowPatching, repository.ToString().ToLowerInvariant()), canCancel: false, isIndeterminate: false)` |
| status thread | 947–964 — `CancellationTokenSource`, `new Thread(UpdatePatchStatus)`, `statusThread.Start()`; local function (954–965) sleeps 30 ms (958) and writes `App.LoadingPage.Line2` (960), `Line3` (961–962), `Progress = CurrentInstallIndex / (float)Downloads.Count` (964) |
| patch run | 968–977 — `await Program.Patcher.PatchAsync(false)` (969), `finally { token.Cancel(); statusThread.Join(1002 ms); }` (972–975); success → `true` (978) |
| failures | 979–1015 — `PatchInstallerException` → `App.ShowMessageBlocking(string.Format(Strings.PatchInstallerStartFailError, ex.Message), Strings.XIVLauncherError)` (980); `NotEnoughSpaceException` per `SpaceKind` (`Patches` 988–992, `AllPatches` 994–998, `Game` 1000–1004; `Debug.Assert(false, "HandlePatchAsync:Invalid NotEnoughSpaceException.SpaceKind value.")` on default 1007); generic `Exception` → `Log.Error(ex, "Error during patching")` + `App.ShowExceptionBlocking(ex, "HandlePatchAsync")` (1013–1014) |
| finally | 1016–1019 — `App.State = LauncherApp.LauncherState.Main` (1018) |
| return | 1021 — `false` for every failure path |

`Program.Patcher` is a static field on `Program` (`Program.cs.md`), so the patcher is process-global, not page-owned.

`PatcherOnFail` (1024–1029) and `InstallerOnFail` (1030–1035) both show a blocking modal then `Environment.Exit(0)` — the process terminates inside a modal handler, not through `Application.Quit`.

## 11. Repair

`RepairGame` (1036–1144): `#if WIN32` mutex block (comment 1040, mutex 1042, busy-wait `WaitOne(0, false)` 1044, `App.ShowMessageBlocking("XIVLauncher is already patching your game in another instance. Please check if XIVLauncher is still open.", "XIVLauncher")` 1046, `Environment.Exit(0)` 1047, unreachable `return false` 1048) as in patching; asserts `PendingPatches != null` (1052) and `Length != 0` (1053); `Log.Information("STARTING REPAIR")` (1055); `PatchVerifier(Program.HttpClient, Program.Config.GamePath!, Program.Config.PatchPath!, loginResult, 100 ms, MaxExpansion, false)` (1058).

`for (var doVerify = true; doVerify;)` (1060) wraps: `App.StartLoading(Strings.NowRepairingFiles, canCancel: false, isIndeterminate: false)` (1062), `verify.Start()` (1064), a `System.Threading.Timer` callback (1066–1090), `timer.Change(0, 252)` (1091), `await verify.WaitForCompletion()` (1093), `timer.Dispose()` (1094), `App.StopLoading()` (1095).

The timer callback writes, per `verify.State`:

- `DownloadMeta` (1071–1074): `Line2 = verify.CurrentFile` (1072), `Line3 = patchSet progress + bytes` (1073), `Progress = Progress/Total` (1074)
- `VerifyAndRepair` (1077–1080): `Line2 = CurrentFile` (1078), `Line3 = patch set + task + bytes` (1079), `Progress` (1080)
- default (1083–1086): `Line2 = ""` (1084), `Line3 = task count` (1085), `Progress = 1.0` when `Done` else `0` (1086)

`Components/LoadingPage/LoadingPage.cs.md` §4 is the relevant rule: `Line2`/`Line3` are plain auto-properties (no widget writes), while `Progress`'s setter posts `Refresh` through `Ui.Post` — so the timer callback is safe with respect to GTK: only `Progress` touches widgets, and it marshals.

Result handling (`switch (verify.State)`, 1097–1138): `Done` (1099) → message built from `NumBrokenFiles` (`Strings.RepairAllFilesValid` / `RepairFixedSingularFile` / `RepairFixedPluralFiles`, lines 1102–1106) plus moved-file text (`RepairSingleFileMoved`/`RepairPluralFilesMoved`, lines 1109–1113), `App.ShowMessageBlocking(mainText + additionalText)` (1116), stop looping (1118); `Error` (1121) → `doLogin = false`, `NoVersionReferenceException` → `Strings.RepairGameVerUnsupportedError` (1124–1126), else `verify.LastException + "\n\n" + Strings.RepairFailureError` (1130), stop looping (1133); `Cancelled` (1136) → `doLogin = doVerify = false` (1137). `return doLogin` (1142) — `RepairGame` returns `true` only when `doLogin` was never cleared, i.e. after a successful verify loop; `TryProcessLoginResult` then sets `State = Ok` and continues to the launch path (`MainPage.cs:322-324`).

## 12. `Hide()` / `Reactivate()`

- `Hide()` (1145–1148) → `Program.HideWindow()`, which minimizes the launcher window so the GTK application stays alive (`Program.cs.md` §5; the GTK `GApplication` exits when the last visible window closes).
- `Reactivate()` (1150–1155) → `IsLoggingIn = false`, `App.State = LauncherState.Main` (marshalled), `Program.ShowWindow()` (restores `WindowState.Normal`). Called from `ProcessLogin`'s failure paths (lines 159, 170, 188, 196) and as the login-failure recovery signal observed in smoke tests (`Log.Verbose("Reactivated after Login() != true")`, line 187).

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

`MainPage` consumes: `LoginFrame` (lines 50, 58, 94, 114, 122–129, 175, 177), `NewsFrame` (51, 92, 118), `ActionButtons` (41, 53, 59–61, 103), `AccountSwitcher` (45, 55, 56, 137–140), `App.*` (`StartLoading` 147/610/743/750/945/1060, `StopLoading` 1095, `ShowMessageBlocking`, `ShowMessage`, `HandleContinuationBlocking`, `AskForOtp` 224, `WaitForOtpAsync` 225, `Settings`, `Accounts`, `Launcher`, `UniqueIdCache`, `LoadingPage` 960–964/1072–1086, `Storage`, `Steam` 827–829), `Program.*` (`CompatibilityTools` 205/727, `ResetUIDCache` 220, `CType` 634, `HttpClient` 1058, `Patcher` 940–964, `SteamDeckHardware` 783, `HideWindow` 1147, `ShowWindow` 1154, `Config` 1058, `DalamudUpdater` 564/584, `DotnetRuntime` 569), `AppUtil.OpenBrowser` (60, 61), `Troubleshooting` (77, 584, 589), `Ui.Post` (183), `Strings`, `Support` exceptions.

## 14. Thread / UI rules

| Context | Rules |
| --- | --- |
| UI thread | ctor, layout, `SwitchAccount`, `DoAutoLoginIfApplicable`, `ProcessLogin` (up to `Task.Run`), `Ui.Post(() => Application.Instance.Quit())` |
| Background (login task) | `Login`, `TryLoginToGame`, `TryProcessLoginResult`, `StartGameAndAddon`, patching, repair, `PersistAccount`, `Reactivate`'s `State` write (marshalled via `Ui.Post` inside `LauncherApp.SetState`, `LauncherApp.cs.md` §4) |
| Blocking modals from background | `App.ShowMessageBlocking`, `App.ShowExceptionBlocking` pump a modal on the UI thread and release a `ManualResetEvent` (`LauncherApp.cs.md` §6), so background callers can block safely |
| Plain property writes from background | `App.LoadingPage.Line2/Line3/Progress` (960–964, 1072–1087): `Line*` are plain fields, `Progress` marshals `Refresh` |
| Direct widget writes from background | `App.StartLoading(...)` (147, 610, 743, 750, 945, 1062) writes `LoadingPage.Line1/Line2/Line3/IsIndeterminate/CanCancel/CanDisableAutoLogin` and calls `LoadingPage.Reset()`, whose `Refresh()` writes GTK labels/buttons/bar directly on the caller's thread (`LauncherApp.cs:261-272`) — see §15 |
| Environment mutation | `StartGameAndAddon` sets process-wide env vars (624–666, 688–699), which affects the launcher process itself, not only the child game |

## 15. Quirks

- **`StartLoading` writes widgets off the UI thread.** Every `StartLoading` call in this file happens inside the background login task (lines 147, 610, 743, 750, 945, 1062). `LauncherApp.StartLoading` (`LauncherApp.cs.md` §7) sets plain `LoadingPage` properties and then calls `Reset()` → `Refresh()`, writing GTK label/bar/button state on the caller's thread. `State`'s setter marshals, so page *switching* is safe; the label/bar writes are not. Eto's GTK handlers for `Label.Text`/`ProgressBar.Value` are not synchronized to `Gtk.Application`'s main thread.
- **`App.StopLoading` does not clear the loading page** (`LauncherApp.cs:274-275`): it only sets `State = Main`. `RepairGame` calls `App.StopLoading()` (line 1095) and patching's `finally` sets `State = Main` (line 1018), so a later `StartLoading` reuses stale `Progress`/`ProgressText` values until they are overwritten.
- **`RepairGame`'s `doLogin` is only true after a successful verify**, and `TryProcessLoginResult` treats `RepairGame == false` as a hard stop (`MainPage.cs:320-322`), so repair failure never reaches the launch path.
- `RepairGame` uses `Program.Config.GamePath!`/`Program.Config.PatchPath!` (line 1057), not `App.Settings.*`, so the patch verifier follows the startup config, not any settings-page change made after startup (`Program.cs.md` §6).
- `Environment.Exit(0)` inside modal handlers (901, 926, 1027, 1033) exits the process from inside a modal dialog handler, bypassing `Application.Quit` and any Eto cleanup.
- `Debug.Assert` calls (112, 384, 909–913, 1050–1051, 988) compile away in release: `Debug.Assert(loginResult.State == Ok)` (384) is a no-op in release, so the launch path runs with whatever `State` the server returned.
- `StartGameAndAddon` returns `null!` in three paths (748, 797, and implicitly via `Process is null` throw path) while its signature is non-nullable `Task<Process>`, so `TryProcessLoginResult` must null-check (392–393).
- The commented-out multibox/kill loop (lines 412–551) means no interactive error dialog exists for launch failures; all failures become exceptions, and `ProcessLogin`'s `ContinueWith` shows a modal via `App.HandleContinuationBlocking` (`LauncherApp.cs.md` §6).
- `doingAutoLogin` is an unused parameter (line 196), kept for API shape.
- `AddonManager` failures reset `IsLoggingIn` (812) but do not reset the loading page, so the UI can remain on `LoadingPage` while `IsLoggingIn` is false, permitting a new login while the old task's exception is being handled.
- `Program.Steam.Shutdown()` (829) is called after the game exits, in `StartGameAndAddon`; Steam state is process-global (`Program.cs.md`).
- `SwitchAccount` writes `Password` only when `account.SavePassword` (128–129), so switching to a saved account without a stored password leaves whatever is in the password box.
- No `OnShow()` override means page entry performs no refresh; `LauncherApp.cs:80`'s `OnShow()` is the empty base (`Components/Page.cs.md` §3), and news refresh comes only from `LauncherApp.cs:69` when leaving settings.

## 16. Referenced / referenced-by

Uses: `Page` (`Components/Page.cs.md`), `LauncherApp` (`LauncherApp.cs.md`), `LoginFrame` (`LoginFrame.cs.md`), `NewsFrame` (`NewsFrame.cs.md`), `ActionButtons` (`ActionButtons.cs.md`), `AccountSwitcher` (`AccountSwitcher.cs.md`), `OtpEntryPage` via `App.AskForOtp` (`Components/OtpEntryPage.cs.md`), `LoadingPage` (`Components/LoadingPage/LoadingPage.cs.md`), `SteamDeckPromptPage` (`Components/SteamDeckPromptPage.cs.md`), `Ui` (`Ui.cs.md`), `Program` (`Program.cs.md`), `AppUtil` (`AppUtil.cs.md`), `Troubleshooting` (`Support/Troubleshooting.cs.md`), `AccountManager`/`XivAccount` (`Accounts/AccountManager.cs.md`), `Strings` (`Resources/Localization/Strings.Designer.cs.md`), and the `XIVLauncher.Common` game/patch/Dalamud/compatibility types in `lib/FFXIVQuickLauncher` (submodule — out of docs scope).
Referenced by: `LauncherApp.cs` (lines 12, 69, 80, 114, 131, 148, 284).
