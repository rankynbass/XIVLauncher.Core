# UnixGameRunner.cs — Specification

**File:** `src/XIVLauncher.Common.Unix/UnixGameRunner.cs` · 44 lines · 1476 bytes · ASCII, LF, no BOM
**Namespace:** `XIVLauncher.Common.Unix` (`:10`) — declared with the `namespace …;` shorthand
**Declaration:** `public class UnixGameRunner : IGameRunner` (`:12`; fields `:14`–`:16`, ctor `:18`–`:23`,
`Start` `:25`–`:43`)

**Role:** the fork's platform glue for "launch the game": it either hands off to the Dalamud injector
(`DalamudLauncher`) or runs the exe directly under wine.

---

## 1. Interface contract

`IGameRunner.cs:8` (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common/PlatformAbstractions/IGameRunner.cs`):

```
Process? Start(string path, string workingDirectory, string arguments,
               IDictionary<string, string> environment, DpiAwareness dpiAwareness);
```

The fork implements it verbatim (`:25`). `Process?` means "the launched process, or nothing".

| Parameter | Used at | Role |
| --- | --- | --- |
| `path` | `:37` (`new FileInfo(path)`) / `:41` (interpolated into the command) | game executable, **unix** path |
| `workingDirectory` | `:41` only | passed to `RunInPrefix`'s defaulting param |
| `arguments` | `:37` / `:41` | already-built argument string |
| `environment` | `:31`/`:35` mutation, `:37`/`:41` pass-through | caller-owned dict |
| `dpiAwareness` | **never read** | see §3 |

## 2. Behaviour

| Line(s) | Effect |
| --- | --- |
| `:27` | branch on the ctor-captured `dalamudOk` |
| `:29`–`:36` | **fork-only:** `if Linux → environment.Add("XL_PLATFORM", "Linux")`; `else if OSX → "MacOS"` |
| `:37` | `return this.dalamudLauncher.Run(new FileInfo(path), arguments, environment)` |
| `:41` | otherwise `return compatibility.RunInPrefix($"\"{path}\" {arguments}", workingDirectory, environment, writeLog: true)` |

* `XL_PLATFORM` is consumed outside this repo (the game/Dalamud side). Values are `"Linux"`/`"MacOS"`.
* Note the shape of the injection branch: `XL_PLATFORM` is added **only** when `dalamudOk` is true — a
  non-Dalamud launch never gets the variable.
* Neither branch adds the variable when the platform is neither Linux nor OSX (unreachable in practice;
  `Program.CreateCompatToolsInstance()` bails on Windows at `Program.cs:360`).

## 3. Invariants and hazards

* **`dpiAwareness` is ignored** — the parameter exists in the interface (`IGameRunner.cs:8`) and the Windows
  implementation uses it, but the Unix one reads it nowhere. Nothing warns, and DPI handling on wine is
  left entirely to wine's own settings.
* **`environment` must be non-null and mutable.** `environment.Add(...)` at `:31`/`:35` throws on a null dict;
  the fork's `LoadConfig`/`Launcher.cs:263` path always passes a freshly built one, but the signature does not
  say so. It also mutates the caller's dict, so the caller observes `XL_PLATFORM` afterwards.
* **`dalamudLauncher` may be null.** `MainPage.cs:183` constructs `new UnixGameRunner(Program.CompatibilityTools, null, false)`
  (the `null` cast is preceded by a `// FIXME` comment at `MainPage.cs:179`), which is safe only because
  `dalamudOk == false` routes to `:41`. Any future code path that sets `dalamudOk == true` on that instance
  null-derefs at `:37`.
* **`new FileInfo(path)` is a *file-path value object*, not a stat check** — a non-existent exe still
  reaches `DalamudLauncher.Run`; that class throws `BinaryNotPresentException` (see
  `lib/.../XIVLauncher.Common/Game/Launcher.cs:262` for the sibling behaviour).
* **The non-Dalamud branch passes one command string** to `CompatibilityTools.cs:113`'s overload — quoting is
  done by wine's own parser; a path containing embedded quotes is not escaped here (`$"\"{path}\" {arguments}"`
  is a naive wrap).

## 4. Construction sites

| Site | Instance purpose |
| --- | --- |
| `MainPage.cs:183` | pre-launch/"no Dalamud" runner (`null` launcher, `dalamudOk = false`) |
| `MainPage.cs:729` | `new UnixGameRunner(Program.CompatibilityTools, dalamudLauncher, dalamudOk)` for the real launch |

Both documented in `docs/XIVLauncher.Core/Components/MainPage/MainPage.cs.md`.

## 5. Upstream delta

`lib/FFXIVQuickLauncher/src/XIVLauncher.Common.Unix/UnixGameRunner.cs` is identical except: BOM removed,
trailing newline added, `using System.Runtime.InteropServices;` added (`:4`), and the eight-line
`XL_PLATFORM` block (`:29`–`:36`) inserted. There is **no** upstream equivalent of the platform variable.

## 6. Referenced by / referenced

* Referenced by: `MainPage.cs:183`, `MainPage.cs:729` (docs: `docs/XIVLauncher.Core/Components/MainPage/MainPage.cs.md`).
* References: [Compatibility/CompatibilityTools.cs.md](Compatibility/CompatibilityTools.cs.md) (§5 `RunInPrefix`),
  `DalamudLauncher` (`lib/.../XIVLauncher.Common/Dalamud/DalamudLauncher.cs:127` is the call it forwards to),
  `IGameRunner` (`lib/.../XIVLauncher.Common/PlatformAbstractions/IGameRunner.cs:8`),
  `DpiAwareness` (same dir), `RuntimeInformation`/`OSPlatform`.
