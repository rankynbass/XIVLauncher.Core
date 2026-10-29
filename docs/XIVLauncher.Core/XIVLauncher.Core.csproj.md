# XIVLauncher.Core.csproj — Specification

**File:** `src/XIVLauncher.Core/XIVLauncher.Core.csproj` · 162 lines · 7,770 bytes
**SDK:** `Microsoft.NET.Sdk` · **OutputType:** `Exe` · **TargetFramework:** `net10.0-windows` when `-r win-x64`, `net10.0` otherwise
**Role:** Build definition of the launcher executable — version pipeline, platform/RID model, resource packaging, native Steam binary deployment, and the Git-metadata injection that the UI reports.

The project directory contains 61 `.cs` files and 88 files total (excluding bin/obj). Nothing in this file is
consumed at run time except what it *emits*: embedded resources, `Content` binaries, the
generated `AppUtil.VersionInfo` constructor, and two `AssemblyMetadata` attributes.

---

## 1. Purpose

This project file owns six contracts:

1. **Executable shape** — `Exe`, conditional `TargetFramework` (`net10.0-windows` when
   `-r win-x64`, `net10.0` otherwise; the Windows TFM is required by
   `Eto.Platform.Windows`, which targets `net10.0-windows7.0`), self-contained
   single-file publish for `win-x64`/`linux-x64`/`osx-x64`.
2. **Version identity** — `Version`/`FileVersion`/`AssemblyVersion` at `1.5.2`, plus the
   `XIVLauncher.VersionGenerator` source generator that materialises
   `AppUtil.VersionInfo`'s constructor.
3. **Platform model** — `IsWindows`/`IsOSX`/`IsLinux` and the `WIN32`/`OSX`/`LINUX`
   `DefineConstants` that `#if` directives read, plus the `ExtraDefineConstants` hook
   (`FLATPAK`).
4. **Reference graph** — the two shared submodule libraries, the fork-local
   `XIVLauncher.Common.Unix`, and `XIVLauncher.Aria` transitively.
5. **Resource surface** — `ApplicationIcon`, 7 `LogicalName`-named embedded resources
   (fonts, images), the 15-file localization suite, and the Steam native libraries.
6. **Git metadata** — the `GetGitHash`/`WriteGitHash` targets that write
   `AssemblyMetadata("GitHash")` and `AssemblyMetadata("BuildOrigin")`.

## 2. Identity and build surface

| Attribute | Value | Notes |
| --- | --- | --- |
| `OutputType` | `Exe` | app host; `Program.Main` (`docs/XIVLauncher.Core/Program.cs.md`) |
| `TargetFramework` | `net10.0-windows` when `-r win-x64`, `net10.0` otherwise | two conditional `PropertyGroup`s; overrides `src/Directory.Build.props` (no `TargetFramework` there) |
| `LangVersion` | `latest` | overrides `src/Directory.Build.props` `LangVersion=8.0` |
| `ImplicitUsings` | `enable` | |
| `Nullable` | `enable` | contrast: `XIVLauncher.Common.Unix` is `disable` |
| `AllowUnsafeBlocks` | `true` | retained from the SDL era; used for `IntPtr` handle marshalling in `LauncherForm`'s `GApplicationHandle()` reflection |
| `ApplicationIcon` | `Resources\dalamud_icon.ico` | Win32 resource icon; not an embedded resource |
| `Version` | `1.5.2` | `FileVersion`/`AssemblyVersion` are `$(Version)` |
| `RuntimeIdentifiers` | `win-x64;linux-x64;osx-x64` | applies **only when `-r`/`RuntimeIdentifier` is passed** (condition `'$(RuntimeIdentifier)' != ''`); a plain `dotnet build` restores no RID packs and compiles `net10.0` |
| `PublishSingleFile` | `true` | |
| `SelfContained` | `true` | required by `PublishSingleFile` on modern SDKs |
| `Configurations` | `Debug;Release` | restricts the solution configuration set |
| Inherited from `src/Directory.Build.props` | `GenerateDocumentationFile=true`, `NoWarn=CS1591;NU1701`, `CodeAnalysisRuleSet=src/CodeAnalysis/xl.ruleset`, `BannedApiAnalyzers` 4.14.0, `NetAnalyzers` 10.0.101, `IsPackable=false`, `PackageLicenseExpression=GPLv3`, `Company=goatsoft` | see `docs/XIVLauncher.Core/…` per-type specs |

## 3. Property inventory

Four `PropertyGroup` blocks with no `Condition`, five with conditions:

| Group | Content | Condition |
| --- | --- | --- |
| 1 | main build surface (§2) | none |
| 2 | `DefineConstants += $(ExtraDefineConstants)` | `ExtraDefineConstants != ''` |
| 3 | `DefineConstants += WIN32` | `'$(IsWindows)'=='true'` |
| 4 | `DefineConstants += OSX` | `'$(IsOSX)'=='true'` |
| 5 | `DefineConstants += LINUX` | `'$(IsLinux)'=='true'` |
| 6 | `AppOutputBase`, `PathMap` | `'$(Configuration)'=='Release'` |

## 4. Platform targeting model

The SDK defines `IsWindows`, `IsLinux`, `IsMacOS` **host-relative** (true when the build
machine is that OS). This csproj re-assigns three properties **target-relative**:

```
IsWindows ← RID == 'win-x64'
IsOSX     ← RID == 'osx-x64'
IsLinux   ← RID == 'linux-x64'
```

Consequences, exactly as the MSBuild property engine resolves them:

- `IsWindows`: true if host is Windows **or** RID is `win-x64` (SDK default plus the
  RID-scoped assignment).
- `IsLinux`: true if host is Linux **or** RID is `linux-x64`.
- `IsOSX`: true **only** when RID is `osx-x64` — the SDK's host name for macOS is
  `IsMacOS`, which this file never reads, so an OS host build (macOS, no RID) gets **no**
  `OSX` define and no `libsteam_api64.dylib` copy.
- A plain `dotnet build` with no RID therefore behaves as a host-platform build and compiles
  `net10.0` — `Eto.Platform.Windows` is not restored and `RuntimeInformation.IsOSPlatform`
  gates in the source decide behaviour. `-r win-x64` defines `WIN32`, compiles
  `net10.0-windows`, restores `Eto.Platform.Windows`, and copies the Windows Steam
  content path.

Define consumers in this repo (`grep '#if'` over `src/`):

| Define | Where read | Effect |
| --- | --- | --- |
| `WIN32` | `Components/MainPage/MainPage.cs:930`, `:1051` | Windows-specific UI/behaviour branches |
| `FLATPAK` | `Components/SettingsPage/Tabs/SettingsTabDebug.cs:44`, `LauncherApp.cs:279` (`RunStartupTasks` → FTS path) | **not** produced by this file; must be injected as `-p:ExtraDefineConstants=FLATPAK` |
| `LINUX` | `AppFonts.cs:39,71`, `LauncherForm.cs:69`, `Components/MainPage/NewsFrame.cs:216`, `Components/OtpEntryPage.cs:59` | read by the GTK-only code: `libgio` application-hold P/Invoke, `Gtk.Label.MaxWidthChars` news cap, clipboard, `FontFamily.FromStreams` registration — the `#else` branches carry the Windows behaviour |
| `OSX` | **never read** in `src/` | defined, unused — dead define |
| `DEBUG` | `MainPage.cs:253,370` (`#if !DEBUG`), `LauncherApp.cs:170`, `Program.cs:195,295` | standard, not from this file; gates the UI-thread warning capture and the `XLCORE_SELFTEST` selftest |

The submodule adds two further defines that this repo never supplies:
`XIVLauncher.Common.Unix/Compatibility/CompatibilityTools.cs:25` reads
`WINE_XIV_ARCH_LINUX` (never defined here) and `:195` reads `FLATPAK_NOTRIGHTNOW`.

`ExtraDefineConstants` is the **only** supported way to add defines: group 2 appends it,
so packagers (Flathub, AUR, nixpkgs) control `FLATPAK` through the CLI property, not by
editing this file.

## 5. Reference graph

```
XIVLauncher.Core (net10.0-windows on win-x64, net10.0 otherwise, Exe)
├─ XIVLauncher.VersionGenerator   (netstandard2.0, referenced as Analyzer, ReferenceOutputAssembly=false)
├─ src/XIVLauncher.Common.Unix    (net10.0, fork-local copy — spec: docs/XIVLauncher.Common.Unix/XIVLauncher.Common.Unix.csproj.md)
├─ lib/…/XIVLauncher.Common.Windows (net9.0, submodule)
└─ lib/…/XIVLauncher.Common       (net9.0, submodule)
   └─ lib/…/XIVLauncher.Aria      (net9.0, submodule)
```

Load-bearing facts:

- `ReferenceOutputAssembly="false"` + `OutputItemType="Analyzer"` makes
  VersionGenerator a compile-time-only dependency: no `XIVLauncher.VersionGenerator.dll`
  ships in the app.
- **Both** platform libraries are referenced unconditionally: `XIVLauncher.Common.Windows`
  is compiled into Linux/macOS builds. `Program.cs` selects `WindowsSteam` vs `UnixSteam`
  at run time via `PlatformID`, so the Windows lib is dead weight on Unix, not absent.
- Target-framework skew is intentional: `net10.0(-windows)` app referencing `net9.0` libs. The
  submodule's `src/Directory.Packages.props` sets `ManagePackageVersionsCentrally=true`,
  so submodule projects get versions from that file while this project uses inline
  `Version=` attributes.
- `Eto.Platform.Windows` declares `net10.0-windows7.0` — lower-case seven — so restoring it
  into the `net10.0-windows` app produces the NU1201 downgrade warning; expected and
  harmless, the package restores and builds.
- The fork-local `XIVLauncher.Common.Unix` assembly name **collides** with the submodule's
  own `XIVLauncher.Common.Unix` project, which is not referenced. Only the local copy
  ships.

## 6. Package references

| Package | Version | Role |
| --- | --- | --- |
| `Eto.Forms` | 2.13.0 | UI surface (`Application`, `Form`, `Control`) |
| `Eto.Drawing.Skia` | 2.13.0 | Skia drawing backend for `Eto.Drawing` (`Image`, `Bitmap`) |
| `Config.Net` | 4.19.0 | `ILauncherConfig` binding (`Configuration/ILauncherConfig.cs.md`) |
| `KeySharp` | 1.0.5 | key-registry helper (`Accounts/Secrets/Providers/KeychainSecretProvider.cs.md`) |
| `Serilog` | 4.3.0 | logging core |
| `Serilog.Enrichers.Thread` | 4.0.0 | |
| `Serilog.Sinks.Console` | 6.0.0 | |
| `Serilog.Sinks.File` | 7.0.0 | `logs/launcher.log` |
| `Eto.Platform.Gtk` | 2.13.0 | GTK backend; included **only when `RuntimeIdentifier != 'win-x64'`** |
| `Eto.Platform.Windows` | 2.13.0 | WinForms backend; included **only when `RuntimeIdentifier == 'win-x64'`**, declared `net10.0-windows7.0` (see §5 NU1201 note) |

The Hexa.NET SDL3/ImGui stack (`Hexa.NET.ImGui`, `Hexa.NET.ImGui.Backends.SDL3`,
`Hexa.NET.SDL3`, `Hexa.NET.SDL3.Image`) is gone — `Eto.Forms` replaced the SDL window loop
(§1, `Program.cs.md` §6).

No test packages, no `Microsoft.NET.Test.Sdk` — the CI `dotnet test` step has no test
project to run.

## 7. Resource surface

### 7.1 `LogicalName`-pinned embedded resources (7)

```
Resources/fonts/FontAwesome5FreeSolid.otf  → "FontAwesome5FreeSolid.otf"
Resources/fonts/NotoSansCJKjp-Regular.otf  → "NotoSansCJKjp-Regular.otf"
Resources/logo.png                          → "logo.png"
Resources/bg_logo.png                       → "bg_logo.png"
Resources/steamdeck_fts.png                → "steamdeck_fts.png"
Resources/steamdeck_fterror.png            → "steamdeck_fterror.png"
Resources/steamdeck_switchprompt.png       → "steamdeck_switchprompt.png"
```

`LogicalName` flattens the resource name to a bare filename. Every consumer asks for the
bare name through `AppUtil.GetEmbeddedResourceBytes` (`AppUtil.cs.md`), which calls
`assembly.GetManifestResourceStream(resourceName)` and throws
`ArgumentException("Resource X not found")` on a miss:

| Resource name requested | Requested by |
| --- | --- |
| `NotoSansCJKjp-Regular.otf` | `AppFonts.cs:19` (`TEXT_FONT_NAME`), opened at `AppFonts.cs:40` (`#if LINUX`, `FontFamily.FromStreams`) / `AppFonts.cs:43` (Windows, `RegisterWindowsFont`) |
| `FontAwesome5FreeSolid.otf` | `AppFonts.cs:20` (`ICON_FONT_NAME`), opened at `AppFonts.cs:41` / `AppFonts.cs:44` |
| `logo.png` | `Components/SettingsPage/Tabs/SettingsTabAbout.cs:39` |
| `bg_logo.png` | `Components/Background.cs:21` |
| `steamdeck_fts.png` | `Components/FtsPage.cs:17` |
| `steamdeck_fterror.png` | `Components/FtsPage.cs:18` |
| `steamdeck_switchprompt.png` | `Components/SteamDeckPromptPage.cs:14` |

**Contract:** dropping `LogicalName` renames the resource to
`XIVLauncher.Core.Resources.<path>.<file>` and breaks every consumer above at *run* time
(the build stays green). Adding a new image requires both an `EmbeddedResource` line and a
matching `LogicalName`.

### 7.2 Localization suite (15 resx, 170 base entries)

```
Resources/Localization/Strings.resx        (base, 170 <data> entries)
Strings.de / es / fr / hu / it / ja / ko / nl / no / pt / ru / sv / tw / zh .resx   (14 locales)
Strings.Designer.cs                        generated, checked in
```

- `EmbeddedResource Update="Resources\Localization\Strings.resx"` with
  `Generator=ResXFileCodeGenerator`, `LastGenOutput=Strings.Designer.cs`; the paired
  `Compile Update` declares `DesignTime`/`AutoGen`/`DependentUpon` — this is IDE wiring,
  not build behaviour.
- `Strings.Designer.cs` builds a `ResourceManager` with base name
  `XIVLauncher.Core.Resources.Localization.Strings`, from `typeof(Strings).Assembly`.
- Locale files are matched by the SDK's `AssignCulture` step (file-name culture), so the 14
  locale resources land in **satellite assemblies**
  (`<culture>/XIVLauncher.Core.resources.dll`) rather than in the main assembly.
- Lookup is driven by the thread's UI culture; `Strings.Culture` (`Strings.Designer.cs:39`)
  is the explicit override and no source file in this repo sets it.
- Crowdin wiring: `.github/crowdin.yml` maps
  `src/XIVLauncher.Core/Resources/Localization/Strings.resx` →
  `Strings.%two_letters_code%.resx`, i.e. the file-name convention is the contract with the
  translation service.

**Quirk:** `Strings.tw.resx` is not a valid two-letter culture (`zh-TW`/`zh-Hant` is). The
satellite is generated under the `tw` culture name, which the `zh` locale users never
resolve to; Traditional Chinese strings are effectively unreachable through culture lookup.

### 7.3 Native `Content` items

`None Remove=` + `Content Include=` with `CopyToOutputDirectory=PreserveNewest` reclassifies
SDK-globbed files from `None` (IDE-visible) to `Content` (copied on build/publish):

| Item | Condition | Comment |
| --- | --- | --- |
| `libsteam_api64.so` (416 KB, committed) | `'$(IsLinux)'=='true'` | shipped beside the executable |
| `libsteam_api64.dylib` (609 KB, committed) | `'$(IsOSX)'=='true'` | see §4: RID-gated only |
| `Resources/binaries/aria2c-xl.exe` (5 MB, committed) | `'$(RuntimeIdentifier)' == 'win10-x64'` | **never matches** — see §11 |

These are custom-built Steamworks natives matching `goaaats.Steamworks` (the
`XIVLauncher.Common.Unix` comment names the `chippy` branch of Facepunch.Steamworks); the
Windows `steam_api64.dll`/`steam_api.dll` are copied by the **submodule's**
`XIVLauncher.Common.Windows.csproj` (host-`IsOSPlatform('Windows')` gated), not by this file.

### 7.4 Analyzer-visible property

`<CompilerVisibleProperty Include="AssemblyVersion" />` is what lets the source generator
read `build_property.AssemblyVersion`. Remove it and the generator's
`GlobalOptions.TryGetValue` fails, so `AppUtil.VersionInfo`'s constructor is **not
generated** — the build fails at `new VersionInfo()`, not at compile time of this file.

## 8. Version pipeline

`Version`, `FileVersion`, `AssemblyVersion` are all `1.5.2` (`$(Version)`).

`XIVLauncher.VersionGenerator` (`VersionSourceGenerator.cs`, an `ISourceGenerator`) emits
`AppUtil.VersionInfo.g.cs` when the `AssemblyVersion` build property is visible:

```csharp
public VersionInfo() {
    Version = ("<context.Compilation.AssemblyName>", new Version("<AssemblyVersion>"));
}
```

so the generated ctor is
`Version = ("XIVLauncher.Core", new Version("1.5.2"))` — `AssemblyName`, not the
`Product`/`FileVersion` value. `AppUtil.VersionInfo` (`AppUtil.VersionInfo.cs.md`) is the
partial half that declares the singleton `Instance()`; `AppUtil.GetAssemblyVersion()`
returns `VersionInfo.Instance().Version.FileVersion.ToString()`.

Note the interaction: `new Version("1.5.2")` is a 3-part version, so `ToString()` prints
`1.5.2` (no `.0` revision), and `AssemblyVersion` is what the generator sees — bumping
`Version` alone moves `AssemblyVersion` too because it is defined as `$(Version)`.

Version appears in the launcher window title (`LauncherForm.cs:24`), the log banner
(`Program.cs:73`), `SettingsTabAbout.cs:28`
(`XIVLauncher Core v{GetAssemblyVersion()}({GetGitHash()})`),
`OtpEntryPage.cs:195` (`OtpListener("core-" + GetAssemblyVersion())`) and
`Support/Troubleshooting.cs:124`.

## 9. Git metadata targets

```
GetGitHash   BeforeTargets="WriteGitHash"  Condition="'$(BuildHash)' == ''"
  Exec: git -C "<ProjectDir>" describe --long --always --dirty > obj/.../gitver
  ReadLinesFromFile → @(GitVersion) → BuildHash
WriteGitHash BeforeTargets="CoreCompile"
  WriteCodeFragment → obj/.../CustomAssemblyInfo.cs, compiled into the build
  AssemblyMetadata "GitHash"   = $(BuildHash)
  AssemblyMetadata "BuildOrigin" = $(GITHUB_REPOSITORY)
```

Runtime readers (`AppUtil.cs.md`):

| Attribute | Reader | Consumers |
| --- | --- | --- |
| `GitHash` | `AppUtil.GetGitHash()` | `Program.cs:73` log banner, `SettingsTabAbout.cs:28`, `Support/Troubleshooting.cs:125` (`<unavailable>` fallback) |
| `BuildOrigin` | `AppUtil.GetBuildOrigin()` | `Support/Troubleshooting.cs:126`: `Official = AppUtil.GetBuildOrigin() == "goatcorp/XIVLauncher.Core"` |

**Contract and consequence:** `BuildOrigin` comes from the `GITHUB_REPOSITORY` **environment
variable** (set explicitly in `.github/workflows/build.yml` and `make-release.yml`). A
fork, a local build, or any build without that env var yields `BuildOrigin == ""`, so
`Troubleshooting` reports **`Official = false`** — the "official build" flag in the
troubleshooting payload is a provenance marker, not a version check.

Mechanical notes:

- `Condition="'$(BuildHash)' == ''"` means `BuildHash` can be pre-set from the CLI/env to
  skip the `git` invocation (needed when `.git` is absent, e.g. tarball packaging).
- The `git` call is a shell `Exec` with `$(ProjectDir.Replace('\','\\'))` — path doubling
  for quoting; it runs on Linux shells as well.
- `fetch-depth: 0` in CI is what makes `git describe --long --always --dirty` meaningful;
  shallow clones give a bare hash with no tags.
- `--dirty` appends `-dirty`; the string is displayed verbatim in the About tab.
- `WriteGitHash` runs before `CoreCompile`, so `CustomAssemblyInfo.cs` is in
  `obj/<config>/net10.0/`.

## 10. Release path mapping

```
Condition Configuration==Release:
  AppOutputBase = $(MSBuildProjectDirectory)\
  PathMap       = $(AppOutputBase)=C:\goatsoft\xl\XIVLauncher.Core\
```

Every `XIVLauncher.*` project in the repo carries the same pattern with its own folder.
It rewrites source paths embedded in the PDB/assembly from the *build machine's* absolute
path to a fixed `C:\goatsoft\xl\…` root, i.e. reproducible PDB path names and no
leakage of packager home directories. The mapping is Windows-style text even when
producing Linux binaries — deliberate, since it is only a string table consumed by the
debugger. `AppOutputBase`'s trailing `\` matters: `PathMap` matching is prefix-based.

## 11. Build and publish contract

From `.github/workflows/build.yml` (working dir `./src/XIVLauncher.Core/`):

```
dotnet restore
dotnet publish -r linux-x64 --sc --configuration Release --no-restore -o ./dist/XIVLauncher.Core
dotnet publish -r win-x64  --sc --configuration Release            -o ./dist/XIVLauncher.Core-win10x64
dotnet test --no-build --verbosity normal
```

`make-release.yml` (tag `*.*.*`, `if: github.repository == 'goatcorp/XIVLauncher.Core'`)
runs the same two publishes, then generates `nuget-dependencies.json` with
`flatpak-dotnet-generator.py --dotnet 10 --freedesktop 25.08 … XIVLauncher.Core.csproj`
— the csproj **is** the input to Flatpak dependency generation, so every
`PackageReference` version here is mirrored into the Flatpak manifest (see
`generate_flatpak_nuget_deps.yml`).

Windows builds run `dotnet build src/XIVLauncher.Core/XIVLauncher.Core.csproj -r win-x64 -c Debug`
(verified end-to-end: conditional TFM picks `net10.0-windows`, `Eto.Platform.Windows` restores,
output lands in `bin/Debug/net10.0-windows/win-x64/`).

Three concrete mismatches to keep in mind:

1. **`win10-x64` is not a publish target.** `RuntimeIdentifiers` lists `win-x64`, CI
   publishes `win-x64`, and the output directory is *named* `XIVLauncher.Core-win10x64` —
   a legacy label. The `Content` condition `'$(RuntimeIdentifier)' == 'win10-x64'`
   therefore never fires, so `aria2c-xl.exe` is **not copied** by CI Windows builds.
2. **Even if it were copied, the path is wrong.** Content preserves its relative structure
   (`Resources/binaries/aria2c-xl.exe`), while the runtime lookup is
   `Path.Combine(Paths.ResourcesPath, "aria2c-xl.exe")` with
   `Paths.ResourcesPath = AppContext.BaseDirectory + "/Resources"`
   (`lib/…/XIVLauncher.Common/Paths.cs:15`) — i.e. it expects
   `Resources/aria2c-xl.exe`, not `Resources/binaries/aria2c-xl.exe`. On Linux,
   `AriaPatchAcquisition` falls back to the system `aria2c`; on Windows there is no
   fallback (`lib/…/Aria/AriaPatchAcquisition.cs:43-48`).
3. **`dotnet test` has nothing to run** — no test packages, no test project in
   `src/XIVLauncher.Core` (the submodule's `XIVLauncher.Common.Tests` exists in the
   submodule but is not in `XIVLauncher.Core.sln`).

Publish output (self-contained, single-file) contains the managed bundle, the native
runtime, the `Content` items that matched (§7.3), and the satellite assemblies for each
culture (§7.2). `PathMap` applies only under `Configuration=Release`.

## 12. Quirks and hazards

- `Nullable=enable` in this project, `disable` in the fork-local `XIVLauncher.Common.Unix`
  — nullable annotations do not flow across the project boundary, so `null` handling
  assumptions differ per project.
- `IsOSX` naming asymmetry (§4): macOS host builds with no RID silently lose both the
  `OSX` define and the `.dylib` copy.
- `OSX` define is emitted and unread in `src/`; `LINUX` is read in five files (GTK P/Invoke
  hold, news width cap, clipboard, `FromStreams` font registration — §4).
  `WINE_XIV_ARCH_LINUX` is read (submodule) and never defined. Both directions of dead code
  exist.
- `FLATPAK` is not defined by this file; the Flatpak-specific UI text and FTS startup path
  only exist in builds that pass `ExtraDefineConstants=FLATPAK`.
- `Resources/binaries/aria2c-xl.exe`, `libsteam_api64.so`, `libsteam_api64.dylib` are
  committed binaries in the repo (5 MB, 416 KB, 609 KB) — no fetch target, no checksum,
  no provenance recorded in the project file.
- Adding a RID to `RuntimeIdentifiers` without adding a matching `Content` condition
  (and vice-versa) produces a published app that starts and then fails at Steam init.
- The `CompilerVisibleProperty` + generator pair is the only place `AssemblyVersion`
  reaches source code; nothing reads `FileVersion`/`InformationalVersion` (no
  `AssemblyInformationalVersion` is set).
- `Configurations=Debug;Release` declares the project's configuration set; building with
  any other `-p:Configuration` value is outside it.

## 13. Extension points

- **New platform**: add the RID to `RuntimeIdentifiers`, add the `Is<Platform>` +
  `DefineConstants` group, add the Steam native `Content` condition. All three must agree;
  the pattern is per-RID string comparison, not `IsOSPlatform`.
- **New build flavour**: pass `-p:ExtraDefineConstants=<DEFINE>`, then add the `#if` site.
  This is the supported hook; do not add `DefineConstants` groups for flavour flags.
- **New embedded asset**: `EmbeddedResource Include=` **and** `LogicalName=`, then add the
  name constant in the consumer (`AppFonts` uses `TEXT_FONT_NAME`/`ICON_FONT_NAME`; on
  Windows a font family must be registered via `AppFonts.RegisterWindowsFont` before any
  control is created, so `AppFonts.Initialize` runs at `Program.cs:281` before the form
  is built).
- **New locale**: add `Strings.<code>.resx` with a valid two-letter culture code matching
  Crowdin's `%two_letters_code%`; do not add custom codes like `tw`.
- **Version bump**: change `Version` only — `FileVersion`/`AssemblyVersion` follow, and the
  generated `VersionInfo` follows.
- **Provenance**: set `GITHUB_REPOSITORY` at build time to control
  `Troubleshooting.Official`.

## 14. Cross-references

- `docs/XIVLauncher.Core/Program.cs.md` — startup order, window title version/hash, `PlatformID` dispatch, `RuntimeInformation.IsOSPlatform` gates
- `docs/XIVLauncher.Core/AppUtil.cs.md` — `GetEmbeddedResourceBytes`, `GetGitHash`, `GetBuildOrigin`, `GetAssemblyVersion`
- `docs/XIVLauncher.Core/AppUtil.VersionInfo.cs.md` — the partial half of the generated ctor
- `docs/XIVLauncher.Core/AppFonts.cs.md`, `docs/XIVLauncher.Core/Resources/Localization/*` — resource consumers
- `docs/XIVLauncher.Core/Support/Troubleshooting.cs.md` — `Official`/`LauncherHash` consumers
- `docs/XIVLauncher.Common.Unix/XIVLauncher.Common.Unix.csproj.md` — the sibling project spec
- `docs/XIVLauncher.Core/Configuration/ILauncherConfig.cs.md` — `Config.Net`-backed schema
