# FileSecretProvider.cs — Specification

**File:** `src/XIVLauncher.Core/Accounts/Secrets/Providers/FileSecretProvider.cs` · 53 lines · 1,350 bytes
**Namespace:** `XIVLauncher.Core.Accounts.Secrets.Providers` · **Declaration:** `public class FileSecretProvider : ISecretProvider`
**Role:** In-process `Dictionary<string,string>` backed by a plaintext JSON file — the "no daemon" secret backend

---

## 1. Purpose

The `XL_SECRET_PROVIDER=FILE` backend: an explicit escape hatch for environments without a usable credential service (Flatpak on GNOME without `org.freedesktop.secrets`, SSH-only setups, CI). Chosen at `Program.cs:396` as `new FileSecretProvider(storage.GetFile(secretsFilePath))`, where `secretsFilePath` is `Environment.GetEnvironmentVariable("XL_SECRETS_FILE_PATH") ?? "secrets.json"` (`Program.cs.md` §env), resolved through `Storage` into the app storage root — so **plaintext passwords sit in `<app storage>/secrets.json`** unless the user points the env var at something else.

## 2. Contract

| Member | Behaviour |
| --- | --- |
| `GetPassword(name)` | `savedPasswords.TryGetValue(name, out p)` ⇒ the password, else `null` |
| `SavePassword(name, pw)` | overwrite if key present, else `Add`; then `Save()` (full rewrite) |
| `DeletePassword(name)` | `Remove(name)` (missing key is a silent no-op); then `Save()` |
| `Save()` (`private`) | `File.WriteAllText(configFile.FullName, JsonSerializer.Serialize(savedPasswords))` |

On-disk shape is a flat JSON object, `accountName → password`, i.e. one file per app storage root:

```json
{ "alice": "hunter2", "bob": "..." }
```

Keys are `XivAccount.UserName` (see `ISecretProvider.cs.md` §2 and `XivAccount.cs.md` §2) — *not* the composite `Id`, so accounts that differ only in `UseOtp`/`UseSteamServiceAccount` collide, exactly as with the keyring backend.

## 3. Construction

```csharp
private readonly FileInfo configFile;
private readonly Dictionary<string, string> savedPasswords;

ctor:  this.configFile = configFile;
       if (configFile.Exists)
           this.savedPasswords = JsonSerializer.Deserialize<Dictionary<string, string>>(configFile.OpenText().ReadToEnd())!;
       this.savedPasswords ??= new Dictionary<string, string>();
```

- Everything happens **in the ctor**: the file is read at startup, and the whole file is kept as a live in-process map. No lazy loading, no reload, no watcher.
- `configFile` is the project's `FileInfo` handle (`FullName`, `Exists`, `OpenText()`); `OpenText().ReadToEnd()` slurps the whole file (fine at this scale, but it is read eagerly even when never queried).
- The trailing `!` is C# 13's **non-null assertion** on the deserialized value; combined with the `??=` on the next statement, null is handled twice. Net effect: absent file, empty file, and literal `null` JSON all end with an empty map — and **no file is created** until the first write (asymmetry with `AccountManager.Load()`, which saves immediately).
- **Malformed or schema-mismatched JSON throws out of the ctor** (`JsonDeserializationError`, uncaught) — a corrupted `secrets.json` crashes startup, unlike `DummySecretProvider`/`KeychainSecretProvider` which degrade. This is the file a user hand-edits, so the failure mode is the one a person is most likely to hit; a `try/catch` ⇒ empty map + `Log.Error` would match the other two backends' degradation style.
- **Schema is enforced only by the type parameter**: `Dictionary<string, string>` (System.Text.Json). Any nested/array/other value type fails; duplicate JSON keys collapse to the last occurrence (System.Text.Json default), so a hand-edited file with repeated keys silently keeps one password.
- `System.Text.Json` (the only `using` here) is the whole format story: **no indenting on write** (`JsonSerializer.Serialize` with no options) ⇒ a one-line file that grows with the password count. Pass a `WriterConfig` with `PrettyPrint = true` if hand editing of `secrets.json` matters.
- No encoding/permission handling: `File.WriteAllText` writes UTF-8 with the process umask's permissions. On a world-readable app dir this is an actual plaintext-password exposure — the single biggest reason this backend is opt-in and the default is `KEYRING`. Consider `0600` + ownership checks if this backend is to stay.

## 4. Mutation and save policy

- `Save()` rewrites the entire file **on every `SavePassword`/`DeletePassword`** (O(n) each; a login apply in `MainPage.cs` triggers at least one).
- There is **no error handling on write**: a read-only storage root raises out of `SavePassword`, which is reached from `XivAccount.Password = ...`, i.e. from the login path.
- Write is `File.WriteAllText` — truncate-then-fill, no temp-file + rename. A crash mid-write leaves truncated/partial JSON that then hits the ctor's throwing path on the next startup (see §3). A `*.tmp` + rename dance is the cheap fix.
- No thread protection: two concurrent writes race into the same file with no lock and no re-read, so the last full serialization wins (elements written by the loser are lost, not merged). The login flow runs off the frame loop inside `Task.Run` (`MainPage.cs:124`, `PersistAccount` at `MainPage.cs:212`), which is why this is a real shape rather than a theoretical one.
- `TryGetValue`/`ContainsKey`/`Add`/`Remove` are **case-sensitive**. `XivAccount`'s ctor lower-cases, but JSON-loaded names are not re-normalised (`XivAccount.cs.md` §2), so a legacy mixed-case key in `accounts.json` produces a mixed-case secret key that will never be found by a lower-cased lookup — a silently "forgotten" password.
- `savedPasswords` is `private readonly`, but the *dictionary* it points to is mutable and unencapsulated: nothing exposes it, so the invariant "the file is the only authority" holds only as long as no one adds an accessor that returns it.

## 5. Relationship to other backends

| Aspect | `FileSecretProvider` | `KeychainSecretProvider` | `DummySecretProvider` |
| --- | --- | --- | --- |
| Availability probe | none (assumed) | ctor probe, `IsAvailable` | n/a |
| Storage location | `<storage>/secrets.json` | keyring under `PACKAGE`/`SERVICE` | nothing |
| Persistence granularity | whole map, per call | per key | n/a |
| Failure channel | exceptions (parse/IO) | `KeyringException` | none |
| UI-visible | no | no | yes (`LoginFrame.cs:168` type check) |

Switching backends between runs does **not** migrate anything: the same account can have a password under a file key *and* a keyring key. `DeletePassword` exists on all three but is called nowhere (`ISecretProvider.cs.md` §3), so stale plaintext rows in `secrets.json` cannot be removed through the UI today.

## 6. Invariants and hazards

- **Plaintext, unrotated, unprotected:** no encryption, no file-mode restriction, no "this backend is degraded" UI marker (the warning covers only `DummySecretProvider`). Treat `FILE` as an explicit user decision and never pick it by default.
- **Whole-file load-bearing:** one corrupt character invalidates all accounts' passwords on next startup — and startup throws rather than degrading (§3).
- **The file *is* the key/value map**: `secrets.json` is safe to hand-edit only because the ctor reads it once at startup; edits made **after** startup (e.g. manual rotation) are invisible until the next run, and the first write re-serializes the in-memory copy and **clobbers** those manual edits.
- Nothing validates that `accountName` is non-empty, so a key of `""` is possible (reachable only from a non-`XivAccount` caller, since `XivAccount.Password`'s getter short-circuits on an empty name and its setter never writes empty values).
- No upper bound on file size, no pruning of accounts that were deleted: `AccountManager.RemoveAccount` is never called and secrets are never purged, so the file only grows.

## 7. Cross-references

- `ISecretProvider.cs.md` — the contract this class implements.
- `XivAccount.cs.md` §4 — how `GetPassword`/`SavePassword` are actually reached (the `Password` accessor) and its non-empty/`DeletePassword` gap.
- `AccountManager.cs.md` — the companion `accounts.json` (password-free) file.
- `docs/XIVLauncher.Core/Program.cs.md` — provider selection, `XL_SECRETS_FILE_PATH`, `XL_SECRET_PROVIDER`.
