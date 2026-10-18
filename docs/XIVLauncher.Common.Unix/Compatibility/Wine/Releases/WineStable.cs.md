# WineStable.cs — Specification

**File:** `src/XIVLauncher.Common.Unix/Compatibility/Wine/Releases/WineStable.cs` · 12 lines · 1097 bytes · ASCII, LF, no BOM
**Namespace:** `XIVLauncher.Common.Unix.Compatibility.Wine.Releases` (`:1`)
**Declaration:** `public sealed class WineStableRelease(WineReleaseDistro wineDistroId) : IWineRelease` (`:3`) — a `sealed` class whose **only member is its constructor**; the class body only holds get-only properties (`:5`–`:11`)

**Role:** the download descriptor for the "Stable" managed wine track: wine-xiv staging-fsync build 10.8,
**-nolsc** variant (no `lsteamclient` patches), one tarball per distro.

---

## 1. Table of constants

| Member | Line | Value |
| --- | --- | --- |
| `Name` | `:5` | `wine-xiv-staging-fsync-git-10.8.r0.a2ca9e4-nolsc` |
| `DownloadUrl` | `:6` | `https://github.com/goatcorp/wine-xiv-git/releases/download/10.8.r0.a2ca9e4/wine-xiv-staging-fsync-git-{wineDistroId}-10.8.r0.a2ca9e4-nolsc.tar.xz` |
| `Checksums` | `:7`–`:11` | 3 digests, commented `arch` (`:8`), `fedora` (`:9`), `ubuntu` (`:10`) |

Notes:
* `Name` is written as an `f"…"` literal (`$"…"`) with **no placeholders** — cosmetic; the value is static,
  unlike `DownloadUrl`, which interpolates the constructor parameter.
* `Name` is what `CompatibilityTools.WineBinPath` (`CompatibilityTools.cs:28`) joins onto
  `<toolsFolder>/wine`, so the on-disk layout is `<toolsFolder>/wine/wine-xiv-staging-fsync-git-10.8.r0.a2ca9e4-nolsc/bin/wine64`.
  The `-nolsc` suffix therefore appears in the path, distinguishing it from
  [./WineBeta.cs.md](./WineBeta.cs.md) (`:5`, same release tag, no `-nolsc`).
* The 3 checksums are keyed by distro **only through the trailing comment**; the array order is
  irrelevant because verification is `.Any(...)` (see [../../CompatUtil.cs.md](../../CompatUtil.cs.md) §3).
  Ordering here is arch/fedora/ubuntu, whereas `WineLegacy.cs` orders fedora/ubuntu/arch.

## 2. The distro parameter

`wineDistroId` is a `WineReleaseDistro` (`ubuntu`, `fedora`, `arch`, `macOS`) from
[../../CompatUtil.cs.md](../../CompatUtil.cs.md) §1. Its **member name** is spliced straight into the URL,
so the four enum members must stay in lockstep with the upstream release asset names:

| `wineDistroId` | URL tail | Covered? |
| --- | --- | --- |
| `ubuntu` | `...-ubuntu-10.8.r0.a2ca9e4-nolsc.tar.xz` | yes (`:10` digest) |
| `fedora` | `...-fedora-…` | yes (`:9`) |
| `arch` | `...-arch-…` | yes (`:8`) |
| `macOS` | `...-macOS-…` | **no** — no such asset, and no digest |

`CompatUtil.GetWineIdForDistro()` returns `macOS` on OSX ([../../CompatUtil.cs.md](../../CompatUtil.cs.md) §2,
`:22`–`:24`), which yields a 404 URL and a checksum failure (`InvalidDataException`) rather than a clean
"macOS is unsupported" error. Nothing on that path is guarded here; see
[../../CompatibilityTools.cs.md](../../CompatibilityTools.cs.md) §5 for the throw site.

Construction site: [../WineSettings.cs.md](../WineSettings.cs.md) `:60`
(`WineManagedVersion.Stable => new WineStableRelease(wineDistroId)`), i.e. only when
`StartupType == Managed` and `WineManagedVersion == Stable`.

## 3. Upstream counterpart

**None.** Upstream pins one wine build (`8.5.r4.g4211bac7`, the *Legacy* track) via
`WINE_XIV_RELEASE_URL`/`WINE_XIV_RELEASE_NAME` constants selected at **compile time** by
`#if WINE_XIV_ARCH_LINUX` / `#elif WINE_XIV_FEDORA_LINUX` in its `CompatibilityTools.cs`. The fork moved
that decision to runtime (`CompatUtil`) and re-expressed the table as classes. See
[../WineSettings.cs.md](../WineSettings.cs.md) §6 for the whole refactor.

## 4. Referenced by / referenced

* Referenced by: `Wine/WineSettings.cs:60` only (plus the `using` at `WineSettings.cs:4`).
* References: `WineReleaseDistro` from [../../CompatUtil.cs.md](../../CompatUtil.cs.md);
  implements [./IWineRelease.cs.md](./IWineRelease.cs.md).
