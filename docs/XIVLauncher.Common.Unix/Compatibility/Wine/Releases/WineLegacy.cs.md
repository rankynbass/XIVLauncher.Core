# WineLegacy.cs — Specification

**File:** `src/XIVLauncher.Common.Unix/Compatibility/Wine/Releases/WineLegacy.cs` · 12 lines · 1073 bytes · ASCII, LF, no BOM
**Namespace:** `XIVLauncher.Common.Unix.Compatibility.Wine.Releases` (`:1`)
**Declaration:** `public sealed class WineLegacyRelease(WineReleaseDistro wineDistroId) : IWineRelease` (`:3`)

**Role:** the download descriptor for the "Legacy" managed wine track (WINE 8.5), the one build that
upstream hard-coded — the fork turned it into an option.

---

## 1. Table of constants

| Member | Line | Value |
| --- | --- | --- |
| `Name` | `:5` | `wine-xiv-staging-fsync-git-8.5.r4.g4211bac7` |
| `DownloadUrl` | `:6` | `https://github.com/goatcorp/wine-xiv-git/releases/download/8.5.r4.g4211bac7/wine-xiv-staging-fsync-git-{wineDistroId}-8.5.r4.g4211bac7.tar.xz` |
| `Checksums` | `:7`–`:11` | 3 digests, commented `fedora` (`:8`), `ubuntu` (`:9`), `arch` (`:10`) |

Ordering note: this file lists the digests **fedora, ubuntu, arch** (`:32`–`:34` in upstream numbering;
`:8`–`:10` relative to the class body), while [./WineStable.cs.md](./WineStable.cs.md) and
[./WineBeta.cs.md](./WineBeta.cs.md) list arch, fedora, ubuntu. The order is only documentation —
`CompatUtil.EnsureChecksumMatch` matches with `.Any(...)` (see [../../CompatUtil.cs.md](../../CompatUtil.cs.md)
§3) — but it makes copy-pasting a digest between files easy to mis-comment.

## 2. Relationship to upstream

Upstream's `CompatibilityTools.cs` (fork counterpart:
[../../CompatibilityTools.cs.md](../../CompatibilityTools.cs.md) §3/§4) hard-coded
`https://github.com/goatcorp/wine-xiv-git/releases/download/8.5.r4.g4211bac7/wine-xiv-staging-fsync-git-<distro>-8.5.r4.g4211bac7.tar.xz`
chosen by `#if WINE_XIV_ARCH_LINUX` / `#elif WINE_XIV_FEDORA_LINUX` / `#else ubuntu`. The URL string here is
byte-identical to the `#else` branch's, so a fork user on `WineManagedVersion.Legacy` downloads what upstream
users always got.

## 3. Construction / distro parameter

Identical to the siblings: built at [../WineSettings.cs.md](../WineSettings.cs.md) `:62`
(`WineManagedVersion.Legacy => new WineLegacyRelease(wineDistroId)`), inside the `Managed` branch.
The `macOS` member of `WineReleaseDistro` is again unmapped to a real asset
([./WineStable.cs.md](./WineStable.cs.md) §2 documents the table).

## 4. Referenced by / referenced

* Referenced by: `Wine/WineSettings.cs:62`.
* References: `WineReleaseDistro` ([../../CompatUtil.cs.md](../../CompatUtil.cs.md)), `IWineRelease`
  ([./IWineRelease.cs.md](./IWineRelease.cs.md)).
