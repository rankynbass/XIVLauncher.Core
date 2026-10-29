# MainPage.cs — Specification

**File:** `src/XIVLauncher.Core/Components/MainPage/MainPage.cs` · 1,199 lines · 49,465 bytes
**Namespace:** `XIVLauncher.Core.Components.MainPage` · **Declaration:** `public class MainPage : Page`
**Role:** The launcher's login → patch → launch → wait-for-game machine plus the main page UI composition

---

## 1. Purpose and the seam

`MainPage` is the concrete `Page` (`Components/Page.cs.md`) that `LauncherApp` hosts for `LauncherState.Main` (`LauncherApp.cs.md`): constructed at `LauncherApp.cs:146`, exposed as `CurrentPage` for `LauncherState.Main` (`LauncherApp.cs:112`), entered via `this.mainPage.OnShow()` (`LauncherApp.cs:80`), refreshed with `this.mainPage.ReloadNews()` when leaving settings (`LauncherApp.cs:69`), and asked to auto-login by `RunStartupTasks` (`LauncherApp.cs:282`).

Two halves in one file:

- **UI half:** the `DynamicLayout` composition built in the constructor (`MainPage.cs:90-117`) of `LoginFrame` (`LoginFrame.cs.md`), `NewsFrame` (`NewsFrame.cs.md`), `ActionButtons` (`ActionButtons.cs.md`), and the public `AccountSwitcher` (`AccountSwitcher.cs.md`).
- **Pipeline half:** `ProcessLogin` → `Login` → `TryLoginToGame` → `TryProcessLoginResult` → `StartGameAndAddon`, plus patching (`HandleBootCheck`, `InstallGamePatch`, `TryHandlePatchAsync`) and repair (`RepairGame`).

In HEAD the pipeline lived inside the immediate-mode draw loop with SDL quit events; in the Eto path the UI half became Eto controls and the quit push became `Ui.Post(() => Application.Instance.Quit())` (`MainPage.cs:196`, `Ui.cs.md`). `MainPage` has **no** `OnShow()` override, so `LauncherApp.cs:80` calls `Page.OnShow()`'s empty base (`Components/Page.cs.md` §3).

Usings: `System.Diagnostics` (`Debug.Assert`, `Process`), `System.Threading.Tasks`, `Eto.Drawing` (`Size`), `Eto.Forms` (`Application`), `Serilog`, the `XIVLauncher.Common` game/patch/compatibility namespaces (submodule `lib/FFXIVQuickLauncher`, out of docs scope), `XIVLauncher.Core.Accounts`, localization, `XIVLauncher.Core.Support`.

## 2. Surface

| Member | Kind | Line | Notes |
| --- | --- | --- | --- |
| `loginFrame`, `newsFrame`, `actionButtons` | `private readonly` | 40–42 | child components |
| `IsLoggingIn` | `public bool { get; private set; }` | 44 | pipeline gate |
| `AccountSwitcher` | `public AccountSwitcher { get; private set; }` | 46 | exposed so `LoginFrame` can open it (`LoginFrame.cs:118`) |
| `MainPage(LauncherApp app)` | ctor | 51–118 | |
| `DoAutoLoginIfApplicable()` | `public` | 117–123 | |
| `ReloadNews()` | `public` | 124 | forwards to `NewsFrame` |
| `SwitchAccount(XivAccount, bool)` | `private` | 127–142 | |
| `AccountSwitcherOnAccountChanged` | `private` | 144–147 | |
| `ProcessLogin(LoginAction)` | `private` | 149–212 | |
| `Login(username, password, isOtp, isSteam, isFreeTrial, doingAutoLogin, action)` | `public async Task<bool>` | 213–256 | |
| `TryLoginToGame(...)` | `private async Task<Launcher.LoginResult>` | 258–296 | |
| `TryProcessLoginResult(loginResult, isSteam, action)` | `private async Task<bool>` | 298–571 | |
| `StartGameAndAddon(loginResult, isSteam, forceNoDalamud, noPlugins, noThird)` | `public async Task<Process>` | 573–855 | |
| `PersistAccount(...)` | `private` | 857–883 | |
| `HandleBootCheck()` | `private async Task<bool>` | 885–922 | |
| `InstallGamePatch(loginResult)` | `private Task<bool>` | 924–932 | |
| `TryHandlePatchAsync(repository, pendingPatches, sid)` | `private async Task<bool>` | 934–1039 | |
| `PatcherOnFail(patch, context)` | `private` | 1041–1045 | |
| `InstallerOnFail()` | `private` | 1047–1051 | |
| `RepairGame(loginResult)` | `private async Task<bool>` | 1053–1160 | |
| `Hide()` | `private` | 1162–1165 | `Program.HideWindow()` |
| `Reactivate()` | `private` | 1167–1172 | `IsLoggingIn = false`, `State = Main`, `Program.ShowWindow()` |
| `IsRunningUnderWine()` | `private static` | 1179–1191 — wine auto-detect: `EnvironmentSettings.IsWine` (`XL_WINEONLINUX`) or the `ntdll.dll` export `wine_get_version`; probe returns false on native Windows (`EntryPointNotFoundException`) and native Linux (`DllNotFoundException`); P/Invoke in nested `WineNtdll` (1194–1198). Gates the elevation warning (72–75) |

`LoginAction` (7 members) lives in `Components/MainPage/LoginAction.cs.md`.

## 3. Constructor

Order matters, because some steps touch the UI thread during app startup (`Program.cs.md` §4 explains the GTK synchronization-context hazard):

| Step | Line(s) | Behaviour |
| --- | --- | --- |
| children | 51–54 | `new LoginFrame(this)`, `new NewsFrame(app)` (starts the news fetch + banner timer), `new ActionButtons()` |
| account switcher | 56–57 | `new AccountSwitcher(app.Accounts)`, `AccountChanged += AccountSwitcherOnAccountChanged` |
| login wiring | 59 | `loginFrame.OnLogin += ProcessLogin` |
| action buttons | 60–62 | settings → `App.State = Settings`; status → `AppUtil.OpenBrowser("https://is.xivup.com/")`; account → `AppUtil.OpenBrowser("https://sqex.to/Msp")` (`AppUtil.cs.md`) |
| padding | 66 | `new Padding(10, 32, 0, 10)` — zero right padding pins the login column to the window right edge |
| saved account | 68–70 | `App.Accounts.CurrentAccount` → `SwitchAccount(savedAccount, false)`, so the login frame is prefilled without touching `CurrentAccount` |
| elevation warning | 72–75 (comment 72–73, gate 74, message 75) | `!IsRunningUnderWine() && PlatformHelpers.IsElevated()` → `App.ShowMessage(Strings.XLElevatedWarning, "XIVLauncher")` — never fires under wine (§15) |
| troubleshooting | 82 | `Task.Run(Troubleshooting.LogTroubleshooting)` — deliberately off the UI thread; HEAD generated the payload synchronously inside the constructor, and `IntegrityCheck.CompareIntegrityAsync().Result` blocks the GTK main loop when run there (`Support/Troubleshooting.cs.md`) |
| layout | 89–118 | MinimumSize floors for both frames (90–91, comment 89), `DynamicLayout` build 94–116, see §4 |

`Padding`, `Width`, `Content` are GTK widget writes ⇒ the ctor runs on the UI thread from `LauncherApp`'s construction inside `application.Initialized` (`Program.cs.md`).

## 4. Layout

```
this.Content = DynamicLayout(Spacing 16x16, line 94)
  BeginHorizontal()                                        (96)
    BeginVertical(xscale: true, yscale: true)              (101)   <- news column section
      BeginScrollable(BorderType.None, Padding 10, xscale/yscale true) (102)
        newsFrame (xscale: true, yscale: true)             (103)
      EndScrollable                                        (104)
      Add(actionButtons, xscale: false, yscale: false)     (105)   <- buttons at column bottom
    EndVertical                                            (106)
    Add(loginFrame, xscale: false, yscale: true)           (110)   <- login column, Width=300 pinned, flush right
  EndHorizontal                                            (112)
  this.Content = layout                                    (114)
```

Eto `DynamicLayout` scales with boolean flags (`LauncherForm.cs.md` on the resizable window, `MinimumSize = new Size(640, 480)`), so the split is expressed as: the news column section is scaled (`BeginVertical(xscale: true, yscale: true)`, line 98) and takes the remaining width and height, the login column is added `xscale: false` (line 110) with the login frame width-pinned `Width = 300` (line 92), zero right padding (line 66), and MinimumSize floors on both frames (lines 90–91, 200 px each) so the columns cannot overlap at the window minimum (news min 200 + 20 px news padding + 16 px spacing + login 300 = 536 < 640).

The action buttons are added inside the news column's `BeginVertical` below the scrollable news frame with `xscale: false, yscale: false` (line 105), so they keep natural size at the column bottom, 10 px above the window bottom edge. `newsFrame` is wrapped in `BeginScrollable` (lines 99–104) so it can shrink independently; the login column has no scrollable wrapper because DynamicScrollable cells expand to the row size, and a scrollable login frame would cover the news column and the bottom buttons (comment at lines 108–109).

> **Line-number note:** all line references in this document are verified against the current working tree (1,199 lines). Baseline commit ccc1ca9 (1,167 lines) → HEAD → working tree: ctor layout rewrite 84–110 → 94–116, login snapshot 145–208 → 152–215, Login 213, TryLoginToGame 258, TryProcessLoginResult 298, StartGameAndAddon 573, patching 885+, repair 1053+, Hide/Reactivate 1162/1167; the wine elevation gate (72–75) adds +3 above it, and the `IsRunningUnderWine` helper (1179–1198) adds +28 at the tail.

## 5. `ProcessLogin(LoginAction action)`

Lines 149–212 — the entry point from `LoginFrame` (`LoginFrame.cs.md` §6, `LoginFrame.cs:59`):

1. Gate: `if (this.IsLoggingIn) return;` (lines 151–152)
2. `App.StartLoading(Strings.LoggingIn, canDisableAutoLogin: true)` (line 154) — switches to `LoadingPage` (`Components/LoadingPage/LoadingPage.cs.md`); `canCancel` keeps the default `false`, so no cancel button appears.
3. **Login widget snapshot** (lines 159–164): `username`, `password`, `isOtp`, `isSteam`, `isFreeTrial`, `isAutoLogin` are read from `loginFrame` on the UI thread, *before* `Task.Run`. Eto reads are UI-object accesses, not just writes: reading `Checkbox.Value`/`Input.Value` calls `Control.Handler` → `Application.EnsureUIThread`, and the pre-fix code did it inside `Task.Run` on a `.NET TP Worker` thread, so GTK read label/checkbox handlers off-thread (`Ui.cs.md` §2 documents the stack Eto reports). The snapshot hands plain values to the background task, so nothing in the task touches a widget.
4. `Task.Run(async () => ...)` (line 169):
   - `GameHelpers.CheckIsGameOpen() && action == Repair` → `App.ShowMessageBlocking(Strings.RepairOfficialLauncherOpenError, Strings.XIVLauncherError)` then `this.Reactivate()` and return (lines 171–177)
   - `Repository.Ffxiv.GetVer(GamePath) == Constants.BASE_GAME_VERSION && IsUidCacheEnabled == true` → `App.ShowMessageBlocking(Strings.ReinstallUIDCacheError, ...)` + `Reactivate()` + return (lines 179–188)
   - `this.IsLoggingIn = true` (line 194)
   - `App.Settings.IsAutologin = isAutoLogin` (line 192) — the snapshot, not the checkbox
   - `Login(username, password, isOtp, isSteam, isFreeTrial, false, action)` (line 194)
   - `result == true` → `Ui.Post(() => Application.Instance.Quit())` (lines 196–201): HEAD pushed an SDL quit event, ending the render loop and exiting the app.
   - `result == false` → `Log.Verbose("Reactivated after Login() != true")` + `Reactivate()` (lines 203–207)
5. `.ContinueWith(t => { if (!App.HandleContinuationBlocking(t)) this.Reactivate(); })` (lines 208–211) — `LauncherApp.HandleContinuationBlocking` handles faulted/cancelled continuations, showing modals (`LauncherApp.cs.md` §6).

`ProcessLogin` is private; `RunLoginTest()` wrappers used during smoke-testing have been removed (`LauncherApp.cs.md`).

## 6. `Login(...)` (public)

Lines 213–256:

| Step | Line(s) |
| --- | --- |
| `LoginAction.Fake` | 215–227 — builds `WindowsGameRunner(null, false)` (line 220) or `UnixGameRunner(Program.CompatibilityTools, null, false)` (line 222), calls `App.Launcher.LaunchGame(gameRunner, "0", 1, 2, false, "", GamePath!, ClientLanguage.Japanese, true, DpiAwareness.Unaware)` (line 224), returns `false` (line 226) |
| `HandleBootCheck()` | 229, gate 231–232 |
| UID cache reset | 236–237 — `isOtp && HasValidCache(username) && IsUidCacheEnabled == false` → `Program.ResetUIDCache()` |
| OTP request | 239–246 — `isOtp && !HasValidCache` → `App.AskForOtp()` (line 241), `otp = await App.WaitForOtpAsync()` (line 242), then `App.State = LauncherState.Loading` (line 245) to restore the loading page |
| OTP cancel | 248–249 — `otp == null` → `false` (`OtpEntryPage.cs.md` §5: `GetResultAsync()` yields `null` on cancel) |
| `PersistAccount` | 251 |
| `TryLoginToGame` | 253 |
| `TryProcessLoginResult` | 255 |

`doingAutoLogin` is accepted but **never read** in this method (parameter line 213; grep finds no use). Auto-login is triggered from `DoAutoLoginIfApplicable` (lines 117–123) with `Debug.Assert(App.State == LauncherApp.LauncherState.Main)` (line 119), gate on `App.Settings.IsAutologin ?? false` plus non-empty username/password (line 121), then `ProcessLogin(LoginAction.Game)` (line 122).

`Login` is public because `LauncherApp`'s smoke/continuation logic uses it; `ProcessLogin` stays private, so the login UI path is `LoginFrame.OnLogin` → `ProcessLogin`.

## 7. `TryLoginToGame(...)`

Lines 258–296. In non-`DEBUG` builds it first queries the login gate (`#if !DEBUG` block, lines 260–278): `App.Launcher.GetGateStatus(ClientLanguage)` (line 265), with `Log.Error(ex, "Could not obtain gate status")` on failure (lines 268–271); `gateStatus == null` → `App.ShowMessageBlocking("Login servers could not be reached or maintenance is in progress. ...")` and `return null!` (lines 273–277).

Then (lines 280–289): `enableUidCache = IsUidCacheEnabled ?? false`, `gamePath = GamePath!`, `language = ClientLanguage ?? English`; `action == Repair` → `App.Launcher.Login(username, password, otp, isSteam, false, gamePath, true, isFreeTrial, language)` (line 287) — the uid-cache flag is forced `false` and `repair` flag `true` for repair; otherwise `App.Launcher.Login(..., enableUidCache, gamePath, false, ...)` (line 289). `catch` logs `"Could not login to game"` and rethrows (lines 291–295).

`return null!` at line 276 is consumed by `TryProcessLoginResult`, which dereferences `loginResult.State` (`MainPage.cs:301`) — a `NullReferenceException` in the login gate failure path, surfaced through `ProcessLogin`'s `ContinueWith` → `App.HandleContinuationBlocking` (`LauncherApp.cs.md` §6).

## 8. `TryProcessLoginResult(...)`

Lines 298–571. Sequence:

| Branch | Line(s) |
| --- | --- |
| `LoginState.NoService` | 304–307 → `throw new OauthLoginException(preErrorMsg + Strings.LoginNoServiceAccountError + postErrorMsg)` |
| `LoginState.NoTerms` | 309–312 → `throw` with `Strings.LoginTermsNotAcceptedError` |
| `LoginState.NeedsPatchBoot` | 326–329 → `throw` with `"Boot conflict, need reinstall"` (the comment at lines 315–325 explains boot binaries changed outside the launcher) |
| `LoginAction.Repair` | 331–357 — `NeedsPatchGame` → `RepairGame(loginResult)` (337), `false` → `return false` (339), else `State = Ok`; non-`NeedsPatchGame` state → `throw` with `"Repair login state not NeedsPatchGame"` (line 344); the `catch` rethrows (lines 349–357, comment notes it should never be reached) |
| `LoginState.NeedsPatchGame` | 359–368 — `InstallGamePatch` false → `Log.Error("patchSuccess != true")`, `return false`; success → `State = Ok` |
| `LoginAction.GameNoLaunch` | 370–375 → `App.ShowMessageBlocking(Strings.UpdateCheckFinished, "XIVLauncher")`, `return false` |
| Gate check (`#if !DEBUG`) | 377–398 — `null` → `"Login servers could not be reached..."` + `false` (lines 393–395); `false` → `"Maintenance is in progress."` + `false` (lines 396–397) |
| `Debug.Assert(State == Ok)` | 401 |
| launch loop | 403–424 — `using var process = await StartGameAndAddon(loginResult, isSteam, action == GameNoDalamud, action == GameNoPlugins, action == GameNoThirdparty)` (line 407); `process is null` → `throw new InvalidOperationException("Could not obtain Process Handle")` (lines 409–410); `ExitCode != 0 && TreatNonZeroExitCodeAsFailure` → `throw new InvalidOperationException("Game exited with non-zero exit code")` (lines 413–416); success → `return true` (line 417); `catch` logs `"StartGameAndError resulted in an exception."` and rethrows (lines 419–423) |

The `while (true)` loop body has `continue` only in HEAD's commented-out exception-dialog block (lines 426–569), so in the current code every iteration either returns `true` (line 417) or throws. `preErrorMsg`/`postErrorMsg` (lines 301–302) wrap messages in the string format `OauthLoginException` expects (`window.external.user("login=auth,ng,err,...")`).

`MessageBoxResult` handling, `Process.GetProcessesByName("ffxiv_dx11"/"ffxiv")` multibox detection, and the process-kill loop are all commented out (lines 426–569) — HEAD's interactive error dialog is absent, so any `StartGameAndAddon` failure becomes a thrown exception that reaches `ProcessLogin`'s continuation handler.

## 9. `StartGameAndAddon(...)` (public)

Lines 573–855. Highlights:

| Step | Line(s) |
| --- | --- |
| Dalamud runner/compat check selection | 579–592 — `Win32NT` → `WindowsDalamudRunner(Program.DalamudUpdater.Runtime)` + `WindowsDalamudCompatibilityCheck`; `Unix` → `UnixDalamudRunner(Program.CompatibilityTools, Program.DotnetRuntime)` + `UnixDalamudCompatibilityCheck`; other platform → `throw new NotImplementedException()` (line 591) |
| troubleshooting payload | 601 — `Task.Run(Troubleshooting.LogTroubleshooting)` (same UI-thread hazard comment as the ctor, lines 597–600) |
| `DalamudLauncher` | 600–606 — `DalamudLoadMethod.GetValueOrDefault(DllInject)`, `Storage.Root`, `Storage.GetFolder("logs")`, `ClientLanguage`, `DalamudLoadDelay`, `noPlugins`, `noThird`, `Troubleshooting.GetTroubleshootingJson()` |
| compatibility check | 609–621 — `EnsureCompatibility()`, rethrow on `NoRedistsException` (line 615) / `ArchitectureNotSupportedException` (line 620) |
| Dalamud wait | 623–636 — `DalamudEnabled.GetValueOrDefault(true) && !forceNoDalamud` → `App.StartLoading(Strings.WaitingForDalamud, Strings.PleaseBePatient)` (line 627), `dalamudOk = dalamudLauncher.HoldForUpdate(GamePath) == DalamudInstallState.Ok` (line 628); `DalamudRunnerException` → log + rethrow (lines 630–635) |
| env hacks | 639–682 — `CoreEnvironmentSettings.IsSteamCompatTool` → merge `XL_PRELOAD` into `LD_PRELOAD` (641–647); `FixLocale` → `LC_ALL`/`LC_CTYPE` = `Program.CType` (653–654); `FixLDP` → strip `gameoverlayrenderer.so` (658–661); `FixIM` → `XMODIFIERS=@im=null` (665–667); `FixError130` → `DOTNET_SYSTEM_GLOBALIZATION_USENLS=true` (671–673); Unix + `DontUseSystemTz` → `TZ=""` (679–681) |
| `AdditionalArgs` parsing | 684–716 — split on `%command%` into `launchEnv`/`gameArgs` (684–700); single token starting with `-` is args, else env (689–695); env pairs skip entries without `=` (707), and `key+` means append to the existing variable (709–713), each pair applied with `Environment.SetEnvironmentVariable` (714) |
| runner selection | 718–783 — `Win32NT` → `WindowsGameRunner(dalamudLauncher, dalamudOk)` (line 720); `Unix` → custom-wine validation (`WineStartupType == Custom`: `WineBinaryPath` null / missing directory / no `wine64` → `InvalidOperationException`, lines 724–733; custom/managed/Dxvk version logging 735–741) + `UnixGameRunner(Program.CompatibilityTools, dalamudLauncher, dalamudOk)` (line 769); other platform → `throw new NotImplementedException()` (line 783) |
| compatibility tool ensure | 743–767 — `ManualResetEvent` (743) + `isFailed` flag (744) + `Task.Run(... Program.CompatibilityTools.EnsureTool(Program.HttpClient, tempPath) ...).ContinueWith(... signal.Set())` (746–758), `App.StartLoading(Strings.PreparingForCompatTool, Strings.PleaseBePatient)` (line 760), `signal.WaitOne()` (761), `signal.Dispose()` (762), `isFailed` → `return null!` (765), then `App.StartLoading(Strings.StartingGame, Strings.HaveFun)` (line 767) |
| `UserPath` | 773–778 — `Program.CompatibilityTools.UnixToWinePath(App.Settings.GameConfigPath!.FullName)` (773), appended as ` UserPath=...` unquoted when `IsEncryptArgs` is true (774–775) and quoted when false (776–777), then `gameArgs.Trim()` (779) |
| launch | 787–796 — `App.Launcher.LaunchGame(runner, loginResult.UniqueId, loginResult.OauthLogin.Region, loginResult.OauthLogin.MaxExpansion, isSteam, gameArgs, App.Settings.GamePath, App.Settings.ClientLanguage.GetValueOrDefault(English), IsEncryptArgs.GetValueOrDefault(true), DpiAwareness.GetValueOrDefault(Unaware))` |
| window state | 800–806 — `!Program.IsSteamDeckHardware \|\| CoreEnvironmentSettings.IsSteamCompatTool` → `this.Hide()` (line 802); else `App.State = LauncherApp.LauncherState.SteamDeckPrompt` (line 806, `SteamDeckPromptPage.cs.md`) |
| null process | 809–814 — `Log.Information("GameProcess was null...")` (811), `IsLoggingIn = false` (812), `return null!` (813) |
| addons | 816–827 — `AddonManager` (816), `App.Settings.Addons ??= new List<AddonEntry>()` (820), enabled entries cast to `IAddon` (822), `addonMgr.RunAddons(launchedProcess.Id, addons)` (824); on exception `IsLoggingIn = false` (828), `StopAddons()` (829), `throw` (827) |
| game wait | 833–840 — `Log.Debug("Waiting for game to exit")` (833), `await Task.Run(() => launchedProcess!.WaitForExit()).ConfigureAwait(false)` (835), `Log.Verbose("Game has exited")` (837), `StopAddons()` when `addonMgr.IsRunning` (839–840) |
| Steam shutdown | 844–851 — `App.Steam?.IsValid == true` (844) → `App.Steam.Shutdown()` (846), `Log.Error(ex, "Could not shut down Steam")` on failure (850) |
| return | 854 — `return launchedProcess!` |

`Steam app id` behaviour is unchanged in HEAD semantics: `Program.UsesFallbackSteamAppId` and `EngcribeSteamAppId` live in `Program.cs.md`, and `ProcessLogin` retains HEAD's commented-out Steam app-id check (lines 165–167).

### Account persistence

`PersistAccount(username, password, isOtp, isSteam, isFreeTrial)` (857–883) is called from `Login` (line 251) before the login attempt:

| Step | Line(s) |
| --- | --- |
| password refresh | 860–863 — current account matches `username` (`StringComparison.Ordinal`), stored password differs, `SavePassword` true → `App.Accounts.UpdatePassword(CurrentAccount, password)` (863) |
| free-trial refresh | 865–868 — current account matches `username` (`OrdinalIgnoreCase`), `IsFreeTrial` differs → `App.Accounts.UpdateFreeTrial(CurrentAccount, isFreeTrial)` (868) |
| new account | 870–882 — no current account, or `CurrentAccount.Id != $"{username}-{isOtp}-{isSteam}"` → `new XivAccount(username) { Password, SavePassword = true, UseOtp, IsFreeTrial, UseSteamServiceAccount }` (872–879), `App.Accounts.AddAccount` (880), `App.Accounts.CurrentAccount = accountToSave` (881) |

`SwitchAccount(XivAccount account, bool saveAsCurrent)` (127–142) is called from `AccountSwitcherOnAccountChanged` (line 144, `saveAsCurrent: true`, `AccountSwitcher.cs.md` §4) and from the ctor with `saveAsCurrent: false` (line 70). It writes `loginFrame.Username` (129), `IsOtp` (130), `IsFreeTrial` (131), `IsSteam` (132), `IsAutoLogin = App.Settings.IsAutologin ?? false` (133), `Password` when `account.SavePassword` (134–135), and `App.Accounts.CurrentAccount = account` when `saveAsCurrent` (140). The account id format `"{username}-{isOtp}-{isSteam}"` (870) is the identity key: switching OTP/Steam flags on the same username creates a new saved account.

## 10. Patching

`HandleBootCheck` (885–923): `App.Settings.PatchPath is { Exists: false }` → `PatchPath = null` (889–892), `PatchPath ??= new DirectoryInfo(Path.Combine(Paths.RoamingPath, "patches"))` (894), `App.Launcher.CheckBootVersion(App.Settings.GamePath!)` (900), inner `catch` → `Log.Error(ex, "Unable to check boot version")` + `App.ShowMessage(Strings.CannotGetBootVerError, "XIVLauncher")` + `false` (902–907); `bootPatches.Length == 0` → `true` (910–911); patches → `TryHandlePatchAsync(Repository.Boot, bootPatches, "")` (913). Outer `catch` → `App.ShowExceptionBlocking(ex, "PatchBoot")` (917) + `Environment.Exit(0)` (918) + `false` (920).

`InstallGamePatch` (924–933): `Debug.Assert` on `NeedsPatchGame` (926–927) and `PendingPatches != null` (929), then `TryHandlePatchAsync(Repository.Ffxiv, loginResult.PendingPatches, loginResult.UniqueId)` (931).

`TryHandlePatchAsync` (934–1040):

| Step | Line(s) |
| --- | --- |
| `#if WIN32` mutex `"XivLauncherIsPatching"` | 936–947 — busy-wait `WaitOne(0, false)` false → `App.ShowMessageBlocking("XIVLauncher is already patching your game in another instance...", "XIVLauncher")` + `Environment.Exit(0)` (942–943); the `return false` at line 944 is unreachable by comment; HEAD's note at line 936 says the mutex does not disappear on Linux |
| game open check | 948–953 — `GameHelpers.CheckIsGameOpen()` → `App.ShowMessageBlocking(Strings.CannotPatchGameOpenError, "XIVLauncher")`, `false` |
| patcher setup | 955–960 — `PatchInstaller(GamePath, KeepPatches ?? false)`, `AriaPatchAcquisition(logs/aria2.log)`, `Program.Patcher = new PatchManager(...)`, `Program.Patcher.OnFail += PatcherOnFail`, `installer.OnFail += InstallerOnFail` |
| loading page | 962 — `App.StartLoading(string.Format(Strings.NowPatching, repository.ToString().ToLowerInvariant()), canCancel: false, isIndeterminate: false)` |
| status thread | 964–981 — `CancellationTokenSource`, `new Thread(UpdatePatchStatus)`, `statusThread.Start()`; local function (971–982) sleeps 30 ms (975) and writes `App.LoadingPage.Line2` (977), `Line3` (978–979), `Progress = CurrentInstallIndex / (float)Downloads.Count` (981) |
| patch run | 985–994 — `await Program.Patcher.PatchAsync(false)` (987), `finally { token.Cancel(); statusThread.Join(1005 ms); }` (989–992); success → `true` (994) |
| failures | 996–1032 — `PatchInstallerException` → `App.ShowMessageBlocking(string.Format(Strings.PatchInstallerStartFailError, ex.Message), Strings.XIVLauncherError)` (997); `NotEnoughSpaceException` per `SpaceKind` (`Patches` 1005–1009, `AllPatches` 1011–1015, `Game` 1017–1021; `Debug.Assert(false, "HandlePatchAsync:Invalid NotEnoughSpaceException.SpaceKind value.")` on default 1024); generic `Exception` → `Log.Error(ex, "Error during patching")` + `App.ShowExceptionBlocking(ex, "HandlePatchAsync")` (1030–1031) |
| finally | 1033–1036 — `App.State = LauncherApp.LauncherState.Main` (1035) |
| return | 1038 — `false` for every failure path |

`Program.Patcher` is a static field on `Program` (`Program.cs.md`), so the patcher is process-global, not page-owned.

`PatcherOnFail` (1041–1046) and `InstallerOnFail` (1047–1052) both show a blocking modal then `Environment.Exit(0)` — the process terminates inside a modal handler, not through `Application.Quit`.

## 11. Repair

`RepairGame` (1053–1161): `#if WIN32` mutex block (comment 1055, mutex 1059, busy-wait `WaitOne(0, false)` 1061, `App.ShowMessageBlocking("XIVLauncher is already patching your game in another instance. Please check if XIVLauncher is still open.", "XIVLauncher")` 1063, `Environment.Exit(0)` 1064, unreachable `return false` 1065) as in patching; asserts `PendingPatches != null` (1069) and `Length != 0` (1070); `Log.Information("STARTING REPAIR")` (1072); `PatchVerifier(Program.HttpClient, Program.Config.GamePath!, Program.Config.PatchPath!, loginResult, 103 ms, MaxExpansion, false)` (1075).

`for (var doVerify = true; doVerify;)` (1077) wraps: `App.StartLoading(Strings.NowRepairingFiles, canCancel: false, isIndeterminate: false)` (1079), `verify.Start()` (1081), a `System.Threading.Timer` callback (1083–1107), `timer.Change(0, 255)` (1108), `await verify.WaitForCompletion()` (1110), `timer.Dispose()` (1111), `App.StopLoading()` (1112).

The timer callback writes, per `verify.State`:

- `DownloadMeta` (1088–1091): `Line2 = verify.CurrentFile` (1089), `Line3 = patchSet progress + bytes` (1090), `Progress = Progress/Total` (1091)
- `VerifyAndRepair` (1094–1097): `Line2 = CurrentFile` (1095), `Line3 = patch set + task + bytes` (1096), `Progress` (1097)
- default (1100–1103): `Line2 = ""` (1101), `Line3 = task count` (1102), `Progress = 1.0` when `Done` else `0` (1103)

`Components/LoadingPage/LoadingPage.cs.md` §3 is the relevant rule: every `LoadingPage` property setter stores the value and posts `ApplyState` through `Ui.Post`, so the timer callback runs the widget writes on the UI thread. The repair timer writes (`Line2`/`Line3`/`Progress`) are safe by construction; the pre-fix version marshalled only `Progress`, and `App.StartLoading(..., isIndeterminate: false)` at line 1079 drove `Reset() → Refresh() → Label.Text` on the timer thread.

Result handling (`switch (verify.State)`, 1114–1156): `Done` (1116) → message built from `NumBrokenFiles` (`Strings.RepairAllFilesValid` / `RepairFixedSingularFile` / `RepairFixedPluralFiles`, lines 1119–1123) plus moved-file text (`RepairSingleFileMoved`/`RepairPluralFilesMoved`, lines 1126–1130), `App.ShowMessageBlocking(mainText + additionalText)` (1133), stop looping (1135); `Error` (1138) → `doLogin = false`, `NoVersionReferenceException` → `Strings.RepairGameVerUnsupportedError` (1141–1143), else `verify.LastException + "\n\n" + Strings.RepairFailureError` (1147), stop looping (1150); `Cancelled` (1153) → `doLogin = doVerify = false` (1154). `return doLogin` (1159) — `RepairGame` returns `true` only when `doLogin` was never cleared, i.e. after a successful verify loop; `TryProcessLoginResult` then sets `State = Ok` and continues to the launch path (`MainPage.cs:341-346`).

## 12. `Hide()` / `Reactivate()`

- `Hide()` (1162–1165) → `Program.HideWindow()` (line 1164), which hides (unmaps) the launcher window while the game runs: the GTK `GApplication` is held across the hidden period so the loop stays alive (`Program.cs.md` §5; `LauncherForm.cs.md` §3).
- `Reactivate()` (1167–1173): `IsLoggingIn = false` (1169), `App.State = LauncherApp.LauncherState.Main` (1170, marshalled), `Program.ShowWindow()` (1172) — restores visibility (`Visible = true`, `WindowState.Normal`) and releases the `GApplication` hold. Called from `ProcessLogin`'s failure paths (lines 175, 186, 204, 209) and as the login-failure recovery signal observed in smoke tests (`Log.Verbose("Reactivated after Login() != true")`, line 203).

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

`MainPage` consumes: `LoginFrame` (lines 59, 91–92, 124, 132–139, 162–169), `NewsFrame` (60, 90, 128), `ActionButtons` (42, 60–62, 110), `AccountSwitcher` (46, 56, 57, 149–152), `App.*` (`StartLoading` 154/627/760/767/962/1079, `StopLoading` 1112, `ShowMessageBlocking`, `ShowMessage`, `HandleContinuationBlocking`, `AskForOtp` 241, `WaitForOtpAsync` 242, `Settings`, `Accounts`, `Launcher`, `UniqueIdCache`, `LoadingPage` 977–981/1089–1103, `Storage`, `Steam` 846–848), `Program.*` (`CompatibilityTools` 222/749, `ResetUIDCache` 237, `CType` 651, `HttpClient` 1075, `Patcher` 957–981, `SteamDeckHardware` 800, `HideWindow` 1164, `ShowWindow` 1172, `Config` 1075, `DalamudUpdater` 583/600, `DotnetRuntime` 588), `AppUtil.OpenBrowser` (61, 62), `Troubleshooting` (82, 604, 609), `Ui.Post` (199), `Strings`, `Support` exceptions.

## 14. Thread / UI rules

| Context | Rules |
| --- | --- |
| UI thread | ctor, layout, `SwitchAccount`, `DoAutoLoginIfApplicable`, `ProcessLogin` (up to `Task.Run`), `Ui.Post(() => Application.Instance.Quit())` |
| Background (login task) | `Login`, `TryLoginToGame`, `TryProcessLoginResult`, `StartGameAndAddon`, patching, repair, `PersistAccount`, `Reactivate`'s `State` write (marshalled via `Ui.Post` inside `LauncherApp.SetState`, `LauncherApp.cs.md` §4) |
| Blocking modals from background | `App.ShowMessageBlocking`, `App.ShowExceptionBlocking` pump a modal on the UI thread and release a `ManualResetEvent` (`LauncherApp.cs.md` §6), so background callers can block safely |
| Plain property writes from background | `App.LoadingPage.Line2/Line3/Progress` (977–981, 1089–1103): `Line*` are plain fields, `Progress` marshals `Refresh` |
| Direct widget writes from background | `App.StartLoading(...)` (154, 627, 760, 767, 962, 1079) writes `LoadingPage.Line1/Line2/Line3/IsIndeterminate/CanCancel/CanDisableAutoLogin` and calls `LoadingPage.Reset()`, whose `Refresh()` writes GTK labels/buttons/bar directly on the caller's thread (`LauncherApp.cs:261-275`) — see §15 |
| Environment mutation | `StartGameAndAddon` sets process-wide env vars (639–682, 684–716), which affects the launcher process itself, not only the child game |

## 15. Quirks

- **`StartLoading` writes widgets off the UI thread.** Every `StartLoading` call in this file happens inside the background login task (lines 154, 627, 760, 767, 962, 1079). `LauncherApp.StartLoading` (`LauncherApp.cs.md` §7) sets plain `LoadingPage` properties and then calls `Reset()` → `Refresh()`, writing GTK label/bar/button state on the caller's thread. `State`'s setter marshals, so page *switching* is safe; the label/bar writes are not. Eto's GTK handlers for `Label.Text`/`ProgressBar.Value` are not synchronized to `Gtk.Application`'s main thread.
- **`App.StopLoading` does not clear the loading page** (`LauncherApp.cs:274-278`): it only sets `State = Main`. `RepairGame` calls `App.StopLoading()` (line 1098) and patching's `finally` sets `State = Main` (line 1035), so a later `StartLoading` reuses stale `Progress`/`ProgressText` values until they are overwritten.
- **`RepairGame`'s `doLogin` is only true after a successful verify**, and `TryProcessLoginResult` treats `RepairGame == false` as a hard stop (`MainPage.cs:341-346`), so repair failure never reaches the launch path.
- **Wine elevation gate.** `IsRunningUnderWine()` (1179–1191) suppresses `XLElevatedWarning` (72–75): wine reports every process token as elevated, so the admin warning is spurious there. Detection: `EnvironmentSettings.IsWine` (`XL_WINEONLINUX` env var, upstream convention) or the `wine_get_version` export of `ntdll.dll`; the probe returns false on native Windows (`EntryPointNotFoundException` — native ntdll lacks the export) and on native Linux (`DllNotFoundException` — no `ntdll.dll` exists), so the gate must catch both.
- `RepairGame` uses `Program.Config.GamePath!`/`Program.Config.PatchPath!` (line 1075), not `App.Settings.*`, so the patch verifier follows the startup config, not any settings-page change made after startup (`Program.cs.md` §6).
- `Environment.Exit(0)` inside modal handlers (918, 943, 1044, 1050) exits the process from inside a modal dialog handler, bypassing `Application.Quit` and any Eto cleanup.
- `Debug.Assert` calls (119, 401, 926–930, 1069–1070, 1024) compile away in release: `Debug.Assert(loginResult.State == Ok)` (401) is a no-op in release, so the launch path runs with whatever `State` the server returned.
- `StartGameAndAddon` returns `null!` in three paths (765, 813, and implicitly via `Process is null` throw path) while its signature is non-nullable `Task<Process>`, so `TryProcessLoginResult` must null-check (409–410).
- The commented-out multibox/kill loop (lines 426–569) means no interactive error dialog exists for launch failures; all failures become exceptions, and `ProcessLogin`'s `ContinueWith` shows a modal via `App.HandleContinuationBlocking` (`LauncherApp.cs.md` §6).
- `doingAutoLogin` is an unused parameter (line 213), kept for API shape.
- `AddonManager` failures reset `IsLoggingIn` (828) but do not reset the loading page, so the UI can remain on `LoadingPage` while `IsLoggingIn` is false, permitting a new login while the old task's exception is being handled.
- `App.Steam.Shutdown()` (846) is called after the game exits, in `StartGameAndAddon`; Steam state is process-global (`Program.cs.md`).
- `SwitchAccount` writes `Password` only when `account.SavePassword` (134–135), so switching to a saved account without a stored password leaves whatever is in the password box.
- No `OnShow()` override means page entry performs no refresh; `LauncherApp.cs:80`'s `OnShow()` is the empty base (`Components/Page.cs.md` §3), and news refresh comes only from `LauncherApp.cs:69` when leaving settings.

## 16. Referenced / referenced-by

Uses: `Page` (`Components/Page.cs.md`), `LauncherApp` (`LauncherApp.cs.md`), `LoginFrame` (`LoginFrame.cs.md`), `NewsFrame` (`NewsFrame.cs.md`), `ActionButtons` (`ActionButtons.cs.md`), `AccountSwitcher` (`AccountSwitcher.cs.md`), `OtpEntryPage` via `App.AskForOtp` (`Components/OtpEntryPage.cs.md`), `LoadingPage` (`Components/LoadingPage/LoadingPage.cs.md`), `SteamDeckPromptPage` (`Components/SteamDeckPromptPage.cs.md`), `Ui` (`Ui.cs.md`), `Program` (`Program.cs.md`), `AppUtil` (`AppUtil.cs.md`), `Troubleshooting` (`Support/Troubleshooting.cs.md`), `AccountManager`/`XivAccount` (`Accounts/AccountManager.cs.md`), `Strings` (`Resources/Localization/Strings.Designer.cs.md`), and the `XIVLauncher.Common` game/patch/Dalamud/compatibility types in `lib/FFXIVQuickLauncher` (submodule — out of docs scope).
Referenced by: `LauncherApp.cs` (lines 12, 69, 80, 114, 131, 148, 284).
