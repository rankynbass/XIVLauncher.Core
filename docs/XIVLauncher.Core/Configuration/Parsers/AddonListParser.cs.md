# AddonListParser.cs — Specification

**File:** `src/XIVLauncher.Core/Configuration/Parsers/AddonListParser.cs` · 32 lines · 735 bytes (ASCII, LF, no BOM)
**Namespace:** `XIVLauncher.Core.Configuration.Parsers` · **Declaration:** `internal class AddonListParser : ITypeParser`
**Role:** Config.Net type-parser for `List<AddonEntry>` — the whole addon list is serialised as one JSON value in one INI key; two deliberate-looking deltas from upstream (no `null` guard, `string.Empty` instead of `null`) make it the riskier of the two parsers

---

## 1. Purpose

`ILauncherConfig.cs:37` declares `Addons { get; set; }` with type `List<AddonEntry>?`, a generic class type Config.Net cannot spell as a scalar, so this parser owns its INI/CLI representation. It is registered at `Program.cs:92` (`.UseTypeParser(new AddonListParser())`), the last link before `.Build()` at `:93`, reached via `Program.cs:27` (`using XIVLauncher.Core.Configuration.Parsers;`).

Element type: `lib/FFXIVQuickLauncher/src/XIVLauncher.Common.Addon/AddonEntry.cs:5` (`public class AddonEntry`, members `:7` `bool IsEnabled { get; set; }`, `:8` `GenericAddon Addon { get; set; }`). The list is not a fork type — the fork consumes it through the lib's addon manager.

Consumer chain (the only place the parsed value matters):

| Line | Use |
| --- | --- |
| `MainPage.cs:780` | `App.Settings.Addons ??= new List<AddonEntry>();` — the sole defaulting site, and it is *not* in `LoadConfig` (`ILauncherConfig.cs.md` §5 lists `Addons` among the 13 never-defaulted members) |
| `MainPage.cs:782` | `App.Settings.Addons.Where(x => x.IsEnabled).Select(x => x.Addon).Cast<IAddon>().ToList()` |
| `MainPage.cs:784` | `addonMgr.RunAddons(launchedProcess.Id, addons)` |
| `MainPage.cs:789` / `:800` | `addonMgr.StopAddons()` |

So the value's only two observable effects are "which entries have `IsEnabled == true`" and "how many" — nothing in the fork edits a parsed `Addons` entry, and there is **no UI** for the list (no `SettingsEntry` anywhere touches `Program.Config.Addons`), which is what makes the JSON blob hand-editable-in-principle and hand-unreadable-in-practice.

## 2. Surface

| Line | Member | Body |
| --- | --- | --- |
| `:11` | `IEnumerable<Type> SupportedTypes` | `new[] { typeof(List<AddonEntry>) }` — one generic instantiation, matched by `Type` equality (`:23`) |
| `:13` | `string ToRawString(object value)` | `:15` `if (value is List<AddonEntry> list)` → `:16` `return JsonConvert.SerializeObject(list);`; else `:18` `return string.Empty;` |
| `:21` | `bool TryParse(string value, Type t, out object result)` | `:23` `if (t == typeof(List<AddonEntry>))` → `:25` `result = JsonConvert.DeserializeObject<List<AddonEntry>>(value);`, `:26` `return true;`; fallthrough `:29` `result = null!;`, `:30` `return false;` |

Usings: `Config.Net` (`:1`), `Newtonsoft.Json` (`:3`), `XIVLauncher.Common.Addon` (`:5`); namespace `:7`. Unlike `./DirectoryInfoParser.cs.md` §2, this file needs the lib import because `AddonEntry` is not prelude-visible.

Newtonsoft is used through `JsonConvert` (the `Newtonsoft.Json` extension-methods style, `:16`/`:25`), not `System.Text.Json`.

## 3. The two directions

```
ToRawString(obj) : obj is List<AddonEntry> ? JSON array of entries   // e.g. [{"...":...}, ...]
                 : string.Empty                                      // writes "Addons="  (an empty value!)

TryParse(str,t)  : t == typeof(List<AddonEntry>) ? JsonDeserialize<List<AddonEntry>>(str) -> true   // may throw
                 : (null!, false)                                                              // "not my type"
```

Asymmetric pair worth spelling out: an empty *list* serialises as `[]`, a `null` *member* serialises as empty, and an empty INI value deserialises back to `null` (Newtonsoft returns no object for empty input) — not to `[]`. So `null` survives a round-trip as `null`, `[]` survives as `[]`, and the distinction between them is exactly what `MainPage.cs:780`'s `??=` later re-materialises.

## 4. Delta vs upstream

Upstream: `lib/FFXIVQuickLauncher/src/XIVLauncher/Settings/Parsers/AddonListParser.cs`. Two behavioural deltas and two cosmetic ones:

| Aspect | Fork (`:18`) | Upstream (`:18`) |
| --- | --- | --- |
| Non-list raw form | `string.Empty` | `null` |
| `null` input guard | **absent** — `:21`/`:23` go straight to the type check | `:23`-`:27` `if (value == null) { result = null; return false; }` |
| Fallthrough result | `result = null!;` (`:29`) | `result = null;` (`:35`) |
| Namespace / declaration | `XIVLauncher.Core.Configuration.Parsers;`, `internal class` | `XIVLauncher.Settings.Parsers` block, bare `class` |
| Usings | 3 | 5 (`System`, `System.Collections.Generic`, + `Config.Net`, `Newtonsoft.Json`, `XIVLauncher.Common.Addon`) |

**Delta 1 (`string.Empty`)** — `null` means "no raw string" (skip the key); `string.Empty` means "write the key with an empty value". So on the fork, a `null` `Addons` member (the common case: `Addons` is never defaulted at startup, and only `MainPage.cs:780` creates an empty list, which serialises as `[]`) leaves `Addons=` in `launcher.ini`. That line is then re-read as an empty value → `null` again, which is stable but means the ini can never express "no addons configured" and "empty addon list" differently from the moment the first save happens.

**Delta 2 (missing `null` guard)** — upstream protects `Json.DeserializeObject` against a `null` raw string (Newtonsoft rejects `null` input with an argument error); the fork does not. The guard is only reachable for a member whose type this parser claims (`:23` type check runs first, so a `null` for any *other* member never lands here), i.e. it is a hole rather than a live crash today. The sibling `DirectoryInfoParser.cs:19`-`:23` keeps the guard — so the pair is inconsistent, and if this file's guard is ever "restored", the two parsers stop being reviewable as one diff.

## 5. The INI value shape

`JsonConvert.SerializeObject(list)` on `AddonEntry` (`AddonEntry.cs:7`/`:8`) emits a JSON array of objects with **member names as keys** — and the fork builds with `--obfuscate`, which renames members. Consequence: the `Addons=` line's key names are build-specific, so a hand-written or hand-edited `Addons=` value cannot be authored against stable key names, and a value produced by one build is not guaranteed readable by another. This is the strongest argument for the fork's current shape (no UI for addons) and for *not* trying to make the value human-editable without also adding a stable serialisation format.

Also implied: the whole list lives in **one** INI key, so a single long line carries the entire addon configuration. Config.Net's ini reader (not vendored in this repo — `grep` for its parse flags finds nothing in `src/` or `lib/`) decides quoting/line-continuation behaviour, so anything about multi-line or quoted handling in the `Addons=` value is unobservable from this fork and must be treated as unspecified; the only safe assumption in the fork's own code is round-tripping a value this parser itself wrote.

## 6. Hazards & invariants

* **Malformed JSON aborts config load.** `:25` has no `try`, and `TryParse` is called inside the builder chain at `Program.cs:88`-`:93`, whose only protection is the enclosing `main` body — so a corrupted `Addons=` value is a startup exception that the `Program.cs` null-guards (`:273`-`:306`) do not cover. Contrast `./DirectoryInfoParser.cs.md` §6 (that parser cannot throw). This is the one asymmetry between the two parsers: **addon values are the only config values that can crash startup**, so `ClearSettings` (delete `launcher.ini`, `Program.cs.md` §4) is the recovery.
* **The list type is matched by `Type` equality** (`:23` `t == typeof(List<AddonEntry>)`), so the parser is tied to the *instantiation* `List<AddonEntry>`; if a member were ever retyped `IList<AddonEntry>`/`AddonEntry[]`, `SupportedTypes` and the check both stop matching and the member silently loses its persistence path (no compile error, no runtime error — the value just never round-trips).
* **`ToRawString` on a non-list returns `string.Empty`, not an error** (`:18`): a `null` or wrongly-typed member value is silently persisted as an empty value. Combined with §4's delta, that is the fork's *only* visible sign of the "wrong type at write time" condition — a key with an empty value in `launcher.ini`.
* **No length/quota guard.** The blob grows with the addon list and is written as one line; nothing here bounds it, and `--obfuscate` renames *and* can change string-literal encoding, so a large list is a large single-line value. Keep it as the reason for not treating ini size as a proxy for correctness here.
* **Nothing edits the parsed list**, so a fork-side addon UI means new code in `Components/SettingsPage/` that must (a) read `Program.Config.Addons` as possibly-`null` (the `??=` at `MainPage.cs:780` is downstream of the settings page, not upstream of it), and (b) write the *whole* list back — a per-entry `SettingsEntry` row cannot express this member, since `SettingsEntry<T>` binds one member to one value (`docs/XIVLauncher.Core/Components/SettingsPage/SettingsEntry{T}.cs.md` §1).
* **`IsEnabled` is the only per-entry switch** (`AddonEntry.cs:7`), and `MainPage.cs:782` filters on it *before* `Cast<IAddon>()` — so disabling an entry is the safe way to turn an addon off without touching the blob.
* **Registration order:** `Program.cs:91` (DirectoryInfo) then `:92` (AddonList). The `SupportedTypes` sets are disjoint, so order is currently irrelevant; adding a shared type would make the first registration win. Keep the two lines adjacent when adding a third parser.

## 7. Referenced / referenced-by

* Implements: `Config.Net`'s `ITypeParser`; serialises via `Newtonsoft.Json`'s `JsonConvert` (`:16`/`:25`).
* Registered at: `Program.cs:92` (`docs/XIVLauncher.Core/Program.cs.md` §4 builder row).
* Serves: `ILauncherConfig.cs:37` (`Addons`, `List<AddonEntry>?`; `../ILauncherConfig.cs.md` §2, §5 "never defaulted").
* Element type: `lib/FFXIVQuickLauncher/src/XIVLauncher.Common.Addon/AddonEntry.cs:5` (`:7` `IsEnabled`, `:8` `Addon`); element implementations live in `lib/FFXIVQuickLauncher/src/XIVLauncher.Common.Addon.Implementations` (`AddonEntry.cs:1` import).
* Consumed by: `docs/XIVLauncher.Core/Components/MainPage/MainPage.cs.md` (`:780`-`:800` addon run/stop), `docs/XIVLauncher.Core/Components/SettingsPage/Tabs/SettingsTabGame.cs.md` (the UI gap: no addon row), `../ILauncherConfig.cs.md` §5 (defaulting map).
* Upstream counterpart: `lib/FFXIVQuickLauncher/src/XIVLauncher/Settings/Parsers/AddonListParser.cs`; sibling: `./DirectoryInfoParser.cs.md`.
