# AccountSwitcherEntry.cs — Specification

**File:** `src/XIVLauncher.Core/Accounts/AccountSwitcherEntry.cs` · 51 lines · 1,344 bytes
**Namespace:** `XIVLauncher.Core.Accounts` · **Declaration:** `class AccountSwitcherEntry` (no access modifier ⇒ **internal**)
**Role:** Per-account avatar entry (profile picture cache) — a port of the Windows `AccountSwitcherEntry`, currently **unused** in Core

---

## 1. Purpose

The Windows launcher's `AccountSwitcher` UI shows one row per account with the character avatar next to it. This file is the Core-side half of that row: it pairs a `XivAccount` with a `TextureWrap?` profile image, and it caches the downloaded avatar on disk. In Core the class is **not referenced anywhere** (see §4) and its body is only half ported (the image decode is commented out), so treat it as scaffolding for a feature that never shipped.

The declaration is `class AccountSwitcherEntry` with no access modifier, i.e. internal visibility: it cannot be used from another compilation unit even though its members are `public`. Everything about this class is file-local.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `AccountSwitcherEntry(XivAccount account)` | ctor | `this.Account = account` — wraps by reference, no copy |
| `Account` | `XivAccount { get; private set; }` | immutable handle from outside; **not** serialized (this class is not part of any JSON record) |
| `ProfileImage` | `TextureWrap? { get; set; }` | no initializer ⇒ starts `null`; **never assigned** (§3) |
| `UpdateProfileImage(DirectoryInfo storage)` | `void`, synchronous | disk cache + HTTP fetch, see §4 |

`TextureWrap` is the project's GPU/ImGui texture handle — see `docs/XIVLauncher.Core/TextureWrap.cs.md` for the ownership/upload contract. `DirectoryInfo` is the project's directory handle (`FullName`, `Exists`, `Create()`), supplied by the caller rather than derived from `Storage`.

## 3. `ProfileImage` is structurally dead

The decode block is commented out (`// TODO /* ... */`), so `UpdateProfileImage` never assigns `ProfileImage` and the field remains `null` after any call. The commented code is Windows-flavoured (`BitmapImage` with `BeginInit`/`EndInit`/`StreamSource`/`CacheOption`/`Freeze`), i.e. a WinForms/`System.Windows` port that has no Core equivalent yet — the Core-side replacement would construct a `TextureWrap` from the bytes (see `TextureWrap.cs.md`).

Consequence: the `MemoryStream` line is live but useless —

```csharp
using var stream = new MemoryStream(imageBytes);
```

`imageBytes` is therefore a live allocation that is read into, never consumed (and never zeroed), and the stream is closed at scope end. A `TextureWrap` upload would be the first thing that consumes it.

## 4. `UpdateProfileImage(DirectoryInfo storage)`

```
ThumbnailUrl empty → return (no clear, no signal)
cacheFolder = <storage.FullName>/profilePictures   // Directory.CreateDirectory (unconditional)
uri         = new Uri(ThumbnailUrl)
cacheFile   = <cacheFolder>/<uri.Segments.Last()>
if File.Exists(cacheFile)  imageBytes = File.ReadAllBytes(cacheFile)
else                       imageBytes = Program.HttpClient.GetByteArrayAsync(uri).Result
                           File.WriteAllBytes(cacheFile, imageBytes)
```

- The download uses **absolute URL** through `Program.HttpClient`, the shared launcher-wide client from `Program.cs.md`, so it inherits that client's global configuration (no proxy/auth handling here).
- **Cache key is only the last path segment** (`uri.Segments.Last()`, and `Uri.Segments` excludes the query string), so two avatar URLs sharing a tail collide, and a query-string version bump does **not** invalidate the cache; a renamed image does. There is no cache eviction, no expiry, and no per-account namespace beyond the shared `profilePictures` folder.
- The empty-`ThumbnailUrl` guard returns **before** `Directory.CreateDirectory`, so the folder is created only for accounts that actually have a thumbnail — but `CreateDirectory` is still called on *every* such call (redundant syscalls in the steady state), and it throws if a *file* (not directory) exists at that name.
- The folder name is hard-coded `"profilePictures"`, i.e. not routed through `Storage.GetFolder(...)`, which is how the other storage subdirectories are obtained (`ffxiv`, `ffxivConfig`) — worth aligning if this ever ships.
- `new Uri(ThumbnailUrl)` throws for anything that is not a valid absolute URI, and `ThumbnailUrl` is user-populated data on disk (the Windows launcher wrote it), so a corrupt `accounts.json` can raise during a Core render path.
- `uri.Segments.Last()` can be `"/"` (bare-path URL) and is used **unsanitised** as a file name: nothing rejects `..`-shaped segments or characters that are invalid on the filesystem. Combined with the unfiltered write, this is a path-manipulation surface: a malformed/hijacked avatar URL decides where bytes are written inside the storage root.
- `GetByteArrayAsync(uri).Result` is a **blocking task read**: `.Result` rethrows a faulted task's exception and stalls the calling thread; there is no status/content-type check and no timeout/cancellation. An HTTP error becomes an uncaught exception at this call site.
- Non-empty `ThumbnailUrl` that fails to load ⇒ **exception**, whereas a missing URL ⇒ **silent return**. The two "no image" states are reported differently, and neither clears a previously set `ProfileImage`.
- No size cap: an avatar URL is downloaded in full into memory.

## 5. Invariants and hazards

- **Unreachable from Core today.** Nothing constructs `AccountSwitcherEntry`, so all of §4 is dormant. Do not reason about it as live behaviour; if the feature is revived, expect to fix the decode (§3) *and* add the call site (Core has none).
- **Not a `Component`.** The Core UI is ImGui-based and component-drawn (`Components/…`), so an entry class of this shape does not plug into `AccountSwitcher.cs` as-is: the Windows design was a WPF list-item; the Core equivalent is to draw inside `AccountSwitcher.Draw()`.
- **Synchronous + HTTP in a method named like a UI update** — do not call it from the frame loop as-is.
- No ownership/lifetime story for `TextureWrap` (upload/destroy), no error channel, no way to reset the cache.

## 6. Cross-references

- `XivAccount.cs.md` — `ThumbnailUrl` (the input) and `FindCharacterThumb()` (its absent producer, see that spec's §6).
- `AccountManager.cs.md` — where accounts come from and why `ThumbnailUrl` is persisted but never written in Core.
- `docs/XIVLauncher.Core/TextureWrap.cs.md` — the intended image type.
- `src/XIVLauncher.Core/Components/MainPage/AccountSwitcher.cs` — the live Core account UI (text buttons only, no avatars).
- `lib/FFXIVQuickLauncher/src/XIVLauncher/Accounts/AccountSwitcherEntry.cs` — the Windows original this was cut from (`async Task UpdateProfileImage()`); submodule, out of scope here.
