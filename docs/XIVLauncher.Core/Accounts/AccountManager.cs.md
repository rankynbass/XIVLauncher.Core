# AccountManager.cs — Specification

**File:** `src/XIVLauncher.Core/Accounts/AccountManager.cs` · 103 lines · 2,976 bytes
**Namespace:** `XIVLauncher.Core.Accounts` · **Declaration:** `public class AccountManager`
**Role:** Persistent account registry — an *observed* collection of `XivAccount` that auto-saves on every mutation

---

## 1. Purpose

`AccountManager` owns the launcher's list of saved accounts (`accounts.json` under the app `Storage` root) and keeps it in sync with the UI (`AccountSwitcher`, `MainPage`) and with the selected-account pointer in the persisted config (`Program.Config.CurrentAccountId`, which lives in `launcher.ini`). Passwords are deliberately **not** part of this file — see the `[JsonIgnore]` on `XivAccount.Password` and `XivAccount.cs.md` §4 — so the registry is a *metadata* store: name + feature flags + character/avatar bookkeeping.

The file opens with a `// TODO: Store XivAccounts by username instead of using Id.` marker: identity is currently the composite `Id` string (see `XivAccount.cs.md` §2), which the TODO treats as the wrong key.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `Accounts` | `public ObservableCollection<XivAccount> = new()` | mutable field, **not** `readonly`: reassignable from outside the class |
| `CurrentAccount` | accessor `get`/`set` | `get` is derived, `set` is a pure config write — see §3 |
| `AccountManager(FileInfo configFile)` | ctor | `FileInfo` = the project's file handle (`Storage.GetFile("accounts.json")`) |
| `UpdatePassword(XivAccount, string)` | method | mutates the in-memory account; **no** `Save()` |
| `UpdateFreeTrial(XivAccount, bool)` | method | mutate + `Save()` |
| `UpdateLastSuccessfulOtp(XivAccount, string)` | method | mutate + `Save()` |
| `AddAccount(XivAccount)` | method | dedup-by-`Id` insert (see §4) |
| `RemoveAccount(XivAccount)` | method | identity-based removal (see §6) |
| `Save()` | `public` | rewrites the whole file |
| `Load()` | `public` | read/normalise the file; also used to *create* it |
| `configFile` | `private readonly FileInfo` | declared mid-file inside `#region SaveLoad` (layout only) |
| `Accounts_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)` | `private` | plain event-handler method matching `ObservableCollection.CollectionChanged`'s signature; `sender` is unused, and the binding at line 25 is an explicit `+=` — this is **not** C# 13's observed-`with` feature |

Usings are `System.Collections.ObjectModel` (the namespace `ObservableCollection` and `NotifyCollectionChangedEventArgs` resolve from), `Newtonsoft.Json` (the serializer used by `Save`/`Load`) and `Serilog`.

## 3. `Accounts` and `CurrentAccount`

`Accounts` is a .NET *observed* collection: mutations fire `CollectionChanged`, and the ctor binds

```csharp
Accounts.CollectionChanged += Accounts_CollectionChanged;   // → Save()
```

so any `Add`/`Remove` on the **bound instance** rewrites the JSON file. Two consequences:

- The handler is bound to whichever instance is current when the binding runs. The ctor calls `Load()` *before* binding, and `Load()` reassigns `Accounts` to a freshly deserialized instance, so the binding lands on the loaded collection — correct as written.
- Because `Accounts` is `public` and not `readonly`, an external reassignment produces a collection with **no** save hook.

`CurrentAccount` is a *view*, not storage. The backing store is a single string field in the persisted config:

```csharp
get { return Accounts.Count > 1 ? Accounts.FirstOrDefault(a => a.Id == Program.Config.CurrentAccountId)
                                : Accounts.FirstOrDefault(); }
set => Program.Config.CurrentAccountId = value?.Id;
```

| Situation | `get` result |
| --- | --- |
| 0 accounts | `null` (LINQ `FirstOrDefault` on a reference type, no throw) |
| exactly 1 account | that account — **regardless** of `Config.CurrentAccountId` |
| > 1 accounts, `Id` matches | the matching account |
| > 1 accounts, no match (stale/deleted id) | `null` |
| > 1 accounts, `CurrentAccountId` unset | `null` |

So with one account the id is never consulted (a stale `CurrentAccountId` is masked), and with several accounts a dangling id yields `null` rather than a fallback. Callers must handle `null` — `MainPage.cs:820` does (`App.Accounts.CurrentAccount != null` in `PersistAccount`, `var savedAccount = App.Accounts.CurrentAccount;` at `MainPage.cs:54`).

The setter writes `value?.Id`, so **assigning `null` clears `CurrentAccountId`**; the setter never adds or mutates `Accounts`. That is why `MainPage.cs:840-841` (`App.Accounts.AddAccount(accountToSave); App.Accounts.CurrentAccount = accountToSave;`) adds first and then selects — reversing the order leaves `CurrentAccount` reading `null`. `set` also does not verify that the account is in `Accounts`, so an id can be stored for an account that was never added.

## 4. Mutation semantics

### `AddAccount(account)`

Lookup is always `Accounts.FirstOrDefault(a => a.Id == account.Id)`, i.e. **`Id` equality**, where `Id = "{UserName}-{UseOtp}-{UseSteamServiceAccount}"`.

1. existing found **and** `existingAccount.Password != account.Password` → copy the password into the existing account and **`return`** (nothing added, other flags not merged).
2. existing found (passwords equal) → `return`.
3. otherwise → `Accounts.Add(account)` (fires `CollectionChanged` ⇒ `Save()`).

Because both operands of the password comparison are *accessors into the same secret store keyed by `UserName`* (see `XivAccount.cs.md` §4), an existing/new pair that shares a `UserName` reads the same secret — the branch is only reachable when the write in step 3 failed or the provider degrades. Practically `AddAccount` is "dedup by `Id`, drop everything else": the new instance's `IsFreeTrial`, `ChosenCharacter*`, `ThumbnailUrl`, `LastSuccessfulOtp` are discarded when an `Id`-twin exists.

The **password-update branch does not call `Save()`** (see §5) — harmless for the JSON file (passwords are not persisted) but it is the odd one out among the three `Update*` methods.

### `UpdatePassword` / `UpdateFreeTrial` / `UpdateLastSuccessfulOtp`

All three locate the account by `Id` and silently do nothing if it is absent. They mutate the **found instance**, not the argument, so `MainPage.cs:820-828` calling `App.Accounts.UpdatePassword(App.Accounts.CurrentAccount, password)` updates the registry entry, and a caller's handle only sees the change if it is the same object. Each write goes through `XivAccount.Password`'s setter, i.e. through `Program.Secrets.SavePassword` (`XivAccount.cs.md` §4) — so `UpdatePassword` touches the secret backend, not the JSON file.

`UpdatePassword` and `UpdateFreeTrial` log `Log.Information("...() called")` unconditionally; `UpdateLastSuccessfulOtp` does not log.

### `RemoveAccount(account)`

`Accounts.Remove(account)` locates by `IndexOf`, i.e. `==` → **object identity** for `XivAccount` (no `operator==` is declared). A distinct-but-equivalent instance is therefore *not* found, and the observed collection **silently does nothing** in that case (verified: no exception, count unchanged). Callers must pass the instance held in `Accounts`.

No call site of `RemoveAccount` exists in this repository (`lib/FFXIVQuickLauncher` calls it from the Windows `AccountSwitcher.xaml.cs`), so "delete an account" is currently unimplemented in Core.

## 5. Persistence contract

```csharp
public void Save() => File.WriteAllText(this.configFile.FullName,
                                         JsonConvert.SerializeObject(Accounts, Formatting.Indented));
```

- The `FileInfo` comes from `Storage.GetFile("accounts.json")` (`LauncherApp.cs.md` §3) — i.e. `<app storage root>/accounts.json`.
- Newtonsoft serializes the observed collection as a **JSON array**; each element is the persisted subset of `XivAccount` (`UserName`, `SavePassword`, `UseSteamServiceAccount`, `UseOtp`, `IsFreeTrial`, `ChosenCharacterName`, `ChosenCharacterWorld`, `ThumbnailUrl`, `LastSuccessfulOtp`) — `Id` and `Password` are `[JsonIgnore]`-suppressed in that class, so **no password ever lands in this file**.
- `Save()` rewrites the whole file (O(n)) on every single mutation, synchronously, and it is reached from the login flow that runs inside `Task.Run` (`MainPage.cs:124`, which calls `PersistAccount` at `MainPage.cs:212`).
- Writing uses `File.WriteAllText` with no temp-file/rename dance and no error handling: a read-only storage root turns every mutation into an unhandled write exception.

### `Load()`

1. File missing → `Save()` (writes `[]`) and return — the file is created as a side effect of construction.
2. File present → `Accounts = JsonConvert.DeserializeObject<ObservableCollection<XivAccount>>(...)`.
3. `Accounts ??= new ObservableCollection<XivAccount>();` — in-source comment "If the file is corrupted, this will be null anyway".

Note that (3) only catches the `null` JSON literal: **malformed JSON throws out of `Load()`** (`JsonParseException`) and out of the ctor, into `LauncherApp`'s construction and `Program` startup — the null-coalesce never runs. Also note (2) **replaces** the collection instance, so any handle captured earlier must re-read the field rather than cache the collection (`AccountSwitcher.cs:14` holds the *manager*, and reads `manager.Accounts` each frame, so it is fine).

### Ordering trap

`Accounts_CollectionChanged` → `Save()` serializes whatever `Accounts` currently points at, and `Accounts` is reassignable by `Load()`. Any future code that mutates the *old* instance (a handle captured before a `Load()`) would fire the handler and save the new instance — an inconsistency the current call graph does not exploit.

## 6. Invariants and hazards

- **Selection vs membership are independent.** `CurrentAccountId` may name a nonexistent account (§3 table rows 4–5); nothing prunes it, and `RemoveAccount` — which would have pruned it — is itself uncalled.
- **Single-account masking:** `Accounts.Count > 1` special-cases the one-account case, so anything assuming `CurrentAccount.Id == Config.CurrentAccountId` breaks as soon as a second account exists.
- **`Id` is a fragile key:** it embeds `bool` formatting, so a change of `UseOtp`/`UseSteamServiceAccount` renames the record; `MainPage.cs:830` re-implements the same format expression (`$"{username}-{isOtp}-{isSteam}"`) instead of reusing `Id`, which is exactly the duplication the file's `TODO` is about.
- **No password in the registry** is deliberate (secrets live in `Program.Secrets`), but the file still carries `ChosenCharacterName`, `ThumbnailUrl` and `LastSuccessfulOtp` in plaintext; `LastSuccessfulOtp` is a previously consumed OTP value — worth revisiting alongside the `TODO`.
- **`Accounts` is public and mutable**, so the auto-save contract can be silently defeated; consider `public readonly` + an explicit mutation API if that ever bites.
- **No thread protection**: login state is applied from inside `Task.Run` (`MainPage.cs:124`) and each mutation performs a full-file write, so concurrent mutations would race with no lock; the current single-login flow keeps this latent rather than observed.
- No cap on account count and no sort — the JSON array order *is* the display order (`AccountSwitcher.cs` iterates `manager.Accounts`).

## 7. Cross-references

- `XivAccount.cs.md` — the element type: `Id`, the `[JsonIgnore]` set, and the `Password` accessor that reaches `Program.Secrets`.
- `docs/XIVLauncher.Core/LauncherApp.cs.md` — owns the instance (`Accounts = new AccountManager(Storage.GetFile("accounts.json"))`) and passes it to pages.
- `docs/XIVLauncher.Core/Program.cs.md` — `Program.Config.CurrentAccountId` (`launcher.ini`) and `Program.Secrets`.
- `src/XIVLauncher.Core/Components/MainPage/AccountSwitcher.cs` — iterates `manager.Accounts`, renders `(Steam)`/`(OTP)`/`(Trial)` suffixes, fires `AccountChanged`.
- `src/XIVLauncher.Core/Components/MainPage/MainPage.cs` — `SwitchAccount`, `PersistAccount` (`UpdatePassword`/`UpdateFreeTrial`/`AddAccount`/`CurrentAccount =`).
