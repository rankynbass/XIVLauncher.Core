# IDxvkRelease.cs — Specification

**File:** `src/XIVLauncher.Common.Unix/Compatibility/Dxvk/IDxvkRelease.cs` · 8 lines · 175 bytes · ASCII, LF, no BOM
**Namespace:** `XIVLauncher.Common.Unix.Compatibility.Dxvk` (`:1`)
**Declaration:** `public interface IDxvkRelease` (`:3`), members `:5`–`:7`

**Role:** the Dxvk counterpart of [../Wine/Releases/IWineRelease.cs.md](../Wine/Releases/IWineRelease.cs.md):
a three-member download descriptor implemented by the three release classes in `Releases/`.

---

## 1. Surface

| Member | Line | Type | Notes |
| --- | --- | --- | --- |
| `Name` | `:5` | `string` (get-only) | directory the tarball unpacks to, relative to `<toolsFolder>/dxvk` |
| `DownloadUrl` | `:6` | `string` (get-only) | absolute URL of the `.tar.gz` |
| `Checksum` | `:7` | `string` (get-only) | **singular** — one acceptable SHA-512 digest |

## 2. Placement quirk

The interface lives in the **parent** namespace (`…Compatibility.Dxvk`, file next to `Dxvk.cs`) while its
implementations live in `…Compatibility.Dxvk.Releases` (files under `Releases/`). Hence
`Dxvk.cs:8` carries `using XIVLauncher.Common.Unix.Compatibility.Dxvk.Releases;` to see the classes,
and `Dxvk.cs:50` can name `IDxvkRelease` unqualified. The wine pair avoided this split
(`IWineRelease.cs` is itself inside `Releases/`, see
[../Wine/Releases/IWineRelease.cs.md](../Wine/Releases/IWineRelease.cs.md) §1).

## 3. Singular checksum

`IDxvkRelease.Checksum` (one string) vs `IWineRelease.Checksums` (string[]): Dxvk tarballs exist in a single
flavour, so the release classes hard-code one digest. Callers must still wrap it for the shared verifier —
`Dxvk.cs:80` builds a one-element array literal `CompatUtil.EnsureChecksumMatch(tempPath, [checksum])`.
See [../../CompatUtil.cs.md](../CompatUtil.cs.md) §3/§4 for the verifier and both throw sites.

## 4. Implementations

[DxvkBeta.cs.md](Releases/DxvkBeta.cs.md) · [DxvkLegacy.cs.md](Releases/DxvkLegacy.cs.md) ·
[DxvkStable.cs.md](Releases/DxvkStable.cs.md) — all `sealed` classes, no ctor (unlike the wine releases,
which take `WineReleaseDistro`).

Instantiation sites: `Dxvk.cs:52`–`:54` inside the `DxvkVersion` switch.

## 5. Referenced

* Referenced by: `Dxvk/Dxvk.cs:50` (`IDxvkRelease release = version switch`), the three release classes
  (`:3` in each, `: … : IDxvkRelease`).
* Upstream counterpart: none — upstream hard-coded `DXVK_DOWNLOAD`/`DXVK_NAME` constants in
  `lib/FFXIVQuickLauncher/src/XIVLauncher.Common.Unix/Compatibility/Dxvk.cs`; see
  [Dxvk.cs.md](Dxvk.cs.md) §6.
