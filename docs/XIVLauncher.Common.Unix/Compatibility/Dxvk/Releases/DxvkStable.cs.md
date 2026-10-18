# DxvkStable.cs — Specification

**File:** `src/XIVLauncher.Common.Unix/Compatibility/Dxvk/Releases/DxvkStable.cs` · 8 lines · 499 bytes · ASCII, LF, no BOM
**Namespace:** `XIVLauncher.Common.Unix.Compatibility.Dxvk.Releases` (`:1`)
**Declaration:** `public sealed class DxvkStableRelease : IDxvkRelease` (`:3`) — no constructor, body is
three get-only properties (`:5`–`:7`)

**Role:** the download descriptor behind `DxvkVersion.Stable` — GPLAsync-patched DXVK 2.6.1.

## 1. Constants

| Member | Line | Value |
| --- | --- | --- |
| `Name` | `:5` | `dxvk-gplasync-v2.6.1-1` |
| `DownloadUrl` | `:6` | `https://raw.githubusercontent.com/goatcorp/xlcore-distrib/refs/heads/main/dxvk-gplasync-v2.6.1-1.tar.gz` |
| `Checksum` | `:7` | one SHA-512 digest (lowercase hex, 128 chars) |

Notes:
* The URL is a **`raw.githubusercontent.com` blob**, not a GitHub release asset — the tarball is committed
  into the `goatcorp/xlcore-distrib` repo's `main` branch. Renames therefore happen by commit, not by
  release-edit, and the digest must be updated in lockstep.
* `Name` doubles as the tarball's root directory *and* the on-disk directory under `<toolsFolder>/dxvk`
  (see [../Dxvk.cs.md](../Dxvk.cs.md) §4 for the invariant at `:58`/`:85`).
* `x64` is the only subdirectory `InstallDxvk` reads (`Dxvk.cs:58` appends `"x64"`); the i386 set in the
  archive is ignored.

## 2. Instantiation

`Dxvk.cs:52` — `DxvkVersion.Stable => new DxvkStableRelease()`; selected by ini key `DxvkVersion`
(`ILauncherConfig.cs:64`, defaulted at `Program.cs:131` to `Stable`).

## 3. Siblings / interface

[../IDxvkRelease.cs.md](../IDxvkRelease.cs.md) · [./DxvkBeta.cs.md](DxvkBeta.cs.md) ·
[./DxvkLegacy.cs.md](DxvkLegacy.cs.md)

## 4. Referenced

* Referenced by: `Dxvk/Dxvk.cs:52` only (plus `Dxvk.cs:8` namespace import).
* References: `IDxvkRelease` only; no code from the fork beyond the interface (contrast the wine releases,
  which take `WineReleaseDistro`).
