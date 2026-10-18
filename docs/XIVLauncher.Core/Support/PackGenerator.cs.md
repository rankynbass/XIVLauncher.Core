# PackGenerator.cs — Specification

**File:** `src/XIVLauncher.Core/Support/PackGenerator.cs` · 53 lines · 2464 bytes
**Namespace:** `XIVLauncher.Core.Support` · **Declaration:** `public static class PackGenerator`
**Role:** Builds the `.tspack` bug-report bundle — one zip in `logs/` containing `trouble.json` plus every launcher/Dalamud/patcher log that exists

---

## 1. Purpose

`PackGenerator.SavePack` is the fork's "generate a pack" lever: `../Components/SettingsPage/Tabs/SettingsTabTroubleshooting.cs.md` §5 draws the button, calls `PackGenerator.SavePack(Program.storage)` (`SettingsTabTroubleshooting.cs:56`) and then opens the logs **folder** (`:57`, `PlatformHelpers.OpenBrowser(logs dir)`) so the user can grab the newest `trouble-*.tspack`. It pairs with `Support/Troubleshooting.cs.md` (the JSON half) and `../Program.cs.md` §10 (the `logs/` layout it harvests).

Usings: `System.IO.Compression` (`ZipFile`, `ZipArchiveMode`), `System.Text`, `XIVLauncher.Common`; plus a **using-alias declaration**, `using ZipArchive = System.IO.Compression.ZipArchive;` (`:6`), so the private helper's parameter reads `ZipArchive zip`. `Path.Combine`/`FileInfo` resolve through the implicit usings (`<ImplicitUsings>enable</ImplicitUsings>`, `net10.0`).

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `SavePack(Storage storage)` | `public static string` (`:12`–`:39`) | returns the **absolute path** of the pack it wrote (`:38`) |
| `AddIfExist(FileInfo file, ZipArchive zip)` | `private static void` (`:41`–`:51`) | copy-if-present helper; the existence check is *inside*, so call sites are unconditional |

The parameter name `storage` shadows the `Storage` **type** in the same signature — legal, and it is the same object `../Program.cs.md` §10 describes (`../LauncherApp.cs.md`'s `Storage`, root `"xlcore"`).

## 3. Pack layout and naming

```
<storage>/logs/trouble-<yyyyMMddhhmmss>.tspack
    trouble.json      <- always present
    launcher.log      <- added when present
    patcher.log
    dalamud.log
    dalamud.injector.log
    dalamud.boot.log
    aria.log
    wine.log
```

* Entry names are **basenames** (`zip.CreateEntry(file.Name)`, `:46`) — the archive has no directory prefix, so a consumer unpacks a flat file set. `trouble.json` is the only entry with a synthesised name (`:18`).
* The seven candidates (`:22`–`:28`) map to: `launcher.log` (`Program.cs:80`, `LogInit.Setup`), `wine.log` (`Program.cs:361`), the Dalamud trio (`dalamud.log`, `dalamud.injector.log`, `dalamud.boot.log` — written by the Dalamud binary/DalamudUpdater, `../Program.cs.md` §10), plus `aria.log` and the external `patcher.log`. Absent ones are skipped silently, so the pack's contents say what the session actually did.
* **The aria names do not match**: nothing in this repo writes `aria.log` (the name upstream's `lib/FFXIVQuickLauncher/src/XIVLauncher/Support/PackGenerator.cs:27` also uses), while the fork's *own* patch-download log is `aria2.log` (`../Components/MainPage/MainPage.cs:916`, `using var acquisition = new AriaPatchAcquisition(...)`) — which is **not** a candidate. So failed aria downloads are exactly the thing a bug report is most likely to need, and the pack omits it; an upstream-inherited name mismatch rather than a fork decision.
* **Rotated logs are not candidates**: `dalamud.boot.old.log` is a real name in the tree (`Program.cs:468`, the `ClearLogs` cleanup list) but has no `FileInfo` line here — packs carry current logs only. That is also the only reason a new candidate list stays honest: add a `FileInfo` line if you ever want history in a report.
* `storage.GetFolder("logs")` is re-derived **eight times** (`:14`, `:22`–`:28`) — cheap but it is the reason the pack always follows a re-homed storage root.

## 4. Name-generation hazard

`DateTime.Now:yyyyMMddhhmmss` (`:14`) uses **`hh`** (12-hour, no AM/PM marker). Two packs generated at 13:57 and 01:57 on the same day therefore produce **identical filenames**, and `ZipFile.Open(..., ZipArchiveMode.Create)` truncates an existing target — the earlier pack is silently destroyed. Use `HH` if you touch this. Timestamp is also **local time**, so packs from different machines sort against each other unpredictably.

## 5. Writing mechanics

* `using var archive = ZipFile.Open(outFile.FullName, ZipArchiveMode.Create);` (`:15`) — the zip is finalised (central directory written) at **scope exit**, not at an explicit call. There is no `try`/`catch` in the method: any throw between `:16` and `:37` unwinds the `using`, closes the archive, leaves a **partial/corrupt `.tspack`** on disk, and rethrows into the caller.
* The caller has no `try` either (`SettingsTabTroubleshooting.cs:56`), and the frame loop in `../Program.cs.md` §6/`Program.cs:331-333` has none around `launcherApp.Draw()` — a mid-pack failure therefore exits the launcher rather than showing a modal. The `PlatformHelpers.OpenBrowser` line at `:57` never runs, so the user gets no folder either.
* `trouble.json` is written explicitly (`CreateEntry(...).Open()` → `Write(bytes, 0, len)` → `Close()`), UTF-8 bytes of `Troubleshooting.GetTroubleshootingJson()`. Note that call re-runs the *whole* snapshot (integrity check included) — see `Support/Troubleshooting.cs.md` §6 for the freshness consequence.
* `AddIfExist` opens logs with `FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete` (`:45`): the same share flags used for files being **appended to** and possibly **rotated/deleted concurrently** (`dalamud.old.log` rotation in the lib, `ClearLogs` in `Program.cs`) — that is the reason the helper is defensive rather than `CreateEntryFromFile` (which is commented out at `:49`).
* `zip.CreateEntry(name)` with no compression argument ⇒ **Deflate** default; no level override, so a large `dalamud.log` dominates pack size. Nothing trims or tails logs: a whole-session `launcher.log` is embedded verbatim, including the base64 `TROUBLESHXLTING:` lines that duplicate `trouble.json`.

## 6. Invariants and hazards

* **The return value is discarded** by the only caller (`SettingsTabTroubleshooting.cs:56`) — the path is surfaced only as "newest file in the opened folder". If you add a second caller, either log/`ShowMessage` the returned path or the artefact becomes undiscoverable.
* **Nothing is redacted.** The pack carries the full log stream: `Support/Troubleshooting.cs.md` payloads (config, account-adjacent info) plus anything Dalamud/patcher logged (OTPs, URLs, exceptions). It is a *publish-to-user* artefact — treat log-content policy, not this method, as the safety boundary.
* **No manifest**: consumers must infer the launcher version from `trouble.json`'s `LauncherVersion`/`LauncherHash` (`../AppUtil.cs.md`), since nothing else in the archive identifies the build.
* **A pack taken after `ClearLogs(true)`** (`../Program.cs.md` §8/`Program.cs:464-474`) contains only `trouble.json` — the pack lever and the cleanup lever interact; see `../Components/SettingsPage/Tabs/SettingsTabTroubleshooting.cs.md` §5.
* `AddIfExist` writes **no** entry-name collision guard; two candidates sharing a basename would collide in the zip. Impossible with the current seven names, but a future candidate added from a *different* folder needs an explicit entry name.
* Extension point: `SavePack` takes the `Storage`, not a folder, so a future "include the config ini" or "include `dalamud.old.log`" change stays local — add a `FileInfo` line + `AddIfExist`, or (for non-log files) a synthesised entry like `trouble.json`'s.

## 7. Referenced / referenced-by

* References: `Support/Troubleshooting.cs.md` (`GetTroubleshootingJson`), `../LauncherApp.cs.md`/`../Program.cs.md` §10 (`Storage`, `logs/`), `System.IO.Compression` (`ZipFile`, `ZipArchiveMode.Create`), `XIVLauncher.Common` (`FileInfo` aliasing), `../Components/SettingsPage/Tabs/SettingsTabTroubleshooting.cs.md` (button + browser-open pair).
* Referenced by: `SettingsTabTroubleshooting.cs:56` (the only call site in the fork).
* Sibling spec: `Support/Troubleshooting.cs.md`.
