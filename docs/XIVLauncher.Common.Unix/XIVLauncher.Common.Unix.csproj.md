# XIVLauncher.Common.Unix.csproj — Specification

**File:** `src/XIVLauncher.Common.Unix/XIVLauncher.Common.Unix.csproj` · 37 lines · 1,601 bytes
**SDK:** `Microsoft.NET.Sdk` · **OutputType:** `Library` · **TargetFramework:** `net10.0`
**Role:** The fork's Unix platform layer — Wine/DXVK management, Wine-prefix game launching,
Dalamud injection under Wine, Steam client integration, and distro-aware `wine-xiv` release
selection. Consumed only by `XIVLauncher.Core`.

17 `.cs` files, 18 files total. It is a **fork-local rewrite** of the submodule's own
`lib/FFXIVQuickLauncher/src/XIVLauncher.Common.Unix` project: same assembly name, same
namespace, different implementation.

---

## 1. Purpose

Four functional areas, all Unix-specific:

1. **Wine lifecycle** — download/verify/extract a `wine-xiv-git` release, manage the
   prefix, run binaries inside the prefix (`Compatibility/CompatibilityTools.cs`).
2. **DXVK lifecycle** — download/verify DXVK releases, install into the prefix, HUD/async
   configuration (`Compatibility/Dxvk/`).
3. **Wine configuration** — startup mode, sync backend (fsync/ESync), fsync capability
   probing, managed-version selection (`Compatibility/Wine/`).
4. **Steam integration** — `ISteam` implementation over `goaaats.Steamworks`
   (`UnixSteam.cs`), and the Wine/DarkLua runner for the game and Dalamud
   (`UnixGameRunner.cs`, `UnixDalamudRunner.cs`, `UnixDalamudCompatibilityCheck.cs`).

The project file itself is thin: 10 properties, 1 project reference, 1 package reference,
and one `PathMap`. Everything behavioural lives in source; the project file's job is to
select TFM, nullable mode, the Steamworks package, and the upstream `XIVLauncher.Common`
dependency.

## 2. Build surface

| Attribute | Value | Notes |
| --- | --- | --- |
| `OutputType` | `Library` | no entry point; consumed as a library by `XIVLauncher.Core` |
| `TargetFramework` | `net10.0` | overrides `src/Directory.Build.props` (which sets none) |
| `LangVersion` | `latest` | overrides inherited `LangVersion=8.0` |
| `Nullable` | **`disable`** | the submodule twin is `enable`; this project opts out |
| `Deterministic` | `true` | reproducible build flag |
| `ProduceReferenceAssembly` | `true` | emits `ref/` assembly; affects consumer compile-time binding |
| `Product` / `AssemblyTitle` | `XIVLauncher.Common.Unix` | |
| `Description` | `Shared XIVLauncher platform-specific implementations for Unix-like systems.` | |
| `VersionPrefix` | `1.0.0` | note: `VersionPrefix`, not `Version` — no `FileVersion`/`AssemblyVersion` override, so the assembly version is `1.0.0.0` |
| Inherited from `src/Directory.Build.props` | `LangVersion=8.0` (overridden), `TreatWarningsAsErrors=false`, `GenerateDocumentationFile=true`, `NoWarn=NU1701;CS1591`, `CodeAnalysisRuleSet=CodeAnalysis\xl.ruleset`, `AdditionalFiles=…/CodeAnalysis/BannedSymbols.txt`, `BannedApiAnalyzers` 4.14.0, `NetAnalyzers` 10.0.101, `IsPackable=false`, `PackageLicenseExpression=GPLv3`, `Company=goatsoft` | see §6 |

## 3. Property inventory

| Group | Content | Condition |
| --- | --- | --- |
| 1 | TFM, LangVersion, Nullable, Deterministic, ProduceReferenceAssembly, Product/Title/Description, VersionPrefix | none |
| 2 | `IsOSX = true` | `'$(RuntimeIdentifier)' == 'osx-x64'` |
| 3 | `IsLinux = true` | `'$(RuntimeIdentifier)' == 'linux-x64'` |
| 4 | `DefineConstants += $(ExtraDefineConstants)` | `ExtraDefineConstants != ''` |
| 5 | `ProjectReference` to `lib/…/XIVLauncher.Common.csproj` | none |
| 6 | `AppOutputBase`, `PathMap = …=C:\goatsoft\xl\XIVLauncher.Common.Unix\` | `Configuration == 'Release'` |
| 7 | `PackageReference goaaats.Steamworks` 2.3.4 | none |

**Inert properties:** groups 2–3 define `IsOSX`/`IsLinux` exactly as
`XIVLauncher.Core.csproj` does, but this file **never** adds `OSX`/`LINUX` to
`DefineConstants` and **never** uses them for `Content` conditions. The properties are
computed and discarded. Grepping `#if` across this project directory returns **zero**
preprocessor directives — the project has no compile-time platform switching at all.

The `ExtraDefineConstants` hook (group 4) is likewise unused by this project's sources.

## 4. Reference and package surface

```
XIVLauncher.Common.Unix (net10.0, Library)
├─ lib/FFXIVQuickLauncher/src/XIVLauncher.Common/XIVLauncher.Common.csproj   (net9.0, submodule)
│  └─ lib/FFXIVQuickLauncher/src/XIVLauncher.Aria/…                          (net9.0)
└─ PackageReference goaaats.Steamworks 2.3.4
```

- **The only project reference is the submodule's `XIVLauncher.Common`.** No reference to
  `XIVLauncher.Common.Windows`, and no reference back into `XIVLauncher.Core` — the
  dependency is one-directional.
- `XIVLauncher.Common` supplies the abstractions this project implements:
  `XIVLauncher.Common.PlatformAbstractions.ISteam` (implemented by `UnixSteam.cs`),
  `IDalamudRunner`/`IGameRunner`, `XIVLauncher.Common.Util` (`PlatformHelpers`,
  `PlatformRun`), `XIVLauncher.Common.Unix.Overlay.OverlayInjector`, and
  `XIVLauncher.Common.DalamudLoader`.
- Version skew is deliberate: `net10.0` consumer of `net9.0` dependency. `ProduceReferenceAssembly`
  is what lets the app compile against the reference assembly.
- `goaaats.Steamworks` **2.3.4** is pinned inline. The submodule's
  `src/Directory.Packages.props` sets `ManagePackageVersionsCentrally=true` with
  `goaaats.Steamworks` **2.3.4** as well, so the inline pin and the central pin agree. This
  project uses the inline form because CPM is enabled only under
  `lib/FFXIVQuickLauncher/src/`.
- The package targets the fork of Facepunch.Steamworks on the `chippy` branch, whose native
  binaries are the `libsteam_api64.so`/`libsteam_api64.dylib` committed in
  `src/XIVLauncher.Core/`. **This csproj copies neither**: `XIVLauncher.Core.csproj` owns the
  native deployment (RID-gated `Content`). The submodule twin *does* own them
  (`IsOSPlatform('Linux')`/`IsOSPlatform('OSX')` host-gated) — see §7.
- No `Serilog`, `Newtonsoft.Json`, `System.Drawing.Common`, or test packages. `Serilog`
  is referenced by `XIVLauncher.Common` and surfaces in `CompatibilityTools.cs` via that
  dependency, not through this project file.

## 5. What it provides to `XIVLauncher.Core`

Type → consumer map (all consumers are in `XIVLauncher.Core`):

| Type (file) | Consumed by |
| --- | --- |
| `CompatibilityTools` (`Compatibility/CompatibilityTools.cs`) | `Program.cs:53` (`Program.CompatibilityTools` singleton), `MainPage.cs:183,709,729,733`, `SettingsTabWine.cs:102,112,119,126,131,134` |
| `CompatUtil` (`Compatibility/CompatUtil.cs`) | internal only — `WineSettings.cs:57`, `Dxvk.cs:80`, `CompatibilityTools.cs:99` |
| `WineReleaseDistro`, `GetWineIdForDistro` | `WineSettings.cs:57` → distro-specific `wine-xiv` URL selection |
| `EnsureChecksumMatch` (SHA-512) | download verification in `CompatibilityTools.cs:99`, `Dxvk.cs:80` |
| `DxvkVersion`, `DxvkHudType` (`Compatibility/Dxvk/Dxvk.cs`), `IDxvkRelease`, `DxvkStable/Beta/Legacy` | `Program.cs:19,131-132`, `SettingsTabWine.cs:17,56-60`, `MainPage.cs:701` |
| `IWineRelease`, `WineStable/Beta/Legacy` (`Compatibility/Wine/Releases/`) | managed Wine release selection (`WineSettings.cs`) |
| `WineSettings`, `WineStartupType`, `WineManagedVersion`, `WineSyncType` | `Program.cs:136`, `SettingsTabWine.cs`, `MainPage.cs` |
| `WineUtility`, `FsyncSupport` (`Compatibility/Wine/WineUtility.cs`) | `Program.cs:136`, `SettingsTabWine.cs:41` (`SystemFsyncSupport()`) |
| `UnixGameRunner` | `MainPage.cs:183,729` |
| `UnixDalamudRunner` | `MainPage.cs:553` |
| `UnixDalamudCompatibilityCheck` | `MainPage.cs:554` |
| `UnixSteam : ISteam` | Steam branch of `Program.cs` |

Contract: `Program.CompatibilityTools` is a process-wide singleton created from
`Program.Config`, so `WineSettings` (prefix path, custom bin path, release, sync type)
comes from the persisted launcher config, not from this project file.

## 6. Inherited analyzer configuration — one live, one broken

- **Live:** `AdditionalFiles Include="$(MSBuildThisFileDirectory)CodeAnalysis\BannedSymbols.txt"`
  resolves through `$(MSBuildThisFileDirectory)` to `src/CodeAnalysis/BannedSymbols.txt`, so
  `Microsoft.CodeAnalysis.BannedApiAnalyzers` is active for this project (banned list bans
  `object.Equals`, `ValueType.Equals`, `Nullable.Equals`, non-generic `IComparable`,
  internal EF/NuGet extension helpers).
- **Broken:** `CodeAnalysisRuleSet` is the bare relative value `CodeAnalysis\xl.ruleset`,
  resolved against each **project** directory. Neither
  `src/XIVLauncher.Core/CodeAnalysis` nor `src/XIVLauncher.Common.Unix/CodeAnalysis`
  exists — the ruleset lives at `src/CodeAnalysis/xl.ruleset`. The rule set therefore does
  not resolve for either project; analyzer severities fall back to defaults. If you intend
  the rule set to apply, it must be `$(MSBuildThisFileDirectory)CodeAnalysis\xl.ruleset`.
- `GenerateDocumentationFile=true` + `NoWarn=CS1591` means every public type here
  (`CompatibilityTools`, `UnixSteam`, enums) must carry XML docs or emit a suppressed
  warning; `NU1701` suppression is what permits `steam_autoconfig.xml`-style legacy package
  restores.

## 7. Relationship to the submodule twin

| Aspect | fork-local (`src/…`) | submodule (`lib/…`) |
| --- | --- | --- |
| `TargetFramework` | `net10.0` | `net9.0` |
| `Nullable` | `disable` | `enable` |
| Steam native `Content` (`libsteam_api64.so/.dylib`) | **absent** — Core owns it, RID-gated | present, **host**-gated (`IsOSPlatform`) |
| `#if` directives | none | 3 (`FLATPAK`, `WINE_XIV_ARCH_LINUX`, `FLATPAK_NOTRIGHTNOW`) |
| Distro selection | runtime: `CompatUtil.GetWineIdForDistro()` reading `/etc/os-release`, SHA-512 checksum per distro | compile-time: `WINE_XIV_ARCH_LINUX`/`WINE_XIV_FEDORA_LINUX` defines with a single hard-coded 8.5 release URL |
| Release files | `Compatibility/Wine/Releases/{WineStable,WineBeta,WineLegacy}.cs`, `Compatibility/Dxvk/{DxvkStable,DxvkBeta,DxvkLegacy}.cs` (10.8.r0.a2ca9e4) | `Compatibility/WineSettings.cs`, `Compatibility/Dxvk.cs`, `Compatibility/GameFixes/` |
| `PathMap` | `C:\goatsoft\xl\XIVLauncher.Common.Unix\` | identical |
| Referenced by `XIVLauncher.Core`? | **yes** | **no** |

Assembly-identity consequence: both projects produce
`XIVLauncher.Common.Unix.dll` with the same root namespace
`XIVLauncher.Common.Unix`. Only the fork-local project is in the build graph
(`XIVLauncher.Core.sln` lists only the local path), so the submodule twin is compiled
only when the submodule's own solution is built. Editing
`lib/FFXIVQuickLauncher/src/XIVLauncher.Common.Unix/*` has **no effect** on launcher
behaviour; the file to change is `src/XIVLauncher.Common.Unix/Compatibility/CompatibilityTools.cs`.

The `GameFixes/` directory exists only in the submodule — fork builds have no game-fix
application path.

## 8. Steam contract

`UnixSteam.cs` implements `ISteam` against `Steamworks` (the `goaaats.Steamworks` package):

- `Initialize(uint appId)` sets `SteamAppId`/`SteamGameId` via `DllImport("c") setenv`
  before `SteamClient.Init(appId)` — the comment explains why
  (`SetEnvironmentVariable` does not touch the real process environment on Unix), so this
  project's ability to talk to Steam depends on libc being present at the expected soname
  `"c"`.
- Members: `IsValid`, `BLoggedOn`, `BOverlayNeedsPresent`, `Shutdown()`,
  `GetAuthSessionTicketAsync()`, `OnGamepadTextInputDismissed` event.
- Requires the native `libsteam_api64.so`/`.dylib` to be deployed next to the executable.
  **Deployment is `XIVLauncher.Core.csproj`'s responsibility** (RID-gated `Content`,
  committed 416 KB/609 KB binaries), so this project compiles, and fails at run time,
  independently of the native library's presence.

## 9. Quirks and hazards

- `Nullable=disable` while its dependency (`XIVLauncher.Common`, `net9.0`) is
  `Nullable=enable`: nullable annotations do not cross the boundary, and `UnixSteam.cs`
  uses `Task<byte[]>?` — legal, unchecked, in an annotations-off project.
- `IsOSX`/`IsLinux` are computed and discarded (§3). Anyone reading this csproj as the
  platform switch will look for `#if OSX`/`#if LINUX` and find none. Platform behaviour is
  decided at **run** time by `RuntimeInformation.IsOSPlatform` (e.g.
  `CompatUtil.cs:22`, `WineUtility.cs`) and by `Program.cs`'s `PlatformID` branch.
- `macOS` support is a first-class enum value (`WineReleaseDistro.macOS`) with a
  corresponding download URL, i.e. distro detection covers macOS, while the csproj's
  `IsOSX` property (RID-only) does not cover host-based macOS builds.
- Distro detection is string matching over `/etc/os-release` values (any value containing
  `fedora`/`tumbleweed` → fedora, `arch` → arch, `ubuntu`/`debian` → ubuntu), with
  `org.freedesktop.platform` (Flatpak runtime) and `ubuntu-core`+`snapcraft.io` mapped to
  ubuntu, `ID`-only lines handled by `Split('=', 2)`, and any exception →
  `WineReleaseDistro.ubuntu`. Detection is deliberately forgiving, and the fallback for an
  unrecognised distro is **ubuntu**, not an error.
- Distro matching uses `kvp.Value.ToLower().Contains("arch")` — a substring test over
  **all** values, so `HOME_URL`/`ID_LIKE`/`PRETTY_NAME` can win over `ID`; the first
  matching key/value pair in dictionary order decides.
- Release pinning is hard-coded in source: `WineBeta.cs` (10.8.r0.a2ca9e4) with three
  SHA-512 checksums (arch/fedora/ubuntu). Adding a distro means adding a checksum **and**
  the `WineReleaseDistro` enum member, not a build define.
- `VersionPrefix` alone means the assembly version is `1.0.0.0` and never changes; nothing
  in the repo reads it, so version drift in this library is invisible at run time
  (`AppUtil.GetAssemblyVersion()` reads `XIVLauncher.Core`'s version).
- `obj/Debug/net8.0` and `obj/Debug/net9.0` artifacts are present in this directory:
  stale caches from the pre-net10 project files (`build: update to dotnet9` commit
  `d1ef778`, `build: update to NET10` commit `6e14a64`). They are not build inputs; they
  are evidence of the TFM history.
- No `PackageReadmeFile`/`RepositoryUrl` metadata despite `IsPackable=false` — the GPL
  license expression is inherited only.

## 10. Extension points

- **New Wine/DXVK release**: add a `Wine*Release` (name, URL, per-distro checksums) and wire
  it into the enum/selection in `WineSettings.cs`; do not add a compile define.
- **New distro**: add a `WineReleaseDistro` member, a matching branch in
  `CompatUtil.GetWineIdForDistro()`, and the release checksum.
- **New platform (BSD)**: extend `CompatUtil` (which currently throws
  `InvalidOperationException` for non-Linux/non-OSX in `GetWineIdForDistro`), add native
  Steam binaries, and add a `Content` condition in `XIVLauncher.Core.csproj`.
- **Steam behaviour changes**: implement in `UnixSteam.cs`; the interface comes from
  `XIVLauncher.Common.PlatformAbstractions.ISteam`, shared with `WindowsSteam`.
- **Package version bump**: change `Version="2.3.4"` here *and* the submodule
  `Directory.Packages.props` pin, since the submodule's `XIVLauncher.Common.Windows`
  (`WindowsSteam.cs`) uses the central pin.

## 11. Cross-references

- `docs/XIVLauncher.Core/XIVLauncher.Core.csproj.md` — owns native Steam deployment, RID model, publish contract
- `docs/XIVLauncher.Core/Program.cs` — `CompatibilityTools` singleton, config defaults, `PlatformID` dispatch
- `docs/XIVLauncher.Core/Components/SettingsPage/Tabs/SettingsTabWine.cs` — UI surface for Wine/DXVK settings
- `docs/XIVLauncher.Core/Components/MainPage/MainPage.cs` — game/Dalamud runner usage
- `lib/FFXIVQuickLauncher/src/XIVLauncher.Common.Unix/XIVLauncher.Common.Unix.csproj` — the unreferenced twin
- `src/Directory.Build.props` — inherited analyzer, docs, licensing, ruleset settings
