# LauncherClientConfig.cs — Specification

**File:** `src/XIVLauncher.Core/Net/LauncherClientConfig.cs` · 37 lines · 1072 bytes (LF, no BOM; line 1 is blank)
**Namespace:** `XIVLauncher.Core.Net` · **Declaration:** `public readonly struct LauncherClientConfig`
**Role:** The fork's single eager network read at startup — fetch Kamori's `GetLauncherClientConfig` JSON into an immutable three-field record, then hand `../LauncherApp.cs.md` the Frontier URL template and the killswitch boot-version cutoff, with a hard-coded Frontier fallback if the fetch fails

---

## 1. Purpose

`LauncherClientConfig` is the transport-and-defaults shim in front of one JSON endpoint. `Program.Main` needs two values **before** it can construct `LauncherApp` (`../Program.cs.md` §6 `unsafe` block sub-step 9 at `:317`, then sub-step 10 at `:318`): the Frontier URL template (required, non-null, by `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Game/Launcher.cs:38`) and the `cutOffBootver` killswitch threshold. Upstream's Windows launcher gets the same pair from the patch lease (`lib/FFXIVQuickLauncher/src/XIVLauncher/Windows/ViewModel/MainWindowViewModel.cs:76` `Updates.UpdateLease?.FrontierUrl`) and only calls the endpoint when updates are disabled (`:78`–`:80`, and it throws if the result is `null` at `:83`–`:84`). The fork has no lease yet at that point (the lease is acquired later, during patch acquisition — `../Components/MainPage/MainPage.cs.md` §patch flow), so this file always asks Kamori and always has a usable URL, which is why it exists at all.

Usings: `System.Net.Http.Json` (`:2` — the JSON flavour of the shared HTTP client, the namespace through which `GetFromJsonAsync` resolves; sibling in the fork is `System.Text.Json` at `../Accounts/Secrets/Providers/FileSecretProvider.cs:1`, whose `JsonSerializer` is likewise used unqualified) and `Serilog` (`:4`, supplying the global `Log`). `Program` is used unqualified at `:25` and resolves through the parent namespace (`XIVLauncher.Core`), which is why `Program.cs:28` needs only `using XIVLauncher.Core.Net;` — the only such import in the fork; nothing else references this namespace.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `LAUNCHER_CONFIG_URL` | `private const string` (`:14`) | `https://kamori.goats.dev/Launcher/GetLauncherClientConfig`; compile-time constant, the fork's only reference to Kamori's launcher-config endpoint |
| `FRONTIER_FALLBACK` | `private const string` (`:15`) | `https://launcher.finalfantasyxiv.com/v740/index.html?rc_lang={0}&time={1}` — a **`string.Format` template**, not a URL; two positional holes |
| `frontierUrl` | `public string { get; init; }` (`:17`) | non-nullable type, **not** non-null in practice (§6) |
| `cutOffBootver` | `public string? { get; init; }` (`:18`) | nullable: `null`/empty ⇒ killswitch skipped |
| `flags` | `public uint { get; init; }` (`:19`) | modelled, **never read** anywhere in fork or lib (§6) |
| `GetAsync()` | `public static async Task<LauncherClientConfig>` (`:21`–`:35`) | the only member anyone calls; `static` on a record type, so the record has no factory beyond the deserialiser or `new LauncherClientConfig { … }` |

`const` fields are a fork-wide spelling (`Program.cs:39`, `Program.cs:147`–`:148`, `../Accounts/Secrets/Providers/KeychainSecretProvider.cs:9`–`:10`, `FontManager.cs:9`–`:11`) — initialise-once, and usable inside a `static` member such as `GetAsync()`. The accessor form with `{ get; init; }` (also at `../Components/Page.cs:9`, `../Components/SettingsPage/SettingsEntry{T}.cs:17`–`:21`) is this style's write-once field: an `init` setter assignable only via designated initialisers in `new T { field = value }`, with no `set;`, so all three fields are frozen after construction. `readonly struct` additionally makes the record itself immutable.

## 3. Record shape vs. payload

JSON keys bind to the fields **by name**, case-insensitively: upstream's private response class spells it PascalCase (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Util/DebugHelpers.cs:91`, `public string FrontierUrl { get; set; } = string.Empty;`) against the byte-identical endpoint literal (`DebugHelpers.cs:79`, `const string API_ENDPOINT = "https://kamori.goats.dev/Launcher/GetLauncherClientConfig"`), and works, while the fork spells `frontierUrl` lowercase. Renaming a field therefore silently yields the type's zero value (`""`, `null`, `0`) rather than a compile error: the mapping is stringly-keyed in both directions, and there is no `[JsonProperty]`-style hook here (`../Style/StyleModel.cs.md` §3 for the contrast).

The record models **three** members, and that trio is exactly the shape upstream's boot lease uses: `lib/FFXIVQuickLauncher/src/XIVLauncher/Updates.cs:73` `public string? CutOffBootver`, `:75` `public string FrontierUrl`, `:77` `public LeaseFeatureFlags Flags` (enum at `:60`, `GlobalDisableDalamud = 1` at `:63`). So this file is a lease-shaped payload arriving over HTTP instead of from the patch repo — which is also why `flags` is dead weight: upstream honours that flag at `MainWindowViewModel.cs:689` via `Updates.HaveFeatureFlag(...)` (`Updates.cs:228` reads `UpdateLease.Flags`), and nothing in the fork consults it, so Kamori cannot globally disable Dalamud through this path.

## 4. `GetAsync()` contract

```csharp
try
    return await Program.HttpClient.GetFromJsonAsync<LauncherClientConfig>(LAUNCHER_CONFIG_URL).ConfigureAwait(false);  // :25
catch (Exception ex)                                                                                                      // :27
    Log.Error(ex, "Could not obtain LauncherClientConfig");                                                               // :29
    return new LauncherClientConfig() { frontierUrl = FRONTIER_FALLBACK };                                                // :30-:33
```

* **Happy path:** reuses the fork's one shared client (`Program.cs:55`, `../Program.cs.md` §11 "HTTP client contract": happy-eyeballs connect callback, `AutomaticDecompression = All`, `AllowAutoRedirect = true`, `MaxAutomaticRedirections = 4`) — so this GET is decompressed, redirected up to 4 hops, cookie-less, and carries **no** custom headers or user-agent of its own. No caching, no `If-None-Match`, no revalidation: one GET per process lifetime.
* `ConfigureAwait(false)` matches the fork's house style; irrelevant to the caller since `Program.cs:317` blocks with `.GetAwaiter().GetResult()` anyway (the "async without `await`" pair: `Task` started, then synchronously waited).
* **`catch (Exception ex)` is total**: parse errors, transport errors, DNS failures, a 4xx/5xx body that isn't JSON, a `null` body, and any deserialiser strictness complaint all land in the same branch. `GetAsync` therefore never rejects — the returned record is always a value, so `Program.cs:317` cannot fail because of the fetch itself.
* The failure path leaves `cutOffBootver = null` and `flags = 0` (init-setter defaults, never assigned) and `frontierUrl = FRONTIER_FALLBACK`. The only trace is one Serilog `Log.Error` line into `launcher.log` — no UI signal, so a user cannot tell the fallback is in play except by the logged line (and by the log's presence in a bug-report pack, `../Support/PackGenerator.cs.md` §3 candidate list).

## 5. Where the three fields land

| Field | Consumer | Effect |
| --- | --- | --- |
| `frontierUrl` | `../LauncherApp.cs.md` §4 item 2 (`LauncherApp.cs:132`) → `Launcher(ISteam, IUniqueIdCache, frontierUrl, acceptLanguage)` (`Launcher.cs:33`) → `frontierUrlTemplate` (`:30`) | consumed by `GenerateFrontierReferer` (`Launcher.cs:698`, `string.Format(this.frontierUrlTemplate, langCode, formattedTime)` at `:703`) as the **`Referer` header** on OAuth (`:470`, `GetOauthTop`) and `DownloadAsLauncher` (`:691`, `:675`) requests |
| `cutOffBootver` | `../LauncherApp.cs.md` §4 item 4 (`LauncherApp.cs:141` guard, `:144` `SeVersion.Parse(cutOffBootver)`, `:146` compare against `SeVersion.Parse(Repository.Boot.GetVer(Config.GamePath))` at `:143`) | killswitch modal + `Environment.Exit(0)` (`:148-:151`, `return` at `:152`) when the game's boot version exceeds the cutoff |
| `flags` | — | no reader in the repo (`grep` over `src` and `lib`: the declaration at `:19` is the only hit) |

The `{0}`/`{1}` holes of the Frontier template are filled **only** inside the lib, and only in that order: `{0}` = `language.GetLangCode().Replace("-", "_")`, `{1}` = `GetLauncherFormattedTimeLong()` → `DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm")` (`Launcher.cs:707`/`:709`/`:711` — `…Time()`, `…TimeLong()`, `…TimeLongRounded()` — and `GenerateFrontierReferer` uses the long, unrounded one). The same host is hardcoded as the `Origin` header at `Launcher.cs:689`, inside `DownloadAsLauncher`.

## 6. Invariants and hazards

* **The template's arg count is a load-bearing contract.** The fork supplies a 2-hole template; anything Kamori returns is `string.Format`ed with exactly two args at **request** time, not fetch time. A returned URL with a third placeholder (`{2}`), or with unescaped `{`/`}`, therefore fails later, inside login/download (`Index out of bound` / `TooManyArguments` from `string.Format`), i.e. as an `OauthLogin`/`DownloadAsLauncher` exception rather than as a config error. Nothing validates hole count here.
* **`v740` is a frozen legacy path**: the fallback pins a Frontier index URL from an older client build. It is only ever used as a `Referer` string (the lib never GETs it), so drift is invisible and harmless — but that also means the fallback is *not* a "latest" URL; if SE stops accepting that referer, the OAuth flow fails in a way that looks like an auth error rather than a config problem.
* **The fallback only covers exceptions, not `null`.** `frontierUrl` is typed non-nullable but is assigned by the deserialiser, so a payload with `"frontierUrl": null` (or absent, with the reader leaving the field un-initialised) yields a `null` template that reaches `Launcher.cs:38` (`frontierUrl ?? throw new Exception("Frontier URL template is null, this is now required")`) — and that throw has **no handler**: `Program.cs:318` sits inside the `unsafe` block (`:269`), whose only protection is per-SDL-step null guards with `Environment.Exit(1)` (`:273-:306`), and the nearest `try` is at `:221`/`:253` (Steam init) or `:346`. A single JSON `null` therefore ends the process as an unhandled exception — no error page, no modal, no exit code contract — whereas a mere network failure would have started the launcher on the fallback. Any defensive read of this endpoint belongs on the **field**, not on the exception path.
* **Killswitch fails open.** `cutOffBootver == null` disables the cutoff check (`LauncherApp.cs:141` `!string.IsNullOrEmpty(...)`), so every fetch failure (and every offline session) silently removes the boot-version gate. Users who want it honoured can only opt out the other way — `EnvironmentSettings.IsNoKillswitch` is `CheckEnvBool("XL_NO_KILLSWITCH")` (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common/EnvironmentSettings.cs:8`), i.e. the env var *disables* it; there is no "require config" knob.
* **Blocking with no timeout.** `Program.cs:317` converts the task to a synchronous wait, and the window is still `Hidden` at that point (shown only at `unsafe` sub-step 11, `SDL.ShowWindow`, per `../Program.cs.md` §6). The client at `Program.cs:55` sets no request/connection timeout and there is no retry, so a hung DNS/TLS resolution keeps the launcher invisible for the full platform default (and on a slow-but-working network it delays the killswitch check that happens at `:318`). Startup is therefore network-critical in a way the UI never communicates.
* **`flags` is dead, `uint` is its own risk.** Unsigned 32-bit from JSON: a payload with a value > `UINT32_MAX` or a non-numeric `flags` fails the parse → whole record → fallback (i.e. a malformed `flags` costs you the real `frontierUrl` and the killswitch). Treat the whole record as "all-or-fallback": partial success is impossible. Nothing in fork or lib reads the field, so the upstream lease semantic attached to the same name (`Updates.cs:63` `GlobalDisableDalamud`) is inert through this path — a wire-compatibility stub, not a feature.
* **No re-fetch mechanism.** The record is read once, at construction; `LauncherApp` keeps `frontierUrl` forever (`Launcher.cs:30` `private readonly string frontierUrlTemplate`). A mid-session Kamori change (or a new killswitch cutoff) requires restarting the process — the launcher is process-per-session (`../Program.cs.md` §6 steps 11-13, teardown + `Environment.Exit`), so restart is cheap — but it also means the launcher cannot re-arm a killswitch after it started.
* `Program.HttpClient` must exist before this runs; it does, because the field is initialised at class init (`Program.cs:55`, before `Main`'s body — see `../Program.cs.md` §5 "Initialisation and ordering rules"). A future `Net/` member that runs earlier would need to construct its own client.
* Extension point: the record is the payload model, so adding a field (e.g. a new killswitch member) is a one-line change here **plus** a consumer in `../LauncherApp.cs.md` §4; `GetAsync`'s signature and the `Program.cs:317` call site stay untouched, and the fallback construction stays valid because new fields keep their init defaults.

## 7. Referenced / referenced-by

* References: `../Program.cs.md` (`Program.HttpClient` §11, call site §6 `unsafe` sub-step 9 at `Program.cs:317`), `Serilog` (`Log.Error`), `System.Net.Http.Json` (`GetFromJsonAsync`), `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Game/Launcher.cs` (template consumer, §5's contract).
* Referenced by: `Program.cs:317` (`LauncherClientConfig.GetAsync().GetAwaiter().GetResult()`) and `Program.cs:318` (both fields forwarded to `LauncherApp`) — the only call sites in the fork; `Program.cs:28` is the only `using` of this namespace.
* Upstream counterparts: `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Util/DebugHelpers.cs:76`–`:93` (`GetFrontierUrlForDebugAsync`, same endpoint, no fallback, single-field response class) and `lib/FFXIVQuickLauncher/src/XIVLauncher/Windows/ViewModel/MainWindowViewModel.cs:76` (lease-sourced `frontierUrl`), `:78`–`:80` (endpoint used only when `AppUtil.IsDisableUpdates`) and `:83`–`:84` (`throw new InvalidOperationException("Could not determine Frontier URL for launcher.")` on `null`).
* Sibling specs: none in this directory (`Net/` has exactly one `.cs` file); conceptually paired with `../LauncherApp.cs.md` §4 (the ctor that consumes both fields) and `../Program.cs.md` §6/§11 (fetch order and client).
