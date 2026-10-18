# CompatibilityTools.cs — Specification

**File:** `src/XIVLauncher.Common.Unix/Compatibility/CompatibilityTools.cs` · 277 lines · 11220 bytes · ASCII, LF, no BOM
**Namespace:** `XIVLauncher.Common.Unix.Compatibility` (`:16`) — declared with the `namespace …;` shorthand
**Declaration:** `public class CompatibilityTools` (`:18`)

**Role:** the fork's single "compatibility tool" object: it owns the wine prefix, the downloaded wine tree,
the downloaded Dxvk tree, and the wine-log file handle; every wine invocation in the launcher goes through
its `RunInPrefix` family. Named `CompatibilityTools` (plural) but there is exactly one instance
(`Program.cs:53`, `Program.cs:367`).

---

## 1. Member inventory

| Member | Line | Kind | Notes |
| --- | --- | --- | --- |
| `WINEDLLOVERRIDES` | `:20` | `private const string` | `"msquic=,mscoree=n,b;d3d9,d3d11,d3d10core,dxgi="` — prefix of the DXVK/WineD3D switch value |
| `DXVK_CLEANUP_THRESHHOLD` | `:21` | `private const uint` = 5 | note the misspelling (`THRESHHOLD`) |
| `WINE_CLEANUP_THRESHHOLD` | `:22` | `private const uint` = 5 | ditto |
| `wineDirectory` / `dxvkDirectory` / `logWriter` | `:24`/`:25`/`:26` | `private readonly` fields | assigned in ctor |
| `WineBinPath` | `:28`–`:30` | private derived property | see §2 |
| `Wine64Path` | `:31` | derived | `WineBinPath + "/wine64"` |
| `WineServerPath` | `:32` | derived | `WineBinPath + "/wineserver"` |
| `dxvkVersion` / `hudType` / `gamemodeOn` / `dxvkAsyncOn` | `:34`–`:37` | `private readonly` ctor copies | `dxvkAsyncOn` is pre-stringified `"1"`/`"0"` |
| `IsToolReady` | `:39` | `public bool { get; private set; }` | written only at `:92` |
| `Settings` | `:40` | `public WineSettings { get; private set; }` | value copy of the ctor argument (§ WineSettings is a `struct`) |
| `IsToolDownloaded` | `:41` | public derived `bool` | `File.Exists(Wine64Path) && Settings.Prefix.Exists` |
| ctor | `:43`–`:79` | | |
| `EnsureTool` | `:81`–`:93` | `public async Task` | only public entry that mutates disk |
| `DownloadTool` | `:95`–`:106` | `private async Task` | |
| `EnsurePrefix` | `:108`–`:111` | public, called only from `:89` | |
| `RunInPrefix(string, …)` | `:113`–`:120` | public overload | |
| `RunInPrefix(string[], …)` | `:122`–`:130` | public overload | |
| `MergeDictionaries` | `:132`–`:144` | `private void` | |
| `RunInPrefix(ProcessStartInfo, …)` | `:146`–`:225` | **`private`** core | the 80-line env/pipe setup |
| `GetProcessIds` | `:227`–`:233` | public | |
| `GetProcessId` | `:235`–`:238` | public | |
| `GetUnixProcessId` | `:240`–`:250` | public | |
| `UnixToWinePath` | `:252`–`:258` | public | |
| `AddRegistryKey` | `:260`–`:265` | public | |
| `Kill` | `:267`–`:276` | public | |

Public surface = 11 members; nothing in the class is `static`, so all state is per-instance.

## 2. Path derivation (the layout contract)

```
toolsFolder          = Program.storage.GetFolder("compatibilitytool")   (Program.cs:364)
wineDirectory        = toolsFolder/wine                                (ctor :51)
dxvkDirectory        = toolsFolder/dxvk                                (ctor :52)
WineBinPath (Managed)= wineDirectory / Settings.Release.Name / bin     (:28–:29)
WineBinPath (Custom) = Settings.CustomBinPath                          (:30)
Wine64Path           = WineBinPath / wine64                            (:31)
WineServerPath       = WineBinPath / wineserver                        (:32)
```

* `Settings.Release.Name` is the extracted directory name — see
  [Wine/Releases/WineStable.cs.md](Wine/Releases/WineStable.cs.md) §1 (`wine-xiv-staging-fsync-git-10.8.r0.a2ca9e4-nolsc`).
* `Program.CreateCompatToolsInstance()` pre-creates `dxvk` and `wine` subfolders at `Program.cs:365`/`:366`,
  and the ctor re-creates them only in the `Managed` branch (`:69`–`:75`).
* **`Custom` + null `WineBinaryPath`** → `Path.Combine(null, "wine64")` at `:31` throws
  (`TypeError`-style null deref). `LoadConfig` guards the config with `Config.WineBinaryPath ??= "/usr/bin"`
  (`Program.cs:137`), and `MainPage.cs:686` refuses an unset custom path before launching; the class itself
  does not.

## 3. Constructor (`:43`–`:79`), in order

| Line(s) | Action |
| --- | --- |
| `:45`–`:49` | copy the six scalars; `dxvkAsyncOn ? "1" : "0"` (`:49`) |
| `:51`/`:52` | build `wineDirectory`, `dxvkDirectory` under `toolsFolder` |
| `:54`–`:65` | **the "cleanup hack"**: if `Directory.GetFiles(dxvkDirectory).Length >= 5` → `Delete(dir, true)` then `CreateDirectory(dir)` (`:58`/`:59`); same for wine (`:63`/`:64`) |
| `:67` | `logWriter = new StreamWriter(Settings.LogFile.FullName)` — opens (truncating) `logs/wine.log` |
| `:69`–`:75` | only when `StartupType == Managed`: create the two sub-directories if missing |
| `:77`/`:78` | create `Settings.Prefix` if it does not exist |

The cleanup hack deletes **recursively** (`Delete(path, true)`): once a user has accumulated 5 wine
builds or 5 Dxvk builds, all previously downloaded trees are erased (along with the prefix-independent
extracted binaries), forcing a re-download on next run. The `// TODO … just a hack` comment at `:54`/`:55`
is the author's own admission. `Directory.GetFiles` counts entries in the directory; ≥5 is compared against
`const uint` thresholds, both 5.

Opening `logWriter` at `:67` happens on **every** construction: the fork constructs the tool at startup
(`Program.cs:263` via `CreateCompatToolsInstance()`, and again at `Program.cs:461` after `ClearTools`), so
`wine.log` is truncated each run and the handle lives for the process lifetime (see §8).

## 4. `EnsureTool` and `DownloadTool`

`EnsureTool(HttpClient, DirectoryInfo tempPath)` (`:81`–`:93`):

1. `:84` `if (!File.Exists(Wine64Path))` → log `:85` (interpolates `Settings.Release.DownloadUrl`,
   NPE risk on `Custom` — see §2/§8) → `await DownloadTool(...)` (`:86`).
2. `:89` `EnsurePrefix()` — synchronous, `RunInPrefix("cmd /c dir %userprofile%/Documents > nul").WaitForExit()`.
3. `:90` `await Dxvk.Dxvk.InstallDxvk(httpClient, Settings.Prefix, dxvkDirectory, dxvkVersion)`.
4. `:92` `IsToolReady = true` — set even if the download/verify step partially failed? No: a throw in
   `DownloadTool`/`InstallDxvk` makes the task fault *before* `:92`; the flag is only set on success.

`DownloadTool` (`:95`–`:106`):

| Line | Step |
| --- | --- |
| `:97` | `tempFilePath = tempPath/<Guid.NewGuid()>` — the file lands in the caller-supplied temp dir (`MainPage.cs:709` passes a fresh `DirectoryInfo`) |
| `:99` | `await File.WriteAllBytesAsync(tempFilePath, await httpClient.GetByteArrayAsync(Settings.Release.DownloadUrl))` — whole tarball in RAM, then on disk |
| `:100`–`:103` | `CompatUtil.EnsureChecksumMatch(tempFilePath, Settings.Release.Checksums)` else `throw InvalidDataException("SHA512 checksum verification failed")` |
| `:104` | `PlatformHelpers.Untar(tempFilePath, wineDirectory.FullName)` — extracts to `<tools>/wine/<Name>` |
| `:105` | `Log.Information("Compatibility tool successfully extracted to {Path}", …)` |
| `:106` | `File.Delete(tempFilePath)` — **skipped when the checksum check throws**, leaving a ~30 MB temp file behind |

## 5. `RunInPrefix` trio and the environment recipe

Public overloads `:113` (string command) and `:122` (string[] args) both build a `ProcessStartInfo` whose
executable is `Wine64Path`, then delegate to the private core at `:146`. Defaults:
`workingDirectory = ""`, `environment = null`, `redirectOutput = false`, `writeLog = false`, `wineD3D = false`.

The `:113` form puts the whole command line into `psi.Arguments` (single string, wine-side word splitting);
the `:122` form appends each element to `psi.ArgumentList`. Only `:113` is used with a literal command
string containing wine-internal shell text (`cmd /c dir …` at `:110`).

### 5.1 Environment assembly (`:153`–`:196`)

| Variable | Line | Value / condition |
| --- | --- | --- |
| `WINEPREFIX` | `:157` | `Settings.Prefix.FullName` |
| `WINEDLLOVERRIDES` | `:158` | `WINEDLLOVERRIDES + (ogl ? "b" : "n,b")` where `ogl = wineD3D \|\| dxvkVersion == DxvkVersion.Disabled` (`:153`) |
| `WINEDEBUG` | `:161`–`:163` | only if `!string.IsNullOrEmpty(Settings.DebugVars)` — default from ini is `"-all"` (`Program.cs:138`) |
| `XL_WINEONLINUX` | `:166` | always `"true"` (consumer: wine/Dalamud-side, not in this tree) |
| `DXVK_HUD` | `:169`–`:175` → `:182` | `None`→`"0"`, `Fps`→`"fps"`, `Full`→`"full"`, `_`→`throw new ArgumentOutOfRangeException()` |
| `DXVK_ASYNC` | `:183` | ctor-stringified `dxvkAsyncOn` |
| `WINEESYNC` / `WINEFSYNC` | `:187` / `:190` | from `Settings.SyncType` switch — exactly one, neither when a new member is added |
| `LD_PRELOAD` | `:167` read, `:177`–`:179` gamemode, `:193` write | inherits the process' `LD_PRELOAD` (`?? ""`) and appends `libgamemodeauto.so.0` **only if** `gamemodeOn` and it isn't already present; separator `:` |

`DXVK_HUD`'s `_` arm throws for a hypothetical 4th `DxvkHudType` member — reachable only if the enum
grows; today the switch is exhaustive.

### 5.2 Merge order (`:195`/`:196`)

`MergeDictionaries(StringDictionary a, IDictionary b)` overwrites existing keys and adds new ones
(`:136` nulls `b` → no-op). Applied as:

1. `psi.EnvironmentVariables` (inherited from the launching process — stdlib `ProcessStartInfo` starts as a
   copy of the parent env) ← `wineEnviromentVariables` (the launcher's recipe wins over the inherited env,
   **including** `LD_PRELOAD`, which is why the gamemode logic reads the inherited value first)
2. `psi.EnvironmentVariables` ← caller `environment` (the caller wins over the recipe)

`MergeDictionaries` mutates the map object it is given; both calls mutate the same `psi` map, so the class
never leaks a mutated `environment` argument to callers — but callers that reuse one dict across calls see
accumulation only through `psi`, not the dict itself.

### 5.3 stderr and logging (`:200`–`:225`)

* `psi.RedirectStandardOutput = redirectOutput`, `psi.RedirectStandardError = writeLog` (`:148`/`:149`),
  `UseShellExecute = false` (`:150`), `WorkingDirectory = workingDirectory` (`:151`, `""` = inherit).
* `helperProcess.ErrorDataReceived +=` a `DataReceivedEventHandler` (`:200`–`:218`) that ignores empty lines
  (`:202` early return), then writes each line to **both** `logWriter` and `Console.Error` (`:207`/`:208`).
* The `catch … when (ArgumentOutOfRangeException || OverflowException || IndexOutOfRangeException)` guard
  (`:210`–`:217`) exists because wine logs contain absurdly long lines that break the writer; the logging
  of the error itself is commented out (`:215`/`:216`), so those lines vanish silently.
* `helperProcess.Start()` at `:220`; `BeginErrorReadLine()` only when `writeLog` (`:221`/`:222`) — i.e. stderr
  is only drained into the log when the caller asked for it. `Start()` is called on a locally built
  `Process helperProcess = new()` (`:198`).
* Return value: the started `Process`, whose stdout/stderr handles the caller may read
  (`StandardOutput.ReadToEnd()`/`ReadLine()` at `:230`, `:243`, `:256`, `UnixDalamudRunner.cs:79`).
  Callers that don't drain risk blocking on a full pipe buffer — see §8.

## 6. Wine process bookkeeping helpers

| Member | Implementation |
| --- | --- |
| `GetProcessIds(string)` (`:227`) | runs `winedbg --command "info proc"` with `redirectOutput: true`, splits on `\n` (drop empty), keeps lines containing `executableName`, parses **`line.Substring(1, 8)` as hex** → `int[]` |
| `GetProcessId(string)` (`:235`) | `GetProcessIds(...).FirstOrDefault()` → `null` when nothing matched (nullable int → `int?` boxing; return type is `int`, so this is a null-to-int assignment — see §8) |
| `GetUnixProcessId(int winePid)` (`:240`) | runs `winedbg --command "info procmap"`; `"syntax error\n"` in output → `0` (`:245`); skips the header line (`.Skip(1)` `:244`), matches lines whose `Substring(1,8)` hex equals `winePid`, then takes `Substring(10,8)` hex as the unix pid; returns `FirstOrDefault()` |
| `UnixToWinePath(string)` (`:252`) | runs `winepath --windows <path>`, returns the **last** non-empty line (`LastOrDefault()` → nullable `string`) |
| `AddRegistryKey(key, value, data)` (`:260`) | `reg add <key> /v <value> /d <data> /f` then `WaitForExit()`; **no caller in the fork** |
| `Kill()` (`:267`) | `Process.Start(wineserver -k)` with only `WINEPREFIX` set — bypasses the whole `:146` recipe (no dll overrides, no HUD, no sync vars), i.e. it is not the same environment as gameplay runs |

The hex-offset parsing (`Substring(1,8)` / `Substring(10,8)`) is welded to winedbg's column layout; a wine
build that prints a different `info procmap` table makes these throw `FormatException`/`ValueError`, which
`UnixDalamudRunner.cs` does **not** catch (only `JsonReaderException` is caught, at `:112`).

## 7. Consumers (fork)

| Call site | Member used |
| --- | --- |
| `Program.cs:367` | construction (see §9 for the arguments) |
| `Program.cs:263`, `:461` | `CreateCompatToolsInstance()` → constructs |
| `SettingsTabWine.cs:102`, `:134` | `IsToolDownloaded` gate |
| `SettingsTabWine.cs:112` | `Settings.Prefix.FullName` (open prefix in browser) |
| `SettingsTabWine.cs:119` / `:126` | `RunInPrefix("winecfg")` / `RunInPrefix("explorer")` (`:113` overload) |
| `SettingsTabWine.cs:131` | `Kill()` — the only live `Kill()` call |
| `MainPage.cs:183` / `:729` | handed to `new UnixGameRunner(Program.CompatibilityTools, …)` |
| `MainPage.cs:553` | handed to `new UnixDalamudRunner(…, …)` |
| `MainPage.cs:709` | `await Program.CompatibilityTools.EnsureTool(Program.HttpClient, tempPath)` |
| `MainPage.cs:733` | `UnixToWinePath(App.Settings.GameConfigPath!.FullName)` |
| `UnixDalamudRunner.cs:35`–`:41`, `:100` | `UnixToWinePath` ×7 (via `Parallel.Invoke`), `GetUnixProcessId` |
| `UnixGameRunner.cs:41` | `RunInPrefix(..., writeLog: true)` no-Dalamud fallback |

Never used anywhere: `IsToolReady` (written, never read), `GetProcessIds`/`GetProcessId`,
`AddRegistryKey`, `EnsurePrefix` (internal only), the `string[]` overload is used by `AddRegistryKey`/`Kill`
siblings but not by fork UI code paths (winecfg/explorer use the string form).

## 8. Invariants and hazards

* **`Settings.Release` may be null** (`Custom` startup type) while `:28`/`:41`/`:85`/`:99` read it
  unconditionally — guarded at `:28` by the `StartupType` test, unguarded at `:85` (log line) and `:99`
  (checksum). Reachable if `IsToolDownloaded` is false with a custom wine: e.g. the configured
  `CustomBinPath` exists but has no `wine64` (checked by `MainPage.cs:691` only on the launch path,
  not on the `SettingsTabWine.cs:102` "download" action).
* **`logWriter` is a single shared handle** (`:26`, `:67`, `:211`): concurrent `RunInPrefix` calls
  (`UnixDalamudRunner.cs:34`–`:42` fires seven `UnixToWinePath` calls, each spawning wine) append
  through the same `StreamWriter` without a lock. `StreamWriter` is not thread-safe → interleaved
  writes; the `catch` at `:210` swallows exactly the resulting `IndexOutOfRange`/`ArgumentOutOfRange`
  symptoms, which is likely why that guard exists.
* **`GetProcessId` returns `int` but can produce `null`** (`:237` `FirstOrDefault()`): reading the
  result of a failed match assigns null into a non-nullable `int` → runtime null-coalescing trap, not a
  `None` value. No fork code calls it, so this is latent.
* **`UnixToWinePath` is nullable** (`LastOrDefault()`): `UnixDalamudRunner.cs` upstream guarded each
  result with `?? throw new Exception("Invalid … path")`; the fork dropped those guards
  (see [../UnixDalamudRunner.cs.md](../UnixDalamudRunner.cs.md) §4 note).
* **`Kill()` does not wait**: `Process.Start(psi)` (`:275`)-style fire-and-forget; the UI at
  `SettingsTabWine.cs:131` proceeds assuming wineserver acknowledged `-k`.
* **`EnsurePrefix` blocks synchronously** (`:110` `WaitForExit()`) inside the async `EnsureTool`; with the
  default `WINEDEBUG=-all` it is quiet, but a wine prefix whose first `cmd.exe` run fails still returns
  normally (exit status is ignored) — so "prefix bootstrapped" is not actually verified.
* **Cleanup thresholds fire at construction, not on demand** — deleting all five wine builds is
  destructive even when the launcher is merely starting up, and `Program.cs:461` re-constructs the
  tool right after `ClearTools` deletes the folders (`Program.cs:456`–`:461`).

## 9. Construction arguments (`Program.cs:367`)

```
new CompatibilityTools(wineSettings,                       /* WineSettings, Program.cs:363 */
                       Config.DxvkVersion ?? DxvkVersion.Stable,
                       Config.DxvkHudType,                 /* non-nullable ini enum */
                       Config.GameModeEnabled ?? false,
                       Config.DxvkAsyncEnabled ?? true,
                       toolsFolder)                        /* storage/compatibilitytool */
```

Note `dxvkVersion` is captured **once** here; changing the ini value later (UI) does not re-construct the
tool, so DXVK choice changes require a restart. Same for `hudType`, `gamemodeOn`, `dxvkAsyncOn`
(`SettingsTabWine.cs` writes into `Config.*` only).

## 10. Upstream delta

Upstream `lib/FFXIVQuickLauncher/src/XIVLauncher.Common.Unix/Compatibility/CompatibilityTools.cs` vs this file:

| Upstream | Fork |
| --- | --- |
| `#if FLATPAK` / `#warning THIS IS A FLATPAK BUILD!!!` (`:12`–`:14`) and `#if WINE_XIV_ARCH_LINUX`-style URL constants | removed; distro detection moved to `CompatUtil.GetWineIdForDistro()` (runtime) |
| tool subdir named `beta` (`this.toolDirectory = …/"beta"`) | `wine` (`:51`) |
| ctor took `HttpClient client` and `bool? gamemodeOn, bool? dxvkAsyncOn` | no client field (passed per `EnsureTool` call), non-nullable bools; adds `DxvkVersion` parameter |
| one wine build (8.5), one hard-coded Dxvk build (1.10.1) | release objects via [Dxvk/Dxvk.cs.md](Dxvk/Dxvk.cs.md) + `Wine/Releases/*` |
| no checksum verification | `CompatUtil.EnsureChecksumMatch` (`:100`) |
| no cleanup hack | added `DXVK/WINE_CLEANUP_THRESHHOLD` block (`:54`–`:65`) |
| `#if FLATPAK_NOTRIGHTNOW` block around part of `RunInPrefix` (`:195`–`:213`) | removed; flatpak handled by distro detection instead |
| — | `XL_WINEONLINUX`, `DXVK_ASYNC`, gamemode `LD_PRELOAD` handling and the long-line `catch` guard are fork-era additions |

## 11. Referenced

* Sibling specs: [CompatUtil.cs.md](CompatUtil.cs.md) (§§2–4), [Dxvk/Dxvk.cs.md](Dxvk/Dxvk.cs.md),
  [Wine/WineSettings.cs.md](Wine/WineSettings.cs.md), [Wine/WineUtility.cs.md](Wine/WineUtility.cs.md).
* Cross-tree: `docs/XIVLauncher.Core/Program.cs.md` (field `:53`, `CreateCompatToolsInstance`
  `:358`–`:368`, startup call `:263`, `ClearTools` `:456`–`:461`),
  `docs/XIVLauncher.Core/Components/MainPage/MainPage.cs.md` (runner construction `:183`/`:553`/`:729`,
  `EnsureTool` `:709`, `UnixToWinePath` `:733`),
  `docs/XIVLauncher.Core/Components/SettingsPage/Tabs/SettingsTabWine.cs.md` (UI actions
  `:102`–`:143`), [../UnixGameRunner.cs.md](../UnixGameRunner.cs.md),
  [../UnixDalamudRunner.cs.md](../UnixDalamudRunner.cs.md),
  `docs/XIVLauncher.Core/Configuration/ILauncherConfig.cs.md` (`DxvkVersion`/`DxvkHudType`/Wine* keys).
* Upstream: `lib/FFXIVQuickLauncher/src/XIVLauncher.Common.Unix/Compatibility/CompatibilityTools.cs`.
