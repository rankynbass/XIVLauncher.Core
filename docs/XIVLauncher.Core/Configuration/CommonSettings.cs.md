# CommonSettings.cs — Specification

**File:** `src/XIVLauncher.Core/Configuration/CommonSettings.cs` · 21 lines · 625 bytes (ASCII, LF, no BOM)
**Namespace:** `XIVLauncher.Core.Configuration` · **Declaration:** `internal class CommonSettings`
**Role:** A 7-member **read-only view** of `ILauncherConfig` spelled in upstream's vocabulary, three of whose members are hardcoded constants — currently constructed once (`Program.cs:49`) and never consumed

---

## 1. Purpose

`CommonSettings` exists so the launcher's own state can be addressed through the **upstream** settings vocabulary. `ILauncherSettingsV3.cs` (`lib/FFXIVQuickLauncher/src/XIVLauncher/Settings/`) names the patch-speed knob `SpeedLimitBytes` (`:29`) and the Dalamud delay `DalamudInjectionDelayMs` (`:30`); `ILauncherConfig.cs` renamed both (`PatchSpeedLimit` at `:51`, `DalamudLoadDelay` at `:94`, see `./ILauncherConfig.cs.md` §6). `CommonSettings.cs:19` and `:20` are the two accessors that bridge that rename. Its siblings do *not* use the upstream names (`ClientLanguage` here matches the fork's name, not upstream's `Language` at `ILauncherSettingsV3.cs:23`), so the bridge is partial.

It is **not** a settings store and not a singleton: it holds no state of its own beyond one reference (`:7` `private readonly ILauncherConfig config;`, assigned by the ctor at `:9` `public CommonSettings(ILauncherConfig config)`). It has no consumers at all — `grep` for `new CommonSettings`/`CommonSettings` in the fork yields only this file and `Program.cs:49` (see `../Program.cs.md` §4/§12 for that construction), so every claim below is about what it would report if a caller used it.

## 2. Surface

| Line | Member | Returns | Body |
| --- | --- | --- | --- |
| `:9` | ctor `CommonSettings(ILauncherConfig config)` | — | `:11` `this.config = config` |
| `:14` | `AcceptLanguage` | **`string`** (non-nullable) | `=> this.config.AcceptLanguage!` |
| `:15` | `ClientLanguage` | `ClientLanguage?` | `=> this.config.ClientLanguage` |
| `:16` | `KeepPatches` | `bool?` | `=> false` |
| `:17` | `PatchPath` | **`DirectoryInfo`** (non-nullable) | `=> this.config.PatchPath!` |
| `:18` | `GamePath` | **`DirectoryInfo`** (non-nullable) | `=> this.config.GamePath!` |
| `:19` | `SpeedLimitBytes` | `long` | `=> 0` |
| `:20` | `DalamudInjectionDelayMs` | `int` | `=> 0` |

7 accessors, 0 methods besides the ctor, 0 sets — the class is write-incompatible by construction, which is the point: a caller handed a `CommonSettings` cannot persist anything and cannot even see whether persistence happened.

4 accessors forward, 3 fabricate (`:16` `false`, `:19` `0`, `:20` `0`). The three fabrications are exactly the three knobs the patch/Dalamud paths use, so a consumer of this view sees "keep nothing, unlimited speed, no injection delay" regardless of what the user chose.

## 3. `internal` visibility and construction-per-read

The declaration is `internal class CommonSettings` (`:5`) — the access modifier spelled as the C# modifier, not a name modifier (contrast `UniqueIdCache.cs:5`, which is `public`, and `ILauncherConfig.cs:9`, `public interface`). The class is declared inside `XIVLauncher.Core.Configuration` (`CommonSettings.cs:3`), and its **only** construction site sits in the parent namespace `XIVLauncher.Core` (`Program.cs:34`), reaching it through `using XIVLauncher.Core.Configuration;` (`Program.cs:26`). So `internal` here reads as a marker rather than a working access boundary — nothing outside this directory references the class today, so the scoping question is moot in practice, but `Program.cs:49` is the single call site that would stop compiling if the dialect ever enforced the C# rule.

The single construction site is a property accessor:

```csharp
public static CommonSettings CommonSettings => new(Config);   // Program.cs:49
```

so every read of `Program.CommonSettings` allocates a **fresh** view object (`Program.cs.md` §12 hazard #4). Three consequences: `==`/`is` between two reads is `false` (no identity), the object cannot be used as a cache key or memoisation target, and the ctor cost is one small allocation per access (cheap, but it means "the view" is a *value-shaped* alias, not a stable one).

## 4. The three constants, and where the truth actually lives

| `CommonSettings` says | Real member (`ILauncherConfig.cs`) | Real reader | Real writer (UI) |
| --- | --- | --- | --- |
| `KeepPatches => false` (`:16`) | `KeepPatches` (`:49`, `bool?`, never defaulted, `?? false` at use) | `MainPage.cs:915` `PatchInstaller(..., App.Settings.KeepPatches ?? false)` | `SettingsTabPatching.cs:22` (`?? false` getter, setter `:22`) |
| `SpeedLimitBytes => 0` (`:19`) | `PatchSpeedLimit` (`:51`, `long`) | `MainPage.cs:917` `new PatchManager(acquisition, App.Settings.PatchSpeedLimit, ...)` | `SettingsTabPatching.cs:20`/`:21` (0…`int.MaxValue`, step 1000) |
| `DalamudInjectionDelayMs => 0` (`:20`) | `DalamudLoadDelay` (`:94`, `int`) | `MainPage.cs:566` (`App.Settings.DalamudLoadDelay` into the launch call), `Support/Troubleshooting.cs:122` | `SettingsTabDalamud.cs:20` (`??`-free direct read, 0…`int.MaxValue`, step 1000) |

No consumer reads the view today, so the constants are currently harmless; they are the reason this file must never be wired in as a convenience without deleting the three bodies. The hardcoded values also match the *effective* defaults (`KeepPatches` unset ⇒ `?? false`; `0` is the zero value for the two non-nullable members per `ILauncherConfig.cs.md` §3), so a consumer would see the default behaviour rather than a visibly wrong one — a lie that looks plausible in logs.

## 5. Nullability of the view

Three accessors narrow a nullable config getter to a **non-nullable** return type with the postfix `!` non-null assertion: `AcceptLanguage!` (`:14`, declared `string`), `PatchPath!` (`:17`, declared `DirectoryInfo`), `GamePath!` (`:18`, declared `DirectoryInfo`). Because the declared types are non-nullable, the `!` is not cosmetic — it is what keeps the signature honest, and it **traps** if the config getter returns `null`. Those three members of the view therefore cannot express "unset", even though the underlying members (`ILauncherConfig.cs:17`/`:47`/`:21`) are `string?`/`DirectoryInfo?`/`DirectoryInfo?` and 13 of the 41 members are still `null` after startup (`ILauncherConfig.cs.md` §5).

The trap is unreachable in the shipped flow, because `LoadConfig` (`Program.cs:86`, called at `Program.cs:94`, defaults span `:95`-`:145`) defaults all three before any consumer could run: `AcceptLanguage` at `:97`, `GamePath` at `:105`, `PatchPath` at `:123`. It *is* reachable through ordering, because the view is constructed lazily by accessor (`Program.cs:49` `public static CommonSettings CommonSettings => new(Config);`): anything that reads `Program.CommonSettings` before `LoadConfig` (`Program.cs:94`) runs would hit the assertion at the read, not at construction. `MainPage.cs.md` §login cites the same `!`-pattern on the config getters (`MainPage.cs:244` `App.Settings.GamePath!`, `MainPage.cs:1049` `Program.Config.GamePath!`/`PatchPath!`, `Support/Troubleshooting.cs:73`), i.e. the whole fork relies on `LoadConfig` having run — `CommonSettings` encodes the same assumption, in one more place.

By contrast `ClientLanguage` (`:15`) forwards with no assertion and is the **only** accessor declared nullable (`ClientLanguage?`), so it is the only member of the view that can still report "unset"; `KeepPatches` (`:16`) is declared `bool?` but its body can never yield `null`, and `SpeedLimitBytes`/`DalamudInjectionDelayMs` (`:19`/`:20`) are non-nullable scalars whose constant `0` is indistinguishable from a user choice.

## 6. Coverage of the config

7 of 41 members are visible; **34 are invisible** through this view. Everything Linux-specific is invisible (`WineStartupType`/`WineManagedVersion`/`WineSyncType`/`WineBinaryPath`/`WineDebugVars`, `FixLocale`/`FixLDP`/`FixIM`/`FixError127`, `DontUseSystemTz`, `GameModeEnabled`, `DxvkVersion`/`DxvkAsyncEnabled`/`DxvkHudType`, `GameConfigPath`), as is everything Dalamud-side except the delay (`DalamudEnabled`, `DalamudLoadMethod`, `DalamudManualInjectionEnabled`/`DalamudManualInjectPath`, `DalamudBetaKind`/`DalamudBetaKey`), and the whole identity/OTP group (`CurrentAccountId`, `IsUidCacheEnabled`, `IsOtpServer`, `IsEncryptArgs`, `IsIgnoringSteam`, `AdditionalArgs`, `Addons`, `FontPxSize`, `GlobalScale`, `CompletedFts`, `IsAutologin`, `TreatNonZeroExitCodeAsFailure`, `DpiAwareness`).

Name-bridge table (view member → config member): `SpeedLimitBytes` → `PatchSpeedLimit` (`:51`), `DalamudInjectionDelayMs` → `DalamudLoadDelay` (`:94`); the other five names match `ILauncherConfig` verbatim. Upstream's names live at `ILauncherSettingsV3.cs:29` (`SpeedLimitBytes`, `long`) and `ILauncherSettingsV3.cs:30` (`DalamudInjectionDelayMs`, `decimal`) — note the fork narrowed the delay from `decimal` to `int` **and** renamed it, so this view keeps upstream's name while adopting the fork's narrower type.

## 7. Hazards & how to extend

* **Never read the 3 constants' names as config.** If a UI or report ever consumes `CommonSettings`, `KeepPatches=false`/`SpeedLimitBytes=0`/`DalamudInjectionDelayMs=0` become indistinguishable from user choices. Extending the view means forwarding all three (`this.config.KeepPatches`, `this.config.PatchSpeedLimit`, `this.config.DalamudLoadDelay`) and renaming accessors to the fork's members if the consumer is fork code.
* **Keep the upstream names when the consumer is upstream code.** The two bridged names are the *only* reason the file exists; changing them to fork names while keeping the class is equivalent to `Program.Config` and loses the bridge.
* **It is a view, not a copy.** `PatchPath`/`GamePath` return the same live `DirectoryInfo` object the config holds (`ILauncherConfig.cs:21`/`:47`), so a caller that mutates it (`DirectoryInfo.FullName` is read-only, but any caller that rewrites `.value` via `ILauncherConfig`'s setter — `MainPage.cs:851` sets `App.Settings.PatchPath = null`, `MainPage.cs:854` re-defaults) is visible to the view immediately. Do not add caching to the view.
* **Do not add sets.** Persistence in this fork is the generated object's own ini write (`ILauncherConfig.cs.md` §4: no explicit `Save()` call exists); a setter here would either write nothing or write twice through a second alias.
* The class is dead, which is the safest thing to record in a spec: a reader wiring it in must first re-read §4 and §5, since both the constants and the `!` assertions assume the *config* defaulting that the view does not perform.

## 8. Referenced / referenced-by

* Extends/delegates: `ILauncherConfig.cs` (same directory, §2 surface, §3 three-state convention, §6 rename map).
* Constructed by: `Program.cs:49` (`public static CommonSettings CommonSettings => new(Config);` — the accessor's name shadows the type, and `new(Config)` is the type-inferred `new` spelling; row `CommonSettings` of `Program.cs.md` §4 method surface, and the allocation-per-read hazard is `Program.cs.md` §12).
* References: `Program.cs:95`-`:145` (`LoadConfig` defaults, the precondition of §5), `ILauncherConfig.cs:17`/`:21`/`:27`/`:47` (the forwarded members), `../Program.cs.md` (module globals), `../LauncherApp.cs.md` (the alias host).
* Upstream counterparts: `lib/FFXIVQuickLauncher/src/XIVLauncher/Settings/ILauncherSettingsV3.cs` (the vocabulary it bridges), `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/CommonUniqueIdCache.cs` (unrelated).
