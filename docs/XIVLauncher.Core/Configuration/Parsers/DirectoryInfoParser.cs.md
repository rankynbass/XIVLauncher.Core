# DirectoryInfoParser.cs — Specification

**File:** `src/XIVLauncher.Core/Configuration/Parsers/DirectoryInfoParser.cs` · 35 lines · 739 bytes (ASCII, LF, no BOM)
**Namespace:** `XIVLauncher.Core.Configuration.Parsers` · **Declaration:** `internal class DirectoryInfoParser : ITypeParser`
**Role:** Config.Net type-parser for `DirectoryInfo` — path ⇄ object in both directions, so the four `DirectoryInfo?` config members can live in `launcher.ini` and on the command line

---

## 1. Purpose

`DirectoryInfo` is a class, not a scalar, so Config.Net's built-in scalar handling (`long`, `bool?`, enums) cannot spell it; this parser supplies the two missing directions. It is registered at `Program.cs:91` (`.UseTypeParser(new DirectoryInfoParser())`) inside the builder chain at `Program.cs:88`-`:93`, and reached by `Program.cs:27` (`using XIVLauncher.Core.Configuration.Parsers;`).

The members it serves (`ILauncherConfig.cs`, §2 there):

| Member | Line | Type |
| --- | --- | --- |
| `GamePath` | `:21` | `DirectoryInfo?` |
| `GameConfigPath` | `:23` | `DirectoryInfo?` |
| `PatchPath` | `:47` | `DirectoryInfo?` |
| `DalamudManualInjectPath` | `:90` | `DirectoryInfo?` |

Those four are the **only** members whose INI/CLI representation depends on this file. Everything downstream consumes `.FullName` (`MainPage.cs:1049` `Program.Config.PatchPath!`, `MainPage.cs:733` `App.Settings.GameConfigPath!.FullName`, `Support/Troubleshooting.cs:73`, `Program.cs:167`-`:172` injector override), i.e. the object's identity in practice is the path string it was built from.

## 2. Surface

| Line | Member | Body |
| --- | --- | --- |
| `:7` | `IEnumerable<Type> SupportedTypes` | `new[] { typeof(DirectoryInfo) }` — exactly one type |
| `:9` | `string? ToRawString(object value)` | `:11` `if (value is DirectoryInfo di)` → `:12` `return di.FullName;`, else `:14` `return null;` |
| `:17` | `bool TryParse(string? value, Type t, out object? result)` | `:19`-`:23` null guard (`result = null`, `return false`); `:25` `if (t == typeof(DirectoryInfo))` → `:27` `var di = new DirectoryInfo(value);`, `:28` `result = di;`, `:29` `return true;`; fallthrough `:32` `result = null;`, `:33` `return false;` |

Usings: `using Config.Net;` (`:1`) — the only import. `Type`, `IEnumerable`, and `DirectoryInfo` need no import in this dialect (`System`, `System.IO`, `System.Collections.Generic` are prelude-implicit), which is why upstream's copy of this file (`lib/FFXIVQuickLauncher/src/XIVLauncher/Settings/Parsers/DirectoryInfoParser.cs:1`-`:4`) carries four usings and this one carries one.

## 3. The two directions

```
ToRawString(obj) : obj is DirectoryInfo ? obj.FullName        // absolute-ish path string, as constructed
                 : null                                        // "no raw form" -> member omitted from the ini
TryParse(str,t)  : t == typeof(DirectoryInfo) ? new DirectoryInfo(str) -> true
                 : (null, false)                               // "not my type" -> next parser / builtin
```

Both directions are lossy by design and the loss is the same in both: only the path survives. `DirectoryInfo`'s other facets (`Name`, `Parent`, `Exists`) are recomputed by whoever reads them, never stored — so a member that "was created because the folder existed" and a member that "points at a missing folder" are indistinguishable after a round-trip (`ILauncherConfig.cs.md` §3's three-state claim covers only *presence*, not validity).

## 4. Nullability contract

`TryParse` takes `string? value` and `out object? result` (`:17`) — annotated nullables. `ToRawString` returns `string?` (`:9`) and returns `null` at `:14`. Upstream's identical-logic file spells all three as non-nullable (`:20` `string value`, `out object result`, `:12` `string ToRawString`); the fork's annotations make the "no value / not my type" cases type-legal instead of implicit. Effect: the null-sentinel meanings in §3 are now part of the signatures, so a caller that forgets the null guard is a compile-time complaint rather than a runtime surprise.

Both files in this directory disagree with each other on this point — `AddonListParser.cs:13` returns non-nullable `string` and falls back to `string.Empty` (`:18`), see `./AddonListParser.cs.md` §4. The pair is inconsistent, not a convention; do not "unify" without re-reading both §4s.

## 5. Delta vs upstream

Logic is byte-equivalent to `lib/FFXIVQuickLauncher/src/XIVLauncher/Settings/Parsers/DirectoryInfoParser.cs` (`:10` supported types, `:14`-`:18` raw form, `:22`-`:36` parse path). Deltas are structural only:

| Aspect | Fork | Upstream |
| --- | --- | --- |
| Namespace | `XIVLauncher.Core.Configuration.Parsers;` (`:3`, semicolon form, no braces) | `XIVLauncher.Settings.Parsers` (block form, `:6`-`:38`) |
| Declaration | `internal class` (`:5`) | bare `class` (`:8`) |
| Nullability | `string?`/`out object?` | non-nullable |
| Usings | 1 | 4 (`System`, `System.Collections.Generic`, `System.IO`, `Config.Net`) |
| BOM | none | UTF-8 BOM (`:1`) |

So `DirectoryInfoParser.cs` is a port, and the parser for the *same* type exists in the lib's vocabulary too — but the lib's settings stack is upstream's Windows schema, which this fork's builder does not use. Changing either copy does not affect the other.

## 6. Hazards & invariants

* **Construction is unconditional:** `new DirectoryInfo(value)` (`:27`) never validates. The string is whatever the ini/CLI held — including a relative path, a path with a typo, or an empty string. `new DirectoryInfo("")` is legal and yields a `DirectoryInfo` whose `.FullName` is the process cwd, so an `ini` line `GamePath=` (empty) becomes **a real path** rather than "unset". Contrast `Program.cs:100`/`:105`, which guards the *defaulting* path with `string.IsNullOrEmpty`/`IsNullOrWhiteSpace` — the guard exists for defaults only, not for parsed values.
* **Empty-vs-absent is conflated:** the ini key present with an empty value produces a `DirectoryInfo` (via `:27`), the key absent produces `null` (nothing calls this parser). Those differ only in the object's existence, and the empty-value case is the dangerous one; `SettingsTabGame.cs:12`/`SettingsTabPatching.cs:9` let the UI write a `DirectoryInfo` into either member with no validation step.
* **`SupportedTypes` is a set, not a whitelist of members:** it says "handle every member of type `DirectoryInfo`", so the parser is implicitly coupled to `ILauncherConfig.cs:21`/`:23`/`:47`/`:90` — adding a fifth `DirectoryInfo?` member automatically routes through this file (good), and so does *any* other type's rename to `DirectoryInfo` (also good, but silently changes that member's INI spelling).
* **`ToRawString` returning `null` is a *skip*, not an empty write:** a `null` raw string means the member is not written. So a `null` `GamePath` at write time yields an absent key, and `Program.cs:105` re-defaults it next startup — the file cannot accumulate a permanently-empty `GamePath=` line *unless* something wrote an empty `DirectoryInfo` (previous bullet).
* **`return false` also means "not my type"** (`:33`): the parser returning `false` for a matching-type value never happens (matching type + non-null ⇒ always `true`), so a `false` here in practice always means "absent or wrong type". Do not treat a `false` as a parse error to report to the user; Config.Net falls back to other parsers/builtins on `false`.
* **A malformed value cannot be reported.** There is no throwing path in `TryParse`, so bad input never aborts startup; it either constructs a nonsense `DirectoryInfo` or is skipped. The visible symptom is downstream: `MainPage.cs:849`/`:851` (`if (App.Settings.PatchPath is { Exists: false }) App.Settings.PatchPath = null;`) — `MainPage` self-heals *missing* patch paths, which is the only recovery from a garbage `PatchPath` line today. Game paths have no equivalent recovery (`Program.cs:100` only fills `null`).
* **Keep the one-type `SupportedTypes` list.** Widening it to other types (e.g. `string`) would route every string member through `new DirectoryInfo(...)` and change their INI type. Extending path-like semantics should add a *new* parser + a new member type, not extra entries here.
* Ordering of `.UseTypeParser` calls (`Program.cs:91` before `:92`) is irrelevant for these two parsers since their `SupportedTypes` sets are disjoint; if a type ever appeared in both, first-registration wins and this file's line order in `Program.cs` becomes load-bearing.

## 7. Referenced / referenced-by

* Implements: `Config.Net`'s `ITypeParser` (`SupportedTypes`/`ToRawString`/`TryParse` trio).
* Registered at: `Program.cs:91`; the type-inferred `new` spelling (`.UseTypeParser(new DirectoryInfoParser())`) is the same shorthand used by `Program.cs:49` (`new(Config)` for the `CommonSettings` view) — see `docs/XIVLauncher.Core/Program.cs.md` §12 hazard list.
* Serves: `ILauncherConfig.cs:21`/`:23`/`:47`/`:90` (`../ILauncherConfig.cs.md` §2).
* Consumed downstream: `docs/XIVLauncher.Core/Program.cs.md` (defaults at `:105`/`:123`), `docs/XIVLauncher.Core/LauncherApp.cs.md` §killswitch (`Repository.Boot.GetVer(Program.Config.GamePath)`), `docs/XIVLauncher.Core/Components/MainPage/MainPage.cs.md` (`:244`, `:733`, `:849`-`:854`, `:1049`), `docs/XIVLauncher.Core/Support/Troubleshooting.cs.md` (`:73`), `docs/XIVLauncher.Core/Components/SettingsPage/Tabs/SettingsTabGame.cs.md` (`:12`), `docs/XIVLauncher.Core/Components/SettingsPage/Tabs/SettingsTabPatching.cs.md` (`:9`).
* Upstream counterpart: `lib/FFXIVQuickLauncher/src/XIVLauncher/Settings/Parsers/DirectoryInfoParser.cs`; sibling: `./AddonListParser.cs.md`.
