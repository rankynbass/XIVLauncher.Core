# UnixDalamudRunner.cs — Specification

**File:** `src/XIVLauncher.Common.Unix/UnixDalamudRunner.cs` · 118 lines · 4826 bytes · ASCII, LF, no BOM
**Namespace:** `XIVLauncher.Common.Unix` (`:16`, shorthand form)
**Declaration:** `public class UnixDalamudRunner : IDalamudRunner` (`:18`)
**Members:** fields `:20`–`:21`, ctor `:23`–`:27`, `Run` `:29`–`:117`

**Role:** the wine implementation of the Dalamud injector handshake. It translates unix paths into wine
paths by spawning `winepath` (in parallel), runs the injector dll as a *wine-inside-wine* process, reads one
line of JSON off its stdout, and converts the wine-internal game handle back into a real unix `Process`.

---

## 1. Interface and signature

`IDalamudRunner.cs:10` (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common/PlatformAbstractions/`) declares
`Process? Run(FileInfo runner, bool fakeLogin, bool noPlugins, bool noThirdPlugins, FileInfo gameExe,
string gameArgs, IDictionary<string,string> environment, DalamudLoadMethod loadMethod,
DalamudStartInfo dalamudStartInfo);` — the fork's `:29` matches it, naming the last parameter
**`startInfo`** instead of `dalamudStartInfo`.

| Parameter | Read at |
| --- | --- |
| `runner` | `:49` (`runner.FullName`, quoted) |
| `gameExe` | `:35` (`gameExe.FullName` → `gameExePath`) |
| `loadMethod` | `:51` (mode selection), `:63` (ACLonly) |
| `fakeLogin` / `noPlugins` / `noThirdPlugins` | `:66` / `:69` / `:72` |
| `gameArgs` | `:76` (appended unconditionally after `"--"`) |
| `environment` | `:44`/`:45` **mutation**, then `:78` pass-through |
| `startInfo` | `:37`–`:41` (five path fields, mutated in place), `:53`–`:59`, `:60` |

`DalamudStartInfo` fields read here: `WorkingDirectory`, `ConfigurationPath`, `LoggingPath`,
`PluginDirectory`, `AssetDirectory` (all also **written** by `:37`–`:41`), `Language`,
`DelayInitializeMs`, `TroubleshootingPackData`.

## 2. Fields and construction

| Line | Content |
| --- | --- |
| `:20` | `private readonly CompatibilityTools compatibility;` — the **concrete** class, not an interface (coupling, see §6) |
| `:21` | `private readonly DirectoryInfo dotnetRuntime;` |
| `:23`–`:27` | ctor: assigns both (`:25`, `:26`) |

Construction site (only one): `MainPage.cs:553` —
`new UnixDalamudRunner(Program.CompatibilityTools, Program.DotnetRuntime)` in the `case PlatformID.Unix:` arm
(`:552`) of the switch at `MainPage.cs:545`; the Windows arm is `:548`, `default:` throws
`NotImplementedException` at `:558`. `Program.DotnetRuntime` is the field at
`Program.cs:69` (`docs/XIVLauncher.Core/Program.cs.md` §fields, `storage.GetFolder("runtime")`) — a
`DirectoryInfo` getter that does **not** create the folder, and `:36` later reads
`dotnetRuntime.FullName` with no existence check.

## 3. Path translation (`Parallel.Invoke`, `:34`–`:42`)

Locals are seeded first: `:31` `var gameExePath = ""`, `:32` `var dotnetRuntimePath = ""`, then seven
lambdas:

| Line | Lambda |
| --- | --- |
| `:35` | `gameExePath = compatibility.UnixToWinePath(gameExe.FullName)` |
| `:36` | `dotnetRuntimePath = compatibility.UnixToWinePath(dotnetRuntime.FullName)` |
| `:37` | `startInfo.LoggingPath = UnixToWinePath(startInfo.LoggingPath)` |
| `:38` | `startInfo.WorkingDirectory = …` |
| `:39` | `startInfo.ConfigurationPath = …` |
| `:40` | `startInfo.PluginDirectory = …` |
| `:41` | `startInfo.AssetDirectory = …` |

Facts:

* `Parallel.Invoke` blocks until all seven finish, so the writes are visible after `:42`. Each lambda
  touches a **distinct** target, so there is no write–write race between the threads.
* Every lambda spawns a wine process: `UnixToWinePath` (`CompatibilityTools.cs:252`–`:258`) runs
  `winepath --windows <path>` (`:254`) through `RunInPrefix` and returns
  `…Split('\n', RemoveEmpty).LastOrDefault()` (`:257`) — i.e. **`null` is possible**, and this fork
  assigns it unchecked (see hazard below).
* **Upstream guarded exactly the five `startInfo` assignments** with
  `?? throw new Exception("Invalid … path");` (lib `UnixDalamudRunner.cs:34`–`:38`); the fork removed all five.
  Consequence: `null` flows into `startInfo.*` and then into the argument builders (`§4`), producing the
  literal text `null` inside `--dalamud-working-directory="null"` etc. The launch proceeds with a
  mis-configured injector instead of failing — a silent-failure regression versus upstream.
* `startInfo` is **mutated in place** (`:37`–`:41`), so the caller's `DalamudStartInfo` object now holds
  wine paths, not unix paths. The lib `DalamudLauncher.cs` builds it once (`DalamudLauncher.cs:31` ctor
  stores it) and uses the same object afterwards; a retry of `Run` on the same object would double-translate.
* The seven concurrent wine runs also write into the shared `CompatibilityTools.logWriter`
  (`CompatibilityTools.cs:26`, opened `:67`, used `:211`) with no lock — see
  [Compatibility/CompatibilityTools.cs.md](Compatibility/CompatibilityTools.cs.md) §8 for that hazard write-up.
* Cost: this is seven wine process starts (plus `winedbg`/`winepath` server traffic) on every launch.

## 4. Argument assembly (`:47`–`:76`)

`launchArguments` (`:47`) is an inferred list of `string` initialised with twelve elements
(`:49`–`:60`), then conditionally appended to. Constants and builders come from
`lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Dalamud/DalamudInjectorArgs.cs:5`–`:20`.

| Line | Element | Builder (lib line) |
| --- | --- | --- |
| `:49` | `"\"{runner.FullName}\""` — the injector runner, quoted | — |
| `:50` | `LAUNCH` (`launch`) | `DalamudInjectorArgs.cs:5` |
| `:51` | `Mode(loadMethod == DalamudLoadMethod.EntryPoint ? "entrypoint" : "inject")` | `:10` |
| `:52` | `Game(gameExePath)` | `:11` |
| `:53`–`:57` | `WorkingDirectory`, `ConfigurationPath`, `LoggingPath`, `PluginDirectory`, `AssetDirectory` | `:14`–`:17` |
| `:58` | `ClientLanguage((int)startInfo.Language)` | `:18` |
| `:59` | `DelayInitialize(startInfo.DelayInitializeMs)` | `:19` |
| `:60` | `TsPackB64(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(startInfo.TroubleshootingPackData)))` | `:20` |
| `:64` | `WITHOUT_DALAMUD` (`--without-dalamud`) | `:6`, gated by `loadMethod == DalamudLoadMethod.ACLonly` (`:63`) |
| `:67` | `FAKE_ARGUMENTS` | `:7`, gated by `fakeLogin` (`:66`) |
| `:70` | `NO_PLUGIN` | `:8`, gated by `noPlugins` (`:69`) |
| `:73` | `NO_THIRD_PARTY` | `:9`, gated by `noThirdPlugins` (`:72`) |
| `:75`/`:76` | `"--"` then `gameArgs` | unconditional |

Notes:

* **`loadMethod` is a three-way enum** (`DalamudLoadMethod.cs` in
  `lib/.../XIVLauncher.Common/Dalamud/`: `EntryPoint`, `DllInject`, `ACLonly`) but the mode builder at
  `:51` is a **two-way ternary**: `DllInject` *and* `ACLonly` both become `--mode=inject`; the ACLonly
  distinction is carried only by the `--without-dalamud` flag at `:64`. There is **no early return** on
  the Unix path — unlike `WindowsDalamudRunner`, the injector still starts (that is how the ACL-only fix works under wine).
* `HandleOwner` (`DalamudInjectorArgs.cs:12`) is **not used** by this implementation (Windows uses it).
* `gameArgs` is appended unconditionally (`:76`) — an empty `gameArgs` yields a trailing `--` plus an empty
  element after `string.Join` (`:78`).
* `--without-dalamud`/`--fake-arguments`/`--no-plugin`/`--no-3rd-plugin` are order-dependent in the joined
  string: they always come *after* the twelve base elements but *before* `"--"` (the game-args separator),
  which is what the injector expects.

## 5. Process start and the JSON handshake (`:78`–`:117`)

| Line(s) | Step |
| --- | --- |
| `:78` | `compatibility.RunInPrefix(string.Join(" ", launchArguments), environment: environment, redirectOutput: true, writeLog: true)` — three named args; `workingDirectory` and `wineD3D` take their defaults (`CompatibilityTools.cs:113` declares `workingDirectory = ""`, `redirectOutput = false`, `writeLog = false`, `wineD3D = false`) |
| `:79` | `var output = dalamudProcess.StandardOutput.ReadLine();` — **first line only**, blocking, no timeout |
| `:81`/`:82` | `if (output == null) throw new DalamudRunnerException("An internal Dalamud error has occured");` (`DalamudRunnerException.cs:5` in `lib/.../XIVLauncher.Common/Dalamud/`) |
| `:84` | `Console.WriteLine(output);` — the raw handshake line to the terminal |
| `:86`–`:95` | anonymous `Thread` (`:95` `.Start()`): `while (!EndOfStream)` (`:88`) → `ReadLine()` (`:90`) → null-check (`:91`) → `Console.WriteLine` (`:92`) |
| `:97`–`:116` | `try` / `catch (JsonReaderException ex)` |
| `:99` | `var dalamudConsoleOutput = JsonConvert.DeserializeObject<DalamudConsoleOutput>(output);` (Newtonsoft, imported `:8`) |
| `:100` | `var unixPid = compatibility.GetUnixProcessId(dalamudConsoleOutput.Pid);` |
| `:102`–`:106` | `if (unixPid == 0)` → `Log.Error("Could not retrive Unix process ID, this feature currently requires a patched wine version")` (`:104`) → `return null` (`:105`) |
| `:108` | `var gameProcess = Process.GetProcessById(unixPid);` |
| `:109` | `Log.Verbose($"Got game process handle {gameProcess.Handle} with Unix pid {gameProcess.Id} and Wine pid {dalamudConsoleOutput.Pid}")` |
| `:110` | `return gameProcess;` |
| `:114`/`:115` | `Log.Error(ex, $"Couldn't parse Dalamud output: {output}")` → `return null` |

`DalamudConsoleOutput` (`lib/.../XIVLauncher.Common/Dalamud/DalamudConsoleOutput.cs:5`) is a
`sealed class` with `[JsonProperty("pid")] int Pid` (`:7`/`:8`) and
`[JsonProperty("handle")] long Handle` (`:10`/`:11`) — i.e. the JSON `pid` is the **wine-internal** value
fed to `GetUnixProcessId`, and `Handle` is the injector-side long (unused on this path, unlike Windows').

`GetUnixProcessId` (`CompatibilityTools.cs:240`–`:250`) runs `winedbg --command "info procmap"`
(`:242`), returns `0` when the output contains `syntax error\n` (`:244`/`:245`), else hex-parses the
wine-pid column at chars 1–8 (`:247`) and the unix-pid column at chars 10–8 (`:248`), and
`FirstOrDefault()` (`:249`) — which yields `null` (→ `0` through the `int` return) when nothing matches.
That is why the fork's gate is `unixPid == 0` (`:102`) and why the message says a **patched wine** is
required: un-patched wine's `procmap` either errors (`:244`) or lacks the second column.

Hazards:

* **The drain thread (`:86`–`:95`) is unjoinable and uncancelled**: everything the injector prints after
  the first line is echoed to the terminal from an anonymous thread while the launcher keeps working.
  Nothing here re-reads that stream, and `StandardOutput` is consumed **twice** in one process object —
  first `:79`, then `:90` — so ordering matters: the thread must not start before `:79` (it doesn't).
* **`JsonConvert.DeserializeObject` returning `null` is unguarded**: upstream appended
  `?? throw new Exception("Deserialized Dalamud output was null")` (lib `UnixDalamudRunner.cs:95`); the fork dropped it,
  so a handshake of literal `null` (or `{}`) faults at `:100`/`:101` with a `NullPointer`-style error that
  the `JsonReaderException` catch (`:112`) does **not** match — the exception escapes `Run` and faults the
  caller instead of returning `null`.
* **`catch (JsonReaderException)` is narrow**: any other deserialization fault (bad field type,
  `int.Parse` inside `GetUnixProcessId` at `CompatibilityTools.cs:247`/`:248` on malformed `winedbg`
  output, `Process.GetProcessById` on a dead pid at `:108`) also escapes.
* Two `null` return paths (`:105`, `:115`) plus the throwing path (`:82`); callers must handle all three
  (§6).

## 6. Consumers and contracts

| Site | Note |
| --- | --- |
| `MainPage.cs:553` | only construction site (docs: `docs/XIVLauncher.Core/Components/MainPage/MainPage.cs.md`) |
| `MainPage.cs:563` | `new DalamudLauncher(dalamudRunner, Program.DalamudUpdater, …)` — the lib launcher that owns this runner (`DalamudLauncher.cs:17` field, ctor `:31`) |
| `MainPage.cs:588` | `dalamudOk = dalamudLauncher.HoldForUpdate(App.Settings.GamePath) == DalamudLauncher.DalamudInstallState.Ok;` |
| `lib/…/DalamudLauncher.cs:127` | `var process = this.runner.Run(this.updater.Runner, …)` — comment at `:123` ("Can be null for custom runners"); the result is returned as-is at `:134` |
| `docs/XIVLauncher.Core/Components/MainPage/MainPage.cs.md` | `dalamudOk` gating: a `null`/thrown result leaves `dalamudOk == false` (`MainPage.cs:540` init) so the launch falls back to the no-Dalamud branch in `StartGameAndAddon` (`UnixGameRunner.cs:41`) |

The concrete-class coupling (`:20`) means this class is tied to `XIVLauncher.Common.Unix.Compatibility`
(and therefore to `WineSettings`/`Releases`); the Windows sibling `WindowsDalamudRunner` takes the abstract
`CompatibilityTool` interface instead. See
[Compatibility/CompatibilityTools.cs.md](Compatibility/CompatibilityTools.cs.md) §2 for the ctor lines and
§9 for the same coupling on the wine-settings side.

## 7. Upstream delta (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common.Unix/UnixDalamudRunner.cs`)

Normalized diff (BOM, blank lines ignored) shows exactly five changes:

1. `:37`–`:41`: five `?? throw new Exception("Invalid … path")` guards removed (see §3).
2. `:84`: upstream logged the handshake line through Serilog
   (`Log.Information("[DALAMUD] {Log}", dalamudOutput)`, lib `:81`) → fork writes it to
   `Console.WriteLine(output)` (`:84`).
3. `:92`: same replacement inside the drain thread (upstream lib `:89`).
4. `:99`: upstream's `?? throw new Exception("Deserialized Dalamud output was null")` removed (lib `:95`).
5. Local renames only: `dalamaudOutput` → `output` (`:79`, `:81`, `:99`, `:114`), and the drain-thread local
   `logOutput` → `output` (shadowing, `:90`). No semantic change.