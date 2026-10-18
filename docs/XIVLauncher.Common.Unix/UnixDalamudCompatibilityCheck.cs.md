# UnixDalamudCompatibilityCheck.cs — Specification

**File:** `src/XIVLauncher.Common.Unix/UnixDalamudCompatibilityCheck.cs` · 34 lines · 1129 bytes · ASCII, LF, no BOM
**Namespace:** `XIVLauncher.Common.Unix` (`:5`, shorthand form)
**Declaration:** `public class UnixDalamudCompatibilityCheck : IDalamudCompatibilityCheck` (`:7`)
**Methods:** `EnsureCompatibility` (`:9`–`:13`), `private static EnsureArchitecture` (`:15`–`:33`)

**Role:** the unix answer to "can Dalamud run here?". On wine there is nothing to check about VC-runtime
redists, so this class reduces to a single architecture assertion.

---

## 1. Interface

`IDalamudCompatibilityCheck.cs:5` (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common/PlatformAbstractions/`):

```
public void EnsureCompatibility();                                  // :7
public class ArchitectureNotSupportedException : Exception          // :9  (ctor(message) :11–:14)
public class NoRedistsException : Exception                         // :17
```

Both exception types are **nested classes of the interface**, which is why the fork spells them
`IDalamudCompatibilityCheck.ArchitectureNotSupportedException` (`:22`, `:28`, `:31`) and why `MainPage.cs:572`/`MainPage.cs:577`
catch them by that qualified name.

## 2. `EnsureCompatibility` (`:9`–`:13`)

| Line | Content |
| --- | --- |
| `:11` | comment: `//Dalamud will work with wines built-in vcrun, so no need to check that` |
| `:12` | `EnsureArchitecture();` |

`EnsureCompatibility` can **never** throw `IDalamudCompatibilityCheck.NoRedistsException` on this platform: the redist scan
exists only in `WindowsDalamudCompatibilityCheck.cs` (lib, not compiled into this fork). Consequence for the
caller — the `catch (…NoRedistsException)` arm at `MainPage.cs:572` (body: `Log.Error(ex, "No Dalamud Redists
found")` `:574`, `throw;` `:575`) is **dead on the unix path**.

## 3. `EnsureArchitecture` (`:15`–`:33`)

| Line | Case / statement |
| --- | --- |
| `:17` | `var arch = RuntimeInformation.Architecture;` — the **process** architecture (the launcher binary), not the machine's |
| `:19` | `switch (arch)` |
| `:21`/`:22` | `case Architecture.X86:` → `throw new IDalamudCompatibilityCheck.ArchitectureNotSupportedException(Dalamud is not supported on x86 architecture.)` |
| `:24`/`:25` | `case Architecture.X64:` → `break;` — the only passing arm |
| `:27`/`:28` | `case Architecture.Arm:` → `throw …(Dalamud is not supported on ARM32.)` |
| `:30`/`:31` | `case Architecture.Arm64:` → `throw …(x64 emulation was not detected. Please make sure to run XIVLauncher with x64 emulation.)` |
| `:32`/`:33`/`:34` | closes switch / method / class |

Message strings (verbatim from the source):

| Throw | Line | Message |
| --- | --- | --- |
| x86 | `:22` | Dalamud is not supported on x86 architecture. |
| ARM32 | `:28` | Dalamud is not supported on ARM32. |
| ARM64 | `:31` | x64 emulation was not detected. Please make sure to run XIVLauncher with x64 emulation. |

Interpretation notes:

* `RuntimeInformation.ProcessArchitecture` (`:17`, provided by the `System.Runtime.InteropServices` import at `:1`) reports the
  architecture of the **running launcher binary**. An x64 binary running under Valve's WoW64-style
  emulation on an aarch64 Steam Deck reports `X64`, so the `Arm64` arm (`:30`) means "the launcher itself
  was built for ARM" — hence x64 emulation was not detected. Please make sure to run XIVLauncher with x64 emulation. mentions x64 emulation rather than the Deck. The message is written from
  the user's point of view, which makes it easy to misread as a Steam-Deck failure.
* There is **no `default:` arm** and nothing after the switch, so any other `Architecture` value
  (`Unknown`, `Ppc64`, `S390x` in the stdlib enum) falls through silently and counts as *compatible*.
* `EnsureArchitecture` is `private static` (`:15`) and takes no argument — `arch` is read inside it, so
  the check cannot be parameterised or overridden.

## 4. Consumers

| Site | Detail |
| --- | --- |
| `MainPage.cs:543` | `IDalamudCompatibilityCheck dalamudCompatCheck;` — declared with the **interface** type |
| `MainPage.cs:545`–`:558` | platform switch: `case PlatformID.Win32NT:` → `new WindowsDalamudCompatibilityCheck()` (`:549`), `case PlatformID.Unix:` → `new UnixDalamudCompatibilityCheck()` (`:554`), `default:` → `throw new NotImplementedException()` (`:558`) |
| `MainPage.cs:568`–`:581` | `try` (`:568`) → `dalamudCompatCheck.EnsureCompatibility();` (`:570`) → `catch NoRedistsException` (`:572`, dead here) → `catch ArchitectureNotSupportedException` (`:577`, `Log.Error(ex, "Architecture not supported")` `:579`, **`throw;`** `:580`) |
| `MainPage.cs:540` | `var dalamudOk = false;` — a thrown exception prevents `DalamudOk = true` at `MainPage.cs:588`, so `UnixDalamudRunner.cs` (`UnixDalamudRunner.cs`, see UnixDalamudRunner.cs.md) later takes the no-Dalamud branch at `:41` |

The unix pair is constructed at `MainPage.cs:553`/`:554` (runner + check together); see
UnixDalamudRunner.cs.md §6 for the runner side.

## 5. Upstream delta

`lib/FFXIVQuickLauncher/src/XIVLauncher.Common.Unix/UnixDalamudCompatibilityCheck.cs` is **semantically identical**: with the BOM and
blank lines ignored the diff is empty. All upstream comments, messages and `Arm`/`Arm64` handling are
preserved verbatim (see Dalamud is not supported on x86 architecture.–x64 emulation was not detected. Please make sure to run XIVLauncher with x64 emulation. above). The lib copy is the origin of the "wines built-in vcrun"
comment (`:11`).

## 6. Referenced by / referenced

* Referenced by: `MainPage.cs:554` (construction), `MainPage.cs:570` (call), `MainPage.cs:577` (catch) —
  documented in `docs/XIVLauncher.Core/Components/MainPage/MainPage.cs.md`.
* References: `IDalamudCompatibilityCheck` + nested `ArchitectureNotSupportedException`/`NoRedistsException`
  (`lib/.../PlatformAbstractions/IDalamudCompatibilityCheck.cs:5`/`:9`/`:17`), `RuntimeInformation` and `Architecture.*` (stdlib, imported at `:1`).
* Windows sibling (lib, not built here): `WindowsDalamudCompatibilityCheck.cs` — redist scan + arch scan.
* Related fork docs: `docs/XIVLauncher.Core/Components/MainPage/MainPage.cs.md`,
  `docs/XIVLauncher.Core/Support/Troubleshooting.cs.md` (the `Troubleshooting.LogTroubleshooting()` call at `MainPage.cs:561`
  that runs **before** this check).
