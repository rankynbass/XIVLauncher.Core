# CoreEnvironmentSettings.cs — Specification

**File:** `src/XIVLauncher.Core/CoreEnvironmentSettings.cs` · 65 lines · 2,943 bytes
**Namespace:** `XIVLauncher.Core` · **Declaration:** `public static class CoreEnvironmentSettings`
**Role:** Environment-variable interpretation layer (out-of-band switches)

---

## 1. Purpose

Provides a single, uniform way to read launcher "out-of-band" settings from the process environment rather than from `launcher.ini`. These are deliberately *not* part of `ILauncherConfig`: they are meant for distribution packagers (Steam Deck, compat-tool launches, Flatpak) and for recovery paths when the normal configuration is broken.

Every accessor is get-only, so values are **re-read from the environment on each access** — an environment change mid-run takes effect immediately.

## 2. Accessor surface

| Member | Type | Env key | Semantics |
| --- | --- | --- | --- |
| `IsDeck` | `bool?` | `XL_DECK` | tri-state Steam Deck detection |
| `IsDeckGameMode` | `bool?` | `XL_GAMEMODE` | Deck game-mode hint |
| `IsDeckFirstRun` | `bool?` | `XL_FIRSTRUN` | Deck first-run hint |
| `ClearSettings` | `bool` | `XL_CLEAR_SETTINGS` | wipe `launcher.ini` at startup |
| `ClearPrefix` | `bool` | `XL_CLEAR_PREFIX` | wipe `wineprefix` |
| `ClearPlugins` | `bool` | `XL_CLEAR_PLUGINS` | wipe Dalamud/plugins/runtime |
| `ClearTools` | `bool` | `XL_CLEAR_TOOLS` | wipe compat tools |
| `ClearLogs` | `bool` | `XL_CLEAR_LOGS` | wipe logs |
| `ClearAll` | `bool` | `XL_CLEAR_ALL` | all of the above |
| `UseSteam` | `bool?` | `XL_USE_STEAM` | tri-state "use the Steam integration"; `false` sets `Config.IsIgnoringSteam` (comment: "Fix for Steam Deck users who lock themselves out") |
| `IsSteamCompatTool` | `bool` | `XL_SCT` | process was relaunched through a Steam compat tool |
| `SteamAppId` | `uint` | `SteamAppId` | primary AppID override |
| `AltAppID` | `uint` | `XL_APPID` | secondary AppID override |

Note the casing asymmetry: the AppID override uses the mixed-case key **`SteamAppId`** (the standard Steam launch variable), whereas every other key is `XL_`-prefixed and uppercase.

## 3. Truth-value grammar

`CheckEnvBool(key)` — used by all `bool` accessors:

- value is lower-cased, absent ⇒ empty string
- `1`, `true`, `yes`, `y`, `on` ⇒ `true`
- **anything else** ⇒ `false` (so `XL_CLEAR_SETTINGS=maybe` silently means "off")

`CheckEnvBoolOrNull(key)` — used by the `bool?` accessors:

- the same truthy set ⇒ `true`
- `0`, `false`, `no`, `n`, `off` ⇒ `false`
- anything else (including unset) ⇒ `null` (= "no opinion", letting the caller's fallback run)

## 4. Helper surface

| Signature | Behaviour |
| --- | --- |
| `static uint GetAppId(string? appid)` | `uint.TryParse`; unparseable or absent ⇒ `0`. `0` is the sentinel meaning "not set" — a real AppID of 0 is indistinguishable, and `Program` skips `0` entries. |
| `static string GetCleanEnvironmentVariable(string envvar, string badstring = "", string separator = ":")` | Splits `envvar` on `separator` (default `:`), drops empty tokens, drops tokens containing `badstring`, re-joins with `separator`. When `badstring == ""` the raw value is returned unchanged (early-out compares with `String.Equals(…, StringComparison.Ordinal)`). Intended for sanitising `PATH`/`LD_LIBRARY_PATH`-style lists. **Unused within this file**; consumed by other units of the project. |
| `static string GetCType()` | Locale lookup. Windows: returns `""` immediately. Else spawns `sh -c "locale -a 2>/dev/null \| grep -i utf"`, reads stdout, splits on `'\n'` (empty items removed), and returns the first entry whose upper-cased form starts with `"C."` — e.g. `C.UTF-8`. No match ⇒ `""`. |

### `GetCType` contract details and hazards

- Called once, as a static field initializer in `Program` (`CType = CoreEnvironmentSettings.GetCType()`), so it runs during class initialisation — before logging, before config, and therefore before any error reporting exists.
- The child process is not waited for explicitly; `StandardOutput.ReadToEnd()` is relied upon to block until EOF. `proc.Start()` + `ReadToEnd()` on `System.Diagnostics.Process` (the legacy class, not `Process.Run`) is used here.
- Output is *not* sorted or normalised; selection order is whatever `locale -a` prints. A locale named `c.utf8` matches (upper-case compare) but a locale such as `en_US.UTF-8` never matches.
- A missing `sh` (or missing `locale` binary) yields empty stdout and therefore `""`, silently.
- Consumers: `Program.CType`, forwarded into game/Wine environment construction elsewhere in the project.

## 5. Relationship to other units

- `Program` reads: the six `Clear*` switches, `UseSteam`, `IsSteamCompatTool`, `SteamAppId`, `AltAppID`, `IsDeck*` (for `IsSteamDeckHardware`), and `GetCType`.
- `AppUtil.OpenBrowser` indirectly depends on it through `Program.IsSteamDeckHardware`.
- `LauncherApp`/component pages consult `Program.IsSteamDeckHardware` for Deck-specific prompts.

## 6. Design notes

- Class is `static`, so it cannot be instantiated and holds no fields — everything is an accessor over the live environment. This is what makes the switches hot-reloadable (e.g. for a restarted launcher process inside a compat-tool chain).
- The `bool?` accessors re-evaluate the env on every access (they are `=>` bodies, not cached fields); `CheckEnvBool` allocates a lower-cased string per access. Acceptable outside the frame loop; do **not** place these in per-frame ImGui code paths.
- No validation, no unknown-key reporting: a typo in a packaging script is indistinguishable from "unset".
