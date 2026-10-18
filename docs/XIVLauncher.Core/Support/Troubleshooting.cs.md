# Troubleshooting.cs — Specification

**File:** `src/XIVLauncher.Core/Support/Troubleshooting.cs` · 206 lines · 7750 bytes
**Namespace:** `XIVLauncher.Core.Support` · **Declaration:** `public static class Troubleshooting`
**Role:** Log-side telemetry — one-shot Serilog markers (`TROUBLESHXLTING:` / `LASTEXCEPTION:`) carrying base64'd JSON snapshots of config, game versions and index integrity

---

## 1. Purpose

`Troubleshooting` exists so a bug report can be reconstructed from `logs/launcher.log` alone: each notable moment emits one line, `PREFIX:<base64 of UTF-8 JSON>`, which maintainers decode server-side (Kamori-style) or by hand. The same JSON string is *also* handed to Dalamud as crash-report context (`Support/PackGenerator.cs.md` zips it as `trouble.json`).

Usings: `System.Text` (base64/UTF-8), `Newtonsoft.Json`, `Serilog` (bare `Log.*`), `XIVLauncher.Common`, `.Dalamud`, `.Game`, `.Util` (`IntegrityCheck`, `Repository`, `PlatformHelpers`, `ClientLanguage`, `DpiAwareness`, `Platform`). `DateTime.Now` and `Path`/`FileInfo` come from the implicit usings (`<ImplicitUsings>enable</ImplicitUsings>` in the csproj, `net10.0`).

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `LastException` | `public static Exception?` accessor with `get; private set;` (`:22`) | written only by `LogException`; **never written in practice** (§7) |
| `LogException(exception, context)` | `public static void` (`:29`–`:53`) | emits `LASTEXCEPTION:` — **no call site in the fork** (§7) |
| `LogTroubleshooting()` | `internal static void` (`:58`–`:69`) | emits `TROUBLESHXLTING:`; swallows all failures |
| `GetTroubleshootingJson()` | `internal static string` (`:71`–`:144`) | builds the payload; **not** wrapped in its own try/catch |
| `ExceptionPayload` | `private class` (`:146`–`:153`) | `When`, `Info`, `Context` — all with `{ get; set; }` accessors; `Info`/`Context` default `""` |
| `TroubleshootingPayload` | `private class` (`:155`–`:204`) | 21 serialised fields + inline `enum IndexIntegrityResult` (`:193`–`:201`, 6 members) |

Both payload classes are `private class` **inside a `static class`** — the C# 13 form, so they are implicitly static and hoisted; that is why every reference spells the enum as `TroubleshootingPayload.IndexIntegrityResult.Success` (`:75`, `:81`, `:88`–`:91`, `:98`): the inline `enum` is a *type* declared in the payload class's scope, not a member.

## 3. Log markers

```
TROUBLESHXLTING:eyJX…      (LogTroubleshooting, :63)
LASTEXCEPTION:eyJX…        (LogException, :47)
```

* `TROUBLESHXLTING` is an intentional (upstream-blessed) mangling of "TROUBLESHOOTING" — the same string appears in `lib/FFXIVQuickLauncher/src/XIVLauncher/Support/Troubleshooting.cs:137`. External tooling keys on it: **do not "fix the spelling"**, that breaks decoders.
* The payload is `Convert.ToBase64String(Encoding.UTF8.GetBytes(json))`, so a decoder is `echo <b64> | base64 -d` (single line, no wrapping — Serilog's `Information` message is one line; the JSON is single-line too).
* `LogTroubleshooting` wraps everything in `try { … } catch (Exception ex) { Log.Error(ex, "Could not print troubleshooting"); }`, i.e. a failure to build the snapshot is itself logged, and the launcher keeps booting. Failures are rare but not hypothetical (§5, integrity check hits the network and the filesystem).

## 4. Payload shape (`GetTroubleshootingJson` → `JsonConvert.SerializeObject(payload)`)

`TroubleshootingPayload`'s member names are the JSON keys (no `[JsonProperty]` renaming, unlike the `Style` specs' short keys). Fields and how they are filled (`:115`–`:141`):

| Key | Source | Notes |
| --- | --- | --- |
| `When` | `DateTime.Now` | local-time, not ISO-8601-normalised in JSON (Newtonsoft emits `"yyyy-MM-ddTHH:mm:ss.……"`) |
| `IsAutoLogin`, `IsUidCache`, `DalamudEnabled` | `Program.Config.…GetValueOrDefault()` | `bool?` → **`false` when unset** |
| `DalamudLoadMethod` | `…GetValueOrDefault()` | `DalamudLoadMethod?` → **`EntryPoint`** (first member, `lib/…/DalamudLoadMethod.cs:6`) when unset — *not* the `DllInject` default `MainPage.cs:564` passes to `DalamudLauncher` (ctor call spans `:563`–`:566`) |
| `DalamudInjectionDelay` | `Program.Config.DalamudLoadDelay` | field is `decimal`, config is `int` → widening, JSON emits `10000` (no `.0`) |
| `EncryptArguments` | `…IsEncryptArgs.GetValueOrDefault(**true**)` | the only `true`-defaulted flag; matches `../Program.cs.md` §8 (`IsEncryptArgs ??= true`) |
| `SteamIntegration` | **never assigned** | always `false` in the fork — see §7 |
| `LauncherVersion` | `AppUtil.GetAssemblyVersion()` | see `../AppUtil.cs.md` |
| `LauncherHash` | `AppUtil.GetGitHash() ?? "<unavailable>"` | fork adds the fallback the upstream lacks |
| `Official` | `AppUtil.GetBuildOrigin() == "goatcorp/XIVLauncher.Core"` | `AppUtil.cs:37-41` reads the `BuildOrigin` assembly metadata attribute — a *self-attestation* marker for repackaged builds |
| `DpiAwareness` | `…GetValueOrDefault()` | `DpiAwareness?` → **`Aware`** (first member, `lib/…/DpiAwareness.cs:5`) when unset; the *configured* default is `Unaware` (`../Program.cs.md` §8) — a decoded `Aware` therefore means unset-in-the-ini, not user-chosen-`Aware` |
| `Platform` | `PlatformHelpers.GetPlatform()` | `Platform` enum from `XIVLauncher.Common` |
| `ObservedGameVersion`, `ObservedEx1..Ex5Version` | `Repository.<X>.GetVer(gamePath)` | 6 forward-check reads |
| `BckMatch` | all six `GetVer(x)` == `GetVer(x, true)` | 6 back-check reads, conjunctive (`:137`–`:138`) |
| `IndexIntegrity` | `IndexIntegrityResult` (see §5) | |

Because every string field defaults to `string.Empty` and every bool to `false`, an unassigned field still **appears** in the JSON — a missing key in a decoded payload means an older launcher build, not a skipped computation.

## 5. Index integrity and version observation (`:73`–`:112`)

* `var gamePath = Program.Config.GamePath!;` — the C# 13 bang-pattern assertion. It only *narrows* the static type; if the value really is `null`, the next line's `gamePath.Exists` throws `NullPointerException` into the caller's catch at `:96`–`:99` → `IndexIntegrityResult.Exception` (payload still emitted, versions become `BASE_GAME_VERSION` via `GetVer`'s missing-file path).
* Gate: `!gamePath.Exists || !gamePath.GetDirectories().Any(x => x.Name == "game")` → `NoGame`. The `"game"` probe is a **prefix-layout sanity check**, mirroring `SettingsTabGame.cs.md` §GamePath validity (a config path that is the prefix root itself, not the `…/game` sub-tree, is reported as "no game").
* `IntegrityCheck.CompareIntegrityAsync(Program.HttpClient, null, gamePath, true).Result` — `onlyIndex = true`, no progress, and a **synchronous `.Result` on a fresh task**. `lib/FFXIVQuickLauncher/src/XIVLauncher/Support/Troubleshooting.cs:66-69` wraps the identical call in a 300 ms `CancellationTokenSource`; the fork dropped that wrapper, so a slow/dead network **stalls the frame indefinitely**. Consequence for callers: `MainPage.cs:61` (ctor) delays startup by the full download, and `:561` delays the game launch.
* Mapping is a **switch expression** on `IntegrityCheck.CompareResult` (`:87`–`:93`) over the upstream enum (`lib/…/IntegrityCheck.cs:30-36`: `Valid`, `Invalid`, `ReferenceNotFound`, `ReferenceFetchFailure`) — three explicit arms map to the payload's own enum, `_ => integrity` keeps `Success` for `Valid`; the surrounding `catch (Exception)` records `Exception`. The fork's payload enum (6 members) is **not** the upstream enum: `NoGame`/`Exception`/`Success` are local, `Failed` replaces upstream's `Invalid`.
* Twelve `GetVer` calls (`:101`–`:112`) resolve through `RepoExtensions` **extension members** (`lib/…/Repository.cs:67`, `public static string GetVer(this Repository repo, …)`) on the `Repository` enum; a missing/blank version file yields `Constants.BASE_GAME_VERSION`, so `BckMatch` is `true` for a never-patched game — `BckMatch == false` means forward/back version files genuinely disagree, i.e. a half-migrated or manually edited prefix.

## 6. Call sites

| Site | Why |
| --- | --- |
| `../Components/MainPage/MainPage.cs:61` | end of the `MainPage` ctor — one snapshot per app construction |
| `../Components/MainPage/MainPage.cs:561` | `StartGameAndAddon`, immediately before the `DalamudLauncher` is built |
| `../Components/MainPage/MainPage.cs:566` | `GetTroubleshootingJson()` passed as `DalamudLauncher`'s `troubleshootingData`; the lib stores it (`…/Dalamud/DalamudLauncher.cs:45`) and emits it as `TroubleshootingPackData` (`:102`) — the in-game crash-report channel |
| `PackGenerator.cs:17` | the `.tspack`'s `trouble.json` member re-invokes `GetTroubleshootingJson()` — so a pack's snapshot is **newer** than the log's last `TROUBLESHXLTING:` line |

`LogTroubleshooting` runs **twice per launch session** (ctor + launch), so `grep -c TROUBLESHXLTING logs/launcher.log` is a session count, not a 1.

## 7. Invariants and hazards

* **`LogException` is unreachable.** No call site exists in `src/XIVLauncher.Core` (upstream wires it in `Program.cs:59` via a Serilog log-line listener, `OnSerilogLogLine` at `lib/FFXIVQuickLauncher/src/XIVLauncher/Program.cs:54-60`, which forwards *any* logged exception's `Exception` field), so `LastException` is always `null` and the `LASTEXCEPTION:` marker never appears in the fork's logs. Extension point: the fork's failure paths currently only `Environment.Exit(0)` / `Log.Error` — see `../Components/MainPage/MainPage.cs.md` — so wiring `LogException(ex, "…")` into those is the natural way to make the channel live again, keeping `LastException` as the "most recent" pointer.
* **`LastException` is process-wide mutable state** on a `static class` (no synchronisation, and the UI loop is single-threaded — `../Program.cs.md` §12 covers that assumption). It is *readable from anywhere* (`public get`); nothing reads it, which is what makes the marker dead rather than merely unused.
* **`GetTroubleshootingJson` is not defensive**; only its two callers are (`LogTroubleshooting`'s catch, and `PackGenerator`'s archive scope). A caller that forgets to wrap it — e.g. a future `GetTroubleshootingJson()` use in a page — turns a filesystem/network hiccup into a crash. Keep the `internal` marker's intent in mind when adding call sites.
* **The `internal` visibility on the two reporters is class-scoped in intent** (`internal static void LogTroubleshooting()`, `internal static string GetTroubleshootingJson()`), mirroring upstream's identical declaration, while the call sites live in *other* classes (`MainPage.cs`). Check the C# 13 static-member access rules against the compiler before moving or tightening these members — this is a documentation-of-intent marker, not the enforcement that makes `MainPage.cs` legal.
* **The payload's `decimal` widening** (`DalamudInjectionDelay`) and the `GetValueOrDefault()` defaults are stable JSON-shape guarantees for decoders; if a decoder distinguishes "unset" from "false", this payload cannot express it — see `Configuration/ILauncherConfig.cs.md` §three-state for why the config *can*.
* `Platform` and `DpiAwareness` serialise as **strings** (Newtonsoft writes enum names), while `DalamudLoadMethod` does too — decoders should expect names, not ordinals.
* `Official`'s comparison string is the **repository slug**, so a fork-of-a-fork that keeps its own `BuildOrigin` metadata reads `Official: true`; it identifies the *build provenance*, not "downloaded from the official source".

## 8. Referenced / referenced-by

* References: `Configuration/ILauncherConfig.cs.md` (config fields), `../AppUtil.cs.md` (`GetAssemblyVersion`/`GetGitHash`/`GetBuildOrigin`), `../Program.cs.md` (`Config`, `HttpClient`), `../Components/MainPage/MainPage.cs.md` (§6 anchors), `../Components/SettingsPage/Tabs/SettingsTabTroubleshooting.cs.md` (§5 pack lever), lib `IntegrityCheck.cs` (`CompareResult`, `CompareIntegrityAsync`), lib `Repository.cs` (`RepoExtensions.GetVer`), lib `DalamudLauncher.cs` (`troubleshootingData`).
* Referenced by: `PackGenerator.cs:17` (`GetTroubleshootingJson`), `MainPage.cs:61/561` (`LogTroubleshooting`), `MainPage.cs:566` (`GetTroubleshootingJson`), `SettingsTabTroubleshooting.cs` (the button that ends the chain).
* Sibling spec: `Support/PackGenerator.cs.md`. Upstream counterpart: `lib/FFXIVQuickLauncher/src/XIVLauncher/Support/Troubleshooting.cs` (205 lines — the fork's copy is a Steam-less, renamed-key trim; see §LauncherHash/§integrity bullets for the concrete deltas).
