# MainPage.cs — Specification

**File:** `src/XIVLauncher.Core/Components/MainPage/MainPage.cs` · 1149 lines · 45429 bytes
**Namespace:** `XIVLauncher.Core.Components.MainPage` · **Declaration:** `public class MainPage : Page`
**Role:** The launcher's whole "log in → patch → launch → wait for the game" machine, plus the MainPage's UI composition — the logic hub every `Components/` page feeds into

---

## 1. Purpose and the seam

`MainPage` is the concrete `Page` (`Page.cs.md`) that `LauncherApp.cs.md` keeps as `CurrentPage` and activates on `LauncherState.Main` (`LauncherApp.cs:71 this.mainPage.OnShow();`, constructed at `LauncherApp.cs:134 this.mainPage = new MainPage(this);`). It has two clearly separated halves in one file, and the specs of the other MainPage components exist mostly so this half can stay readable:

- **UI half** — ctor wiring (`:36-62`) and `Draw()` (`:74-88`) compose `LoginFrame.cs.md`, `NewsFrame.cs.md`, `AccountSwitcher.cs.md`, `ActionButtons.cs.md`, and translate clicks into `LoginAction.cs.md` values.
- **Logic half** — `ProcessLogin` (`:114`) → `Login` (`:174`) → `TryLoginToGame` (`:219`) → `TryProcessLoginResult` (`:259`) → `StartGameAndAddon` (`:538`), with the patch (`:845-1025`) and repair (`:1027`) machinery and the exit/`Hide`/`Reactivate` plumbing (`:1137-1148`).

The seam is one event: `MainPage.cs:47 this.loginFrame.OnLogin += this.ProcessLogin;`. Everything else in the logic half is reached from that or from `LauncherApp.cs:256 this.mainPage.DoAutoLoginIfApplicable();` (inside `RunStartupTasks`, `LauncherApp.cs:251`, right after the `#if FLATPAK` Fts hook at `:254`).

## 2. Surface

| Member | Anchor | Kind | Notes |
| --- | --- | --- | --- |
| `loginFrame` / `newsFrame` / `actionButtons` | `:30/:31/:32` | `private readonly` fields | children; `AccountSwitcher` is the odd one out (§3) |
| `IsLoggingIn` | `:34` | `public bool { get; private set; }` | the re-entry gate, §5 |
| ctor | `:36-62` | `public MainPage(LauncherApp app) : base(app)` | `base(app)` is `Page`'s protected ctor (`Page.cs.md`) |
| `AccountSwitcher` | `:64` | `public AccountSwitcher { get; private set; }` | accessor field, not `readonly` |
| `DoAutoLoginIfApplicable()` | `:66` | `public void` | called by `LauncherApp.cs:256` |
| `Draw()` | `:74-88` | `override` | §4 |
| `ReloadNews()` | `:90` | `public void` (expression body) | forwarder to `NewsFrame.cs.md`, called by `LauncherApp.cs:59` |
| `SwitchAccount(account, saveAsCurrent)` | `:92-107` | `private` | writes the six `LoginFrame.cs.md` accessors |
| `AccountSwitcherOnAccountChanged(sender, e)` | `:109` | `private` | the `AccountChanged` handler bound at `:45` |
| `ProcessLogin(LoginAction)` | `:114-172` | `private void` | §5 |
| `Login(…, doingAutoLogin, action)` | `:174-217` | `public async Task<bool>` | §6 — public so a second caller could drive login directly |
| `TryLoginToGame(…)` | `:219-257` | `private async Task<Launcher.LoginResult>` | §7 |
| `TryProcessLoginResult(result, isSteam, action)` | `:259-536` | `private async Task<bool>` | §8 |
| `StartGameAndAddon(result, isSteam, forceNoDalamud, noPlugins, noThird)` | `:538-815` | `public async Task<Process>` | §9 — returns `null!` on two paths (§9) |
| `PersistAccount(…)` | `:817-843` | `private` | writes `App.Accounts`, `docs/XIVLauncher.Core/Accounts/AccountManager.cs.md` |
| `HandleBootCheck()` | `:845-882` | `private async Task<bool>` | §10 |
| `InstallGamePatch(loginResult)` | `:884-892` | `private Task<bool>` (non-async) | `Debug.Assert`s |
| `TryHandlePatchAsync(repository, patches, sid)` | `:894-1013` | `private async Task<bool>` | §10 |
| `PatcherOnFail(patch, context)` / `InstallerOnFail()` | `:1015` / `:1021` | `private void` | both end in `Environment.Exit(0)` |
| `RepairGame(loginResult)` | `:1027-1135` | `private async Task<bool>` | §10 |
| `Hide()` | `:1137` | `private` | `Program.HideWindow()` (`docs/XIVLauncher.Core/Program.cs.md`) |
| `Reactivate()` | `:1142-1148` | `private` | clears `IsLoggingIn`, `App.State = Main`, `Program.ShowWindow()` |

Usings (`:1-24`): `System.Diagnostics`, `System.Numerics`, `Hexa.NET.ImGui`, `Hexa.NET.SDL3`, `Serilog`, and the library namespaces `XIVLauncher.Common` (+ `.Addon`, `.Dalamud`, `.Game`, `.Game.Exceptions`, `.Game.Patch`, `.Game.Patch.Acquisition.Aria`, `.Game.Patch.PatchList`, `.PlatformAbstractions`, `.Unix`, `.Unix.Compatibility.Wine`, `.Util`, `.Windows`), `XIVLauncher.Core.Accounts`, `XIVLauncher.Core.Resources.Localization`, `XIVLauncher.Core.Support`. It is the most import-heavy file in `Components/` — a sign that the *logic* half pulls the whole launcher vocabulary into the same class that draws the UI. Keep new logic in this class only if that vocabulary is already imported; new UI belongs in the MainPage children (§3).

## 3. Construction (`:36-62`) — what is wired and why

```
:39 loginFrame     = new LoginFrame(this);            // takes the OWNING MainPage
:40 newsFrame      = new NewsFrame(app);              // takes the LauncherApp  ← asymmetry, see NewsFrame.cs.md §3
:42 actionButtons  = new ActionButtons();             // takes nothing
:44 AccountSwitcher = new AccountSwitcher(app.Accounts);
:45 AccountSwitcher.AccountChanged  += this.AccountSwitcherOnAccountChanged;
:47 loginFrame.OnLogin              += this.ProcessLogin;
:48/:49/:50 actionButtons.{Settings,Status,Account}  += handlers
:52 this.Padding = new Vector2(32f, 32f);             // the ONLY Padding writer in the package
:54/:56 savedAccount restore
:58/:59 PlatformHelpers.IsElevated() warning
:61 Troubleshooting.LogTroubleshooting();
```

- **`IsElevated` warning (`:58-59`)** runs at construction, i.e. at `LauncherApp.cs:134` (inside `LauncherApp`'s ctor, before the first frame) — a blocking-free `App.ShowMessage` on a window that does not exist yet; see `docs/XIVLauncher.Core/LauncherApp.cs.md` §show-message for how messages route. Keep this call in the ctor (it reports "you are root / elevated", which must be reported before any game-side work).
- **`Troubleshooting.LogTroubleshooting()` is called twice**, here (`:61`) and again at `MainPage.cs:561` inside `StartGameAndAddon`: once for "app started" and once for "about to launch the game". It is not redundant; the second call captures the post-settings, pre-launch environment (see `docs/XIVLauncher.Core/Support/Troubleshooting.cs.md`).
- **`Padding` (`:52`)** is the `init`-only accessor of `Page.cs.md` §5 — legal *only* in the ctor body, and its sole consumer is `LauncherApp.Draw()`'s `PushStyleVar(WindowPadding, this.CurrentPage.Padding ?? …)`. Do not move this assignment into `Draw()`; it will not compile against `init`, and `LauncherApp.cs.md` §8 reads it every frame.
- **The saved-account restore (`:54-56`)** pre-fills the login form from `App.Accounts.CurrentAccount` before the first frame — it calls `SwitchAccount(…, saveAsCurrent: false)`, i.e. restore *without* re-recording `CurrentAccount` (contrast `:109`'s handler, which passes `true`). That boolean is the class's only "did the user choose this?" distinction; keep it that way when touching `docs/XIVLauncher.Core/Accounts/AccountManager.cs.md` §`CurrentAccount`.
- **`AccountSwitcher` is not `readonly`** (`:64` accessor with `private set;`) while the other three children are `private readonly` fields — today nothing reassigns it, but the accessor makes it *readable* publicly (`MainPage/LoginFrame.cs.md` reaches it through `mainPage.AccountSwitcher.Open()`). If a new child needs public access, follow the accessor form, not a public field.
- **Handler placement convention:** event handlers live in this ctor (`:45`, `:47`, `:48-:50`) rather than in the children — `ActionButtons.cs.md` §6 and `MainPage/AccountSwitcher.cs.md` §5 both rely on it; `LoginFrame.cs.md` is the exception that handles its own popup items inline (§5 of that spec).

## 4. `Draw()` (`:74-88`)

```csharp
public override void Draw()
{
    base.Draw();                                              // :76 — Page/Component child loop FIRST
    ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(32f, 32f));
    this.newsFrame.Draw();                                    // :79
    ImGui.SameLine();                                         // :81
    this.loginFrame.Draw();                                   // :83
    this.AccountSwitcher.Draw();                              // :84
    this.actionButtons.Draw();                                // :86
    ImGui.PopStyleVar();
}
```

- **`base.Draw()` is first, not last** — unlike every leaf/page override in this package (see `Component.cs.md` §Draw chain: `Button`/`Checkbox`/`Input` skip it, most pages call it last). Here it matters: `Page` inherits `Component.Draw` (`Page.cs.md`/`Component.cs.md` §children), so the (empty-by-default) `Children` loop paints *before* the frames. Keep the first position: `AccountSwitcher.cs.md` §3's popup anchor rule and `NewsFrame.cs.md` §7's width arithmetic both depend on child-window creation happening in a predictable order within a frame.
- **One `PushStyleVar(WindowPadding, 32)` wraps all four children.** `LoginFrame.cs.md` §6 and `NewsFrame.cs.md` §7 push their *own* padding inside their child windows; this push applies to the shared content window (`LauncherApp.cs.md` §8). The value `32` duplicates `:52`'s `Padding` (`Page.cs.md` §5) — two places encode the same inset, which is why the file says the same number twice; changing one without the other produces a doubled/again-default inset. Treat `{`:52`, `:78`}` as a pair to edit together.
- **Order is load-bearing.** `newsFrame` before `loginFrame` and the `ImGui.SameLine()` between them define the two-column layout both `NewsFrame.cs.md` §7 (fractional width) and `LoginFrame.cs.md` §6 (`-1` = "rest of the width") describe. `AccountSwitcher.Draw()` immediately after `loginFrame.Draw()` is the frame-ordering contract recorded in `MainPage/AccountSwitcher.cs.md` §3 (`doOpen` consumed same frame) and `LoginFrame.cs.md` §7 (the friends icon calls `Open()` during `loginFrame.Draw()`).
- **`actionButtons.Draw()` last, after `AccountSwitcher`** puts the icon strip in the same row flow (no `SameLine`/`NewLine` between them), i.e. `ActionButtons.cs.md` §4's row continues on the login frame's row; `AccountSwitcher.Draw()` adds no visible items outside its popup, which is what keeps that row flush.

## 5. `ProcessLogin` (`:114`) and the re-entry gate

```
:119 this.App.StartLoading(Strings.LoggingIn, canDisableAutoLogin: true);
     Task.Run(async () => {
:126     if (GameHelpers.CheckIsGameOpen() && action == LoginAction.Repair) → ShowMessageBlocking + Reactivate() + return
:137     if (Repository.Ffxiv.GetVer(GamePath) == Constants.BASE_GAME_VERSION && IsUidCacheEnabled == true) → ShowMessageBlocking + Reactivate() + return
:145     IsLoggingIn = true;
:147     App.Settings.IsAutologin = this.loginFrame.IsAutoLogin;
:149     result = await Login(username, password, isOtp, isSteam, isFreeTrial, /*doingAutoLogin*/ false, action)
         if (result) push SDL Quit event (:153-:157) else Log + Reactivate()
     }).ContinueWith(t => { if (!App.HandleContinuationBlocking(t)) this.Reactivate(); });   // :169/:170
```

- **The gate is checked at `:115-117` (`if (IsLoggingIn) return;`) but set at `:145`, inside the task.** So it prevents *cross-frame* re-entry (a second click while a login runs) but cannot prevent two submissions that both pass the guard before the task reaches `:145`. `LoginFrame.cs.md` §4's three triggers plus `DoAutoLoginIfApplicable` (`:66`) are the entry points; do not assume the guard is a mutex.
- **`StartLoading` (`:119`) runs synchronously before the task starts**, and `canDisableAutoLogin: true` is what makes `LoadingPage.cs.md` draw its "disable auto-login" button during login (`LoadingPage.cs.md` §5's `isDrawDisableAutoLogin`). The other loading calls in this file use different flags (§10) — the flags are per-call, not per-page state.
- **The body is fire-and-forget:** `Task.Run(...).ContinueWith(...)` — `ProcessLogin` returns `void`, so `MainPage/LoginFrame.cs.md`'s click handlers all return immediately and the whole login runs off the draw loop. The only failure reporting is `ContinueWith` → `App.HandleContinuationBlocking(t)` (`LauncherApp.cs:203`, `docs/XIVLauncher.Core/LauncherApp.cs.md`: returns `false` on `IsFaulted`/`IsCanceled` after showing a blocking message), and `Reactivate()` when it returns `false`. Note `Reactivate()` is also reached with `this.` at `:141`/`:165`/`:170` and bare at `:130` — cosmetic, but keep both spellings if editing nearby lines with an exact-match tool.
- **`Login() != true` means "not an error"** — the `else` branch at `:164-166` logs `"Reactivated after Login() != true"` and reactivates the window, because `GameNoLaunch` deliberately returns `false` (`LoginAction.cs.md` §2 row for `GameNoLaunch`, produced at `:333`). Do not treat `false` as failure when changing `Login`'s return contract.
- **The SDL Quit push (`:153-157`) on success** (`SDLEvent { Type = (int)SDLEventType.Quit }`, `SDL.PushEvent(ref sdlEvent)`) is the *self*-termination mechanism: `docs/XIVLauncher.Core/ImGuiBindings.cs.md` §`ProcessExit` (`ImGuiBindings.cs:143-156`) polls SDL events and returns `true` on `Quit`, which ends `Program.cs`'s main loop (`:326-336`) and runs `SDL.Quit()` (`:340`) plus the `Patcher.StartCancellation()` path (`Program.cs:343-360`). `Login() == true` implies the game already exited (the launch path waits, §9), so "the game is gone → close the launcher" is exactly this block. Keep the `ref` argument (the binding writes into the struct) and keep the `Log.Error` on push failure — a failed push leaves the launcher window alive after the game exits.
- **`MainPage.cs:147` writes persisted config from inside the login task** (`App.Settings.IsAutologin = this.loginFrame.IsAutoLogin;`). This is the counterpart of `LoadingPage.cs.md` §3's `App.Settings.IsAutologin = false` and of `Page.cs.md`-adjacent `SwitchAccount`'s `MainPage.cs:98` read — `LoginFrame.cs.md` §8 records the two-truths invariant across those three sites; any change here must be reflected in that spec.
- **The commented-out guard at `:121-122`** (`// if (Program.UsesFallbackSteamAppId && this.loginFrame.IsSteam) throw …`) is a *deliberate* removal: the fallback Steam app-id path exists (`docs/XIVLauncher.Core/Program.cs.md`), so this line documents a decision, not dead code to delete. Keep it commented or restore it intentionally, per `FtsPage.cs.md` §error-mode discussion.

## 6. `Login` (`:174-217`) — the pipeline order is the contract

| Step | Anchor | What | On failure |
| --- | --- | --- | --- |
| `Fake` action | `:176-:188` | builds a throwaway `WindowsGameRunner(null, false)` / `UnixGameRunner(Program.CompatibilityTools, null, false)` and calls `App.Launcher.LaunchGame(…, "0", 1, 2, false, "", GamePath!, ClientLanguage.Japanese, true, DpiAwareness.Unaware)`, then `return false` | debug-only (produced by `LoginFrame.cs.md` §5's `LauncherApp.IsDebug` menu item) — the `FIXME` comment about the `null` `DalamudLauncher` is the file's own note, keep it |
| Boot check | `:190` `HandleBootCheck()` | boot patches (§10) | `return false` at `:194` |
| UID-cache reset | `:198` `Program.ResetUIDCache()` | only when `isOtp && HasValidCache(username) && IsUidCacheEnabled == false` | — |
| OTP | `:202-:206` | `App.AskForOtp()` then `otp = await App.WaitForOtpAsync()` | `otp == null → return false` at `:210-:212` (user cancelled) |
| (state) | `:206` `App.State = LauncherApp.LauncherState.Loading` | "make sure we are loading again" after OTP returns | — |
| Persist | `:215` `PersistAccount(…)` | before the login attempt | — |
| Login | `:218` `TryLoginToGame(username, password, otp, isSteam, isFreeTrial, action)` | discards `doingAutoLogin` — **the parameter is never used in the body** | — |
| Process | `:219`… | `TryProcessLoginResult(loginResult, isSteam, action)` | §8 |

- **The OTP await happens on the *task*, not on the draw loop**: `App.AskForOtp()` (`LauncherApp.cs:220`) sets `LauncherState.OtpEntry` (`OtpEntryPage.cs.md`) and `await App.WaitForOtpAsync()` (`LauncherApp.cs:226`, which also calls `StopOtpListener()` unconditionally) blocks the login task until the page's `TaskCompletionSource` yields. That is the `OtpEntryPage.cs.md` §"TaskCompletionSource completion protocol" contract; the three outcomes (6-digit string, `null`, hang) map to this file's `otp == null → return false` and to a permanently parked login task in the hang case.
- **`doingAutoLogin` is an unused parameter** (`:174` declares it, nothing reads it; the only call passes `false` at `:149`). It is the placeholder for "auto-login needs different UX during OTP", i.e. the parameter is the *intent*, `MainPage.cs:149` is the site to change. Do not silently drop it from the signature: `Login` is `public`, so external callers (and future auto-login code) name arguments positionally.
- **`Program.ResetUIDCache()` (`:198`) is gated by `IsUidCacheEnabled == false`** — note the `== false` (not `!…`): a `null`/unset flag means "enabled", matching `MainPage.cs:137`'s `IsUidCacheEnabled == true` test and `LauncherApp.cs.md` §uid-cache defaults. Keep the three-way comparison, since `App.Settings` flags are nullable.
- **`PersistAccount` runs *before* `TryLoginToGame`** (`:215` before `:218`): a failed login still stores the account/password it tried. That ordering is deliberate (it is what makes "remember the account you couldn't log into" work) and is the reason `docs/XIVLauncher.Core/Accounts/AccountManager.cs.md` §`AddAccount`/`UpdatePassword` must stay idempotent for the retry case.

## 7. `TryLoginToGame` (`:219-257`)

- **The `#if !DEBUG` gate block (`:221-:238`) handles only `gateStatus == null`** (`:236` "Login servers could not be reached or maintenance is in progress…"), i.e. a `gateStatus == false` (maintenance) *falls through and logs in*. Compare §8's second gate block (`:338-:362`), which **does** handle `false` (`:357` "Maintenance is in progress."). The asymmetry is real: pre-login gate unknown → abort; gate explicitly down → abort only later. Preserve both blocks when editing, and do not "unify" them by adding a `false` case to the first without checking that the second still runs (`:259` `TryProcessLoginResult` runs only after a successful `Login`, so the two gates cover different phases: the first blocks an unreachable server *before* spending an OTP round-trip).
- **The repair call differs only by two `Login` arguments** (`:247`): `Login(username, password, otp, isSteam, /*isPatchBans*/ false, gamePath, /*repair*/ true, isFreeTrial, language)` vs the normal `Login(…, enableUidCache, gamePath, false, …)`. The 6th/7th booleans are "use UID cache" and "this is a repair" in the library's signature (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Game/Launcher.cs`); keep them paired if the signature changes.
- **`isFreeTrial` is threaded through but not `forceNoDalamud`**: the "no Dalamud" variants affect §9 only.
- **`catch { Log.Error; throw; }`** (`:253-:256`): rethrows so §5's `ContinueWith` reports it. Do not swallow here — swallowing yields a silently-stuck launcher (the `IsFaulted` → `Reactivate` path is what returns the user to the Main page).
- **`#if !DEBUG` means release-only**: debug builds skip the gate entirely, which is why the string is an English literal and not a `Strings` key (`LoginAction.cs.md` §2 records the literal-string convention for debug text). Same for §8's block.

## 8. `TryProcessLoginResult` (`:259-536`) — result mapping and the exception-wrap trick

- **The `preErrorMsg`/`postErrorMsg` pair (`:261-:262`)** wraps a localized message into the exact shape `OauthLoginException` expects: `"window.external.user(\"login=auth,ng,err,"` + message + `"\");"`. The library parses that JS-ish form (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Game/Exceptions/`), so the wrapping is a *protocol*, not cosmetics: three sites use it (`:270` `NoService`, `:276` `NoTerms`, `:300` `NeedsPatchBoot` with a non-localized `"Boot conflict, need reinstall"`, `:344` repair-state mismatch). Keep all sites wrapped, or the exception renders as a bare message and loses the "login error" presentation.
- **`LoginState.NeedsPatchBoot` here is a fatal inconsistency** (`:293-:300`, and the file's long comment above it): boot patches were already applied by `HandleBootCheck` (§6, `:190`), so a second boot-patch request means out-of-band modification, and the only exit is the throw. Keep the comment if the code moves.
- **Repair path (`:321-:340`)**: requires `NeedsPatchGame` (else throws the wrapped `OauthLoginException` at `:344`), calls `RepairGame(loginResult)` (§10), and on success **mutates `loginResult.State = LoginState.Ok`** (`:335`) so the shared code below continues to launch. Same mutation appears after the game-patch branch (`:410`). The mutation, not a new flag, is what threads "we are now good to launch" through `while (true)`; keep it that way or change both sites.
- **`GameNoLaunch` (`:331`)** shows `Strings.UpdateCheckFinished` and `return false` *after* the patches have run — i.e. "patched, don't launch", and the `false` is handled as a normal return (§5).
- **The second `#if !DEBUG` gate switch (`:338-:362`)** runs *after* patching and before launching, with both `null` (`:354`) and `false` (`:357`) aborting.
- **`Debug.Assert(loginResult.State == LoginState.Ok);` (`:361`) then `while (true)` (`:364`)**: the loop exists to host a retry menu (the huge commented-out `/* … */` block at `:381-:534` is the Windows variant's multi-exception handler with Yes/No/Cancel semantics and the "kill then retry" branch — `:420`'s `LaunchGameKillThenRetry` string is inside that comment). The live `catch` (`:378-:380`) logs and **rethrows**, so the loop cannot currently iterate; the `NOTE(goat)` line at `:381` ("This HAS to handle all possible exceptions from StartGameAndAddon!!!!!") is the file's own reminder that the loop is a placeholder for that commented block. Consequences: keep `while (true)` only if you also keep a non-`throw` path inside `catch`, otherwise the loop is dead-ish scaffolding that reviewers read as a TODO. Deleting the comment block is a decision to record in this spec, not one to take silently.
- **`process.ExitCode != 0 && (App.Settings.TreatNonZeroExitCodeAsFailure ?? false)` (`:373`)** throws `InvalidOperationException("Game exited with non-zero exit code")` — i.e. by default a non-zero game exit code is **success** (`Login() == true` → SDL Quit push at §5). The `?? false` default is the documented behaviour; `SettingsPage/SettingsTabTroubleshooting.cs.md` (or `SettingsTabGame.cs.md`, whichever exposes it — see that spec's settings-entry table) is the user-facing lever.
- **`using var process = await StartGameAndAddon(...)` (`:368`)** is a C# `using` declaration: the `Process` handle is disposed at the end of the loop body, so the launcher never leaves a dangling process handle even on the throw paths. Keep the `using` form if the loop is restructured — dropping it leaks the handle that `docs/XIVLauncher.Core/Program.cs.md`'s shutdown cannot see.
- **`StartGameAndAddon(... , action == LoginAction.GameNoDalamud, action == LoginAction.GameNoPlugins, action == LoginAction.GameNoThirdparty)` (`:368`)** is where three enum values become three booleans. `LoginAction.cs.md` §2 is the cross-reference; keep the mapping positional-correct (`forceNoDalamud, noPlugins, noThird`, §9).

## 9. `StartGameAndAddon` (`:538-815`) — launch, environment, and the wait

| Region | Anchor | What / invariant |
| --- | --- | --- |
| Runner selection | `:545-:556` | `PlatformID.Win32NT` → `WindowsDalamudRunner(Program.DalamudUpdater.Runtime)` + `WindowsDalamudCompatibilityCheck`; `Unix` → `UnixDalamudRunner(Program.CompatibilityTools, Program.DotnetRuntime)` + `UnixDalamudCompatibilityCheck`; `default: throw new NotImplementedException()` — the platform switch has **no third case**, so macOS/etc. aborts from inside the launch path |
| Troubleshooting | `:561` | second `LogTroubleshooting()` call (§3) |
| `DalamudLauncher` ctor | `:564-:571` | arguments map `App.Settings.DalamudLoadMethod ?? DllInject`, `GamePath`, `App.Storage.Root`, `logs` folder, `ClientLanguage ?? English`, `DalamudLoadDelay`, `false`, `noPlugins`, `noThird`, `Troubleshooting.GetTroubleshootingJson()` — the last is the crash-report channel, see `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Dalamud/DalamudLauncher.cs` |
| Compat check | `:574-:583` | `dalamudCompatCheck.EnsureCompatibility()`, rethrown for the two named cases (`NoRedistsException`, `ArchitectureNotSupportedException`) |
| Dalamud wait | `:585-:595` | only `if (App.Settings.DalamudEnabled ?? true) && !forceNoDalamud`: `App.StartLoading(Strings.WaitingForDanshimud…)` (`:587 StartLoading(Strings.WaitingForDalamud, Strings.PleaseBePatient)`) then `dalamudOk = dalamudLauncher.HoldForUpdate(GamePath) == DalamudInstallState.Ok` — the flag pair here is (§8's `forceNoDalamud`, `App.Settings.DalamudEnabled`), the value feeds the runner ctor below |
| Env hacks | `:599-:643` | order is load-bearing: XL_PRELOAD→LD_PRELOAD (`:601-:608`) **before** the `FixLDP` strip (`:618`), whose comment at `:599` says "This check must be done before the FixLDP check so that it will still work"; then `FixLocale` (`:611`, `LC_ALL`/`LC_CTYPE` = `Program.CType`), `FixLDP` (`:618`, strips `gameoverlayrenderer.so` via `CoreEnvironmentSettings.GetCleanEnvironmentVariable`), `FixIM` (`:625`, `XMODIFIERS=@im=null`), `FixError127` (`:631`, `DOTNET_SYSTEM_GLOBALIZATION_USENLS=true`), `DontUseSystemTz` (`:639`, `TZ=""` on Unix only). Every flag is `== true` (three-way, nullable), and all of them are process-global `System.Environment` mutations — they persist for the rest of the launcher's life, not just the game |
| `AdditionalArgs` | `:645-:673` | `%command%` split (Steam-Style): no `%command%` → leading `-` means game args, otherwise env; with `%command%` → left = env, right = args; env tokens need `=`, and a `KEY+=value` form appends to the existing value (`:668-:671`). This is the Steam-deck-style overlay syntax — `SettingsPage/SettingsTabGame.cs.md` (see that spec's `AdditionalArgs` row) is the UI side |
| Wine validation | `:684-:696` | `WineStartupType.Custom` requires `WineBinaryPath` to exist *and* contain `wine64`; three `throw new InvalidOperationException` messages are user-facing prose (not `Strings` keys) — they surface through §5's `ContinueWith` (`LauncherApp.cs:203`), which is why they read like instructions |
| Compat tool | `:703-:728` | `ManualResetEvent` + `Task.Run(async () => CompatibilityTools.EnsureTool(HttpClient, tempPath))` + `.ContinueWith` setting `isFailed`, then `App.StartLoading(Strings.PreparingForCompatTool, Strings.PleaseBePatient)` (`:720`) **after** starting the task, `signal.WaitOne()` (`:721`), `signal.Dispose()` (`:722`), `if (isFailed) return null!;` (`:725`), then `App.StartLoading(Strings.StartingGame, Strings.HaveFun)` (`:727`) |
| Runner | `:680` / `:729` | `WindowsGameRunner(dalamudLauncher, dalamudOk)` / `UnixGameRunner(Program.CompatibilityTools, dalamudLauncher, dalamudOk)` — note the Unix runner also takes `CompatibilityTools` |
| `UserPath` | `:734-:739` | appended to `gameArgs`; quoted only when `IsEncryptArgs ?? true` is *false* — the file's comment explains SE re-encodes spaces. `App.Settings.GameConfigPath!` unwrap, so the `null`-path case throws |
| `LaunchGame` | `:747-:757` | `App.Launcher.LaunchGame(runner, loginResult.UniqueId, loginResult.OauthLogin.Region, loginResult.OauthLogin.MaxExpansion, isSteam, gameArgs, GamePath!, ClientLanguage ?? English, IsEncryptArgs ?? true, DpiAwareness ?? Unaware)` |
| Window state | `:762` / `:766` | `!Program.IsSteamDeckHardware \|\| CoreEnvironmentSettings.IsSteamCompatTool` → `Hide()` (`Program.HideWindow()`, `docs/XIVLauncher.Core/Program.cs.md`; `Program.cs:326-330` then *pauses* the draw loop via `SDL.Delay(10); continue;` on `Minimized\|Hidden` flags); else `App.State = LauncherApp.LauncherState.SteamDeckPrompt` — the only writer of that state, see `SteamDeckPromptPage.cs.md`. This is **before** the process wait, so the launcher either stops drawing or shows the prompt page while the game runs |
| Null handle | `:771-:774` | `launchedProcess == null` → log, `IsLoggingIn = false`, `return null!` — the second of the two `null!` returns (the first is `:725`) |
| Addons | `:776-:791` | `AddonManager().RunAddons(launchedProcess.Id, App.Settings.Addons.Where(x => x.IsEnabled).Select(x => x.Addon).Cast<IAddon>())`; `catch (Exception) { IsLoggingIn = false; addonMgr.StopAddons(); throw; }` — `Addons ??= new List<AddonEntry>()` at `:779` first |
| Wait | `:794` | `await Task.Run(() => launchedProcess!.WaitForExit())` — the launch path *blocks until the game exits*, which is what makes §5's SDL-Quit push mean "game gone" |
| Cleanup | `:799-:812` | `if (addonMgr.IsRunning) addonMgr.StopAddons();` then `App.Steam?.Shutdown()` guarded by `App.Steam?.IsValid == true`, wrapped in `catch` that logs "Could not shut down Steam" (`:806`) |
| Return | `:814` | `return launchedProcess!;` |

**`StartGameAndAddon` returns `Task<Process>` but two paths return `null!`** (`:725`, `:773`), while `TryProcessLoginResult` immediately converts `process is null` into `throw new InvalidOperationException("Could not obtain Process Handle")` (`:370`). That is the file's convention: `null!` (assert-non-null type) to keep the signature honest-ish and let the *caller* decide the error presentation. Keep both sides if you change either.

## 10. Patch and repair machinery (`:845-:1135`)

- **`HandleBootCheck` (`:845-:882`)** is the first thing `Login` does (§6): it nulls a non-existent `App.Settings.PatchPath` (`App.Settings.PatchPath is { Exists: false }` — a C# 13 pattern on the accessor, `Program.cs.md` §patch-path), defaults it to `{Roaming}/patches`, calls `App.Launcher.CheckBootVersion(GamePath!)`, and with zero patches returns `true` immediately; otherwise it routes through `TryHandlePatchAsync(Repository.Boot, …, "")` (`:872`). A failed version check shows `Strings.CannotGetBootVerError` (`:875`) and returns `false`; the outer `catch` (`:876-:880`) shows `ShowExceptionBlocking(ex, "PatchBoot")` **then `Environment.Exit(0)` (`:878`)** — an exit from inside the login pipeline, i.e. the launcher kills itself rather than continue with an unusable patch state. That is one of four `Environment.Exit(0)` sites here: `:878`, `:903`, `:1018`, `:1024` (plus `FtsPage.cs.md`'s dead one). They bypass `Reactivate()` and the SDL path entirely.
- **`InstallGamePatch` (`:884`)** is the non-async `Task<bool>` wrapper asserting the caller's preconditions (`loginResult.State == NeedsPatchGame`, `PendingPatches != null`) before delegating — i.e. the assertions document the `TryProcessLoginResult` contract (§8).
- **`TryHandlePatchAsync` (`:894-:1013`)**: the `#if WIN32` mutex block (`:896-:905`, `"XivLauncherIsPatching"`) is **Windows-only** and the file's own `BUG(goat)` comment (`:894-:895`) says the mutex "doesn't seem to disappear on Linux, .NET issue", so on Unix there is no double-patch guard at all — the guard is `GameHelpers.CheckIsGameOpen()` (`:908`), which is *not* equivalent. Keep `#if WIN32` when editing, and keep `:902-:903`'s message-with-`Environment.Exit(0)` shape (its `return false;` after it is documented as unreachable in the source's own comment).
- **Patch progress is drawn through `LoadingPage.cs.md`'s fields** (`:951`/`:952`/`:955` inside the local `UpdatePatchStatus` function, `:941-:965`): a `new Thread(UpdatePatchStatus)` + `Thread.Sleep(30)` loop writing `App.LoadingPage.Line2/Line3/Progress`, cancelled by a `CancellationTokenSource` in the `finally` of the `PatchAsync` try (`:966 token.Cancel(); statusThread.Join(1000)`). `LoadingPage.cs.md` §5 and `LoadingPage/DalamudOverlayInfoProxy.cs.md` §4 both point here as an existing `Progress` writer — the pattern is "a thread writes the page's fields", and the `Thread.Sleep(30)` + `Join(1000)` pair is what bounds the lag. The same pattern recurs in `RepairGame` with `Timer` instead of `Thread` (§10 next bullet).
- **`Program.Patcher = new PatchManager(acquisition, PatchSpeedLimit, repository, pendingPatches, GamePath, PatchPath, installer, App.Launcher, sid)` (`:917`)** assigns the process-wide handle `docs/XIVLauncher.Core/Program.cs.md` uses for its shutdown `Patcher.StartCancellation()` — so `Program.Patcher` must stay non-null across the patch run; `using var installer`/`using var acquisition` (`:912-:913`, `AriaPatchAcquisition` logging to `{Storage}/logs/aria2.log`) dispose the downloader with the method scope.
- **`finally { App.State = LauncherApp.LauncherState.Main; }` (`:1009`)** — the page comes back to Main whether the patch succeeded, threw `PatchInstallerException`, `NotEnoughSpaceException` (three `SpaceKind` cases, `:973-:1000` with `NotEnoughSpacePatchesError`/`AllPatchesError`/`GameError`), or hit the generic `catch` (`:1005 ShowExceptionBlocking`). Keep it in `finally`, not on the success path.
- **`RepairGame` (`:1027-:1135`)**: `#if WIN32` mutex again (`:1029-:1038`), `using var verify = new PatchVerifier(Program.HttpClient, Program.Config.GamePath!, Program.Config.PatchPath!, loginResult, 100ms, MaxExpansion, false)` (`:1049`), a `doVerify` loop (`:1052`) that starts the verifier under a `Timer` callback writing `LoadingPage.Line2/Line3/Progress` (`:1057-:1082`, `timer.Change(0, 250)`), waits (`:1084 verify.WaitForCompletion()`), disposes the timer (`:1085`), and **`this.App.StopLoading()` (`:1086`)** — `StopLoading` sets `App.State = Main` (`LauncherApp.cs:246`), i.e. the repair loop returns the page per iteration; then the `verify.State` switch maps `Done` (`:1090-:1110`, summary text built from `verify.NumBrokenFiles` and `verify.MovedFiles`/`MovedFileToDir` via a `switch` expression + `ShowMessageBlocking`), `Error` (`:1112`, `NoVersionReferenceException` special-cased with `Strings.RepairGameVerUnsupportedError`), `Cancelled` (`:1127-:1128` clearing both `doLogin` and `doVerify`). It returns **`doLogin`** (`:1134`), and `TryProcessLoginResult` treats `false` as "stop, don't launch" (`:330-:332`). The `MovedFiles` reporting exists to tell the user where quarantined files went, and the "TODO: show more progress info here" and "TODO: ask the user if they want to login or rerun after repair" markers are the file's own unfinished UX.
- **`PatcherOnFail` (`:1015`) and `InstallerOnFail` (`:1021`)** are bound at `:921`/`:922` (`Program.Patcher.OnFail += PatcherOnFail`, `installer.OnFail += this.InstallerOnFail`) and both end in `Environment.Exit(0)` — the file's rule is "an async failure callback cannot resume the pipeline, so exit". Keep that shape for new failure handlers; the alternative (`Reactivate()`) leaves the launcher running and is used only where the user *can* retry (§5, §11).

## 11. Exit and recovery

```csharp
private void Hide()      { Program.HideWindow(); }                                   // :1137
private void Reactivate(){ IsLoggingIn = false; this.App.State = LauncherApp.LauncherState.Main; Program.ShowWindow(); }   // :1142
```

- **`Hide()`/`ShowWindow()` are SDL window operations** (`docs/XIVLauncher.Core/Program.cs.md` §`ShowWindow`/`HideWindow`, `Program.cs:374`/`:382`, and the `Minimized|Hidden` guard in the main loop at `Program.cs:326`) — "hidden" is not "closed": the frame loop keeps spinning with `SDL.Delay(10)`, which is what keeps the launcher quiet while the game runs (§9, `:762`).
- **`Reactivate()` is the only recovery path** and is reached from `:130`, `:141`, `:165`, `:170` (all inside `ProcessLogin`'s task) and from `Hide()`'s counterpart on the deck branch (`SteamDeckPromptPage.cs.md` — that page never calls back, so recovery there goes through `LauncherApp.cs.md` §`State`). It clears `IsLoggingIn` (the gate, §5) **and** the page, so a user can retry. Keep both statements: clearing the gate without the page leaves a live-but-hidden window; clearing the page without the gate re-enters `ProcessLogin` and immediately returns at `:115`.
- **Three different "back to Main" mechanisms coexist here**: `Reactivate()` (page + gate + SDL show), `App.StopLoading()` (`:1086`, page only, via `LauncherApp.cs:246`), and `finally { App.State = Main; }` (`:1009`, page only). Pick by whether the gate must be cleared — patch/repair paths deliberately leave `IsLoggingIn` set so a second login cannot start behind them.

## 12. Invariants and extension points

- **Two halves in one class** is the file's structural risk: adding UI to this class (rather than to the MainPage children) drags library vocabulary into a `Page` and breaks the composition recorded in §3/§4. Adding *logic* to this class is the intended place (there is no separate "launcher core" component here).
- **`Login` and `StartGameAndAddon` are `public`** despite having no external callers in `src/` (only `LauncherApp.cs.md` reaches this class through `DoAutoLoginIfApplicable`, `ReloadNews`, `OnShow`, `Draw`). Treat the visibility as the extension hook for a future "headless launch" path; do not widen `ProcessLogin`'s visibility, which is the click-side entry and is already reachable through `loginFrame.OnLogin` (`MainPage/LoginFrame.cs.md` §5).
- **Every `App.Settings.*` read here is nullable and most use `== true` / `?? false` / `GetValueOrDefault(true)`** inconsistently: `FixLocale == true`, `DontUseSystemTz == true`, `IsUidCacheEnabled == true`, `IsEncryptArgs.GetValueOrDefault(true)`, `TreatNonZeroExitCodeAsFailure ?? false`, `DalamanudEnabled.GetValueOrDefault(true)`. The defaults differ per flag (some default *on*), and `Program.cs.md` §defaults / `docs/XIVLauncher.Core/Configuration/ILauncherConfig.cs` are the source of truth. Do not "normalize" the three-way tests: a flag that defaults to true must use `!= false`, not `== true`. Cross-reference `SettingsPage/Tabs/SettingsTabGame.cs.md` and `SettingsTabTroubleshooting.cs.md` for the user-facing side of these flags.
- **`Environment.Exit(0)` inside the pipeline (`:878`, `:903`, `:1018`, `:1024`) skips `Program.cs`'s shutdown tail only partially**: `Program.cs:343-360` still runs `Patcher.StartCancellation()` when `Patcher is not null`, but the ImGui frame loop's `guiBindings.Dispose()` (`Program.cs:335`) is skipped because `Environment.Exit` never returns. Keep the exits only where the source already has them; every other error path in this file uses `throw` → §5's `ContinueWith` or `ShowMessageBlocking` + `Reactivate()`.
- **`#if` regions** in this file are `{!DEBUG}` (twice: `:221`, `:338`) and `{WIN32}` (twice: `:896`, `:1029`) plus `{FLATPAK}` (`FtsPage.cs.md` §"OpenFtsIfNeeded" is reached from `LauncherApp.cs:254`, not here). Any new build-gated behaviour should follow the same placement rule (the *reason* is documented in the file's own comments — e.g. the mutex's `BUG(goat)` line), and the region contents must stay compileable in the other configuration because CI builds both.
- **`while (true)` at `:364`** has exactly one `return` and one `throw` in its live body, so today it never iterates; if the retry-menu block (`:381-:534`) is restored, the `continue`/`return false`/`Cancel`-kill branches it describes must be re-implemented against this file's `Process`-based flow, not the Windows variant's message-box API.

## 13. Referenced / referenced-by

Constructed by: `LauncherApp.cs:134`. Activated by: `LauncherApp.cs:71` (`OnShow`), `LauncherApp.cs:256` (`DoAutoLoginIfApplicable`), `LauncherApp.cs:59` (`ReloadNews`), `LauncherApp.cs.md` §`CurrentPage`/`LauncherState.Main`.
Owns/creates: `MainPage/LoginFrame.cs.md` (`:39`), `MainPage/NewsFrame.cs.md` (`:40`), `MainPage/ActionButtons.cs.md` (`:42`), `MainPage/AccountSwitcher.cs.md` (`:44`); draws them in §4; uses `LoginAction.cs.md` values at `:71`, `:126`, `:174`, `:176`, `:247`, `:292`, `:331`, `:368`.
Reads/writes: `Page.cs.md` (`Padding`, `App`, protected ctor), `Component.cs.md` (`Draw` chain, `Children`), `LoadingPage.cs.md` (via `App.StartLoading`/`StopLoading` and direct `Line2/Line3/Progress` writes at `:951-:955`, `:1063-:1077`), `OtpEntryPage.cs.md` (via `App.AskForOtp`/`WaitForOtpAsync`), `SteamDeckPromptPage.cs.md` (the only `SteamDeckPrompt` state writer, `:766`), `docs/XIVLauncher.Core/LauncherApp.cs.md` (`Settings` state change at `:48`, `HandleContinuationBlocking`, `ShowMessageBlocking`/`ShowExceptionBlocking`), `docs/XIVLauncher.Core/Program.cs.md` (`Patcher`, `HttpClient`, `CompatibilityTools`, `DotnetRuntime`, `Secrets`, `CType`, `IsSteamDeckHardware`, `ResetUIDCache`, `ShowWindow`/`HideWindow`), `docs/XIVLauncher.Core/AppUtil.cs.md` (`OpenBrowser`), `docs/XIVLauncher.Core/CoreEnvironmentSettings.cs.md` (`IsSteamCompatTool`, `GetCleanEnvironmentVariable`), `docs/XIVLauncher.Core/Accounts/` (`XivAccount`, `AccountManager`), `docs/XIVLauncher.Core/Configuration/ILauncherConfig.cs.md` (flag defaults), `docs/XIVLauncher.Core/Support/Troubleshooting.cs.md`, `docs/XIVLauncher.Core/ImGuiHelpers.cs.md`, `docs/XIVLauncher.Core/FontAwesomeIcon.cs.md` (through the children), `src/XIVLauncher.Core/Resources/Localization/Strings.Designer.cs`.
Library (no specs here; cite source paths): `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Game/Launcher.cs` (`Login`, `CheckBootVersion`, `GetGateStatus`), `Game/Patch/{PatchManager,PatchInstaller,PatchVerifier,NotEnoughSpaceException}.cs`, `Game/Patch/PatchList`, `Game/Headlines.cs`, `Game/GateStatus.cs`, `Game/Exceptions/OauthLoginException.cs`, `Addon/{AddonManager,IAddon}.cs`, `Dalamud/{DalamudLauncher,DalamudUpdater,DalamudRunnerException}.cs`, `PlatformAbstractions/{IDalamudCompatibilityCheck}.cs`, `Windows/{WindowsGameRunner,WindowsDalamudRunner,WindowsDalamudCompatibilityCheck}.cs`, `Unix/{UnixGameRunner,UnixDalamudRunner,UnixDalamudCompatibilityCheck}.cs`, `Unix/Compatibility`, `Http/{HttpServer,OtpListener}.cs`.
Related specs (behaviour mirrors): `FtsPage.cs.md` (startup gate), `LoadingPage/DalamudOverlayInfoProxy.cs.md` (the unwritten Dalamud-progress bridge, sibling of §10's progress writers), `LoginAction.cs.md` (the action vocabulary), `MainPage/LoginFrame.cs.md` §8 (the auto-login two-truths invariant).
