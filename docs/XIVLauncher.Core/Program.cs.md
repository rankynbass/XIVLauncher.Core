# Program.cs — Specification

**File:** `src/XIVLauncher.Core/Program.cs` · 485 lines · 18,743 bytes
**Namespace:** `XIVLauncher.Core` · **Primary declaration:** `sealed class Program`
**Role:** Application entry point / global singleton ("the trunk" of the launcher)

---

## 1. Purpose

`Program` is the single root object of the launcher process. It owns:

- process-wide mutable state (config, storage, HTTP client, Steam handle, Dalamud updater, compatibility tools, secret provider, ImGui bindings, SDL window/GPU device handles),
- the startup sequence (`Main`): storage → logging → config → secrets → Steam → Dalamud → compat tools → SDL/ImGui → UI event loop → teardown,
- the ImGui event loop that drives `launcherApp.Draw()`,
- a family of `Clear*` maintenance routines that wipe launcher state (used as a "reset/troubleshoot" facility).

Everything else in the project hangs off accessors of this class. The class is `sealed` (C# 13), so no type may derive from it and no external extension is permitted.

## 2. Language / build surface

| Attribute | Value |
| --- | --- |
| C# features used | `sealed class`, `unsafe` blocks, native pointer types (`SDLWindow*`, `SDLGPUDevice*`), `const` fields, `??=` defaults, tuple/`switch` expressions, `using`-alias declarations |
| Requires | `AllowUnsafeBlocks=true`, `LangVersion=latest` (see `XIVLauncher.Core.csproj`) |
| Platform libs | `Hexa.NET.SDL3` (SDL 3 bindings via HexaGen) |
| Project libs | `XIVLauncher.Common(.Windows/.Unix)`, `XIVLauncher.Core.*` sub-packages, `Config.Net`, `Serilog` |

Type aliases declared at file scope: `SDLGPUDevice = Hexa.NET.SDL3.SDLGPUDevice`, `SDLWindow = Hexa.NET.SDL3.SDLWindow`.

## 3. State surface

### 3.1 Public global accessors (the implicit application API)

| Member | Type | Kind | Source / notes |
| --- | --- | --- | --- |
| `Config` | `ILauncherConfig` | accessor, `get`/`private set` | populated by `LoadConfig`; `null!` until then |
| `CommonSettings` | `CommonSettings` | get-only accessor | `new(Config)` — view over `Config` (`Configuration/CommonSettings.cs`) |
| `Steam` | `ISteam?` | accessor, `private set` | `WindowsSteam` or `UnixSteam`; `null` if init failed |
| `DalamudUpdater` | `DalamudUpdater` | accessor | built by `CreateDalamudUpdater()` |
| `DalamudLoadInfo` | `DalamudOverlayInfoProxy` | accessor | shared overlay status shown in UI |
| `CompatibilityTools` | `CompatibilityTools` | accessor | `null` on Windows (see `CreateCompatToolsInstance`) |
| `Secrets` | `ISecretProvider` | accessor | `FILE` / `KEYRING` / `NONE` |
| `HttpClient` | `HttpClient` | accessor | constructed at class init (see §5) |
| `Patcher` | `PatchManager?` | `get`/`set` (public setter) | only externally assigned member; `null` by default |
| `storage` | `Storage` | static field | `new Storage(APP_NAME)`, first thing in `Main` |
| `DotnetRuntime` | `DirectoryInfo` | get-only accessor | `storage.GetFolder("runtime")` |
| `CType` | `string` | static field (initializer) | `CoreEnvironmentSettings.GetCType()` — computed at class init, spawns `sh` (see §7) |
| `IsSteamDeckHardware` | `bool` | get-only accessor | tri-state env override, else heuristic (see §7) |
| `GPUDevice` | `SDLGPUDevice*` | accessor | handle to the SDL GPU device |
| `ImGuiBindings` | `ImGuiBindings` | accessor | the UI controller (`ImGuiBindings.cs`) |

### 3.2 Private backing state

| Member | Type | Notes |
| --- | --- | --- |
| `APP_NAME` | `const string = "xlcore"` | app/storage name (implicit static const field) |
| `ClearColor` | `static readonly Vector3 (0.1,0.1,0.1)` | **declared but never referenced in this file** — the clear colour actually used is `ImGuiBindings.clearColor`; treat as dead code |
| `mainArgs` | `static string[]` | raw `argv`, kept so `SetupLogging` can be re-run by `ClearLogs(true)` |
| `launcherApp` | `static LauncherApp` | the root UI component |
| `window` / `gpuDevice` | `static unsafe SDLWindow*` / `SDLGPUDevice*` | raw handles, `null!` until `Main` |
| `guiBindings` | `static ImGuiBindings` | backing field for the `ImGuiBindings` accessor |

### 3.3 Constants

| Name | Value | Meaning |
| --- | --- | --- |
| `STEAM_APP_ID` | `39210u` | Steam AppID, FFXIV retail |
| `STEAM_APP_ID_FT` | `312060u` | Steam AppID, FFXIV Free Trial |
| `DALAMUD_INJECTOR_NAME` | `"Dalamud.Injector.exe"` | injector executable name (annotated "TODO: move this somewhere better") |

Both Steam constants are declared `public const uint` in the middle of the class body; declaration order in this file is significant for static initialisation (see §5).

## 4. Method surface

| Signature | Visibility | Purpose |
| --- | --- | --- |
| `static void Main(string[] args)` | private | entry point (see §6 flow) |
| `static void SetupLogging(string[] args)` | private | Serilog sink setup at `logs/launcher.log` + version banner |
| `static void LoadConfig(Storage storage)` | private | Config.Net build + default injection (see §8) |
| `static DalamudUpdater CreateDalamudUpdater()` | private | manual vs automatic injection decision (see §9) |
| `static ISecretProvider GetSecretProvider(Storage storage)` | private | provider selection from env (see §7) |
| `static void CreateCompatToolsInstance()` | public | Wine + DXvk settings instance; no-op on Windows |
| `static void ShowWindow()` / `HideWindow()` | public | thin SDL window visibility wrappers (`unsafe`) |
| `static void ClearSettings(bool tsbutton = false)` | public | delete `launcher.ini` |
| `static void ClearPrefix()` | public | delete `wineprefix` folder (recursive) |
| `static void ClearPlugins(bool tsbutton = false)` | public | delete Dalamud state, plugins, runtime |
| `static void ClearTools(bool tsbutton = false)` | public | delete `compatibilitytool` |
| `static void ClearLogs(bool tsbutton = false)` | public | delete `logs` folder + Dalamud log files |
| `static void ClearAll(bool tsbutton = false)` | public | the five clears above, in order (`ClearSettings`→`ClearPrefix`→`ClearPlugins`→`ClearTools`→`ClearLogs`); note `ClearPrefix` takes **no** `tsbutton` parameter at all, and `ClearLogs` is always called with `true` here |
| `static void ResetUIDCache(bool tsbutton = false)` | public | `launcherApp.UniqueIdCache.Reset()` |

All `Clear*` methods re-create the folders/files they deleted (the trailing `storage.GetFolder(...)` calls materialise empty directories), and their `tsbutton` variants re-run the corresponding initialiser so the launcher can continue after a wipe.

## 5. Initialisation and ordering rules

- `storage` must be assigned **before** anything else touches the class; `Main` does `storage = new Storage(APP_NAME)` as its first statement.
- Static/`const` field initializers in this class run in declaration order at class initialisation, i.e. before `Main`'s body: `HttpClient` is built, then `CType` is computed, then `IsSteamDeckHardware` (accessor, so it is deferred to access).
- `CType` therefore performs a subprocess launch (`sh -c "locale -a 2>/dev/null | grep -i utf"`) during class init — before logging exists and before any `CoreEnvironmentSettings` clear happens. On Windows it short-circuits to `""`.
- `ClearColor`'s `Vector3` value is the same triple used by `ImGuiBindings`, but the two are not linked; changing this file's value has no effect on rendering.

## 6. `Main` control flow (contract)

1. `mainArgs = args`; `storage = new Storage("xlcore")`.
2. Wipe phase — if `CoreEnvironmentSettings.ClearAll` then `ClearAll()`, else individually: `ClearSettings`, `ClearPrefix`, `ClearPlugins`, `ClearTools`, `ClearLogs` (all driven by env booleans, see §7).
3. `SetupLogging(mainArgs)` — file sink at `<storage>/logs/launcher.log`, logs a banner and the assembly version + git hash (`AppUtil`).
4. `LoadConfig(storage)`.
5. `Secrets = GetSecretProvider(storage)`.
6. Steam app-id map, insertion order: env `SteamAppId`→`"XLM"`, env `XL_APPID`→`"XL_APPID"` (each added only when non-zero, i.e. only if the env var parsed), then `39210`→`"FFXIV Retail"` and `312060`→`"FFXIV Free Trial"` appended only if not already present (`ContainsKey` guard).
7. Platform dispatch: `PlatformID.Win32NT` → `WindowsSteam`, `PlatformID.Unix` → `UnixSteam`, else `PlatformNotSupportedException`. Initialization loop runs only if `Config.IsIgnoringSteam != true` **or** `CoreEnvironmentSettings.IsSteamCompatTool`; first successful `Steam.Initialize(appId)` wins (`break`), failures are logged per entry, a total failure logs "Steam couldn't load" and leaves `Steam == null`.
8. Dalamud: `DalamudLoadInfo = new DalamudOverlayInfoProxy()`, `DalamudUpdater = CreateDalamudUpdater()`, `DalamudUpdater.Run(Config.DalamudBetaKind, Config.DalamudBetaKey)`.
9. `CreateCompatToolsInstance()` (skipped on Windows).
10. `unsafe` block, in order, each failure being fatal (`Environment.Exit(1)` after logging `SDL.GetErrorS()`):
    1. `SDL.Init(Video | Gamepad)`
    2. `mainScale = SDL.GetDisplayContentScale(SDL.GetPrimaryDisplay())`
    3. `SDL.CreateWindow("XIVLauncher {version} ({hash})", 1280*scale, 800*scale, Resizable | Hidden | HighPixelDensity)` then `SDL.SetWindowPosition(..., SDL_WINDOWPOS_CENTERED_MASK, ...)` (window starts **hidden**; shown only in step 12)
    4. `SDL.CreateGPUDevice(Spirv | Dxil | Metallib, true, null)` — accepts all three shader formats, vsync requested
    5. `SDL.ClaimWindowForGPUDevice`
    6. `SDL.SetGPUSwapchainParameters(SDR, Vsync)`
    7. `guiBindings = new ImGuiBindings(window, gpuDevice, <storage>/launcherUI.ini, Config.FontPxSize ?? 21.0f, mainScale)`
    8. `StyleModelV1.DalamudStandard.Apply()`
    9. `LauncherClientConfig.GetAsync().GetAwaiter().GetResult()` — **blocking** network fetch of `frontierUrl` and `cutOffBootver`
    10. `launcherApp = new LauncherApp(storage, frontierUrl, cutOffBootver)` (this is where the killswitch check runs)
    11. `SDL.ShowWindow(window)`
11. Frame loop, until `guiBindings.ProcessExit()` returns `true`:
    - if window flags include `Minimized` or `Hidden` → `SDL.Delay(10)` and skip the frame (minimised/idle guard),
    - else `guiBindings.NewFrame()` → `launcherApp.Draw()` → `guiBindings.Render()`.
12. Teardown: `guiBindings.Dispose()` (disposes referenced textures + shuts ImGui/SDL-GPU backends), `ReleaseWindowFromGPUDevice`, `DestroyGPUDevice`, `DestroyWindow`, `SDL.Quit()`.
13. Post-loop: if `Patcher != null`, `Patcher.StartCancellation()`, then `Environment.Exit(0)` wrapped in a `try/catch` that logs "Could not uninitialize patch acquisition."

**Quirk:** the `try { Environment.Exit(0); } catch` block is a deliberate workaround — the comment states `PatchManager.UnInitializeAcquisition()` is private and `StartCancellation()` is its only public caller. `Environment.Exit` does not normally throw, so the `catch` is effectively unreachable; the intent is to let `PatchManager` complete its teardown side effect.

## 7. Environment interface (`CoreEnvironmentSettings` + `Program`)

| Variable | Effect |
| --- | --- |
| `XL_CLEAR_SETTINGS` / `PREFIX` / `PLUGINS` / `TOOLS` / `LOGS` / `ALL` | trigger the corresponding `Clear*` routine at startup |
| `XL_DECK`, `XL_GAMEMODE`, `XL_FIRSTRUN` | tri-state Steam Deck hints (`true`/`1`/`yes`/`y`/`on`, `false`/`0`/`no`/`n`/`off`) |
| `XL_USE_STEAM` | `false` forces `Config.IsIgnoringSteam = true` ("Fix for Steam Deck users who lock themselves out") |
| `XL_SCT` | `IsSteamCompatTool` — forces Steam init even when Steam is ignored |
| `SteamAppId`, `XL_APPID` | extra Steam AppIDs tried before the retail/free-trial IDs |
| `XL_SECRET_PROVIDER` | `FILE` \| `KEYRING` (default) \| `NONE`; any other value throws `ArgumentException` |
| `XL_SECRETS_FILE_PATH` | secrets file name, default `"secrets.json"` (resolved through `Storage`, i.e. under the app dir) |
| `STEAM_COMPAT_INSTALL_PATH`, `STEAM_COMPAT_APP_ID` | read in `LoadConfig` to auto-detect the compat-tool game path |

`IsSteamDeckHardware` contract: `CoreEnvironmentSettings.IsDeck` if set, else `Directory.Exists("/home/deck") || IsDeckGameMode || IsDeckFirstRun`. The adjacent `// TODO` notes there is no Steamworks API backing this detection yet.

## 8. Configuration surface

Config is a `ConfigurationBuilder<ILauncherConfig>` composed from: command-line arguments, `<storage>/launcher.ini`, plus type parsers `DirectoryInfoParser` and `AddonListParser` (`Configuration/Parsers`). `AcceptLanguage` is filled from `ApiHelpers.GenerateAcceptLanguage()` when empty.

Defaults asserted by this file (applied with `??=`, except where noted):

| Key | Default |
| --- | --- |
| `GamePath` | `STEAM_COMPAT_*` derived dir when AppID is 39210/312060, else `storage.GetFolder("ffxiv")` |
| `GameConfigPath` | `ffxivConfig` |
| `ClientLanguage` | `English` |
| `DpiAwareness` | `Unaware` |
| `IsAutologin`, `CompletedFts` | `false` |
| `FontPxSize` | `22.0f` |
| `IsEncryptArgs` | `true` |
| `IsOtpServer` | `false` |
| `IsIgnoringSteam` | plain `=`, not `??=`: `!UseSteam` if `XL_USE_STEAM` set, else existing value or `false` |
| `PatchPath` | `patch` |
| `DalamudEnabled` | `true` |
| `DalamudLoadMethod` | `EntryPoint` |
| `GlobalScale` | `1.0f` |
| `GameModeEnabled` | `false` |
| `DxvkVersion` | `Stable` |
| `DxvkAsyncEnabled` | `true` |
| `WineStartupType` | `Managed` |
| `WineManagedVersion` | `Stable` |
| `WineSyncType` | `FSync` if `WineUtility.SystemFsyncSupport() == Supported`, else `ESync` |
| `WineBinaryPath` | `/usr/bin` |
| `WineDebugVars` | `-all` |
| `FixLDP`, `FixIM`, `FixLocale`, `FixError127`, `DontUseSystemTz` | `false` |

## 9. Manual Dalamud injection contract

`CreateDalamudUpdater()` uses a manual injector only when **all** of: `DalamudManualInjectPath != null`, `DalamudManualInjectionEnabled == true`, the path exists, and it contains a file named `Dalamud.Injector.exe`. Otherwise `RunnerOverride = null` and the normal Dalamud branch resolution applies.

Updater folders: `dalamud` (payload), `runtime` (shared dotnet runtime), `dalamudAssets` (asset cache). `HttpClient` and `DalamudLoadInfo` are injected as the transport and status overlay.

## 10. On-disk layout (owned by this file)

```
<storage root "xlcore">/
  launcher.ini              launcher config (also launcherUI.ini for ImGui, dalamudUI.ini, dalamudConfig.json)
  launcherUI.ini            ImGui style ini (ImGuiBindings)
  secrets.json              when XL_SECRET_PROVIDER=FILE
  logs/                     launcher.log (this file), wine.log (compat tools), dalamud*.{log,old.log}
  runtime/                  shared dotnet runtime (Dalamud + patcher)
  ffxiv/ ffxivConfig/       game path / game config path
  patch/                    patcher data
  dalamud/ dalamudAssets/ installedPlugins/
  wineprefix/
  compatibilitytool/wine, compatibilitytool/dxvk
```

## 11. HTTP client contract

The single shared `HttpClient` (used by Dalamud updater, launcher client config, and everything else in `XIVLauncher.Core.Net`) is configured as:

- `ConnectCallback` = a `HappyEyeballsCallback` instance (`XIVLauncher.Common.Http.HappyEyeballs`), nested handler sets `UseCookies = false`,
- `AutomaticDecompression = All`,
- `AllowAutoRedirect = true`, `MaxAutomaticRedirections = 4`.

It is constructed at class init, i.e. before config/logging exist.

## 12. Thread-safety and lifecycle hazards

- All global state is unsynchronised; the design assumes a single-threaded startup and a single frame loop.
- Raw SDL handles (`window`, `gpuDevice`) are leaked on any early `Environment.Exit(1)` before teardown.
- `Clear*(tsbutton = true)` is unsound when called before `Main` constructs `launcherApp` — `ClearSettings(true)` touches `launcherApp.State`, `ClearAll(true)` propagates that. From `Main` they are always called with the default `false`.
- `ResetUIDCache` ignores its `tsbutton` parameter entirely (the parameter is unused), and NPEs if `launcherApp` is null.
- `ClearPrefix`/`ClearPlugins`/`ClearTools`/`ClearLogs` delete recursive folders without checking in-flight use; they are only safe at startup.
- `HideWindow`/`ShowWindow` are public but `window` is `null!` before `Main` reaches step 10.
- `Patcher` is the only global with a public setter — external code may legitimately assign it; nothing in this file does.

## 13. Referenced (not defined here)

`Storage`, `ConfigurationBuilder`/`ILauncherConfig` (Config.Net), `CommonSettings`, `DirectoryInfoParser`, `AddonListParser`, `ApiHelpers.GenerateAcceptLanguage`, `DalamudUpdater`, `DalamudOverlayInfoProxy`, `CompatibilityTools`, `WineSettings`, `WineUtility.SystemFsyncSupport`, `WindowsSteam`/`UnixSteam`, `FileSecretProvider`/`KeychainSecretProvider`/`DummySecretProvider`, `LauncherApp`, `ImGuiBindings`, `StyleModelV1`, `LauncherClientConfig`, `AppUtil.GetAssemblyVersion/GetGitHash`, `LogInit.Setup`, `CoreEnvironmentSettings`, SDL3 C ABI (`SDL.Init`, `SDL.CreateWindow`, `SDL.CreateGPUDevice`, `SDL.ClaimWindowForGPUDevice`, `SDL.SetGPUSwapchainParameters`, `SDL.ShowWindow`, `SDL.GetWindowFlags`, `SDL.Delay`, `SDL.PollEvent`…).

## 14. Extension points / notes for maintainers

- To add a new global, prefer a get-only accessor backed by a `private static` field (the existing pattern) so that assignment stays inside this class; `Patcher` is the deliberate exception.
- Anything needing `window`/`gpuDevice` handles must live inside an `unsafe` block and must be ordered after step 10 of `Main`.
- `DALAMUD_INJECTOR_NAME` and the `// TODO: move this somewhere better` marker indicate an intended relocation out of `Program`.
- `ClearColor` here and `ImGuiBindings.clearColor` are duplicated constants — change both or none.
