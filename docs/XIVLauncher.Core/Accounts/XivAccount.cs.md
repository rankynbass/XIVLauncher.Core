# XivAccount.cs — Specification

**File:** `src/XIVLauncher.Core/Accounts/XivAccount.cs` · 98 lines · 3,107 bytes
**Namespace:** `XIVLauncher.Core.Accounts` · **Declaration:** `public class XivAccount`
**Role:** The persisted account record — identity, secret delegation, feature flags — plus the XIVAPI character lookup

---

## 1. Purpose

One `XivAccount` is one saved login entry. Instances are created only in `MainPage.PersistAccount` (`new XivAccount(username) { Password = ..., SavePassword = true, ... }`) and read back through `AccountManager`, which serializes a list of them to `accounts.json`. The class therefore defines both the **in-memory identity** used by `AccountManager` and the **JSON record** on disk, and it hides the password behind an accessor that talks to a global secret provider.

Usings: `Newtonsoft.Json` (the `[JsonIgnore]` annotations), `Newtonsoft.Json.Linq` (the `JObject` results of the XIVAPI helpers), `Serilog`.

## 2. Identity

```csharp
[JsonIgnore]
public string Id => $"{UserName}-{UseOtp}-{UseSteamServiceAccount}";
public override string ToString() => Id;
```

- `Id` is **computed, not stored** — it is `[JsonIgnore]`-suppressed, so the disk record does not contain it and it is re-derived on load (a *name-based* `Id` is what the `TODO` in `AccountManager.cs` is about).
- It embeds `bool` formatting, so `Id` is `"alice-true-false"`. Changing `UseOtp`/`UseSteamServiceAccount` **renames** the record: `AccountManager` locates accounts by `Id`, so flag flips are "a different account", and `MainPage.cs:830` hand-duplicates the same format (`$"{username}-{isOtp}-{isSteam}"`). If `ToString()` ever becomes display-facing, note it is just the id.
- `UserName` is lower-cased **only in the ctor**. Anything that arrives via Newtonsoft deserialization does not run the ctor, so historical mixed-case names keep their case and stop matching freshly built ids — a case-mismatch hazard for both `Id` and the secret key (§4).
- `ToString()` is `override` without `= default`; it takes the `object`-family slot, which is why it needs `override`.

## 3. Persisted surface

| Member | Kind | Notes |
| --- | --- | --- |
| `UserName` | `string { get; private set; }` | **no public setter** — identity is immutable from outside; the ctor is the only writer in this file |
| `SavePassword` | `bool { get; set; }` | default `false` |
| `UseSteamServiceAccount` | `bool { get; set; }` | default `false` |
| `UseOtp` | `bool { get; set; }` | default `false` |
| `IsFreeTrial` | `bool { get; set; }` | default `false` |
| `ChosenCharacterName` / `ChosenCharacterWorld` | `string = ""` | field-with-initializer, fed to `FindCharacterThumb` |
| `ThumbnailUrl` | `string = ""` | assigned only by `FindCharacterThumb` **callers**; nothing in this repo assigns it (see §6) |
| `LastSuccessfulOtp` | `string = ""` | written only through `AccountManager.UpdateLastSuccessfulOtp` |
| `XivAccount(string userName)` | ctor | `UserName = userName.ToLower()` — the single normalisation point |

`SavePassword` is **never read inside this file**; its consumers are `MainPage.cs:100` (`if (account.SavePassword) loginFrame.Password = account.Password;`) and `MainPage.cs:822` (the password-refresh guard). Semantics (undocumented in code): "this account was saved with a password we may re-use", as opposed to a session-only login.

`SavePassword`, `ChosenCharacterName`, `ChosenCharacterWorld`, `ThumbnailUrl` are written by nothing in this file either: only `LastSuccessfulOtp` has a writer (`AccountManager`). The character/avatar trio is thus a dormant persistence slot in Core, whereas the Windows port (`lib/FFXIVQuickLauncher`) actively fills it.

## 4. `Password` — the accessor that is not a field

```csharp
[JsonIgnore]
public string Password
{
    get { if (UserName empty) return ""; return Program.Secrets.GetPassword(UserName) ?? ""; }
    set { if (value non-empty) Program.Secrets.SavePassword(UserName, value); }
}
```

Contract and consequences:

- **Not persisted here.** `[JsonIgnore]` means the account record never contains a password; storage is whatever `Program.Secrets` (`Program.cs.md`) was configured to (`FILE` / `KEYRING` / `NONE`).
- **Key is `UserName` alone, not `Id`.** Two accounts differing only in `UseOtp`/`UseSteamServiceAccount` share the same secret slot: they always read/write the same password. That is simultaneously the reason `AccountManager.AddAccount`'s password-diff branch is mostly inert.
- The getter **never throws for "missing"** — an absent secret yields `""`, which makes `existingAccount.Password != account.Password` comparisons treat "no secret" and "empty secret" as equal.
- The setter **silently ignores `null`/empty** and **never calls `DeletePassword`**: passwords cannot be cleared through this accessor. That is why `ISecretProvider.DeletePassword` has no caller anywhere in XIVLauncher.Core.
- Reading a password has side effects from the provider's point of view (the keychain provider logs at `Information`, the file provider touches disk); it is a plain accessor, so don't call it speculatively per frame.
- Every read is an IPC/disk round-trip; `MainPage.cs:101` and `MainPage.cs:822` each do a read, and `AccountManager.UpdatePassword` does another — a login apply costs several provider accesses.

## 5. XIVAPI lookup

| Member | Notes |
| --- | --- |
| `URL` | `private const string = "https://xivapi.com/"` — a `const` **compile-time constant**, not a field |
| `static async Task<JObject> GetCharacterSearch(string name, string world)` | `Get("character/search" + $"?name={name}&server={world}")` |
| `static async Task<dynamic> Get(string endpoint)` | `Program.HttpClient.GetStringAsync(URL + endpoint)`, then `JObject.Parse` |

- **No escaping** of `name`/`world`: names containing spaces, `&`, `=`, `/` or `%` corrupt the query string. XIVAPI character names do contain spaces, so this is a real defect, not a hypothetical.
- `Get` is public static and string-typed — a general XIVAPI shim usable from anywhere; `endpoint` is concatenated onto `URL` without validation.
- Both return *unparsed* JSON shapes (`JObject` / `dynamic`), i.e. the shape contract is XIVAPI's `character/search` response: `Results[]` with `Name.Value`, `Server.Value`, `Avatar.Value`. Nothing enforces that; a shape change turns into a runtime failure.
- `ConfigureAwait(false)` is used inside these `static async Task<...>`s.

### `FindCharacterThumb()`

```
if (ChosenCharacterName empty || ChosenCharacterWorld empty) return null;
try {
    var searchResponse = GetCharacterSearch(...).GetAwaiter().GetResult();   // blocking
    if (Results.Count > 1)
        for each r in Results:
            if (r.Name.Value.ToLower() == ChosenCharacterName.ToLower()
                && r.Server.Value.ToLower().Contains(ChosenCharacterWorld.ToLower()))
                    return r.Avatar.Value;
    return Results.Count > 0 ? (string) Results[0].Avatar : null;
} catch (Exception ex) { Log.Information(ex, "Couldn't download character search"); return null; }
```

- `dynamic searchResponse` (C# 13 deferred type) — member access is resolved at runtime, so a wrong shape is a *runtime* failure (caught by the surrounding `catch`).
- The multi-match disambiguation only runs when `Results.Count > 1`. With **exactly one** result the name is **not** validated, so a mistyped name can return the first match's avatar. The in-source comment explains the `Contains` on the server value: XIVAPI returns `Server.Value` as the world **display name (with the DC prefix)**, so a substring match on the user's world string is used instead of an exact compare.
- Matching is by lower-casing, not `equalsIgnoreCase`, and it allocates per comparison — cosmetic only.
- The fallback `(string)searchResponse.Results[0].Avatar` is a **node→`String` cast**, not the `.Value` accessor used in the loop. Behaviour is identical when the node *is* a string, so it is latent rather than visible.
- Failure mode is uniform: any exception (transport, parse, cast) is logged at `Information` and reported as `null`. A `null` return means "no thumb" and "lookup failed" are indistinguishable — and note the log level (`Information`) hides it below warning thresholds.

## 6. Invariants and hazards

- **Synchronous-on-async:** `GetCharacterSearch(...).GetAwaiter().GetResult()` blocks the caller; `FindCharacterThumb` is itself non-`async` by design, so converting it ripples into its callers.
- **Dead in this repo:** in `src/`, `ThumbnailUrl` appears only in `Accounts/XivAccount.cs:44` (field) and in `AccountSwitcherEntry.cs:16,22` (read), while `FindCharacterThumb` appears only at `Accounts/XivAccount.cs:53`. Nothing assigns the URL and nothing calls the lookup, so the whole path is unreachable from the Core UI — whereas the Windows port does use it (`lib/FFXIVQuickLauncher/src/XIVLauncher/AccountSwitcher.xaml.cs:57`, `var thumb = account.FindCharacterThumb();`). Spec it as "kept for a future Core profile-picture feature".
- **No HTTP status/content-type check** — `GetStringAsync` on a non-2xx or an HTML error page yields a parse failure that collapses to `null`.
- **`Program.HttpClient` and `Program.Secrets` are `static` globals** assigned at startup (`= null!`). Using a `XivAccount` before `Program` startup (unit tests, tooling) NPEs on both the `Password` accessor and the HTTP helpers — the class is not usable standalone despite looking self-contained.
- **No equality/identity override:** two instances with equal fields are distinct. `AccountManager.AddAccount`/`RemoveAccount` rely on `Id` comparison (add) and object identity (remove); a "same account" handle obtained twice is not interchangeable. See `AccountManager.cs.md` §4.
- **Case drift:** ctor-normalised vs. JSON-loaded names (`UserName { get; private set; }` has no normalisation in the setter) — see §2.
- **`Password` is named like a field, so it is easy to log by accident:** it is an accessor, so a stray `Log.Information($"{account.Password}")` writes a real password into the log file. The provider implementations already log *around* password traffic at `Information`; treat "no password values in logs" as a manual invariant to police.

## 7. Cross-references

- `AccountManager.cs.md` — the registry, the `[JsonIgnore]`-driven record shape, and the `Id`-based lookups.
- `ISecretProvider.cs.md` — the three implementations behind `Program.Secrets`.
- `docs/XIVLauncher.Core/Program.cs.md` — `Program.Secrets`, `Program.HttpClient`.
- `src/XIVLauncher.Core/Components/MainPage/AccountSwitcher.cs` — display suffixes `(Steam)`, `(OTP)`, `(Trial)` from `UseSteamServiceAccount`/`UseOtp`/`IsFreeTrial`.
- `src/XIVLauncher.Core/Components/MainPage/MainPage.cs` — construction and the `PersistAccount` refresh path.
