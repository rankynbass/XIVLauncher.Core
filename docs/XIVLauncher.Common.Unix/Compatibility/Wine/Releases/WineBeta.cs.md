# WineBeta.cs — Specification

**File:** `src/XIVLauncher.Common.Unix/Compatibility/Wine/Releases/WineBeta.cs` · 12 lines · 1065 bytes · ASCII, LF, no BOM
**Namespace:** `XIVLauncher.Common.Unix.Compatibility.Wine.Releases` (`:1`)
**Declaration:** `public sealed class WineBetaRelease(WineReleaseDistro wineDistroId) : IWineRelease` (`:3`)

**Role:** the download descriptor for the "Beta" managed wine track: the same wine-xiv staging-fsync
build 10.8 **with** the `lsteamclient` patches (i.e. no `-nolsc` suffix), which is what
`WineManagedVersion.Beta` selects ("testing ground for the newest wine changes … with lsteamclient patches",
wording from [../WineSettings.cs.md](../WineSettings.cs.md) §2).

---

## 1. Table of constants

| Member | Line | Value |
| --- | --- | --- |
| `Name` | `:5` | `wine-xiv-staging-fsync-git-10.8.r0.a2ca9e4` |
| `DownloadUrl` | `:6` | `https://github.com/goatcorp/wine-xiv-git/releases/download/10.8.r0.a2ca9e4/wine-xiv-staging-fsync-git-{wineDistroId}-10.8.r0.a2ca9e4.tar.xz` |
| `Checksums` | `:7`–`:11` | 3 digests, commented `arch` (`:8`), `fedora` (`:9`), `ubuntu` (`:10`) |

Key relationship — Beta vs Stable:

| | release tag | `Name` | asset suffix |
| --- | --- | --- | --- |
| [./WineStable.cs.md](./WineStable.cs.md) | `10.8.r0.a2ca9e4` | `…-10.8.r0.a2ca9e4-nolsc` | `-nolsc` |
| this file | `10.8.r0.a2ca9e4` | `…-10.8.r0.a2ca9e4` | (none) |

They are **two different tarballs from the same release**, so they also unpack to two different
directories under `<toolsFolder>/wine`. `CompatibilityTools.IsToolDownloaded`
(`CompatibilityTools.cs:41`, see [../../CompatibilityTools.cs.md](../../CompatibilityTools.cs.md) §4) keys on
`WineBinPath`, which follows `Settings.Release.Name` — switching `WineManagedVersion` from Stable to Beta
therefore re-downloads rather than reusing the existing tree.

## 2. Construction

Same shape as Stable: constructed only for `WineStartupType.Managed` + `WineManagedVersion.Beta`
at [../WineSettings.cs.md](../WineSettings.cs.md) `:61`. `wineDistroId` comes from
`CompatUtil.GetWineIdForDistro()` (`WineSettings.cs:57`); the `macOS` enum member produces a
non-existent URL (see [./WineStable.cs.md](./WineStable.cs.md) §2 for the URL-vs-asset table, which applies
here with `-nolsc` removed).

## 3. Upstream counterpart

None — upstream never offered a lsteamclient track; its single hard-coded build is the 8.5 one
(documented in [../WineSettings.cs.md](../WineSettings.cs.md) §6).

## 4. Referenced by / referenced

* Referenced by: `Wine/WineSettings.cs:61`.
* References: `WineReleaseDistro` ([../../CompatUtil.cs.md](../../CompatUtil.cs.md)), `IWineRelease`
  ([./IWineRelease.cs.md](./IWineRelease.cs.md)).
