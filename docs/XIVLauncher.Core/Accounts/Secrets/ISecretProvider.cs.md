# ISecretProvider.cs — Specification

**File:** `src/XIVLauncher.Core/Accounts/Secrets/ISecretProvider.cs` · 10 lines · 256 bytes
**Namespace:** `XIVLauncher.Core.Accounts.Secrets` · **Declaration:** `public interface ISecretProvider`
**Role:** The three-method capability contract every password backend must satisfy

---

## 1. Purpose

A minimal, backend-agnostic contract for storing the *launcher-side* password of an account, i.e. the FFXIV account password the launcher keeps so it can log in without re-typing. It exists so the app can run on Flatpak/GNOME-without-credential-backend (`NONE`), on a plain file (`FILE`), or on a real keyring (`KEYRING`) without `XivAccount` knowing which (see `XivAccount.cs.md` §4 and `Program.cs.md`).

It is a pure *capability* type: no availability flag, no error model, no identity. Everything beyond the three verbs has been bolted onto individual implementations (see §4).

## 2. Contract

| Member | Expected behaviour | Nullability / errors |
| --- | --- | --- |
| `string? GetPassword(string accountName)` | Look up the stored password for `accountName` | `null` ⇒ "not stored"; a **normal** miss must not throw (see §3) |
| `void SavePassword(string accountName, string password)` | Store/overwrite the password | `void` ⇒ no failure channel; must throw only on genuine backend failure |
| `void DeletePassword(string accountName)` | Remove the entry | no "did it exist" signal; missing key should be a no-op |

`accountName` is the **`XivAccount.UserName`** (lower-cased at construction — see `XivAccount.cs.md` §2) — *not* `Id`. That is what makes accounts differing only in `UseOtp`/`UseSteamServiceAccount` share one secret.

No default implementations, no `virtual`/`default` members, no static members — so all three methods are mandatory in each implementor, which is exactly what the three files under `Accounts/Secrets/Providers/` do. The explicit `public` on each member is the C# 13 interface-member style; interface members are implicitly public anyway, so it is documentation, not behaviour.

## 3. Consumers in-repo

| Site | Use |
| --- | --- |
| `Program.cs:54` `public static ISecretProvider Secrets { get; private set; } = null!;` | the single global handle, typed as the interface |
| `Program.cs:202` `Secrets = GetSecretProvider(storage);` | selection at startup, before the UI runs (`Program.cs.md`) |
| `Program.cs:386` `private static ISecretProvider GetSecretProvider(Storage storage)` | `XL_SECRET_PROVIDER` env switch (`FILE`/`KEYRING`/`NONE`, default `KEYRING` at `Program.cs:390`), file name from `XL_SECRETS_FILE_PATH` at `Program.cs:388` (default `secrets.json`) |
| `XivAccount.cs` `Password` accessor | the **only** in-repo calls of `GetPassword`/`SavePassword` |
| `src/XIVLauncher.Core/Components/MainPage/LoginFrame.cs:168` `if (Program.Secrets is DummySecretProvider)` | shows `Strings.NoSecretsProviderWarning` |

`DeletePassword` has **no caller anywhere** in XIVLauncher.Core (verified by grep across `src/`), and `XivAccount.Password`'s setter deliberately refuses to call it for empty values — so password removal is unreachable through the normal UI. Treat the method as reserved for a future "forget account" action.

`Program.Secrets` is `null!`-typed: any use before `Program` startup completes NPEs. The UI only runs after `Secrets` is assigned, so the accessor path is safe in practice, but the hazard is visible to any future code that touches `XivAccount.Password` earlier (tooling, tests, generators).

## 4. Design notes

- **Availability is not part of the contract.** `KeychainSecretProvider` grows an `IsAvailable { get; private set; }` (see `KeychainSecretProvider.cs.md`), and `Program.cs:398-410` must special-case it (`if (!keyChain.IsAvailable) { Log.Error(...); return new DummySecretProvider(); }`). The interface has no place for that probe, which forces the concrete type out from behind the interface — a smell worth resolving by giving the contract an `IsUsable`-style member (or a `Result`-shaped return) before adding a 4th backend.
- **Degradation is expressed by type choice, and the UI tests it by type.** `LoginFrame.cs:168` matches on the concrete `DummySecretProvider`, so "secrets are not being stored" is a property of a class identity, not of `ISecretProvider` — a second no-op backend would show no warning.
- **No error model.** `void` + `string?` means every implementation invents its own failure channel (KeySharp throws `KeyringException`; `FileSecretProvider` lets JSON/IO errors escape). Callers must catch per-backend, which the accessor in `XivAccount.cs` does **not** do — see `XivAccount.cs.md` §4 and §6 for the resulting behaviour: a keyring failure during `Password = ...` propagates into the login path.
- **No key namespace.** Implementors choose their own (KeySharp uses `PACKAGE = "dev.goats.xivlauncher"` / `SERVICE = "SEID"`), so secrets are not interchangeable between backends and switching `XL_SECRET_PROVIDER` between runs silently "loses" passwords (they exist under a different backend's key scheme). A real launcher-facing contract would either name the namespace here or state that backends are not migratable.
- No notion of which account owns which password beyond the flat name key: no grouping, no metadata, no way to enumerate/rotate. If the UI ever needs "list saved accounts' secrets", the contract must gain an iteration member — `AccountManager` keeps its own (password-free) list as a workaround today.

## 5. Cross-references

- `DummySecretProvider.cs.md`, `FileSecretProvider.cs.md`, `KeychainSecretProvider.cs.md` — the three implementations.
- `Program.cs.md` — the selection logic, env vars, and the storage path.
- `XivAccount.cs.md` — the only real caller (`Password`), and the keying consequence.
- `docs/XIVLauncher.Core/LauncherApp.cs.md` — where the warning surfaces in the UI (`LoginFrame.cs`).
