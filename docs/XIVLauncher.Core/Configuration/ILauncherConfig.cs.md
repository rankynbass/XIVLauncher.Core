# ILauncherConfig.cs — Specification

**File:** `src/XIVLauncher.Core/Configuration/ILauncherConfig.cs` · 97 lines · 2427 bytes (ASCII, LF, no BOM)
**Namespace:** `XIVLauncher.Core.Configuration` · **Declaration:** `public interface ILauncherConfig`
**Role:** The whole persisted state of the launcher as a **schema-only interface**: 41 `{ get; set; }` accessors that Config.Net's generated object implements, keyed by member name in `launcher.ini` and on the command line

---

## 1. Purpose

The interface contains **no implementation**. It is the shape Config.Net builds against at `Program.cs:88` (`Config = new ConfigurationBuilder<ILauncherConfig>()`), and the type of the process-wide config object `Program.Config` (`Program.cs:48`, `public static ILauncherConfig Config { get; private set; } = null!`). Nothing in the fork or in `lib/FFXIVQuickLauncher` writes a class `: ILauncherConfig`; the implementing object is synthesised by the builder, which is why the interface is legal C# despite having bodies with no `{}` block semantics to satisfy at compile time: the members are pure declarations.

Consequence: the interface is the **only** definition of the INI vocabulary. Adding/renaming a member changes the on-disk key set and every `Program.Config.X` read site and every `SettingsEntry` lambda in `Components/SettingsPage/`; removing one silently truncates the ini on next write. Upstream's equivalent is `lib/FFXIVQuickLauncher/src/XIVLauncher/Settings/ILauncherSettingsV3.cs` (see §6's rename map), and `Program.Config` is aliased as `LauncherApp.Settings` (`src/XIVLauncher.Core/LauncherApp.cs:107`, `public ILauncherConfig Settings => Program.Config;` — see `../LauncherApp.cs.md` §3), so every consumer reaches the same object by two names.

## 2. Surface

Usings: `XIVLauncher.Common` (`:1`), `XIVLauncher.Common.Addon` (`:2`), `XIVLauncher.Common.Dalamud` (`:3`), `XIVLauncher.Common.Unix.Compatibility.Dxvk` (`:4`), `XIVLauncher.Common.Unix.Compatibility.Wine` (`:5`) — i.e. the enum/record types below resolve through the lib, and `DirectoryInfo`/`List<>` need no import in this dialect. Namespace is the semicolon form (`:7`), so the three `Configuration` siblings (`CommonSettings.cs`, `UniqueIdCache.cs`) and the `Parsers/` sub-namespace share this vocabulary.

| Line | Member | Type | Line | Member | Type |
| --- | --- | --- | --- | --- | --- |
| `:11` | `CompletedFts` | `bool?` | `:51` | `PatchSpeedLimit` | `long` |
| `:13` | `FontPxSize` | `float?` | `:57` | `WineStartupType` | `WineStartupType?` |
| `:15` | `CurrentAccountId` | `string?` | `:58` | `WineManagedVersion` | `WineManagedVersion?` |
| `:17` | `AcceptLanguage` | `string?` | `:59` | `WineSyncType` | `WineSyncType?` |
| `:19` | `IsAutologin` | `bool?` | `:60` | `WineBinaryPath` | `string?` |
| `:21` | `GamePath` | `DirectoryInfo?` | `:62` | `GameModeEnabled` | `bool?` |
| `:23` | `GameConfigPath` | `DirectoryInfo?` | `:64` | `DxvkVersion` | `DxvkVersion?` |
| `:25` | `AdditionalArgs` | `string?` | `:66` | `DxvkAsyncEnabled` | `bool?` |
| `:27` | `ClientLanguage` | `ClientLanguage?` | `:68` | `DxvkHudType` | `DxvkHudType` |
| `:29` | `IsUidCacheEnabled` | `bool?` | `:70` | `WineDebugVars` | `string?` |
| `:31` | `GlobalScale` | `float?` | `:72` | `FixLocale` | `bool?` |
| `:33` | `DpiAwareness` | `DpiAwareness?` | `:74` | `FixLDP` | `bool?` |
| `:35` | `TreatNonZeroExitCodeAsFailure` | `bool?` | `:76` | `FixIM` | `bool?` |
| `:37` | `Addons` | `List<AddonEntry>?` | `:78` | `FixError127` | `bool?` |
| `:39` | `IsEncryptArgs` | `bool?` | `:80` | `DontUseSystemTz` | `bool?` |
| `:41` | `IsOtpServer` | `bool?` | `:86` | `DalamudEnabled` | `bool?` |
| `:43` | `IsIgnoringSteam` | `bool?` | `:88` | `DalamudLoadMethod` | `DalamudLoadMethod?` |
| `:47` | `PatchPath` | `DirectoryInfo?` | `:89` | `DalamudManualInjectionEnabled` | `bool?` |
| `:49` | `KeepPatches` | `bool?` | `:90` | `DalamudManualInjectPath` | `DirectoryInfo?` |
| — | — | — | `:91` | `DalamudBetaKind` | `string?` |
| — | — | — | `:92` | `DalamudBetaKey` | `string?` |
| — | — | — | `:94` | `DalamudLoadDelay` | `int` |

**41 members total**, in four visual groups delimited by `#region Patching` (`:45`)/`#endregion` (`:53`), `#region Linux` (`:55`)/`#endregion` (`:82`), `#region Dalamud` (`:84`)/`#endregion` (`:96`); the first 17 members (`:11`-`:43`) have no region marker. Regions are cosmetic — they do **not** become INI sections, so all 41 keys live flat in `launcher.ini` (`Program.cs.md` §10 on-disk layout).

`{ get; set; }` on every member is the *interface* accessor spelling: implementors supply storage + both directions, which is what makes `Program.Config.X = value` legal from anywhere (the whole settings UI writes through that setter — see §7).

## 3. The three-state convention

`T?` on a member is **not** "this might be null at the call site" — it is the tri-state slot: `null` = *never written to the ini*, value = *explicitly chosen*. That is the reason the file is written with nullable types for 38 of 41 members and the call sites are free to keep `?? false`, `== true`, `!= false`, and `GetValueOrDefault(true)` tests that disagree with each other (`Components/MainPage/MainPage.cs.md` §login flags calls this out for `MainPage`; `Support/Troubleshooting.cs.md` §payload says the serialised report cannot express "unset").

Exactly **three** members opt out of the tri-state: `PatchSpeedLimit` (`:51` `long`), `DxvkHudType` (`:68` `DxvkHudType`), `DalamudLoadDelay` (`:94` `int`). For those, absence means the type's zero value (`0`, `0`, `0` = `DxvkHudType.None`, first member at `lib/FFXIVQuickLauncher/src/XIVLauncher.Common.Unix/Compatibility/Dxvk.cs:46`) and no `??`/`??=` is meaningful — so `DxvkHudType` must never be written as `Config.DxvkHudType ??= ...`, and `Program.cs.md` §8's defaulting block conspicuously skips all three.

## 4. How the builder wires the schema (`Program.cs:88`-`:93`)

```csharp
Config = new ConfigurationBuilder<ILauncherConfig>()   // :88
         .UseCommandLineArgs()                          // :89
         .UseIniFile(storage.GetFile("launcher.ini").FullName)  // :90
         .UseTypeParser(new DirectoryInfoParser())      // :91
         .UseTypeParser(new AddonListParser())          // :92
         .Build();                                      // :93
```

* Keys are **member names**: an upstream XIVLauncher ini (`AutologinEnabled`, `AddonList`, `SpeedLimitBytes`) sets none of the fork's keys. Key matching is case-insensitive, and `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Util/DebugHelpers.cs:91` binds to `FrontierUrl` against a lowercase `frontierUrl` field in `Net/LauncherClientConfig.cs:17`, which is the empirical proof that the mapping is name-based and case-folded. Renaming a member is therefore a **breaking change for existing installs**, with no compile error and no migration code anywhere.
* `UseCommandLineArgs()` makes every member a CLI flag; nothing in the fork parses `mainArgs` itself (`Program.cs` §4/§6 hands `args` to `LogInit` only), so flag spelling is entirely the builder's and entirely member-named.
* `.UseTypeParser(...)` supplies the two custom parsers for the two type families Config.Net cannot spell (`DirectoryInfo` × 4 members: `GamePath` `:21`, `GameConfigPath` `:23`, `PatchPath` `:47`, `DalamudManualInjectPath` `:90`; and `List<AddonEntry>` `:37`). See `Parsers/DirectoryInfoParser.cs.md` and `Parsers/AddonListParser.cs.md`.
* No `Save()` exists for the config object — the only `Save()` methods in the fork are the secrets store's (`FileSecretProvider.cs:49`, write body at `:51`) and the account list's (`AccountManager.cs:83`, write body at `:85`), neither of which touches `Program.Config` (and the settings UI's `SettingsEntry.Save()`, which re-invokes its own setter lambda). Ini writes are therefore the builder's own write-back on assignment, which means every `Program.Config.X = y` in a settings lambda is a persisted mutation with no dirty-flag and no confirmation step.

## 5. Defaulting: who fills what

`LoadConfig` (`Program.cs:86`-`:145`, `Program.cs.md` §4 row / §8 "Configuration surface") touches **28** of the 41 members:

| Default | Line | Default | Line |
| --- | --- | --- | --- |
| `AcceptLanguage` ← `ApiHelpers.GenerateAcceptLanguage()` | `:95` guard, `:97` assign | `GlobalScale` ← `1.0f` | `:128` |
| `GamePath` ← `STEAM_COMPAT_INSTALL_PATH`/`ffxiv` folder | `:100` guard, `:105` assign | `GameModeEnabled` ← `false` | `:130` |
| `GameConfigPath` ← `ffxivConfig` | `:112` | `DxvkVersion` ← `Stable` | `:131` |
| `ClientLanguage` ← `English` | `:113` | `DxvkAsyncEnabled` ← `true` | `:132` |
| `DpiAwareness` ← `Unaware` | `:114` | `WineStartupType` ← `Managed` | `:134` |
| `IsAutologin` ← `false` | `:115` | `WineManagedVersion` ← `Stable` | `:135` |
| `CompletedFts` ← `false` | `:116` | `WineSyncType` ← `FSync`/`ESync` by probe | `:136` |
| `FontPxSize` ← `22.0f` | `:117` | `WineBinaryPath` ← `"/usr/bin"` | `:137` |
| `IsEncryptArgs` ← `true` | `:119` | `WineDebugVars` ← `"-all"` | `:138` |
| `IsOtpServer` ← `false` | `:120` | `FixLDP` ← `false` | `:140` |
| `IsIgnoringSteam` ← derived from env, **not** `??=` | `:121` | `FixIM` ← `false` | `:141` |
| `PatchPath` ← `patch` folder | `:123` | `FixLocale` ← `false` | `:142` |
| `DalamudEnabled` ← `true` | `:125` | `FixError127` ← `false` | `:143` |
| `DalamudLoadMethod` ← `EntryPoint` | `:126` | `DontUseSystemTz` ← `false` | `:144` |

**13 members are never defaulted** and can still be `null` after `LoadConfig`: `CurrentAccountId` (`:15`), `AdditionalArgs` (`:25`), `IsUidCacheEnabled` (`:29`), `TreatNonZeroExitCodeAsFailure` (`:35`), `Addons` (`:37`), `KeepPatches` (`:49`), `DxvkHudType` (`:68`), `PatchSpeedLimit` (`:51`), `DalamudLoadDelay` (`:94`) — the last three being the non-nullable trio of §3, which therefore read as their zero values — plus `DalamudManualInjectionEnabled` (`:89`), `DalamudManualInjectPath` (`:90`), `DalamudBetaKind` (`:91`), `DalamudBetaKey` (`:92`). Second-wave defaulting exists at `Components/MainPage/MainPage.cs:780` (`App.Settings.Addons ??= new List<AddonEntry>()`) and `MainPage.cs:854` (`App.Settings.PatchPath ??= ...`), plus per-read fallbacks (`Program.cs:363` re-applies `?? WineStartupType.Custom`/`?? WineManagedVersion.Stable`/`?? WineSyncType.FSync` — a *different* set from `LoadConfig`'s `Managed`/`Stable`/probe at `:134`-`:136`).

## 6. Rename map vs upstream's schema

Upstream's equivalent is `lib/FFXIVQuickLauncher/src/XIVLauncher/Settings/ILauncherSettingsV3.cs` (35 members). 9 names are shared verbatim (`AcceptLanguage`, `CurrentAccountId`, `DpiAwareness`, `GamePath`, `KeepPatches`, `PatchPath`, `TreatNonZeroExitCodeAsFailure`, `DalamudBetaKey`, `DalamudBetaKind`). Everything else is a fork rename or a fork/lib-side addition — arithmetic: 41 fork members = 9 shared + 32 fork-only (the 12 renames tabulated below, the rest new Linux/manual-injection state); upstream's 35 = 9 shared + 26 upstream-only:

| Fork member | Upstream name (`ILauncherSettingsV3.cs`) |
| --- | --- |
| `IsAutologin` (`:19`) | `AutologinEnabled` (`:15`) |
| `ClientLanguage` (`:27`) | `Language` (`:23`) |
| `AdditionalArgs` (`:25`) | `AdditionalLaunchArgs` (`:18`) |
| `Addons` (`:37`) | `AddonList` (`:16`) |
| `IsUidCacheEnabled` (`:29`) | `UniqueIdCacheEnabled` (`:17`) |
| `IsEncryptArgs` (`:39`) | `EncryptArguments` (`:26`) |
| `IsOtpServer` (`:41`) | `OtpServerEnabled` (`:21`) |
| `DalamudEnabled` (`:86`) | `InGameAddonEnabled` (`:19`) |
| `DalamudLoadMethod` (`:88`) | `InGameAddonLoadMethod` (`:20`) |
| `PatchSpeedLimit` (`:51`, `long`) | `SpeedLimitBytes` (`:29`, `long`) |
| `DalamudLoadDelay` (`:94`, `int`) | `DalamudInjectionDelayMs` (`:30`, `decimal`) |
| `CompletedFts` (`:11`) | `IsFt` (`:42`) (nearest) |

Dropped wholesale from upstream's schema (26): `AskBeforePatchInstall`, `AutoStartSteam`, `DalamudRolloutBucket`, `ExitLauncherAfterGameExit`, `ForceNorthAmerica`, `HasComplainedAboutAdmin/GShadeDxgi/NoOtp`, `HasShownAutoLaunchDisclaimer`, `InGameAddonEnabled/LoadMethod`, `IsFt`, `Language`, `LastVersion`, `LauncherLanguage`, `MainWindowPlacement`, `OtpAlwaysOnTopEnabled`, `UniqueIdCacheEnabled`, `VersionUpgradeLevel`, … — all Windows/XAML state (`MainWindowPlacement`'s type comes from `XIVLauncher.Xaml`). Added by the fork: the entire `Linux` region (14 members, `:57`-`:80`) plus the manual-injection pair (`:89`/`:90`) — none of which upstream's `ILauncherSettingsV3` names.

## 7. Consumers (read/write pattern per member)

Every read/write goes through `Program.Config` or `App.Settings` (both the same object, `../LauncherApp.cs.md` §3). Highest-traffic members: `GamePath` (14 reads, incl. `LauncherApp.cs:143` killswitch `Repository.Boot.GetVer`, `MainPage.cs:244`, `MainPage.cs:917` `PatchManager`, `MainPage.cs:1049` `PatchVerifier`, `Support/Troubleshooting.cs:73`), `ClientLanguage` (10), `WineBinaryPath` (8), `DalamudManualInjectPath` (9, incl. `Program.cs:167`-`:172` injector override), `IsUidCacheEnabled` (6), `IsIgnoringSteam` (5 + `Program.cs:121` env-derived write), `IsOtpServer` (3 + `Accounts/Secrets` OTP flows), `TreatNonZeroExitCodeAsFailure` (1, `MainPage.cs:373`), `CurrentAccountId` (`Accounts/AccountManager.cs:16`/`:17` — the accessor pair whose backing store **is** config), `DxvkHudType` (`SettingsTabWine.cs:57`), `Addons` (`MainPage.cs:780`/`:782`), `AcceptLanguage` (`LauncherApp.cs:132`, `Launcher.cs` Referer generation via `../Net/LauncherClientConfig.cs.md` §5).

**`GlobalScale` (`:31`) is the odd one out: it is written once (`Program.cs:128` `??= 1.0f`) and never read.** The UI's live scale is a different value entirely — `ImGuiHelpers.cs:10` (`ImGuiHelpers.GlobalScale => ImGui.GetStyle().FontScaleMain`), fed by `ImGuiBindings.cs:74` (`style.FontScaleDpi = mainScale`), which is not this member and not even the same style field. Wiring `Config.GlobalScale` into the style is the unfinished half of that feature; do not "fix" by renaming.

Settings UI round-trip is uniformly `new SettingsEntry<T>(label, desc, () => Program.Config.X, x => Program.Config.X = x)` (`../Components/SettingsPage/SettingsEntry{T}.cs.md` §1) with per-entry `??` display defaults that **differ from `LoadConfig`'s**: `SettingsTabTroubleshooting.cs:38` shows `Program.Config.DontUseSystemTz ?? true` while `Program.cs:144` defaults the field to `false`; `SettingsTabWine.cs:53` shows `WineDebugVars ?? string.Empty` while `:138` defaults to `"-all"`. Those UI defaults are cosmetic for `null` members only (all 13 never-defaulted ones are the members where such a divergence is visible).

## 8. Invariants and hazards

* **Read sites must re-decide the default.** With 13 members still `null` after startup and 38 nullable types, every consumer is a policy decision. The three-way test matters: `x == true`, `x != false`, `x ?? false`, `x.GetValueOrDefault(true)` all differ at `null`, and `MainPage.cs:611`-`:639` (`FixLocale == true`, `FixLDP == true`, `FixIM == true`, `FixError127 == true`, `DontUseSystemTz == true`) all require an *explicit* `true` — safe today only because `Program.cs:140`-`:144` defaulted them before any launch path. A new `null`-allowed member read that way silently disables its hack.
* **Defaults are duplicated in three places and disagree.** `LoadConfig` (`Program.cs:113` `ClientLanguage.English`), `MainPage.cs:226`/`:245` (`?? ClientLanguage.English`), `MainPage.cs:565` (`GetValueOrDefault(English)`) — and for Wine, `Program.cs:134`/`:135`/`:136` (`Managed`, `Stable`, probe) vs `Program.cs:363` (`?? WineStartupType.Custom`, `?? WineManagedVersion.Stable`, `?? WineSyncType.FSync`). `Program.cs:363`'s `Custom` fallback is the dangerous one: it is reachable only if a caller builds `WineSettings` before `LoadConfig` ran, but it turns "unset" into the *custom* wine path instead of the launcher-managed one. Any refactor that centralises defaults must pick one of these sets deliberately.
* **`GlobalScale`, and the 3 non-nullable members, cannot express "unset".** Persisting them is lossy for a decoder that cares about unset-vs-zero (`Support/Troubleshooting.cs.md` §payload makes the same claim for the report). `DxvkHudType`'s zero value (`None`) is also a legitimate user choice, so "unset" and "None" are unifiable only by luck.
* **Type changes are INI-breaking.** `DalamudLoadDelay` (`int`) vs upstream's `decimal`, and `PatchSpeedLimit` `long` read through an `int` UI view (`SettingsTabPatching.cs:20` `(int)Program.Config.PatchSpeedLimit`, setter `:21` writing an `int` into a `long` with range `0, int.MaxValue, 1000`): a hand-edited ini value above `INT32_MAX` is truncated on display and cannot be re-set from the UI, and the UI's step unit is undocumented here (upstream treats `SpeedLimitBytes` as bytes, cf. `XIVLauncher/Windows/PatchDownloadDialog.xaml.cs:224` `* BYTES_TO_MB`).
* **No schema version.** Upstream's name is literally `ILauncherSettingsV3`; the fork's is unversioned, so `launcher.ini` from an older fork build (or upstream) is read as a set of unknown keys and silently yields all-`null`, i.e. *every* user setting is lost without a message — `Program.cs.md` §4's `ClearSettings` row (delete `launcher.ini`) is the only "clean slate" lever that exists.
* **`#region` ordering is the file's only grouping.** The generated object's key order and thus the ini's readability follow declaration order; a new Linux option added outside the Linux region (`:55`-`:82`) is unreviewable later. `#region` at `:45`/`:55`/`:84` with `#endregion` at `:53`/`:82`/`:96` are the three anchors to extend.
* **Member name == ini key == CLI flag == JSON key for other payloads.** `Support/Troubleshooting.cs` and `Components/SettingsPage/` both echo these names, so a rename silently changes the troubleshooting report's shape (`Support/Troubleshooting.cs.md` §payload) and any decoder's expectations.

## 9. Referenced / referenced-by

* Referenced by: `Program.cs:26` (`using XIVLauncher.Core.Configuration;`) and `Program.cs:48`/`:88` (`Config`, builder); `LauncherApp.cs:17`/`:107`/`:132`/`:143` (`Settings` alias, `AcceptLanguage`, killswitch `GamePath`); `../Components/SettingsPage/` (every tab's `SettingsEntry` lambda pair); `MainPage.cs` (launch/patch/login paths), `Program.cs:363` (`WineSettings` construction), `Support/Troubleshooting.cs:73`/`:122`, `Accounts/AccountManager.cs:16`/`:17`, `../Configuration/CommonSettings.cs` (the 7-member view), `../Configuration/UniqueIdCache.cs` (unrelated to config but same namespace).
* References (types): `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/ClientLanguage.cs:5` (`Japanese` first), `DpiAwareness.cs:5` (`Aware` first), `Addon/AddonEntry.cs:5` (`IsEnabled`, `Addon`), `Dalamud/DalamudLoadMethod.cs:6` (`EntryPoint` first), `Unix/Compatibility/WineSettings.cs:5` (`Managed` first), `Unix/Compatibility/Dxvk.cs:43` (`None` first), plus `WineManagedVersion`/`WineSyncType`/`DxvkVersion` in the same `Unix/Compatibility` namespaces; `Config.Net`'s builder + `ITypeParser` (`Configuration/Parsers/`).
* Counterpart: `lib/FFXIVQuickLauncher/src/XIVLauncher/Settings/ILauncherSettingsV3.cs` (upstream's Windows/XAML schema, §6's rename map).
* Sibling specs: `../Configuration/CommonSettings.cs.md`, `../Configuration/UniqueIdCache.cs.md`, `../Configuration/Parsers/DirectoryInfoParser.cs.md`, `../Configuration/Parsers/AddonListParser.cs.md`.
