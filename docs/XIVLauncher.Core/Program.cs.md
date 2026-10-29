# Program.cs — Specification

**File:** `src/XIVLauncher.Core/Program.cs` · 481 lines · 19,804 bytes
**Namespace:** `XIVLauncher.Core` · **Primary declaration:** `sealed class Program`
**Role:** Application entry point / global singleton ("the trunk" of the launcher), Eto.Forms host

---

## 1. Purpose

`Program` is the single root object of the launcher process. It owns:

- process-wide mutable state (config, storage, HTTP client, Steam handle, Dalamud updater, compatibility tools, secret provider, patch manager),
- the startup sequence (`Main`): storage → logging → config → secrets → Steam → Dalamud → compat tools → Eto `Application` → GTK main loop → teardown,
- the Eto UI-thread bridge that the rest of the launcher uses to show/hide the launcher window (`ShowWindow`/`HideWindow`),
- a family of `Clear*` maintenance routines that wipe launcher state (used as a "reset/troubleshoot" facility).

Everything else in the project hangs off accessors of this class. The class is `sealed` (C# 13), so no type may derive from it and no external extension is permitted.

## 2. Language / build surface

- `sealed class Program`, no access modifier → `internal`; `Main` is `private static void Main(string[] args)` (line 179) carrying `[STAThread]` (line 178), the C# entry point. WinForms requires a main-thread STA for OLE drag-and-drop; a non-STA main thread throws `ThreadStateException` at startup.
- Usings (lines 1–27) include `Eto.Forms` (the Eto UI surface), `Config.Net`, `Serilog`, `System.Runtime.InteropServices` (`RuntimeInformation` for platform gates, `[STAThread]`), and the `XIVLauncher.Common*` namespaces, including `XIVLauncher.Common.Unix.*` (Wine/Dxvk/CompatibilityTools), `XIVLauncher.Common.Windows` (`WindowsSteam`) and `XIVLauncher.Common.Http.HappyEyeballs`. The `XIVLauncher.Common.Unix.*` imports stay on Windows builds too — the namespace is referenced, but the code path is gated (see `CreateCompatToolsInstance`, §5).
- No SDL/ImGui using directives remain: the SDL window loop, `SDLGPUDevice`, `ImGuiBindings`, `FontManager`, `ImGuiColors`, `ImGuiHelpers` and `TextureWrap` are gone from the project, replaced by `Eto.Forms` types and by `Ui`, `EtoTheme`, `AppFonts` (`Ui.cs.md`, `EtoTheme.cs.md`, `AppFonts.cs.md`).
- `mainArgs` is `private static string[] mainArgs = []` (line 36) — the collection-expression form; it survives startup so `ClearLogs(true)` can re-init logging with the original argv (line 468).

## 3. State surface

Statics, lines 35–66:

| Member | Kind | Notes |
| --- | --- | --- |
| `APP_NAME` | `private const string` = `"xlcore"` | used as the `Storage` name (line 182) **and** as `Application.Name` (line 281) |
| `mainArgs` | `private static string[]` | argv retained for `ClearLogs` |
| `launcherApp` | `private static LauncherApp` `null!` | created inside `application.Initialized` (line 289) |
| `launcherForm` | `private static LauncherForm` `null!` | created inside `application.Initialized` (line 292) |
| `Config` | `public static ILauncherConfig` | built once in `LoadConfig` |
| `Steam` | `public static ISteam?` | `WindowsSteam`/`UnixSteam` chosen by the `switch` at lines 227–239 |
| `DalamudUpdater` | `public static DalamudUpdater` | `CreateDalamudUpdater()` (line 264) |
| `DalamudLoadInfo` | `public static DalamudOverlayInfoProxy` | line 263 |
| `CompatibilityTools` | `public static CompatibilityTools` | `CreateCompatToolsInstance()` (line 267); stays `null!` on Windows — the factory returns early, the whole `XIVLauncher.Common.Unix` section is Linux-only |
| `Secrets` | `public static ISecretProvider` | `GetSecretProvider` (line 206) |
| `HttpClient` | `public static HttpClient` | lines 45–55, Happy Eyeballs connect callback |
| `Patcher` | `public static PatchManager?` | assigned by `MainPage` during patching (`MainPage.cs.md`) |
| `storage` | `public static Storage` | `new Storage(APP_NAME)` (line 182) |
| `DotnetRuntime` | `public static DirectoryInfo` | expression body → `storage.GetFolder("runtime")` |
| `CType` | `public static string` | `CoreEnvironmentSettings.GetCType()`, evaluated at type init |
| `IsSteamDeckHardware` | `public static bool` | expression body (line 63) |
| `STEAM_APP_ID`/`STEAM_APP_ID_FT` | `public const uint` = `39210`/`312060` (lines 144–145) |
| `DALAMUD_INJECTOR_NAME` | `public const string` = `"Dalamud.Injector.exe"` (line 151) |

The two UI fields (`launcherApp`, `launcherForm`) are the only state created inside the UI thread; every consumer that reads them runs after `Main form shown` is logged.

## 4. Method surface

| Method | Line | Notes |
| --- | --- | --- |
| `SetupLogging(string[] args)` | 68 | `LogInit.Setup(...)` on `logs/launcher.log` + session banner |
| `LoadConfig(Storage)` | 76 | `Config.Net` builder, ini + cmdline, defaults, `STEAM_COMPAT_*` game-path inference |
| `CreateDalamudUpdater()` | 161 | manual-injection `RunnerOverride` resolution |
| `Main(string[] args)` | 179 | startup + Eto run + teardown; `[STAThread]` at line 178 |
| `CreateCompatToolsInstance()` | 364 | Wine/Dxvk settings + tool folders; returns early on Windows (line 366), Linux-only section gated at the call site |
| `ShowWindow()` | 377 | `Ui.Post(() => launcherForm.ShowWindow())` |
| `HideWindow()` | 380 | `Ui.Post(() => launcherForm.HideWindow())` |
| `GetSecretProvider(Storage)` | 382 | `FILE`/`KEYRING`/`NONE` selection |
| `ClearSettings/ClearPrefix/ClearPlugins/ClearTools/ClearLogs/ClearAll` | 396–457 | on-disk wipes; `tsbutton` variants restart the affected subsystem |
| `ResetUIDCache(bool)` | 480 | `launcherApp.UniqueIdCache.Reset()` |

## 5. Initialisation and ordering rules

`Main` (line 179, `[STAThread]` at line 178) performs, in order:

1. `mainArgs = args; storage = new Storage(APP_NAME)` (lines 181–182) — `storage` must exist before anything else touches folders.
2. `ClearAll()`/individual `Clear*` based on `CoreEnvironmentSettings` (lines 184–195). These run **before** logging, so a `ClearLogs` at startup produces no log lines for the clearing itself.
3. `SetupLogging(mainArgs)` (line 197) then `LoadConfig(storage)` (line 198).
4. **Debug-only UI-thread warning capture** (lines 200–204, `#if DEBUG`): `Ui.CaptureUIThreadWarnings()` — a `TraceListener` that catches Eto's "Accessing UI object from a non-UI thread" warning and logs the offending managed stack (`Ui.cs.md` §2). It has to be installed before the Eto application exists, which is why it sits here, not in `Initialized`.
5. `Secrets = GetSecretProvider(storage)` (line 206).
6. Steam app-id map (lines 208–224), `switch` on `Environment.OSVersion.Platform` (lines 227–239), then the `Steam.Initialize(app.Key)` loop (lines 242–256) guarded by `Config.IsIgnoringSteam != true || CoreEnvironmentSettings.IsSteamCompatTool`; failures are logged and swallowed (`Log.Error(ex, "Steam couldn't load")`, line 259).
7. `DalamudLoadInfo` + `CreateDalamudUpdater()` + `DalamudUpdater.Run(...)` (lines 263–265).
8. `CreateCompatToolsInstance()` (line 267) — on Windows it returns at once (`if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;`, line 366), so `CompatibilityTools` stays `null!`; `WineUtility`, `WineSettings` and `DxvkVersion` are Linux-only `XIVLauncher.Common.Unix` code that must never be reached on Windows, gated at this call site.
9. Eto bootstrap (lines 269–345), including the Debug-only `XLCORE_SELFTEST` selftest (lines 301–335) inside `Initialized`.
10. `Ui.MarkUIThread()` (line 343) — records the thread that owns the UI loop, before the loop starts (`Ui.cs.md` §2).
11. `application.Run()` (line 347) — blocks for the lifetime of the UI.
12. Post-loop: `Patcher.StartCancellation()` (line 351) and `Environment.Exit(0)` inside a `try` (lines 349–361).

## 6. `Main` control flow (contract)

The Eto block (lines 263–298) is the new UI bootstrap and replaces the previous SDL window + `SDLGPUDevice` + `ImGuiBindings` + `FontManager` + frame loop:

```csharp
Log.Debug("Creating the Eto application...");
var version = AppUtil.GetAssemblyVersion();
var hash = AppUtil.GetGitHash();
// The launcher forces a dark theme: the previous build shipped a dark ImGui style, and the
// launcher artwork is dark. Eto 2.13 crashes on GTK3 when asked for Themes.Dark on a system
// theme with no dark variant, so the theme is chosen through GTK_THEME instead.
EtoTheme.PreferDarkGtkTheme();

var application = new Application { Name = APP_NAME };

application.Initialized += (_, _) =>
{
    AppFonts.Initialize(Config.FontPxSize ?? 21.0f);

    var launcherClientConfig = LauncherClientConfig.GetAsync().GetAwaiter().GetResult();
    launcherApp = new LauncherApp(storage, launcherClientConfig.frontierUrl, launcherClientConfig.cutOffBootver);
    Log.Debug("LauncherApp created");

    launcherForm = new LauncherForm(launcherApp, version, hash);
    launcherApp.AttachForm(launcherForm);
    Log.Debug("LauncherForm created");

    application.MainForm = launcherForm;
    launcherForm.Show();
    Log.Debug("Main form shown");

#if DEBUG
    // Debug-only selftest (XLCORE_SELFTEST=1): drives the patching and repair status loop from a
    // worker thread, so the LoadingPage marshalling is exercised without a real patch queue.
    if (Environment.GetEnvironmentVariable("XLCORE_SELFTEST") == "1")
    {
        Task.Run(() =>
        {
            launcherApp.StartLoading("Working on patch 0", "left to download", "left to install", false, true, true);
            for (var i = 0; i < 200; i++)
            {
                launcherApp.LoadingPage.Line2 = $"Working on patch {i}";
                launcherApp.LoadingPage.Line3 = $"left to download: {i} patch(es), {(long)i * 33000000} MiB, 200 MiB in 0s\n1.0 MiB/s";
                launcherApp.LoadingPage.Progress = i / 200f;
                Thread.Sleep(30);
            }

            launcherApp.StartLoading("Now repairing files", "current file", "0/10", false, false, false);

            // Hide/show: MainPage hides the launcher window when the game starts and restores it
            // on Reactivate(), so the GTK loop must stay alive with no mapped window.
            Ui.Run(() => Program.HideWindow());
            Ui.Run(() => Console.WriteLine($"[selftest] after hide: form.Visible={launcherForm.Visible}"));

            for (var i = 0; i < 20; i++)
                Ui.Post(() => launcherApp.LoadingPage.Line2 = $"hidden heartbeat {i}");

            Thread.Sleep(2000);
            Ui.Run(() => Console.WriteLine($"[selftest] last UI post while hidden: {launcherApp.LoadingPage.Line2}"));

            Ui.Run(() => Program.ShowWindow());
            Ui.Run(() => Console.WriteLine($"[selftest] after restore: form.Visible={launcherForm.Visible}"));

            Log.Information("selftest complete: patching and repair status driven from a worker thread");
            Environment.Exit(0);
        });
    }
#endif
};

// The Eto UI thread is the thread that calls Application.Run. The login, patching, and startup
// threads marshal every widget write through Ui, so record the owner before the loop starts;
// Ui.IsUIThread then detects unmarshalled widget writes in Debug builds.
Ui.MarkUIThread();

Log.Debug("Eto OK!");

application.Run();
```

Contract facts:

- **`Main` is `[STAThread]`** (line 178): Eto WinForms registers OLE drag-and-drop at startup, which requires a Windows STA main thread; without the attribute startup throws `System.Threading.ThreadStateException` (verified via probe on Windows).
- **Theme selection happens before `Application` is constructed** (line 277, `EtoTheme.PreferDarkGtkTheme()`). Eto 2.13's GTK `ThemeHandler` crashes on a system theme that has no dark variant, so the dark look is obtained by setting `GTK_THEME` in the process environment (`EtoTheme.cs.md`), not by `Application.Theme = Themes.Dark`. On Windows the call is inert (`EtoTheme.IsLinux` is false): GTK does not read the environment, so the probe never runs there.
- **All UI construction lives inside `application.Initialized`.** Eto's GTK `ApplicationHandler.Run()` fires `OnInitialized` and then enters `Gtk.Application.Run()`; a handler that blocks here blocks the loop, so the window only appears after the handler returns. The three debug logs (`LauncherApp created`, `LauncherForm created`, `Main form shown`) exist to make that placement observable in `launcher.log`.
- **`launcherForm.Show()` is explicit.** Setting `application.MainForm` does not show the form; without the explicit `Show()` call the launcher never appears. `launcherApp.AttachForm(launcherForm)` (line 293) wires `LauncherApp.PageShown` to `LauncherForm.ShowPage` before the form is shown, so the first page (`LauncherState.Main`) is hosted at first paint (`LauncherApp.cs.md`, `LauncherForm.cs.md`).
- **`AppFonts.Initialize(Config.FontPxSize ?? 21.0f)` runs first inside the handler** (line 286). Eto font families must be registered before any control is created, because `Common.Button` icons (`AppFonts.Glyph`) are applied during component construction (`AppFonts.cs.md`, `Components/Common/Button.cs.md`). Note the literal fallback `21.0f` differs from `LoadConfig`'s default `22.0f` (line 107).
- **`LauncherClientConfig.GetAsync().GetAwaiter().GetResult()` blocks the UI thread** (line 288) on a network fetch (`Net/LauncherClientConfig.cs.md`); this happens on the GTK main thread, so a slow/absent network delays first paint and can throw inside `Initialized` (no `try` around it).
- **`application.Run()` returns when the UI loop exits** — i.e. when the launcher window is closed/quit. There is no SDL event pump; `MainPage` exits the launcher via `Application.Instance.Quit()` (`MainPage.cs.md`).
- **`Log.Debug("Eto OK!")` (line 345) is printed before `Run()`** (line 347), so it appears in the log before `Main form shown`; the ordering is intentional and useful for spotting a handler that never returned.
- **`Ui.MarkUIThread()` (line 343) runs immediately before `application.Run()`** (line 347): Eto's UI thread is the thread that enters `Gtk.Application.Run()`, so recording it here makes `Ui.IsUIThread` match Eto's notion, which is what `Ui.AssertUIThread` guards check (`Ui.cs.md` §2).
- **The Debug-only selftest (lines 301–335) is the reproduction harness for the login crash.** `XLCORE_SELFTEST=1` starts `StartLoading` plus 200 `Line2`/`Line3`/`Progress` writes from a `Task.Run` worker, mirroring the patching loop (`MainPage.cs:955`, `970–974`) and the repair timer (`MainPage.cs:1072`); then it drives `Program.HideWindow()` → 20 posted UI writes → `Program.ShowWindow()`, mirroring the game-start/`Reactivate()` window path (`LauncherForm.cs.md` §3), and exits. On the pre-fix code this run logged 32 Eto warnings with the `StartLoading → Reset → Refresh → TextControl.set_Text` stack; on the marshalled code it logs 0, and the hide/show stage reports `form.Visible=False` after hide, the last post processed while hidden, `form.Visible=True` after restore. It exists because forcing a real pending patch, which is the only natural way to exercise that path, is not practical.

## 7. Environment interface (`CoreEnvironmentSettings` + `Program`)

- `CType` (line 60) is a **type-initializer** read of `CoreEnvironmentSettings.GetCType()`, so it is resolved before `Main` starts and is a snapshot: changing env vars later does not change `Program.CType`. Consumers: `MainPage.cs:632–635` (`LC_ALL`/`LC_CTYPE` for the locale hack) and `SettingsTabTroubleshooting.cs:34–35` (locale hack label text).
- `IsSteamDeckHardware` (line 63) is evaluated **per access** — it is an expression-body property over `CoreEnvironmentSettings.IsDeck` / `/home/deck` / `IsDeckGameMode` / `IsDeckFirstRun`. Consumers: `MainPage.cs:781` (hide launcher on Steam Deck), `FtsPage.cs:81,87`, `OtpEntryPage.cs:128` (gamepad input), `SettingsTabDebug.cs:56`, `AppUtil.cs:63`.
- `GamePath` inference (lines 85–95) reads `STEAM_COMPAT_INSTALL_PATH`/`STEAM_COMPAT_APP_ID` only when `Config.GamePath` is null, matching only `39210`/`312060`.
- `IsIgnoringSteam` (line 111) is the only config default written from `CoreEnvironmentSettings.UseSteam` and is assigned unconditionally (not `??=`).

## 8. Configuration surface

`LoadConfig` (line 76) is the sole writer of defaults, in the order: `AcceptLanguage`, `GamePath`, `GameConfigPath`, `ClientLanguage`, `DpiAwareness`, `IsAutologin`, `CompletedFts`, `FontPxSize`, `IsEncryptArgs`, `IsOtpServer`, `IsIgnoringSteam`, `PatchPath`, `DalamudManualInjectPath`, `DalamudEnabled`, `DalamudLoadMethod`, `GlobalScale`, `GameModeEnabled`, `DxvkVersion`, `DxvkAsyncEnabled`, `WineStartupType`, `WineManagedVersion`, `WineSyncType`, `WineBinaryPath`, `WineDebugVars`, `FixLDP`, `FixIM`, `FixLocale`, `FixError127`, `DontUseSystemTz`.

- `GlobalScale` (line 124) remains in the config surface but **has no consumer in the Eto UI** — the previous build used it to scale ImGui (`ImGuiHelpers.GlobalScale`); Eto sizes controls natively. It is written and persisted, but nothing reads it any more.
- `FontPxSize` (default `22.0f`, line 107) is now the font size for Eto fonts via `AppFonts.Initialize` (line 286), replacing `FontManager`'s scaled pixel size (`FontManager.cs` was deleted).
- `Config.Net` writes ini on property set, so settings tabs write `Program.Config` directly (`SettingsPage.cs.md`).
- `WineSyncType` default calls `WineUtility.SystemFsyncSupport()` only when `RuntimeInformation.IsOSPlatform(OSPlatform.Linux)` (lines 131–133); on Windows `WineSyncType.FSync` is assigned with no probe. `SystemFsyncSupport` lives in `XIVLauncher.Common.Unix` — the Linux-only section must never run on Windows (its checks are kernel-level `fsync` probes), so the call is gated at this default site.

## 9. Manual Dalamud injection contract

`CreateDalamudUpdater` (line 161) resolves `RunnerOverride` only when `DalamudManualInjectPath` exists **and** `DalamudManualInjectionEnabled == true` **and** a file named `DALAMUD_INJECTOR_NAME` is present (lines 164–167). `SettingsTabDalamud` writes `Program.DalamudUpdater.RunnerOverride` directly at runtime (`SettingsTabDalamud.cs:33,41,50`), so the updater is reconfigured mid-session; the factory is re-run only by `ClearPlugins(tsbutton: true)` (line 448).

## 10. On-disk layout (owned by this file)

Folders/filenames created or consumed through `storage`: `launcher.ini`, `logs/launcher.log`, `logs/wine.log`, `runtime`, `ffxiv`, `ffxivConfig`, `patch`, `dalamud`, `dalamudAssets`, `installedPlugins`, `compatibilitytool/wine`, `compatibilitytool/dxvk`, `wineprefix`, `secrets.json` (overridable via `XL_SECRETS_FILE_PATH`), `dalamudUI.ini`, `dalamudConfig.json`, `dalamud.boot.log`, `dalamud.boot.old.log`, `dalamud.log`, `dalamud.injector.log`.

`ClearPrefix`/`ClearPlugins`/`ClearTools`/`ClearLogs` `Delete(true)` then re-`GetFolder` to recreate empty folders — the recreate calls are required because `Storage.GetFolder` is what creates them (`LauncherApp` and `CreateCompatToolsInstance` rely on that).

## 11. HTTP client contract

`HttpClient` (lines 45–55) is built once as a static initializer: a `SocketsHttpHandler` whose `ConnectCallback` is itself a nested `SocketsHttpHandler`'s `ConnectCallback`, produced by `new HappyEyeballsCallback().ConnectCallback`. Consumers: `XivAccount.cs:94`, `MainPage.cs:730,1056`, `NewsFrame.cs:109`, `DalamudBranchMetaSettingsEntry.cs:27`, `LauncherClientConfig.cs:25`, `Troubleshooting.cs:85`. The nested construction is intentional — `HappyEyeballsCallback` needs a handler to delegate to — and it means the outer client's connect path is not the default. `AutomaticDecompression = DecompressionMethods.All`, `AllowAutoRedirect = true`, `MaxAutomaticRedirections = 4`.

## 12. Thread-safety and lifecycle hazards

- **UI-thread marshalling is mandatory, for reads as well as writes.** Any code that touches Eto controls from a background task must go through `Ui.Post`/`Ui.Run` (`Ui.cs.md`). `Program.ShowWindow`/`Program.HideWindow` are the canonical example: they post to the UI thread (lines 377, 375) because they are called from login/patch tasks (`MainPage.cs:1157,1165`). Reads count: Eto's `Control.Handler` accessor calls `Application.EnsureUIThread`, so `Input.Value`/`Checkbox.Value` on a worker thread is the same class of violation as a `Label.Text` write, and the login crash needed both fixed (`MainPage.cs.md` §5, `LoadingPage.cs.md` §3).
- **Blocking in `Initialized` blocks first paint.** `LauncherClientConfig` fetch and `LauncherApp` construction (which starts news loading, patch checks, and `Troubleshooting.LogTroubleshooting`) all run on the UI thread.
- **`launcherApp`/`launcherForm` are `null!`** until `Initialized` completes; `ClearSettings(true)`/`ClearLogs(true)`/`ClearPlugins(true)`/`ClearTools(true)`/`ClearAll(tsbutton)` called from the UI (troubleshooting buttons) dereference them, so those paths are only safe after startup.
- **`ResetUIDCache(bool tsbutton)` ignores its parameter's semantics**: it forwards to `launcherApp.UniqueIdCache.Reset()` regardless, and `launcherApp` must be initialized (`MainPage.cs:218`, `SettingsTabGame.cs:61`).
- **Post-loop teardown is partial:** `Patcher.StartCancellation()` then `Environment.Exit(0)` in a `try` whose body cannot throw (the comment acknowledges `PatchManager.UnInitializeAcquisition()` is private and cannot be called) — so patch acquisition cleanup is effectively `StartCancellation()` + immediate exit.
- **Window visibility is the app lifetime** on GTK: the `GApplication` exits when its last window closes, so `LauncherForm.HideWindow` unmaps the window **and** holds the `GApplication` across the hidden period, which is what keeps the launcher alive while the game runs (`LauncherForm.cs.md` §3). On Windows there is no `GApplication`; `HoldApplication`/`ReleaseApplication` are no-op stubs and `launcherForm.HideWindow()` just hides the form.

## 13. Referenced (not defined here)

Uses: `Storage`, `LogInit`, `ApiHelpers.GenerateAcceptLanguage`, `ClientLanguage`, `DpiAwareness`, `DalamudLoadMethod`, `DalamudUpdater`, `DalamudOverlayInfoProxy`, `PatchManager`, `WindowsSteam`/`UnixSteam`, `CompatibilityTools`, `WineSettings`, `WineUtility`, `DxvkVersion`, `HappyEyeballsCallback`, `CoreEnvironmentSettings`, `PlatformHelpers`, `AppUtil`, `ILauncherConfig`, `DirectoryInfoParser`, `AddonListParser`, `LauncherClientConfig`, `KeychainSecretProvider`/`FileSecretProvider`/`DummySecretProvider`, `AppFonts`, `EtoTheme`, `Ui`, `LauncherApp`, `LauncherForm`.
Consumers of this file: `MainPage.cs` (patcher, window show/hide, Steam, compat tools, `ResetUIDCache`), `NewsFrame.cs` (`HttpClient`), `XivAccount.cs` (`Secrets`, `HttpClient`), `AccountManager.cs` (`Config`), `Input.cs` (`Steam`), `FtsPage.cs`/`OtpEntryPage.cs` (`Steam`, `IsSteamDeckHardware`), `AppUtil.cs` (`IsSteamDeckHardware`, `Steam`), all settings tabs (`Config`), `SettingsTabWine.cs:165` (`CreateCompatToolsInstance`), `SettingsTabGame.cs:61` (`ResetUIDCache`), `SettingsTabTroubleshooting.cs:66–67` (`storage`), `Troubleshooting.cs:85` (`HttpClient`), `LauncherClientConfig.cs:25` (`HttpClient`).

## 14. Extension points / notes for maintainers

- **Do not re-introduce `Application.Theme = Themes.Dark`** on GTK3; use `EtoTheme.PreferDarkGtkTheme()` before `Application` construction, and keep the theme probe's candidate list in sync with installed themes (`EtoTheme.cs.md`).
- **Keep UI construction inside `application.Initialized`** — creating Eto controls before `Application` exists throws; creating them after `Run()` returns never executes.
- **New process-wide services belong as statics here**, with `null!` initializers, initialized in `Main` before `application.Run()`; anything UI-facing must be initialized inside `Initialized` so the UI thread owns it.
- If a second platform (WPF/Cocoa) is added, `EtoTheme` becomes a no-op path and `GTK_THEME` must be dropped; `AppFonts` needs re-verification because Eto font registration is platform-handled (`Eto.Forms` docs: `https://github.com/picoe/Eto`).
- Windows is a supported platform: `[STAThread]` on `Main` (STA for OLE drag-drop), `RuntimeInformation.IsOSPlatform` gates on `WineSyncType`, `CreateCompatToolsInstance` and the news width cap, `AppFonts` registers fonts on Windows through `AddFontResourceExW` (`AppFonts.cs.md`), `EtoTheme` is inert there, and `LauncherForm`'s GTK `GApplication` hold calls are stubbed (`LauncherForm.cs.md`).
