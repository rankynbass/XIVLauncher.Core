# DummySecretProvider.cs — Specification

**File:** `src/XIVLauncher.Core/Accounts/Secrets/Providers/DummySecretProvider.cs` · 19 lines · 376 bytes
**Namespace:** `XIVLauncher.Core.Accounts.Secrets.Providers` · **Declaration:** `public class DummySecretProvider : ISecretProvider`
**Role:** The "no secret storage" fallback — read returns nothing, write is a no-op

---

## 1. Purpose

A null-object implementation of `ISecretProvider` used when no usable backend exists. It keeps the app running (no exceptions, no prompts) at the cost of **not remembering passwords**, so the user must type the password every login.

Two ways it gets installed (both in `Program.GetSecretProvider`, see `Program.cs.md` §"Secret provider selection"):

- `Program.cs:406` — `XL_SECRET_PROVIDER=KEYRING` **and** a fresh `KeychainSecretProvider()` reported `IsAvailable == false`, i.e. the implicit downgrade. `Program.cs:404` logs `"An org.freedesktop.secrets provider is not available - no secrets will be stored"` alongside it.
- `Program.cs:413` — explicit opt-out via `XL_SECRET_PROVIDER=NONE`.
- Note `Program.cs:415-416` (`default:` ⇒ `throw new ArgumentException($"Invalid secret provider: {envVar}")`) so an unrecognised provider name is a **hard startup failure**, not a downgrade to this class.

It is also the only backend whose presence the UI can see: `src/XIVLauncher.Core/Components/MainPage/LoginFrame.cs:168-171` tests `Program.Secrets is DummySecretProvider` and shows `Strings.NoSecretsProviderWarning` (`Resources/Localization/Strings.resx`, regenerated `Strings.Designer.cs:906`). Because that test is a **type check**, this class doubles as the UI's "secrets unsupported" sentinel.

## 2. Contract

| Member | Behaviour |
| --- | --- |
| `GetPassword(accountName)` | always `null` |
| `SavePassword(accountName, password)` | returns immediately; the password is **discarded** |
| `DeletePassword(accountName)` | returns immediately |

Stateless, total (never throws), deterministic. Nothing is written to disk, to the keyring, or to the log.

## 3. Observable effect on `XivAccount.Password`

With this provider, `XivAccount.cs` §4 gives:

- `get` ⇒ `Program.Secrets.GetPassword(...)` is `null` ⇒ **`""`**. Passwords are not retrievable.
- `set` ⇒ `Program.Secrets.SavePassword(UserName, value)` is a no-op; the accessor's own non-empty guard is irrelevant here. Setting `Password = x` looks successful but stores nothing.

That is exactly the trap the warning exists to cover, and it interacts with the live call sites:

- `MainPage.cs:820-823` guards the refresh with `App.Accounts.CurrentAccount.Password != password && ...SavePassword`, which is always true against a non-empty typed password — so the branch fires on every login that has a current account, and `UpdatePassword` re-writes the in-memory copy.
- `MainPage.cs:100-101` (`if (account.SavePassword) loginFrame.Password = account.Password;`) therefore assigns `""` to the login frame when switching accounts — the user must retype.
- `AccountManager.UpdatePassword` then stores that password into the **account object**, which is `[JsonIgnore]` on serialization, so it is still not persisted to `accounts.json`. Net effect: passwords live only in memory for the run, as designed.

## 4. Notes and invariants

- **The name is load-bearing**, twice: the type is the sentinel for `LoginFrame.cs`, and the ctor path is the only downgrade branch in `Program.cs`. Do not rename it without touching both.
- **Silent data loss.** Writing a password here is indistinguishable (by return value) from writing one successfully; only the UI type-check reports it. If a second no-op backend is added, update the interface to carry an "accepts secrets" capability rather than adding another type to the `is` chain (see `ISecretProvider.cs.md` §4).
- The class deliberately implements the whole interface instead of letting the app handle `null` providers (`Program.cs` uses `null!`-initialized fields elsewhere), so keep it as the null object; deleting it would force `Program.Secrets` to become nullable and every accessor to null-check.
- Three `public` members, no state, no ctor — the class is only an identity. It is a `class`, so a second `new DummySecretProvider()` is a distinct instance; the `is` check is type-based (`LoginFrame.cs:168`), so multiple instances are fine. Do not switch to `==`/instance comparison.
- `// ignored` comments mark the intentional emptiness — keep them if the body is ever replaced by a shared no-op base, otherwise reviewers lose the "this is not a bug" signal.

## 5. Cross-references

- `ISecretProvider.cs.md` — the contract and its design gaps.
- `KeychainSecretProvider.cs.md` — the `IsAvailable` probe that decides this fallback.
- `FileSecretProvider.cs.md` — the alternative fallback (`FILE`).
- `docs/XIVLauncher.Core/Program.cs.md` — the selection table and env vars.
- `src/XIVLauncher.Core/Components/MainPage/LoginFrame.cs` — the UI warning (out of scope for this spec pass, in the sibling page folder).
