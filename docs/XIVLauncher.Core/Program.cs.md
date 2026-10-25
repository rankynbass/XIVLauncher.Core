# Program.cs — Specification

**File:** `src/XIVLauncher.Core/Program.cs` · 424 lines · 16,608 bytes
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

- `sealed class Program`, no access modifier → `internal`; `Main` is `private static void Main(string[] args)` (line 171), the C# entry point.
- Usings (lines 1–27) include `Eto.Forms` (the Eto UI surface), `Config.Net`, `Serilog`, and the `XIVLauncher.Common*` namespaces, including `XIVLauncher.Common.Unix.*` (Wine/Dxvk/CompatibilityTools), `XIVLauncher.Common.Windows` (`WindowsSteam`) and `XIVLauncher.Common.Http.HappyEyeballs`.
- No SDL/ImGui using directives remain: the SDL window loop, `SDLGPUDevice`, `ImGuiBindings`, `FontManager`, `ImGuiColors`, `ImGuiHelpers` and `TextureWrap` are gone from the project, replaced by `Eto.Forms` types and by `Ui`, `EtoTheme`, `AppFonts` (`Ui.cs.md`, `EtoTheme.cs.md`, `AppFonts.cs.md`).
- `mainArgs` is `private static string[] mainArgs = []` (line 36) — the collection-expression form; it survives startup so `ClearLogs(true)` can re-init logging with the original argv (line 410).

## 3. State surface

Statics, lines 35–66:

| Member | Kind | Notes |
| --- | --- | --- |
| `APP_NAME` | `private const string` = `"xlcore"` | used as the `Storage` name (line 174) **and** as `Application.Name` (line 267) |
| `mainArgs` | `private static string[]` | argv retained for `ClearLogs` |
| `launcherApp` | `private static LauncherApp` `null!` | created inside `application.Initialized` (line 275) |
| `launcherForm` | `private static LauncherForm` `null!` | created inside `application.Initialized` (line 278) |
| `Config` | `public static ILauncherConfig` | built once in `LoadConfig` |
| `Steam` | `public static ISteam?` | `WindowsSteam`/`UnixSteam` chosen by the `switch` at lines 213–224 |
| `DalamudUpdater` | `public static DalamudUpdater` | `CreateDalamudUpdater()` (line 250) |
| `DalamudLoadInfo` | `public static DalamudOverlayInfoProxy` | line 249 |
| `CompatibilityTools` | `public static CompatibilityTools` | `CreateCompatToolsInstance()` (line 253) |
| `Secrets` | `public static ISecretProvider` | `GetSecretProvider` (line 192) |
| `HttpClient` | `public static HttpClient` | lines 45–55, Happy Eyeballs connect callback |
| `Patcher` | `public static PatchManager?` | assigned by `MainPage` during patching (`MainPage.cs.md`) |
| `storage` | `public static Storage` | `new Storage(APP_NAME)` (line 174) |
| `DotnetRuntime` | `public static DirectoryInfo` | expression body → `storage.GetFolder("runtime")` |
| `CType` | `public static string` | `CoreEnvironmentSettings.GetCType()`, evaluated at type init |
| `IsSteamDeckHardware` | `public static bool` | expression body (line 63) |
| `STEAM_APP_ID`/`STEAM_APP_ID_FT` | `public const uint` = `39210`/`312060` (lines 137–138) |
| `DALAMUD_INJECTOR_NAME` | `public const string` = `"Dalamud.Injector.exe"` (line 144) |

The two UI fields (`launcherApp`, `launcherForm`) are the only state created inside the UI thread; every consumer that reads them runs after `Main form shown` is logged.

## 4. Method surface

| Method | Line | Notes |
| --- | --- | --- |
| `SetupLogging(string[] args)` | 68 | `LogInit.Setup(...)` on `logs/launcher.log` + session banner |
| `LoadConfig(Storage)` | 76 | `Config.Net` builder, ini + cmdline, defaults, `STEAM_COMPAT_*` game-path inference |
| `CreateDalamudUpdater()` | 154 | manual-injection `RunnerOverride` resolution |
| `Main(string[] args)` | 171 | startup + Eto run + teardown |
| `CreateCompatToolsInstance()` | 306 | Wine/Dxvk settings + tool folders; no-op on Windows |
| `ShowWindow()` | 319 | `Ui.Post(() => launcherForm.ShowWindow())` |
| `HideWindow()` | 322 | `Ui.Post(() => launcherForm.HideWindow())` |
| `GetSecretProvider(Storage)` | 324 | `FILE`/`KEYRING`/`NONE` selection |
| `ClearSettings/ClearPrefix/ClearPlugins/ClearTools/ClearLogs/ClearAll` | 358–419 | on-disk wipes; `tsbutton` variants restart the affected subsystem |
| `ResetUIDCache(bool)` | 422 | `launcherApp.UniqueIdCache.Reset()` |

## 5. Initialisation and ordering rules

`Main` (line 171) performs, in order:

1. `mainArgs = args; storage = new Storage(APP_NAME)` (lines 173–174) — `storage` must exist before anything else touches folders.
2. `ClearAll()`/individual `Clear*` based on `CoreEnvironmentSettings` (lines 176–187). These run **before** logging, so a `ClearLogs` at startup produces no log lines for the clearing itself.
3. `SetupLogging(mainArgs)` (line 189) then `LoadConfig(storage)` (line 190).
4. `Secrets = GetSecretProvider(storage)` (line 192).
5. Steam app-id map (lines 194–210), `switch` on `Environment.OSVersion.Platform` (lines 213–224), then the `Steam.Initialize(app.Key)` loop (lines 226–240) guarded by `Config.IsIgnoringSteam != true || CoreEnvironmentSettings.IsSteamCompatTool`; failures are logged and swallowed (`Log.Error(ex, "Steam couldn't load")`, line 245).
6. `DalamudLoadInfo` + `CreateDalamudUpdater()` + `DalamudUpdater.Run(...)` (lines 249–251).
7. `CreateCompatToolsInstance()` (line 253).
8. Eto bootstrap (lines 255–290).
9. `application.Run()` (line 290) — blocks for the lifetime of the UI.
10. Post-loop: `Patcher.StartCancellation()` and `Environment.Exit(0)` inside a `try` (lines 292–298).

## 6. `Main` control flow (contract)

The Eto block (lines 255–290) is the new UI bootstrap and replaces the previous SDL window + `SDLGPUDevice` + `ImGuiBindings` + `FontManager` + frame loop:

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
};

Log.Debug("Eto OK!");

application.Run();
```

Contract facts:

- **Theme selection happens before `Application` is constructed** (line 263, `EtoTheme.PreferDarkGtkTheme()`). Eto 2.13's GTK `ThemeHandler` crashes on a system theme that has no dark variant, so the dark look is obtained by setting `GTK_THEME` in the process environment (`EtoTheme.cs.md`), not by `Application.Theme = Themes.Dark`.
- **All UI construction lives inside `application.Initialized`.** Eto's GTK `ApplicationHandler.Run()` fires `OnInitialized` and then enters `Gtk.Application.Run()`; a handler that blocks here blocks the loop, so the window only appears after the handler returns. The three debug logs (`LauncherApp created`, `LauncherForm created`, `Main form shown`) exist to make that placement observable in `launcher.log`.
- **`launcherForm.Show()` is explicit.** Setting `application.MainForm` does not show the form; without the explicit `Show()` call the launcher never appears. `launcherApp.AttachForm(launcherForm)` (line 279) wires `LauncherApp.PageShown` to `LauncherForm.ShowPage` before the form is shown, so the first page (`LauncherState.Main`) is hosted at first paint (`LauncherApp.cs.md`, `LauncherForm.cs.md`).
- **`AppFonts.Initialize(Config.FontPxSize ?? 21.0f)` runs first inside the handler** (line 272). Eto font families must be registered before any control is created, because `Common.Button` icons (`AppFonts.Glyph`) are applied during component construction (`AppFonts.cs.md`, `Components/Common/Button.cs.md`). Note the literal fallback `21.0f` differs from `LoadConfig`'s default `22.0f` (line 116).
- **`LauncherClientConfig.GetAsync().GetAwaiter().GetResult()` blocks the UI thread** (line 274) on a network fetch (`Net/LauncherClientConfig.cs.md`); this happens on the GTK main thread, so a slow/absent network delays first paint and can throw inside `Initialized` (no `try` around it).
- **`application.Run()` returns when the UI loop exits** — i.e. when the launcher window is closed/quit. There is no SDL event pump; `MainPage` exits the launcher via `Application.Instance.Quit()` (`MainPage.cs.md`).
- **`Log.Debug("Eto OK!")` (line 288) is printed before `Run()`**, so it appears in the log before `Main form shown`; the ordering is intentional and useful for spotting a handler that never returned.

## 7. Environment interface (`CoreEnvironmentSettings` + `Program`)

- `CType` (line 60) is a **type-initializer** read of `CoreEnvironmentSettings.GetCType()`, so it is resolved before `Main` starts and is a snapshot: changing env vars later does not change `Program.CType`. Consumers: `MainPage.cs:632–635` (`LC_ALL`/`LC_CTYPE` for the locale hack) and `SettingsTabTroubleshooting.cs:34–35` (locale hack label text).
- `IsSteamDeckHardware` (line 63) is evaluated **per access** — it is an expression-body property over `CoreEnvironmentSettings.IsDeck` / `/home/deck` / `IsDeckGameMode` / `IsDeckFirstRun`. Consumers: `MainPage.cs:781` (hide launcher on Steam Deck), `FtsPage.cs:81,87`, `OtpEntryPage.cs:128` (gamepad input), `SettingsTabDebug.cs:56`, `AppUtil.cs:63`.
- `GamePath` inference (lines 85–95) reads `STEAM_COMPAT_INSTALL_PATH`/`STEAM_COMPAT_APP_ID` only when `Config.GamePath` is null, matching only `39210`/`312060`.
- `IsIgnoringSteam` (line 111) is the only config default written from `CoreEnvironmentSettings.UseSteam` and is assigned unconditionally (not `??=`).

## 8. Configuration surface

`LoadConfig` (line 76) is the sole writer of defaults, in the order: `AcceptLanguage`, `GamePath`, `GameConfigPath`, `ClientLanguage`, `DpiAwareness`, `IsAutologin`, `CompletedFts`, `FontPxSize`, `IsEncryptArgs`, `IsOtpServer`, `IsIgnoringSteam`, `PatchPath`, `DalamudEnabled`, `DalamudLoadMethod`, `GlobalScale`, `GameModeEnabled`, `DxvkVersion`, `DxvkAsyncEnabled`, `WineStartupType`, `WineManagedVersion`, `WineSyncType`, `WineBinaryPath`, `WineDebugVars`, `FixLDP`, `FixIM`, `FixLocale`, `FixError127`, `DontUseSystemTz`.

- `GlobalScale` (line 119) remains in the config surface but **has no consumer in the Eto UI** — the previous build used it to scale ImGui (`ImGuiHelpers.GlobalScale`); Eto sizes controls natively. It is written and persisted, but nothing reads it any more.
- `FontPxSize` (default `22.0f`, line 107) is now the font size for Eto fonts via `AppFonts.Initialize` (line 272), replacing `FontManager`'s scaled pixel size (`FontManager.cs` was deleted).
- `Config.Net` writes ini on property set, so settings tabs write `Program.Config` directly (`SettingsPage.cs.md`).
- `WineSyncType` default calls `WineUtility.SystemFsyncSupport()` at startup — a side-effecting probe (subprocess/`dlopen` style checks) executed on the main thread before the UI.

## 9. Manual Dalamud injection contract

`CreateDalamudUpdater` (line 154) resolves `RunnerOverride` only when `DalamudManualInjectPath` exists **and** `DalamudManualInjectionEnabled == true` **and** a file named `DALAMUD_INJECTOR_NAME` is present (lines 157–160). `SettingsTabDalamud` writes `Program.DalamudUpdater.RunnerOverride` directly at runtime (`SettingsTabDalamud.cs:33,41,50`), so the updater is reconfigured mid-session; the factory is re-run only by `ClearPlugins(tsbutton: true)` (line 389).

## 10. On-disk layout (owned by this file)

Folders/filenames created or consumed through `storage`: `launcher.ini`, `logs/launcher.log`, `logs/wine.log`, `runtime`, `ffxiv`, `ffxivConfig`, `patch`, `dalamud`, `dalamudAssets`, `installedPlugins`, `compatibilitytool/wine`, `compatibilitytool/dxvk`, `wineprefix`, `secrets.json` (overridable via `XL_SECRETS_FILE_PATH`), `dalamudUI.ini`, `dalamudConfig.json`, `dalamud.boot.log`, `dalamud.boot.old.log`, `dalamud.log`, `dalamud.injector.log`.

`ClearPrefix`/`ClearPlugins`/`ClearTools`/`ClearLogs` `Delete(true)` then re-`GetFolder` to recreate empty folders — the recreate calls are required because `Storage.GetFolder` is what creates them (`LauncherApp` and `CreateCompatToolsInstance` rely on that).

## 11. HTTP client contract

`HttpClient` (lines 45–55) is built once as a static initializer: a `SocketsHttpHandler` whose `ConnectCallback` is itself a nested `SocketsHttpHandler`'s `ConnectCallback`, produced by `new HappyEyeballsCallback().ConnectCallback`. Consumers: `XivAccount.cs:94`, `MainPage.cs:730,1056`, `NewsFrame.cs:109`, `DalamudBranchMetaSettingsEntry.cs:27`, `LauncherClientConfig.cs:25`, `Troubleshooting.cs:85`. The nested construction is intentional — `HappyEyeballsCallback` needs a handler to delegate to — and it means the outer client's connect path is not the default. `AutomaticDecompression = DecompressionMethods.All`, `AllowAutoRedirect = true`, `MaxAutomaticRedirections = 4`.

## 12. Thread-safety and lifecycle hazards

- **UI-thread marshalling is mandatory.** Any code that mutates Eto controls from a background task must go through `Ui.Post`/`Ui.Run` (`Ui.cs.md`). `Program.ShowWindow`/`Program.HideWindow` are the canonical example: they post to the UI thread (lines 319, 322) because they are called from login/patch tasks (`MainPage.cs:1145,1153`).
- **Blocking in `Initialized` blocks first paint.** `LauncherClientConfig` fetch and `LauncherApp` construction (which starts news loading, patch checks, and `Troubleshooting.LogTroubleshooting`) all run on the UI thread.
- **`launcherApp`/`launcherForm` are `null!`** until `Initialized` completes; `ClearSettings(true)`/`ClearLogs(true)`/`ClearPlugins(true)`/`ClearTools(true)`/`ClearAll(tsbutton)` called from the UI (troubleshooting buttons) dereference them, so those paths are only safe after startup.
- **`ResetUIDCache(bool tsbutton)` ignores its parameter's semantics**: it forwards to `launcherApp.UniqueIdCache.Reset()` regardless, and `launcherApp` must be initialized (`MainPage.cs:218`, `SettingsTabGame.cs:61`).
- **Post-loop teardown is partial:** `Patcher.StartCancellation()` then `Environment.Exit(0)` in a `try` whose body cannot throw (the comment acknowledges `PatchManager.UnInitializeAcquisition()` is private and cannot be called) — so patch acquisition cleanup is effectively `StartCancellation()` + immediate exit.
- **Window visibility is the app lifetime**: the GTK `GApplication` exits when the last visible window closes, which is why `LauncherForm.HideWindow` minimises instead of hiding (`LauncherForm.cs.md`).

## 13. Referenced (not defined here)

Uses: `Storage`, `LogInit`, `ApiHelpers.GenerateAcceptLanguage`, `ClientLanguage`, `DpiAwareness`, `DalamudLoadMethod`, `DalamudUpdater`, `DalamudOverlayInfoProxy`, `PatchManager`, `WindowsSteam`/`UnixSteam`, `CompatibilityTools`, `WineSettings`, `WineUtility`, `DxvkVersion`, `HappyEyeballsCallback`, `CoreEnvironmentSettings`, `PlatformHelpers`, `AppUtil`, `ILauncherConfig`, `DirectoryInfoParser`, `AddonListParser`, `LauncherClientConfig`, `KeychainSecretProvider`/`FileSecretProvider`/`DummySecretProvider`, `AppFonts`, `EtoTheme`, `Ui`, `LauncherApp`, `LauncherForm`.
Consumers of this file: `MainPage.cs` (patcher, window show/hide, Steam, compat tools, `ResetUIDCache`), `NewsFrame.cs` (`HttpClient`), `XivAccount.cs` (`Secrets`, `HttpClient`), `AccountManager.cs` (`Config`), `Input.cs` (`Steam`), `FtsPage.cs`/`OtpEntryPage.cs` (`Steam`, `IsSteamDeckHardware`), `AppUtil.cs` (`IsSteamDeckHardware`, `Steam`), all settings tabs (`Config`), `SettingsTabWine.cs:165` (`CreateCompatToolsInstance`), `SettingsTabGame.cs:61` (`ResetUIDCache`), `SettingsTabTroubleshooting.cs:66–67` (`storage`), `Troubleshooting.cs:85` (`HttpClient`), `LauncherClientConfig.cs:25` (`HttpClient`).

## 14. Extension points / notes for maintainers

- **Do not re-introduce `Application.Theme = Themes.Dark`** on GTK3; use `EtoTheme.PreferDarkGtkTheme()` before `Application` construction, and keep the theme probe's candidate list in sync with installed themes (`EtoTheme.cs.md`).
- **Keep UI construction inside `application.Initialized`** — creating Eto controls before `Application` exists throws; creating them after `Run()` returns never executes.
- **New process-wide services belong as statics here**, with `null!` initializers, initialized in `Main` before `application.Run()`; anything UI-facing must be initialized inside `Initialized` so the UI thread owns it.
- If a second platform (WPF/Cocoa) is added, `EtoTheme` becomes a no-op path and `GTK_THEME` must be dropped; `AppFonts` needs re-verification because Eto font registration is platform-handled (`Eto.Forms` docs: `https://github.com/picoe/Eto`).
