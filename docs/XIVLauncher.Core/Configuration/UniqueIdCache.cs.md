# UniqueIdCache.cs — Specification

**File:** `src/XIVLauncher.Core/Configuration/UniqueIdCache.cs` · 27 lines · 481 bytes (ASCII, LF, no BOM)
**Namespace:** `XIVLauncher.Core.Configuration` · **Declaration:** `public class UniqueIdCache : IUniqueIdCache`
**Role:** A **null-object implementation** of the uid-cache interface — four members, all of which report "no cache" and store nothing; the class has no consumers and is dead code in this fork

---

## 1. Purpose

It satisfies `XIVLauncher.Common.PlatformAbstractions.IUniqueIdCache` (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common/PlatformAbstractions/IUniqueIdCache.cs:3`) with the emptiest legal behaviour: every lookup misses, every insert is dropped, every reset does nothing. It holds **no fields**, so it is not a cache with an empty backing store — it is a cache that cannot hold anything.

It is also **unused**: the launcher's cache is `CommonUniqueIdCache`, declared at `src/XIVLauncher.Core/LauncherApp.cs:115` (`public CommonUniqueIdCache UniqueIdCache;`) and built at `LauncherApp.cs:130` (`new CommonUniqueIdCache(this.Storage.GetFile("uidCache.json"))`), then handed to the lib at `LauncherApp.cs:132` (`this.Launcher = new Launcher(Program.Steam, UniqueIdCache, frontierUrl, ...)`, whose `Launcher.cs:33` parameter type is `IUniqueIdCache`). Nothing anywhere constructs the fork's `UniqueIdCache` (`grep` for `new UniqueIdCache` finds nothing; `Program.cs:26` imports the namespace, which is how the class is *visible* at `Program.cs`'s scope but never *used*).

Consequence for specs and refactors: this file is a **type-level placeholder**, not a policy. Changing it changes no runtime behaviour; deleting it (and the class name) only removes the option to instantiate it. Keep it as the record of "the fork deliberately provides no local uid cache", and never treat its bodies as the fork's uid-cache semantics — those live in `./CommonUniqueIdCache` (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common/CommonUniqueIdCache.cs`, §4 below).

## 2. Surface

| Line | Member | Returns / effect |
| --- | --- | --- |
| `:7` | `bool HasValidCache(string name)` | `:9` `return false;` — never valid, for any `name` |
| `:12` | `void Add(string name, string uid, int region, int maxExpansion)` | `:13`-`:15` empty body (body line `:14` blank) — arguments discarded |
| `:17` | `bool TryGet(string userName, out IUniqueIdCache.CachedUid cached)` | `:19` `cached = default;` then `:20` `return false;` — miss, with the out-param zero-initialised (`UniqueId`/`Region`/`MaxExpansion` → `null`/`0`/`0`) |
| `:23` | `void Reset()` | `:24`-`:26` empty body — nothing cleared, nothing persisted |

Class declaration at `:5`, usings `XIVLauncher.Common.PlatformAbstractions` at `:1`, namespace at `:3` (`public class`, i.e. the visibility marker, not a name modifier — see `./CommonSettings.cs.md` §3 for the sibling that spells `internal`). `TryGet` spells the nested out-param type fully qualified (`IUniqueIdCache.CachedUid`); inside the interface itself the same type is spelled bare `CachedUid` (`IUniqueIdCache.cs:9`).

## 3. The interface contract it must satisfy

`IUniqueIdCache.cs:5` `HasValidCache(string name)`, `:7` `Add(string name, string uid, int region, int maxExpansion)`, `:9` `TryGet(string userName, out CachedUid cached)`, `:11` `Reset()`, and the nested `:13`-`:18` `public struct CachedUid { :15 string UniqueId; :16 int Region; :17 int MaxExpansion; }` — a **nested type declared inside an interface**, so `CachedUid` is spelled `IUniqueIdCache.CachedUid` from outside (§2). Note the interface has no data members: cache storage is entirely the implementor's, which is why a null object like this one is expressible at all.

The contract's implied semantics, from the *real* implementor and the lib's use of it:

| Member | Meaning |
| --- | --- |
| `HasValidCache(name)` | "an entry exists **and** is fresh" — `CommonUniqueIdCache.cs:62` combines `IsValidCache` (`:102`, name match plus age `:104` vs `DAYS_TO_TIMEOUT` `:12` = 1 day) and `DeleteOldCaches` (`:57`, purge `:59`) |
| `TryGet(user, out cached)` | miss ⇒ `false` and `cached = default` (`CommonUniqueIdCache.cs:89`/`:90`); hit ⇒ `false`-free fill with `UniqueId`/`Region`/`MaxExpansion` (`:93`) |
| `Add(user, uid, region, expansion)` | insert **and persist** (`CommonUniqueIdCache.cs:67`, which calls `Save()` `:27`) |
| `Reset()` | clear the store and persist the empty state (`CommonUniqueIdCache.cs:49`) |

## 4. Stub vs. the cache that is actually wired

| Aspect | `UniqueIdCache.cs` (stub) | `CommonUniqueIdCache.cs` (wired at `LauncherApp.cs:130`) |
| --- | --- | --- |
| Backing store | none | `_cache` list, `Load()`/`Save()` at `:35`/`:27` against `uidCache.json` (`LauncherApp.cs:130` `this.Storage.GetFile("uidCache.json")`) |
| Validity window | always invalid | 1 day (`:12` `DAYS_TO_TIMEOUT = 1`, `:59` age purge, `:102`/`:104` check) |
| Entry identity | n/a | `UserName`-keyed (`:106` `public class UniqueIdCacheEntry`) with `CreationDate` |
| Insert | dropped | stores + `Save()` (`:67`) |
| Lookup | always miss | may return a uid + `Region` + `MaxExpansion` (`:93`) |
| Reset | no-op | `Clear()` + `Save()` (`:49`) |

Call sites of the wired cache (what the stub would sit in place of):

* `Launcher.cs:158` `if (!useCache || !this.uniqueIdCache.TryGet(userName, out var cached))` — miss ⇒ full `OauthLogin` + `RegisterSession`; hit ⇒ the `else` branch (`Launcher.cs:189`, log line at `:191` "Cached UID found, using instead") reuses `cached.UniqueId` and skips OAuth entirely.
* `Launcher.cs:187` `this.uniqueIdCache.Add(userName, uid, oauthLoginResult.Region, oauthLoginResult.MaxExpansion)` — only when `useCache`.
* `MainPage.cs:197` `if (isOtp && App.UniqueIdCache.HasValidCache(username) && App.Settings.IsUidCacheEnabled == false) Program.ResetUIDCache();` — self-heal: user turned caching off, so drop the stale entry.
* `MainPage.cs:200` `if (isOtp && !App.UniqueIdCache.HasValidCache(username))` ⇒ `App.AskForOtp()` + `await App.WaitForOtpAsync()`.
* `MainPage.cs:243` `var enableUidCache = App.Settings.IsUidCacheEnabled ?? false;` → `MainPage.cs:252` `Login(..., enableUidCache, ...)`; the Repair branch at `MainPage.cs:249` passes `false` literally, so repair never uses or writes the cache.
* `Program.cs:484` `public static void ResetUIDCache(bool tsbutton = false) => launcherApp.UniqueIdCache.Reset();`, driven by `SettingsTabGame.cs:56` (`if (ImGui.Button(Strings.ResetUIDCacheButton))`) under `SettingsTabGame.cs:51` (`if (Program.Config.IsUidCacheEnabled == true)`).
* UI toggle: `SettingsTabGame.cs:42` (`Strings.UseUIDCacheSetting`, `Program.Config.IsUidCacheEnabled ?? false` read + write).
* Report: `Support/Troubleshooting.cs:119` `IsUidCache = Program.Config.IsUidCacheEnabled.GetValueOrDefault()` (`Support/Troubleshooting.cs.md` §payload).

## 5. Behaviour if the stub were wired

Every one of the four members is a worst case for the user, i.e. wiring this class in would be a *silent* feature regression:

* `HasValidCache` `false` ⇒ `MainPage.cs:200` always asks for OTP (an interactive prompt on every login, including when `IsUidCacheEnabled == true`), and `MainPage.cs:197`'s auto-reset branch becomes unreachable.
* `TryGet` `false` ⇒ `Launcher.cs:158` always takes the OAuth branch: `uidCache.json` is never consulted, and the "Cached UID found" fast path can never run.
* `Add` no-op ⇒ nothing is ever written to `uidCache.json` (`CommonUniqueIdCache.cs:67`/`:27` never invoked) — the file stays absent or stale-but-unused.
* `Reset` no-op ⇒ `Program.ResetUIDCache()` (`Program.cs:484`) and the settings-page button (`SettingsTabGame.cs:56`) become invisible no-ops: the button presses do nothing, so the "clear stale uid" affordance stops working without an error.
* `region`/`maxExpansion` arguments discarded ⇒ even a hypothetical future store could not reproduce the values `Launcher.cs:187` passes.

## 6. Hazards & how to extend

* **Do not read this file as evidence that uid caching is off.** The fork *does* cache, through `CommonUniqueIdCache` (`LauncherApp.cs:115`/`:130`); the class here is simply not referenced. Anything that "fixes" uid caching by editing this file is editing dead code.
* **Its `public` visibility is a second-order inconsistency**: `ILauncherConfig.cs:9` is a `public interface`, `UniqueIdCache.cs:5` a `public class`, but `CommonSettings.cs:5` and both `Parsers/*.cs` are `internal`. If a future house rule is "everything in `Configuration/` is internal", this is the one declaration that must change first (`Program.cs:26` keeps the namespace import regardless).
* **`Add`'s parameter names differ from the wired cache's** (`name` vs `userName` at `CommonUniqueIdCache.cs:67`, and `maxExpansion` vs `expansionLevel`) — harmless here, but a call-by-name refactor would not port between the two.
* **The stub is not a valid cache even as a placeholder for tests.** If a future test wires it, it asserts the "always miss / never store" side only; nothing here exercises the freshness rule (`DAYS_TO_TIMEOUT`, `:102`/`:104`), the persistence side (`Save()`/`Load()`), or the `null`-vs-`default` out-param distinction (`:19` `cached = default;` is the *only* line in this file that mirrors upstream: `CommonUniqueIdCache.cs:89` writes the identical statement on its own miss path).
* **`TryGet`'s `cached = default;` is required, not optional** — an `out` parameter of a nested struct type must be assigned on every path; leaving it unassigned is a compile error, so a "simplify to `return false;`" refactor is not available.
* Extending this class into a real local cache means re-implementing §3's four-row contract *and* deciding the storage location (the wired one takes the file as a ctor argument, `LauncherApp.cs:130`) — the fork has no path that constructs it, so extending without rewiring `LauncherApp.cs:115`/`:130`/`:132` changes nothing observable.

## 7. Referenced / referenced-by

* Implements: `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/PlatformAbstractions/IUniqueIdCache.cs` (`:5`/`:7`/`:9`/`:11`, nested `CachedUid` `:13`-`:18`).
* Referenced by: nothing (no construction site in `src/`). Namespace import exists at `Program.cs:26` (`using XIVLauncher.Core.Configuration;`).
* The live counterpart: `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/CommonUniqueIdCache.cs` (`:10` declaration `: IUniqueIdCache`, `:12` `DAYS_TO_TIMEOUT`, `:27` `Save()`, `:35` `Load()`, `:49` `Reset()`, `:57` `DeleteOldCaches()`, `:62` `HasValidCache`, `:67` `Add`, `:81` `TryGet`, `:102` `IsValidCache`, `:106` `UniqueIdCacheEntry`), reached through `LauncherApp.cs:115`/`:130`/`:132`, consumed by `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Game/Launcher.cs:158`/`:187`.
* Related specs: `../Program.cs.md` (`ResetUIDCache`, `:484`), `docs/XIVLauncher.Core/LauncherApp.cs.md` (cache ownership), `docs/XIVLauncher.Core/Components/MainPage/MainPage.cs.md` §login (OTP gating), `../Configuration/ILauncherConfig.cs.md` (`IsUidCacheEnabled` `:29`, never defaulted), `../Components/SettingsPage/Tabs/SettingsTabGame.cs.md` (UI toggle + reset button), `../Support/Troubleshooting.cs.md` §payload.
