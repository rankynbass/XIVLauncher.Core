# UnixSteam.cs — Specification

**File:** `src/XIVLauncher.Common.Unix/UnixSteam.cs` · 83 lines · 2751 bytes · ASCII, LF, no BOM
**Namespace:** `XIVLauncher.Common.Unix` — **block form**: `namespace …` at `:8` with the body braces at
`:9`/`:82`; the only file in this directory that is not brace-less
**Declaration:** `public class UnixSteam : ISteam` (`:10`–`:81`)

**Role:** the unix implementation of the Steamworks abstraction. It is a thin wrapper over the same
`Steamworks` package the Windows build uses, plus one libc trick: `setenv` must write the app id into the
**process environment** before `SteamClient.Init`, because the C# `SetEnvironmentVariable` shim does not
touch the real `environ` on unix.

---

## 1. Member map (fork line ↔ interface line)

Interface: `ISteam.cs:6` (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common/PlatformAbstractions/`),
members `:8`–`:21`, nested enum `EFloatingGamepadTextInputMode` at `:23` (members `:25`–`:28`), nested event at `:31`.

| Member | Fork line | Steamworks call |
| --- | --- | --- |
| ctor `UnixSteam()` | `:12`–`:15` (body `:14`) | `SteamUtils.OnGamepadTextInputDismissed += …` |
| `void Initialize(uint appId)` | `:17`–`:27` | `setenv` ×2 (`:23`/`:24`) + `SteamClient.Init` (`:26`) |
| `bool IsValid { get; }` | `:29` | `SteamClient.IsValid` |
| `bool BLoggedOn { get; }` | `:31` | `SteamClient.IsLoggedOn` |
| `bool BOverlayNeedsPresent { get; }` | `:33` | `SteamUtils.DoesOverlayNeedPresent` |
| `void Shutdown()` | `:35`–`:38` | `SteamClient.Shutdown()` (`:37`) |
| `Task<byte[]?> GetAuthSessionTicketAsync()` | `:40`–`:44` | `await SteamUser.GetAuthSessionTicketAsync().ConfigureAwait(true)` (`:42`) → `ticket?.Data` (`:43`) |
| `bool IsAppInstalled(uint appId)` | `:46`–`:49` | `SteamApps.IsAppInstalled` (`:48`) |
| `string GetAppInstallDir(uint appId)` | `:51`–`:54` | `SteamApps.AppInstallDir` (`:53`) |
| `bool ShowGamepadTextInput(…)` | `:56`–`:59` | `SteamUtils.ShowGamepadTextInput` (`:58`) |
| `string GetEnteredGamepadText()` | `:61`–`:64` | `SteamUtils.GetEnteredGamepadText` (`:63`) |
| `bool ShowFloatingGamepadTextInput(EFloatingGamepadTextInputMode mode, …)` | `:66`–`:70` | `SteamUtils.ShowFloatingGamepadTextInput` (`:68`), `return true` (`:69`) |
| `bool IsRunningOnSteamDeck()` | `:72` | `SteamUtils.IsRunningOnSteamDeck` |
| `uint GetServerRealTime()` | `:74` | `(uint)((DateTimeOffset)SteamUtils.SteamServerTime).ToUnixTimeSeconds()` |
| `void ActivateGameOverlayToWebPage(url, modal = false)` | `:76`–`:79` | `SteamFriends.OpenWebOverlay(url, modal)` (`:78`) |
| `event Action<bool> OnGamepadTextInputDismissed` | `:81` | (declared here, fired by the ctor forwarder) |

Coverage is complete: every interface member (`ISteam.cs:8`–`:31`) has an implementation, so there is no
abstract-member trap.

## 2. Constructor (`:12`–`:15`)

```
SteamUtils.OnGamepadTextInputDismissed += b => OnGamepadTextInputDismissed?.Invoke(b);
```

* The Steamworks binding exposes the gamepad-dismissal notification on the **process-wide** `SteamUtils`
  singleton; `ISteam` wants an instance-level event (`ISteam.cs:31`). This line is the bridge: Steamworks
  fires, the lambda re-fires this instance's event.
* Consumers subscribe to **this class's** event: `Input.cs:60`
  (`Program.Steam.OnGamepadTextInputDismissed += this.SteamOnOnGamepadTextInputDismissed;`) and `OtpEntryPage.cs:26`
  (guarded by `Program.Steam is not null` at `:25`); the handlers are `Input.cs:65`/`Input.cs:68`
  (`Program.Steam!.GetEnteredGamepadText()` — note the **forced non-null** `!` with no `IsValid` check) and
  `OtpEntryPage.cs:29`/`OtpEntryPage.cs:32`.
* Registration happens once per instance; the fork constructs exactly one (`Program.cs:230`), so today
  there is one forwarder. A second construction would add a second lambda to the singleton event (double
  notification) and keep the previous instance alive.
* `OnGamepadTextInputDismissed?.Invoke(b)` is null-safe: an `event` member with no subscribers is null.

## 3. `Initialize` (`:17`–`:27`) — the libc workaround

| Line | Statement |
| --- | --- |
| `:19` | comment: `// workaround because SetEnvironmentVariable doesn't actually touch the process environment on unix` |
| `:20` | `[System.Runtime.InteropServices.DllImport("c")]` — method-scope dllimport attribute |
| `:21` | `static extern int setenv(string name, string value, int overwrite);` — libc `setenv` against mono's built-in `"c"` import library |
| `:23` | `setenv("SteamAppId", appId.ToString(), 1);` |
| `:24` | `setenv("SteamGameId", appId.ToString(), 1);` |
| `:26` | `SteamClient.Init(appId);` |

Notes:

* Ordering is load-bearing: Steamworks reads the two variables during `Init`, so `:23`/`:24` must precede
  `:26`.
* `overwrite = 1` clobbers any pre-existing value.
* Because `setenv` mutates the **real process environment**, both variables persist for the launcher's
  lifetime and are inherited by every child — including every wine run, whose own environment
  `CompatibilityTools` layers on top (see [Compatibility/CompatibilityTools.cs.md](Compatibility/CompatibilityTools.cs.md)).
* The `int` return (0 / −1 with `errno == ENOMEM`) is discarded: a failed `setenv` is silent, and `Init`
  then decides whether Steam initialises; the failure surfaces at the caller's catch (§4).

## 4. Construction and `Initialize` call path (`Program.cs`)

| Line | Content |
| --- | --- |
| `:50` | `public static ISteam? Steam { get; private set; }` — **nullable**, so every consumer null-checks |
| `:212` | `apps.Add(CoreEnvironmentSettings.AltAppID, "XL_APPID")` — the optional custom app id, added **first** |
| `:215`/`:219` | `apps.Add(STEAM_APP_ID, "FFXIV Retail")` / `apps.Add(STEAM_APP_ID_FT, "FFXIV Free Trial")` |
| `:147`/`:148` | `STEAM_APP_ID = 39210`, `STEAM_APP_ID_FT = 312060` |
| `:221`/`:253` | outer `try` / `catch` (`Log.Error(ex, "Steam couldn't load")` `:255`) |
| `:223` | `switch (Environment.OSVersion.Platform)` — `Win32NT` → `new WindowsSteam()` (`:226`), `Unix` → `new UnixSteam()` (`:230`), `default:` → `throw new PlatformNotSupportedException()` (`:234`) |
| `:236` | `if (Config.IsIgnoringSteam != true \|\| CoreEnvironmentSettings.IsSteamCompatTool)` — the gate around initialization |
| `:238` | `foreach (var app in apps)` |
| `:240`/`:246` | inner `try`/`catch` (`Log.Error(ex, "Failed to initialize Steam Steam entry …")` `:248`) |
| `:242` | `Steam.Initialize(app.Key);` |
| `:244` | `break;` — first successful `Initialize` wins |

Consequence: `Steam` can be **non-null but uninitialised** — construction succeeded but every `Initialize` threw
(caught at `:246`) or the gate at `:236` skipped the loop. Consumers that only null-check (see §5) can then
call into an uninitialised Steamworks context.

## 5. Live consumers

| Site | Member | Note |
| --- | --- | --- |
| `Program.cs:230` | construction | unix branch |
| `Program.cs:242` | `Initialize` | see §4 |
| `LauncherApp.cs:109` | view | `public ISteam? Steam => Program.Steam;` |
| `LauncherApp.cs:132` | injection | `new Launcher(Program.Steam, …)` → lib `Launcher.cs:108` null-checks, lib `Launcher.cs:129` reads **`!this.Steam.BLoggedOn`** |
| `AppUtil.cs:63`/`:65` | `IsValid` / `ActivateGameOverlayToWebPage(url)` | Deck-only web open through the Steam overlay |
| `Input.cs:58`/`:60` | null-check / event subscribe | text-input widget |
| `Input.cs:68` | `GetEnteredGamepadText` | **`Program.Steam!`** — forces non-null, no `IsValid` check |
| `Input.cs:109`/`:111` | `IsValid` / `ShowGamepadTextInput` | passes the ImGui `Password` flag |
| `OtpEntryPage.cs:26` | event subscribe | |
| `OtpEntryPage.cs:31`/`:32` | null-check / `GetEnteredGamepadText` | |
| `OtpEntryPage.cs:45`/`:47` | `IsValid` + `IsSteamDeckHardware` gate / `ShowGamepadTextInput(false, false, Strings.EnterYourOTP, 6, string.Empty)` | OTP entry |
| `MainPage.cs:804`/`:806` | `App.Steam?.IsValid == true` → `Shutdown()` | exit path |
| `FtsPage.cs:40` | `Program.IsSteamDeckHardware == null \|\| !Program.Steam.IsValid` | FTS prompt gate |
| lib `Ticket.cs:30`/`:35` | `GetAuthSessionTicketAsync` / `GetServerRealTime` | auth-ticket path |

## 6. Members the fork never calls

Verified by grep over `src/` and `lib/` (excluding the implementation files and `WindowsSteam.cs`):
`BOverlayNeedsPresent` (`:33`), `IsAppInstalled` (`:46`), `GetAppInstallDir` (`:51`),
`ShowFloatingGamepadTextInput` (`:66`), `IsRunningOnSteamDeck` (`:72`).

* **Deck detection instead**: the fork uses `Program.IsSteamDeckHardware` (from `CoreEnvironmentSettings`, see
  `docs/XIVLauncher.Core/CoreEnvironmentSettings.cs.md`) to gate the same UI — `Input.cs:109` (`HasSteamDeckInput`),
  `AppUtil.cs:63`, `FtsPage.cs:40`, `OtpEntryPage.cs:45`. `IsRunningOnSteamDeck` (`:72`) is the obvious
  replacement, but the flag also covers the compat-tool / non-Steam launch cases (`CoreEnvironmentSettings.IsSteamCompatTool`,
  `Program.cs:236`) that Steamworks cannot answer.
* **`BOverlayNeedsPresent` (`:33`) is more than dead code**: upstream's Windows app uses it to keep the
  Steam overlay alive while gamepad text input is up (the classic "re-present the overlay" dance). Nothing
  on the fork's UI path re-presents the overlay, so gamepad OTP input (`OtpEntryPage.cs:47`) relies on the
  overlay still being up by luck. Cross-check `docs/XIVLauncher.Core/Components/Common/Input.cs.md` and
  `docs/XIVLauncher.Core/Components/OtpEntryPage.cs.md` for the fork's own workaround before "fixing" this.

## 7. Coercions and gotchas

* **`ShowFloatingGamepadTextInput` (`:66`–`:70`)** casts the interface's nested enum
  `ISteam.EFloatingGamepadTextInputMode` (`ISteam.cs:23`, members `EnterDismisses`/`UserDismisses`/`Email`/`Numeric` at
  `:25`–`:28`) to Steamworks' own `TextInputMode` with a bare `(TextInputMode)mode` at `:68` — **ordinal
  coupling**, no mapping table. Reordering either enum silently changes meaning. The method then returns
  `true` unconditionally (`:69`): "we called it", not "Steam accepted it".
* **`GetServerRealTime` (`:74`)**: `DateTime` → `DateTimeOffset` → `ToUnixTimeSeconds()` → `uint`, matching
  the interface's `uint` (`ISteam.cs:20`). Used as the auth-ticket nonce at lib `Ticket.cs:35`.
* **`GetAuthSessionTicketAsync` (`:40`–`:44`)**: interface says `Task<byte[]?>` (`ISteam.cs:13`),
  implementation is `async Task<byte[]?>` with `ticket?.Data` (`:43`) propagating `null` when Steamworks
  has no ticket; consumer lib `Ticket.cs:30` awaits with `.ConfigureAwait(true)` and
  lib `Launcher.cs:533`/`:550` later null-check the ticket.
* **`ShowGamepadTextInput` (`:56`–`:59`)** maps the two booleans to
  `GamepadTextInputMode.Password/Normal` and `GamepadTextInputLineMode.MultipleLines/SingleLine` (`:58`),
  and keeps the interface's default `existingText = ""` (`ISteam.cs:16`).
* **`IsValid`/`BLoggedOn`/`BOverlayNeedsPresent` (`:29`/`:31`/`:33`)** are one-liner accessors into
  `SteamClient`/`SteamUtils`: they are safe to call before `Init` only insofar as the Steamworks binding
  tolerates it — see §4's "non-null but uninitialised" note.

## 8. Upstream delta

`lib/FFXIVQuickLauncher/src/XIVLauncher.Common.Unix/UnixSteam.cs` differs from the fork in exactly one statement
(BOM and blank lines ignored):

```
-  public event Action<bool>? OnGamepadTextInputDismissed;   // upstream
+  public event Action<bool>   OnGamepadTextInputDismissed;  // fork (:81)
```

i.e. the nullable-event spelling was dropped to match the interface's non-nullable event
(`ISteam.cs:31`). Everything else — the `setenv` workaround, the ordinal cast, the `return true` stub — is
upstream-identical.

## 9. Referenced by / referenced

* Referenced by: `Program.cs:230` (unix branch; Windows branch sibling
  `lib/.../XIVLauncher.Common.Windows/WindowsSteam.cs`).
* References: `ISteam` (`lib/.../PlatformAbstractions/ISteam.cs:6`–`:31`), the Steamworks package
  (`SteamClient`, `SteamUtils`, `SteamUser`, `SteamApps`, `SteamFriends`,
  `GamepadTextInputMode`, `GamepadTextInputLineMode`, `TextInputMode`), libc `setenv` via
  `DllImport("c")` (`:20`/`:21`), `System.Threading.Tasks` (`:2`).
* Cross-tree specs: `docs/XIVLauncher.Core/Program.cs.md` (§§ 4 table above),
  `docs/XIVLauncher.Core/LauncherApp.cs.md`, `docs/XIVLauncher.Core/AppUtil.cs.md`,
  `docs/XIVLauncher.Core/Components/Common/Input.cs.md`, `docs/XIVLauncher.Core/Components/OtpEntryPage.cs.md`,
  `docs/XIVLauncher.Core/Components/FtsPage.cs.md`, `docs/XIVLauncher.Core/CoreEnvironmentSettings.cs.md`,
  [Compatibility/CompatibilityTools.cs.md](Compatibility/CompatibilityTools.cs.md) (child-env inheritance).
