# NewsFrame.cs — Specification

**File:** `src/XIVLauncher.Core/Components/MainPage/NewsFrame.cs` · 131 lines · 4239 bytes
**Namespace:** `XIVLauncher.Core.Components.MainPage` · **Declaration:** `public class NewsFrame : Component`
**Role:** The MainPage's news panel — a rotating banner image plus the server's headlines, fetched over HTTP at startup and on language change

---

## 1. Purpose

`NewsFrame` is the only component here that talks to the game's CDN. It holds the fetched state (`headlines`, `banners`, `bannerList`) and the rotation cursor (`currentBanner`), and it draws them inside its own child window. `MainPage.cs.md` (§Draw: `this.newsFrame.Draw()` at `MainPage.cs:79`, before `loginFrame.Draw()` at `:83`) only places it; `MainPage.cs:90` (`public void ReloadNews() => this.newsFrame.ReloadNews();`) is a forwarder for the one external refresh trigger, `LauncherApp.cs:59` in the `State` setter (`if (this.state == LauncherState.Settings) this.mainPage.ReloadNews();` — leaving Settings re-fetches because the client language may have changed).

Data shapes come from the launcher library: `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Game/Headlines.cs` (`Headlines` = `News[] News` + `News[] Topics` + `News[] Pinned`; `Banner` = `Uri LsbBanner` + `Uri Link` + `int? OrderPriority/FixOrder`; `News` = `Date/Title/Url/Id/Tag`). Textures come from `TextureWrap.cs.md` (§load).

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `BANNER_TIME` | `private const int = 8000` | rotation interval, ms — §Timer |
| `app` | `private readonly LauncherApp` | ctor arg — **`LauncherApp`, not `MainPage`** (§3) |
| `bannerTimer` | `private readonly Timer` | `XIVLauncher.Common.Timer` (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Timer.cs`), `Change(0, BANNER_TIME)` |
| `headlines` | `private Headlines?` | nullable field, set only inside the async task |
| `banners` | `private TextureWrap[] = []` | one texture per banner, filled by index |
| `bannerList` | `private IReadOnlyList<Banner> = new List<Banner>()` | the metadata half, index-parallel to `banners` (§4) |
| `currentBanner` | `private int = 0` | rotation cursor |
| `newsLoaded` | `private bool = false` | the "ready" gate (§4) |
| ctor | `NewsFrame(LauncherApp app)` | calls `ReloadNews()` at construction, then builds the timer |
| `TimerElapsed(object?)` | `private` | `currentBanner = (currentBanner + 1) % banners.Length` (§5: div-by-zero hazard) |
| `ReloadNews()` | `public void` | fire-and-forget async fetch (§6) |
| `GetSize()` | `private Vector2` | child-window size (§7) |
| `Draw()` | `override` | child window `"###newsFrame"`; banner + news entries; `base.Draw()` last |

Usings: `System.Numerics`, `Hexa.NET.ImGui`, `XIVLauncher.Common`, `XIVLauncher.Common.Game` (`Headlines`, `News`, `Banner`), `XIVLauncher.Core.Resources.Localization` (`Strings`); `AppUtil.cs.md`'s `OpenBrowser` is reached through the `AppUtil` static (imported via `XIVLauncher.Core…` package-implicit — see `MainPage.cs.md` §imports for the file's import block; NewsFrame relies on the same-namespace resolution).

## 3. Ownership shape — `NewsFrame(LauncherApp app)` vs `LoginFrame(MainPage)`

The two MainPage children disagree on what they hold: `NewsFrame` takes the *app* (it needs `app.Launcher` for the CDN calls and `app.Settings.ClientLanguage` for the language), while `MainPage/LoginFrame.cs.md` takes the *owning MainPage* (it needs `mainPage.AccountSwitcher`). Consequences for editing either:

- `NewsFrame` cannot see MainPage state (`IsLoggingIn`, `SwitchAccount`, …) and must not be refactored to take `MainPage` without also routing `App.Launcher`/`App.Settings` to it — the field name `app` and the ctor argument `new NewsFrame(app)` (`MainPage.cs:40`) are the pair.
- `MainPage.cs:31` declares it `private readonly NewsFrame newsFrame;`, so the panel is not reachable from outside MainPage; anything that must trigger a refresh goes through `MainPage.cs:90`'s `ReloadNews()` forwarder (that is exactly what `LauncherApp.cs:59` does).

## 4. The fetch lifecycle and its torn-state windows

```csharp
public void ReloadNews() => Task.Run(async () =>
{
    this.newsLoaded = false;                                    // runs synchronously, before the first await
    await Headlines.GetWorlds(app.Launcher, lang);              // result DISCARDED
    bannerList = await Headlines.GetBanners(app.Launcher, lang);
    await Headlines.GetMessage(app.Launcher, lang);             // result DISCARDED
    this.headlines = await Headlines.GetNews(app.Launcher, lang);
    this.banners = new TextureWrap[bannerList.Count];
    for (i …) this.banners[i] = TextureWrap.Load(await HttpClient.GetByteArrayAsync(bannerList[i].LsbBanner));
    this.newsLoaded = true;
});
```

- **The task is fire-and-forget**: `Task.Run`'s handle is dropped, `ReloadNews()` returns `void`, nothing awaits or catches. An HTTP failure leaves `newsLoaded == false` forever → the panel permanently shows `Strings.NewsLoading` (`Draw`'s else branch) and the app keeps working. Do not "improve" by letting it throw: the ctor path (`MainPage.cs:40`) would then kill startup, and `LauncherApp.cs:59`'s call sits inside the `State` setter, where an escaping exception would break every page transition.
- **Two of the four fetches are discarded** (`GetWorlds`, `GetMessage`). They are not decorative: they exercise the CDN endpoints and their results would be `ReadOnlyCollection<Banner>` values that this panel never renders. Removing them is a behaviour change only for the network side (fewer requests, possible server-side caching/warm-up), so keep them unless the library documents them as side-effect-free. `GetBanners` and `GetNews` are the two whose values matter.
- **Language is read four times** as `app.Settings.ClientLanguage ?? ClientLanguage.English` — the same expression per call. If `Settings` is mutated concurrently (SettingsPage writes `ClientLanguage`), the four fetches may use different languages: a real, currently-tolerated inconsistency (`LauncherApp.cs:59` deliberately refreshes on Settings exit to make a *single* consistent pass happen afterwards). If you refactor, hoist it to one local — that is the intended fix, not a style preference.
- **`newsLoaded` is reset inside the async lambda**, but C# `Task.Run(async …)` runs synchronously up to the first `await`, so the reset is effectively immediate while `= true` happens only after all awaits. Net effect: the panel flips to `Strings.NewsLoading` the instant a refresh starts, and becomes ready only when *every* texture is downloaded.
- **Torn state during a refresh:** `banners` and `bannerList` are reassigned while a previous frame's `Draw()` may be reading them, and the array is allocated (`new TextureWrap[bannerList.Count]`) *before* its elements are filled. `Draw()` reads `banners[currentBanner]` and immediately dereferences `banner.ImGuiHandle`/`banner.Size` — during the download loop that slot can be `null`, and a `null` `TextureWrap` (see `TextureWrap.cs.md` §fields) throws an NRE from inside the ImGui frame. This is a latent bug today (it needs a refresh to coincide with a frame boundary), so the shape to preserve when fixing is "never expose a partially-filled `banners` array": build a local array, fill it completely, then assign the field. The same lockstep rule must survive the fix — `banners[i]` must always correspond to `bannerList[i]` (§4's click-through depends on it).

## 5. Banner rotation — `XIVLauncher.Common.Timer`

```csharp
this.bannerTimer = new Timer(TimerElapsed);
bannerTimer.Change(0, BANNER_TIME);          // interval 0, count 8000 ms
```

- **The two `Change` arguments are (interval, count)** in this library's `Timer` (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Timer.cs`): `0` interval + `8000` count = "fire every 8 s, forever". Passing `(8000, 0)` or swapping them is a silent behavioural change (fires once at start, or never) — this pairing convention is the same one `MainPage.cs.md` §patch/repair progress loops rely on (`timer.Change(0, 250)`).
- **`TimerElapsed` returns early while `!newsLoaded`**, which is what protects `(currentBanner + 1) % banners.Length` before any fetch completed (`banners` starts as `[]`, so `% 0` would throw `ArithmeticException` inside the timer callback). But the guard is *not* sufficient for a completed-but-empty fetch: if the CDN returns zero banners, `newsLoaded == true` and `banners.Length == 0` → `% 0` throws every 8 s. The shape that fixes both is an explicit `if (banners.Length == 0) return;` — keep that check if you touch the callback, since it is the only reason the modulo is safe.
- **`TimerElapsed`'s `(object? state)` signature** is the library's callback shape (`TimerCallback`); the payload is unused. It is referenced as a method value in the ctor, so renaming it needs both sites.
- **`currentBanner` is unbounded-safe only via the modulo**, and is never reset on `ReloadNews()`: after a refresh the cursor keeps its index into a possibly *shorter* new banner list — if the previous list had 5 banners and the new one has 2, `Draw()` reads `banners[currentBanner]` with `currentBanner` up to 4 → out-of-bounds. Concretely: the rotation cursor must be re-clamped when the list is replaced (or clamped in `Draw()`), which today only survives because the server returns a stable count. This is the same index-parallel invariant as §4.
- **`bannerTimer` is `private readonly` and never `Dispose()`d** — the object lives for the process lifetime (contrast `MainPage.cs.md`'s repair timer, which is explicitly disposed after its wait). A `Timer` here is a long-lived process-wide ticker; do not "clean up" by disposing it in a destructor-ish place, `Draw()` runs every frame.

## 6. Draw structure and the `##` id trick

```csharp
if (ImGui.BeginChild("###newsFrame", this.GetSize()))
{
    PushStyleVar(WindowPadding, 32)
    if (newsLoaded) { Image(banners[currentBanner].ImGuiHandle, banner.Size)
                      if (IsItemClicked(Left)) AppUtil.OpenBrowser(bannerList[currentBanner].Link.ToString());
                      Dummy(15)
                      ShowNewsEntry(headlines.News) … Spacing() … ShowNewsEntry(headlines.Topics) }
    else ImGui.Text(Strings.NewsLoading);
    PopStyleVar()
}
ImGui.EndChild();
base.Draw();
```

- **The banner click is bound by adjacency**: `ImGui.IsItemClicked(ImGuiMouseButton.Left)` tests the *last added item*, i.e. the `Image` above it. Any item added between them (a `Dummy`, a caption) retargets the click silently. Keep the pair adjacent.
- **The URL is taken from `bannerList[currentBanner]`, the image from `banners[currentBanner]`** — two arrays indexed by the same cursor in the same frame. Because `ReloadNews()` can swap both mid-frame, the pair can mix an old image with a new link (or vice-versa). Acceptable today; not acceptable if you ever restructure (§4's "build local, then assign" fix also fixes this).
- **`News` rows render through a local function** `void ShowNewsEntry(News newsEntry)` declared *inside* the `newsLoaded` block, and it is re-created every frame. It is local on purpose: it is only meaningful in that branch, and it closes over nothing but its parameter (`AppUtil.OpenBrowser` is a static call). Note the second, redundant `!string.IsNullOrEmpty(newsEntry.Url)` inside the `if (ImGui.Selectable(...))` — it re-checks the guard already established outside; it is what prevents a URL-less row from becoming selectable *and* then opening a browser, so keep one of the two checks, never neither.
- **The selectable label is `$"{newsEntry.Title}##{newsEntry.Id}"`**: ImGui's label separator is `##`, so the displayed text is `Title` and the id is `Title##Id`. This is *one* `#` fewer than the `###` form used elsewhere in the package (`MainPage/LoginFrame.cs.md`'s `"popup_loginaction"` popup id, `OtpEntryPage.cs.md`'s `"###otp"`, `MainPage/AccountSwitcher.cs.md`'s `$"###{account.Id}"`), and the difference matters: `###` renders **empty** text plus an id, `##` renders `Title` plus an id. See `Component.cs.md` §5 for the id-convention survey. If `newsEntry.Id` is null/empty (the `News` field is not guaranteed), rows with the same title collide in the ImGui id space — which is why the id is appended at all.
- **`ImGui.Selectable(label, false, default, ImGui.CalcTextSize(newsEntry.Title))`**: the 4th positional argument is the text wrap position, fed with the row's own measured width (full-width hit box for the click), and the 3rd argument is the C# 13 `default` keyword, i.e. "pass this parameter's declared default value" rather than a literal. The same idiom appears in `SettingsPage/Tabs/SettingsTabAbout.cs:30` (`SettingsTabAbout.cs.md`) and in `SettingsEntry{T}.cs` — a package-wide pattern, so change all sites together if the binding signature shifts.
- **Only two of three `Headlines` lists are rendered**: `News` under `Strings.NewsCaption` and `Topics` under `Strings.TopicsCaption`, separated by `ImGui.Spacing()`; `Headlines.Pinned` is fetched-but-unrendered data (§1). The captions use `ImGui.TextDisabled` (greyed label) while rows use selectable/`TextUnformatted` text — keep the caption/row contrast so the two groups stay visually distinct.
- **A row with an empty `Url` falls back to `ImGui.TextUnformatted(title)`**, i.e. it is deliberately *not* clickable; that is the reason the branch exists, not an accident to collapse.

## 7. Geometry — the two frames must agree on the 128 px band

```csharp
private Vector2 GetSize()
{
    var vp = ImGuiHelpers.ViewportSize;
    var calculatedSize = vp.X >= 1280 ? vp.X * 0.7f : vp.X * 0.5f;
    return new Vector2(calculatedSize, vp.Y - 128f);
}
```

- **Width is a fraction of the viewport with a breakpoint at 1280**: `0.7 × vp.X` on wide screens, `0.5 × vp.X` otherwise (`ImGuiHelpers.cs.md` §viewport/scale). The `1280` breakpoint pairs with the hardcoded 1280×800 geometry used by `FtsPage.cs.md` and `SteamDeckPromptPage.cs.md` (deck-shaped window) — if the app ever fixes its window size, this breakpoint is where the two disagree.
- **`LoginFrame.cs.md`'s `GetSize()` returns `(-1, vp.Y - 128f)`**: the news frame takes a *fraction* of the width and the login frame takes the *remainder* (`-1` = extend to the parent edge). Both subtract the same `128f` height. That `128f` band is where MainPage's action-button strip and the window chrome live (`MainPage.cs.md` §Draw: `actionButtons.Draw()` runs after both frames, inside the same `PushStyleVar(WindowPadding, 32)`). The invariant is "both frames agree on the vertical reservation", and `ActionButtons.cs.md` §geometry says how much vertical space that strip actually consumes (`btnSize` = 80 px scaled, one row) — the `128f` is headroom for that row plus the `ImGui.NewLine()` gap. Do not change one `128f` without the other, and do not scale it by `GlobalScale` (neither is scaled today; the fraction-based width is scaled implicitly by `vp`).
- **The banner `Image` is drawn at `banner.Size`, unscaled** (contrast `ActionButtons.cs.md`'s `btnSize * GlobalScale` and `MainPage/LoginFrame.cs.md`'s `extraButtonSize`). So the panel grows with the CDN's image size, not with the UI scale — a scaling-bug candidate in this file family; treat it as intentional until confirmed.
- **Child-window id is `"###newsFrame"`** (the `###` id convention of `Component.cs.md` §5), and the `BeginChild`/`EndChild` pair plus the inner `PushStyleVar`/`PopStyleVar` are balanced exactly as `Component.cs.md` §Draw requires; `base.Draw()` is *outside* the `EndChild()`, so the (empty) child-component loop runs in the parent window, not in the panel.

## 8. Invariants and extension points

- **`Draw()` must survive every partial state**: never fetched (`headlines == null`, `banners == []`), mid-fetch (§4, including the null-texture window), and fetched-with-zero-rows. The `if (this.headlines is not null)` guard inside the `newsLoaded` branch exists for exactly the fetched-but-empty-`headlines` case; both guards (`newsLoaded` and the null check) are what make the body safe.
- **Nothing here is persisted**: news state is ephemeral and re-fetched, so `Configuration/` need not know about it.
- **Adding a UI affordance (e.g. a "refresh now" button)** belongs in `MainPage/LoginFrame.cs.md` or `MainPage/ActionButtons.cs.md` calling `MainPage.cs:90`'s `ReloadNews()`, not inside this class — the class has no input channel of its own and `newsFrame` is `private readonly` in MainPage.
- **Adding a rendering change** (e.g. showing `Pinned`, or `News.Tag`/`News.Date`) belongs in `Draw()`'s `ShowNewsEntry` region; keep the id/label split of §6 so ids stay unique, and keep the `Url`-empty fallback so unlinked rows stay non-clickable.

## 9. Referenced / referenced-by

Constructed by: `MainPage.cs:40` (`new NewsFrame(app)`); declared `MainPage.cs:31`; drawn at `MainPage.cs:79`; refreshed via `MainPage.cs:90` ← `LauncherApp.cs:59` (`docs/XIVLauncher.Core/LauncherApp.cs.md` §`State` setter).
Uses: `XIVLauncher.Common.Timer` (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Timer.cs`), `Headlines`/`News`/`Banner` (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Game/Headlines.cs`), `Program.HttpClient` (`docs/XIVLauncher.Core/Program.cs.md` §http client), `TextureWrap.cs.md` (§load / §fields), `ImGuiHelpers.cs.md` (§viewport size), `AppUtil.cs.md` (§open browser), `Strings` (`src/XIVLauncher.Core/Resources/Localization/Strings.Designer.cs`: `NewsCaption`, `TopicsCaption`, `NewsLoading`).
Related: `MainPage/LoginFrame.cs.md` (the sibling frame whose `GetSize()` must stay consistent, §7), `MainPage.cs.md` (§Draw ordering and the padding band), `MainPage/ActionButtons.cs.md` (occupies the 128 px band), `docs/XIVLauncher.Core/LauncherApp.cs.md` (the only refresh trigger).
