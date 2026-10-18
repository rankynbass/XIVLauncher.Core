# KeychainSecretProvider.cs — Specification

**File:** `src/XIVLauncher.Core/Accounts/Secrets/Providers/KeychainSecretProvider.cs` · 73 lines · 2,089 bytes
**Namespace:** `XIVLauncher.Core.Accounts.Secrets.Providers` · **Declaration:** `public class KeychainSecretProvider : ISecretProvider`
**Role:** The default secret backend: the D-Bus `org.freedesktop.secrets` service via `KeySharp` (libsecret), with a startup availability probe

---

## 1. Purpose

The default backend (`XL_SECRET_PROVIDER` defaults to `KEYRING`; `Program.cs:401` constructs it, `Program.cs:406` falls back to `DummySecretProvider` if it reports unavailable). Passwords go to the desktop credential service rather than to a file, which is the reason the launcher does not need an "I don't have a password store" state on a normal Steam Deck / GNOME / KDE setup.

The `PACKAGE` value is the **Flathub app id** (`dev.goats.xivlauncher`), i.e. secrets are stored under a Flatpak-app identity. That is a deliberate namespace choice (see §4) and means the *same user* running the launcher differently gets a different namespace.

## 2. Contract (and the constants)

| Member | Behaviour |
| --- | --- |
| `PACKAGE` | `public const string = "dev.goats.xivlauncher"` — the libsecret *application* name |
| `SERVICE` | `public const string = "SEID"` — the libsecret *category* (SEID = the FFXIV "SE account id" naming) |
| `IsAvailable` | `get`/`private set` — set by the ctor from `SetDummyAndCheck()` |
| ctor | `this.IsAvailable = SetDummyAndCheck();` |
| `SetDummyAndCheck` | the probe, `public bool` — writes a dummy entry, reads it back, returns the bool |
| `GetPassword(name)` | `Keyring.GetPassword(PACKAGE, SERVICE, name)`, with `NotFound` ⇒ `null` |
| `SavePassword(name, pw)` | `Keyring.SetPassword(PACKAGE, SERVICE, name, pw)`, unguarded |
| `DeletePassword(name)` | `Keyring.DeletePassword(PACKAGE, SERVICE, name)`, unguarded |

`const` **fields** (compile-time constants, not fields in the ordinary sense) mean `PACKAGE`/`SERVICE` are usable in constant contexts and cannot be reassigned per instance — the tradeoff is that neither is overridable by config, so "change your secret namespace" requires editing this file. They are `public`, so they *could* be referenced by callers; nothing does today.

Keyring keys are `(PACKAGE, SERVICE, accountName)` — `accountName` is the `XivAccount.UserName` (see `ISecretProvider.cs.md` §2 and `XivAccount.cs.md` §2), so accounts that differ only by `UseOtp`/`UseSteamServiceAccount` share one credential.

The three `const` local strings inside `SetDummyAndCheck` (`DUMMY_SVC = "XIVLauncher Safe Storage Control"`, `DUMMY_NAME = "XIVLauncher"`, `DUMMY_PW = "Honi soit qui mal y pense"`) are scoped to the probe and form a **separate key** `(PACKAGE, "XIVLauncher Safe Storage Control", "XIVLauncher")` from the password namespace — they never collide with `SERVICE = "SEID"`. `DUMMY_PW` is a fixed, public value and therefore *not* a secret; the probe only needs a known round-trip value.

## 3. The availability probe

```
try:
    Keyring.SetPassword(PACKAGE, DUMMY_SVC, DUMMY_NAME, DUMMY_PW)      // Log.Verbose
    var saved = Keyring.GetPassword(PACKAGE, DUMMY_SVC, DUMMY_NAME)     // Log.Verbose
    return saved == DUMMY_PW
catch (Exception ex):
    Log.Error(ex, "Could not init the keychain")
return false
```

- The in-source comment explains **why** the write exists: it is the documented libsecret/GNOME workaround for "keyring not unlocked/created on first use" (`http://crbug.com/660005`) — writing one known entry forces the service to create/unlock a real entry, after which reads work. Do **not** simplify the probe to a read-only availability check; that is the whole point of this method.
- Probe semantics are "write-then-read-back-equal", which is stronger than "service responds": a backend that lies on writes (a no-op compatibility service) is correctly rejected. `saved == DUMMY_PW` is value equality on `String`, so a service that returns a different representation of the stored secret (added/trimmed bytes) makes the probe report `false` and the app silently drops to `DummySecretProvider` — i.e. **not** remembering passwords — with only an `Error` log.
- **Cost and side effects of construction:** constructing the class writes two dummy keys and performs a D-Bus round trip. It is constructed exactly once at `Program.cs:401`, but the class is not defensive: calling `SetDummyAndCheck` (or constructing twice) repeats the dummy write. `IsAvailable` is `get`/`private set`, so it is write-once and there is no re-probe API — the availability decision is frozen at startup; a keyring that becomes available later in the run is not picked up.
- **Catch-all `Exception`** rather than `KeyringException`: any failure (D-Bus absent, timeout, cancellation, SELinux) becomes `false`, which is the desired degradation, but it also swallows programming errors into a single `Log.Error` whose text (`"Could not init the keychain"`) does not name the backend — logs alone may not say "keyring".
- No timeout/cancellation handling; this runs on the **startup** path, so a broken D-Bus setup costs startup time, not UI frames.

## 4. Method-level behaviour and error model

- `GetPassword` catches **`KeyringException`** (deliberately narrower than the probe's catch-all) and returns `null` only for `ex.Type == ErrorType.NotFound`; anything else is **rethrown with bare `throw;`** (original stack preserved, no wrapping). `ErrorType.OperationCancelled`, timeouts and other failure kinds therefore propagate — and `XivAccount.Password`'s **getter** in `XivAccount.cs` has no `try/catch` (`XivAccount.cs.md` §4), so a non-`NotFound` failure surfaces as an exception from an innocuous-looking property read (reachable from `MainPage.cs:101` and the startup-modal path in `LauncherApp.cs.md` §5).
- `SavePassword` and `DeletePassword` are **unguarded**: every `KeyringException` propagates into the property setter (`XivAccount.Password = ...`). Because that setter refuses empty values, a failed store is reported only by exception, with no fallback and no logged reason at the call site.
- `Log.Information` on `GetPassword`/`SavePassword`/`DeletePassword` means every password access is logged at info level — **password traffic is logged by default** (the *values* are not, but the events are). Combined with the `Log.Verbose` pair in the probe, expect keyring chatter in the log file at the configured verbosity. Do not add value logging near these calls.
- `SavePassword` does not verify it stored anything (KeySharp returns `void`), so the interface's "no failure channel" is honoured by exception only — see `ISecretProvider.cs.md` §4.
- Availability is per-instance state (`IsAvailable`) and **the three methods do not consult it**: if a caller ever used this class without checking `IsAvailable`, the calls would throw rather than no-op. `Program.cs`'s `if (!keyChain.IsAvailable)` is the only enforcement point in the codebase — see `ISecretProvider.cs.md` §4 for the contract gap.
- `ErrorType` (KeySharp) values are matched by name here; a KeySharp upgrade that renames/replaces `NotFound` turns into a compile error rather than silent mis-handling, which is the safer direction.

## 5. Environment notes

- Namespace `...Secrets.Providers`, package `KeySharp` 1.0.5 (`src/XIVLauncher.Core/XIVLauncher.Core.csproj:73`) → `libsecret` over D-Bus → requires a running `org.freedesktop.secrets` provider (`gnome-keys`, KDE's equivalent). Flatpak runs the launcher *with* D-Bus but with a restricted service set, which is why the probe exists rather than an unconditional trust.
- `PACKAGE`/`SERVICE` (not a "provider type" field) decide which entries are visible — switching to/from `FILE` leaves both namespaces populated (`ISecretProvider.cs.md` §5), and the launcher never purges either (`DeletePassword` has no caller anywhere in `src/`).
- The probe's dummy keys persist in the user's credential service permanently (nothing deletes them); `DeletePassword` — available but uncalled — is the natural place to clean them if that ever matters.

## 6. Cross-references

- `ISecretProvider.cs.md` — the contract, and the "availability is not in the contract" note.
- `DummySecretProvider.cs.md` — the fallback `Program.cs:406` installs when `IsAvailable == false`.
- `FileSecretProvider.cs.md` — the alternative backend and the namespace-migration note.
- `XivAccount.cs.md` §4 — the accessor that reaches all three methods and its missing `try/catch`.
- `docs/XIVLauncher.Core/Program.cs.md` — the default selection and the downgrade branch.
