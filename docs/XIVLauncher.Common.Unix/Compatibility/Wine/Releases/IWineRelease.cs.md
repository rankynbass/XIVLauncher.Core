# IWineRelease.cs — Specification

**File:** `src/XIVLauncher.Common.Unix/Compatibility/Wine/Releases/IWineRelease.cs` · 8 lines · 187 bytes · ASCII, LF, no BOM
**Namespace:** `XIVLauncher.Common.Unix.Compatibility.Wine.Releases` (`:1`) — declared with the `namespace …;` shorthand
**Declaration:** `public interface IWineRelease` (`:3`), members at `:5`–`:7`

**Role:** the three-member download descriptor every wine release class implements: the extracted
directory name, the tarball URL, and the acceptable SHA-512 digests. It is the seam that lets
`CompatibilityTools` pick a release **at runtime** instead of baking one URL into the class.

---

## 1. Surface

| Member | Line | Type | Contract |
| --- | --- | --- | --- |
| `Name` | `:5` | `string` (get-only) | Directory the tarball unpacks to, relative to `<toolsFolder>/wine` |
| `DownloadUrl` | `:6` | `string` (get-only) | Absolute URL of the `.tar.xz` |
| `Checksums` | `:7` | `string[]` (get-only) | Every digest that counts as a valid download |

All three are *interface properties with only a `get;` clause* — implementers must provide a getter,
and the concrete classes spell them `{ get; } = <expr>` (immutable, computed at construction).

## 2. Consumers

* `Wine/WineSettings.cs:58` assigns an `IWineRelease`-typed field from a `WineManagedVersion` switch
  → `new WineStableRelease(wineDistroId)` / `WineBetaRelease` / `WineLegacyRelease` (`:60`–`:62`).
* `Compatibility/CompatibilityTools.cs:28` reads `Settings.Release.Name` to derive `WineBinPath`.
* `Compatibility/CompatibilityTools.cs:41`/`:85` read `Settings.Release.DownloadUrl` (download decision
  + log line) and `Settings.Release.Checksums` (verification, `:99`).

Both readers null-guard nothing: with `WineStartupType.Custom`, `Settings.Release` is never assigned
(see [../WineSettings.cs.md](../WineSettings.cs.md) §4), so these paths only run for managed setups.

## 3. Interface vs sibling

The Dxvk counterpart, [../../Dxvk/IDxvkRelease.cs.md](../../Dxvk/IDxvkRelease.cs.md), is deliberately
asymmetric: it exposes **`Checksum`** (singular, `:7`) because each Dxvk release ships one digest,
whereas wine releases ship one per distro flavour. The verification helper is shared by both
(`CompatUtil.EnsureChecksumMatch` takes `string[]` in both cases, see
[../../CompatUtil.cs.md](../../CompatUtil.cs.md) §3).

## 4. Referenced by / referenced

* Referenced by: `WineStable.cs`, `WineBeta.cs`, `WineLegacy.cs`, `Wine/WineSettings.cs:4`
  (`using XIVLauncher.Common.Unix.Compatibility.Wine.Releases;`), `CompatibilityTools.cs`.
* References: nothing beyond `WineReleaseDistro` from [../../CompatUtil.cs.md](../../CompatUtil.cs.md)
  (constructor parameter type in the concrete classes).
* Upstream counterpart: **none** — upstream has no release abstraction
  (its `Compatibility/WineSettings.cs` hard-codes one URL in `CompatibilityTools`, see
  [../WineSettings.cs.md](../WineSettings.cs.md) §6).
