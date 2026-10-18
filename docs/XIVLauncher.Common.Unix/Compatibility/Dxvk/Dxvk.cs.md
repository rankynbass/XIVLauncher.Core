# Dxvk.cs — Specification

**File:** `src/XIVLauncher.Common.Unix/Compatibility/Dxvk/Dxvk.cs` · 90 lines · 2876 bytes · ASCII, LF, no BOM
**Namespace:** `XIVLauncher.Common.Unix.Compatibility.Dxvk` (`:12`) — one level deeper than its folder
(`Compatibility/Dxvk/`), which is why callers reach it as `Dxvk.Dxvk.InstallDxvk`
(`CompatibilityTools.cs:90`, class `Dxvk` inside namespace `…Dxvk`).
**Declarations:** `public enum DxvkVersion` (`:14`–`:28`), `public enum DxvkHudType` (`:30`–`:40`),
`public static class Dxvk` (`:42`, methods `:44`–`:88`)

**Role:** owns everything DXVK-shaped: the user-facing version choice, the HUD choice, the download of one
of three release tarballs, and installing the extracted `x64` DLLs into the wine prefix's `system32`.

---

## 1. `DxvkVersion` — the ini vocabulary

| Member | Line | `SettingsDescription` label / help | Release class |
| --- | --- | --- | --- |
| `Stable` | `:18` | "DXVK 2.6.1 with GPLAsync patches. Requires Mesa 24.0 or Nvidia 535 drivers. Ideal for most." | `DxvkStableRelease` |
| `Beta` | `:21` | "DXVK 2.7 with GPLAsync patches. Requires Mesa 25.0 or Nvidia 550 drivers. Ideal for modern graphics cards." | `DxvkBetaRelease` |
| `Legacy` | `:24` | "DXVK 1.10.3 with Async patches. Requires Mesa 22.0 or Nvidia 470 drivers. Ideal for older graphics card." | `DxvkLegacyRelease` |
| `Disabled` | `:27` | "Use OpenGL/WineD3D instead. Slow, and might not work with Dalamud." | none (short-circuit) |

The help strings mention Mesa/Nvidia versions **only as prose** — nothing in the fork probes driver
versions. `Disabled` is honoured in two places: the `InstallDxvk` early-return (`:46`–`:49`) and the
`WINEDLLOVERRIDES` tail in `CompatibilityTools.cs:153`/`:158` (`ogl` ⇒ `b`, else `n,b`).

## 2. `DxvkHudType`

| Member | Line | `DXVK_HUD` value (assigned in `CompatibilityTools.cs:169`–`:182`) |
| --- | --- | --- |
| `None` | `:33` | `"0"` |
| `Fps` | `:36` | `"fps"` |
| `Full` | `:39` | `"full"` |

`SettingsDescription` captions here are UI-side ("Show nothing" / "Only show FPS" / "Show everything").
`DxvkHudType` is the only enum whose ini field is **non-nullable** in `ILauncherConfig.cs:68`
(`public DxvkHudType DxvkHudType`), so it reaches `CompatibilityTools` without a `??` fallback
(`Program.cs:367`) — a missing ini key yields the zero value (`None`) rather than a default from a probe.

## 3. `InstallDxvk(HttpClient, prefix, installDirectory, version)` (`:44`–`:72`)

| Line | Step |
| --- | --- |
| `:46`–`:49` | `if (version is DxvkVersion.Disabled) return;` — **the only no-op exit** |
| `:50`–`:56` | `IDxvkRelease release = version switch { Stable → new DxvkStableRelease() (`:52`), Beta → `DxvkBetaRelease` (`:53`), Legacy → `DxvkLegacyRelease` (`:54`), `_ → throw new NotImplementedException()` (`:55`) }` |
| `:58` | `dxvkPath = installDirectory / release.Name / "x64"` |
| `:59`–`:63` | `if (!Directory.Exists(dxvkPath))` → log "DXVK does not exist, downloading" (`:61`) → `await DownloadDxvk(httpClient, installDirectory, release.DownloadUrl, release.Checksum)` (`:62`) |
| `:65` | `system32 = prefix / drive_c / windows / system32` |
| `:66` | `files = Directory.GetFiles(dxvkPath)` |
| `:68`–`:71` | `foreach` → `File.Copy(fileName, system32/<basename>, true)` (`:70`, overwrite = `true`) |

Observations:

* The release classes take **no constructor arguments** (`Dxvk/Releases/DxvkStable.cs:3`), unlike the wine
  ones; there is no distro dimension for Dxvk.
* `Disabled` is not in the switch, so it never reaches `:55`; the `_` arm is a placeholder for future
  members (and, unlike `CompatibilityTools.cs:28`'s ` ArgumentOutOfRangeException`, uses
  `NotImplementedException` — a different exception type family, see §6).
* The `system32` copy loop runs **every** call, even when the tarball already exists: `EnsureTool`
  (`CompatibilityTools.cs:90`) calls `InstallDxvk` on every launch, so each start re-copies the Dxvk
  DLL set over itself with `overwrite = true`. Consequence: user-patched Dxvk DLLs in
  `drive_c/windows/system32` are silently restored.
* `Directory.GetFiles(dxvkPath)` runs after the download branch, but a download that produced a
  directory *without* an `x64` subdirectory (tarball layout change) makes `:66` read a non-existent path;
  the resulting IO error propagates out of the `async Task` and faults `EnsureTool`
  (`MainPage.cs:709` handles a faulted task, so the launcher keeps running with a broken DXVK install).

## 4. `DownloadDxvk(httpClient, installDirectory, url, checksum)` (`:74`–`:88`)

| Line | Step |
| --- | --- |
| `:76` | `tempPath = PlatformHelpers.GetTempFileName()` — a **file** path (not a `DirectoryInfo`, unlike `EnsureTool`'s `tempPath` parameter in `CompatibilityTools.cs:81`) |
| `:78` | `File.WriteAllBytes(tempPath, await httpClient.GetByteArrayAsync(url))` — synchronous write (the wine counterpart uses `WriteAllBytesAsync`, `CompatibilityTools.cs:99`) |
| `:80` | `CompatUtil.EnsureChecksumMatch(tempPath, [checksum])` — the one-element **array literal** adapting the singular `IDxvkRelease.Checksum` (`:7`) to the `string[]` API |
| `:82` | `throw new InvalidDataException("SHA512 checksum verification failed")` |
| `:85` | `PlatformHelpers.Untar(tempPath, installDirectory.FullName)` — extracts into `<tools>/dxvk/`, where the archive's top-level directory name must equal `release.Name` |
| `:87` | `File.Delete(tempPath)` — again skipped when `:82` throws |

The archive-name coupling is a real invariant: `DxvkStable.cs:5` `Name` must equal the tarball's root
directory or `:58`'s `dxvkPath` never exists and the launcher re-downloads forever.

## 5. Consumers

| Site | Use |
| --- | --- |
| `CompatibilityTools.cs:90` | `await Dxvk.Dxvk.InstallDxvk(httpClient, Settings.Prefix, dxvkDirectory, dxvkVersion)` — the only live call |
| `CompatibilityTools.cs:34`/`:153`/`:169` | `DxvkVersion` captured, `ogl` test, HUD switch |
| `Program.cs:131` | `Config.DxvkVersion ??= DxvkVersion.Stable` (defaulting) |
| `Program.cs:367` | passed to `CompatibilityTools` (`Config.DxvkHudType` un-defaulted) |
| `SettingsTabWine.cs` | `SettingsEntry<DxvkVersion>`/`<DxvkHudType>` rows (import at `:6`) |
| `docs/XIVLauncher.Core/Configuration/ILauncherConfig.cs.md` | `:64` `DxvkVersion?`, `:68` `DxvkHudType` |

## 6. Upstream delta (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common.Unix/Compatibility/Dxvk.cs`)

* Upstream had **no enums and no version parameter**: `InstallDxvk(HttpClient, prefix, installDirectory)`
  with `private const string DXVK_DOWNLOAD = "…/Sporif/dxvk-async/1.10.1/…"` and
  `DXVK_NAME = "dxvk-async-1.10.1"`. The fork's `Legacy` row is the same *project* (bumped to 1.10.3), its
  `Stable`/`Beta` rows come from `goatcorp/xlcore-distrib`.
* Upstream had no checksum step; the fork added `:80`/`:82`.
* Upstream had no `Disabled` concept; `WINEDLLOVERRIDES` was unconditional.
* Upstream file sat at `Compatibility/Dxvk.cs` with namespace `…Compatibility`; the fork moved it to
  `Compatibility/Dxvk/` with namespace `…Compatibility.Dxvk` and added `Releases/` sub-classes
  ([Releases/DxvkStable.cs.md](Releases/DxvkStable.cs.md) etc.), leaving the interface
  [IDxvkRelease.cs.md](IDxvkRelease.cs.md) in the parent namespace (see its §3 for the naming asymmetry).

## 7. Referenced by / referenced

* Referenced by: `CompatibilityTools.cs` (`:12` import, `:90` call, `:34`/`:153`/`:169` enum use),
  `Program.cs:19` (import), `SettingsTabWine.cs:6`, `ILauncherConfig.cs` fields.
* References: `IDxvkRelease` ([IDxvkRelease.cs.md](IDxvkRelease.cs.md)), the three release classes
  ([Releases/DxvkBeta.cs.md](Releases/DxvkBeta.cs.md), [Releases/DxvkLegacy.cs.md](Releases/DxvkLegacy.cs.md),
  [Releases/DxvkStable.cs.md](Releases/DxvkStable.cs.md)), `CompatUtil.EnsureChecksumMatch`
  ([../../CompatUtil.cs.md](../CompatUtil.cs.md) §3), `PlatformHelpers.Untar` /
  `PlatformHelpers.GetTempFileName` (docs/XIVLauncher.Core sibling: Common/Util), `HttpClient`.
