# NewsFrame.cs — Specification

**File:** `src/XIVLauncher.Core/Components/MainPage/NewsFrame.cs` · 184 lines · 6,244 bytes
**Namespace:** `XIVLauncher.Core.Components.MainPage` · **Declaration:** `public class NewsFrame : Component`
**Role:** The news column: rotating headline banner, news list, and topics list, loaded from the game API

---

## 1. Purpose

Replaces the immediate-mode news child window. The banner rotates on a `UITimer` (a `System.Threading.Timer` cannot touch widgets, so the old pattern needed a redraw-flag; here the timer runs on the UI thread and sets `ImageView.Image` directly), and clickable news entries become Eto `LinkButton`s, keeping the "title opens the news page" behaviour.

Usings: `Eto.Drawing`, `Eto.Forms`, `XIVLauncher.Common` (`ClientLanguage`), `XIVLauncher.Common.Game` (`Headlines`, `Banner`, `News`), `XIVLauncher.Core.Resources.Localization`.

## 2. Constants and state

| Member | Kind | Notes |
| --- | --- | --- |
| `BANNER_TIME` | `private const int = 8000` (line 20) | banner rotation period in ms; the timer interval is `BANNER_TIME / 1000.0` = 8 s (line 84) |
| `NEWS_ENTRY_WIDTH` | `private const int = 780` (line 23) | title width: 800 px column minus 20 px gutter |
| `app` | `private readonly LauncherApp` (line 25) | used for `Launcher` and `Settings.ClientLanguage` |
| `bannerList` | `private IReadOnlyList<Banner>` (line 27) | banner metadata (link URLs), starts as an empty list |
| `banners` | `private Bitmap[]` (line 28) | decoded banner bitmaps, starts empty |
| `headlines` | `private Headlines?` (line 29) | last loaded news set |
| `currentBanner` | `private int` (line 30) | rotation index |
| `newsLoaded` | `private bool` (line 31) | gate for banner clicks and rotation |
| `loadingLabel` | `private readonly Label` (line 33) | `Strings.NewsLoading` |
| `bannerImage` | `private readonly ImageView` (line 34) | `Size(384, 192)`, starts `Visible = false` (lines 49–53) |
| `newsCaptionLabel`, `topicsCaptionLabel` | `private readonly Label` (lines 35–36) | `Strings.NewsCaption` / `Strings.TopicsCaption`, `TextColor = EtoColors.Grey3`, start `Visible = false` (lines 60–72) |
| `listPanel` | `private readonly Panel` (line 37) | `Width = NEWS_ENTRY_WIDTH` (line 76) |
| `bannerTimer` | `private readonly UITimer` (line 39) | interval 8 s, `Elapsed → RotateBanner` (lines 84–85), `Start()` at line 89 |

## 3. Layout

`this.Content = new StackLayout { Spacing = 15, Items = { loadingLabel, bannerImage, newsCaptionLabel, listPanel } }` (lines 78–82) — vertical stack. `listPanel` sits after the news caption, so both news and topics entries render inside the same panel: `ShowNews` puts the news caption, news entries, topics caption, and topic entries into one `DynamicLayout` and assigns it as the panel content (lines 144–162), i.e. the topics caption appears **inside** the list panel, after the news entries. The outer stack does not include `topicsCaptionLabel`.

Two explicit widths exist for the same reason (GTK reports an unwrapped label's full text width as its natural width, which would push the news column off-screen):

- `bannerImage.Size = new Size(384, 192)` (line 51) keeps HEAD's fixed banner box; the column around it stretches.
- `listPanel.Width = NEWS_ENTRY_WIDTH` (line 76) plus per-entry `Width = NEWS_ENTRY_WIDTH` on every label/link (lines 171, 178).

## 4. `ReloadNews()`

`ReloadNews()` (lines 93–120) runs everything inside `Task.Run`, leaving the UI thread:

1. `newsLoaded = false` (line 97)
2. `Headlines.GetWorlds`, `GetBanners`, `GetMessage`, `GetNews`, each with `this.app.Settings.ClientLanguage ?? ClientLanguage.English` and `ConfigureAwait(false)` (lines 99–103)
3. `bitmaps = new Bitmap[banners.Count]` (line 105), then per banner: `Program.HttpClient.GetByteArrayAsync(banners[i].LsbBanner)` and `Ui.Run(() => new Bitmap(bannerBytes))` (lines 107–111) — the bitmap is constructed **on the UI thread** through `Ui.Run` (`Ui.cs.md`), because Eto `Bitmap` creation is platform-bound.
4. `newsLoaded = true`, `bannerList = banners`, `banners = bitmaps` (lines 113–115)
5. `Ui.Post(() => this.ShowNews(headlines))` (line 117)

Consequences:

- `bannerList`/`banners` are written from the background task and read from UI-thread code (`RotateBanner`, banner click), without locks. The rotation timer sees the new arrays as soon as they are assigned; a rotation landing between line 113 and line 115 can read new `banners` against the old `bannerList`, i.e. an index that exists in both lists is fine, and `RotateBanner`'s guard uses `banners.Length` only.
- The list assignments are reference swaps, so a reader in `RotateBanner` may see the new array while `bannerList` is still the old one, and the banner click uses `bannerList[this.currentBanner].Link` — a rotation during a click can pair a new bitmap index with old banner metadata.
- A failed fetch (exception inside `Task.Run`) leaves `newsLoaded = false`, `loadingLabel` visible, and the `Task`'s exception unobserved: nothing awaits the `Task.Run` result, so the fault is silent in this file.

## 5. `RotateBanner()`

Lines 121–128: returns when `!newsLoaded` or `banners.Length == 0` (lines 123–124); otherwise `currentBanner = (currentBanner + 1) % banners.Length` (line 126) and `bannerImage.Image = banners[currentBanner]` (line 127). Runs on the UI timer, i.e. UI thread, so widget writes are safe.

`ShowNews` also sets `bannerImage.Image` when banners exist (lines 136–139), so the first banner is shown without waiting for a rotation tick, and the timer keeps rotating afterwards.

## 6. `ShowNews(Headlines)`

Lines 130–163, executed on the UI thread via `Ui.Post`:

1. `headlines = loadedHeadlines` (line 132), `loadingLabel.Visible = false` (line 134)
2. If `banners.Length > 0`: `bannerImage.Visible = true` and `Image = banners[currentBanner]` (lines 136–139)
3. `layout = new DynamicLayout { Spacing = new Size(0, 5) }` (line 144)
4. If `headlines.News.Length > 0`: show `newsCaptionLabel`, add it, then add each news entry (lines 146–151)
5. If `headlines.Topics.Length > 0`: show `topicsCaptionLabel`, add it, then add each topic entry (lines 154–159)
6. `listPanel.Content = layout` (line 162) — replaces the previous list; old `LinkButton`s are discarded with the panel content.

Note `.Length` (arrays) is used, not `.Count`: `Headlines.News` and `Headlines.Topics` are `News[]` (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Game/Headlines.cs`), while `banners.Count` (line 105) is a `List<Banner>` count. `Headlines.Pinned` is not read here.

## 7. `NewsEntry(News)`

Lines 165–183, static:

- When `newsEntry.Url` is null/empty → plain `Label` with `Wrap = WrapMode.Word`, `Width = NEWS_ENTRY_WIDTH` (lines 167–173).
- Otherwise → `LinkButton` with the same wrap/width (lines 175–179) and `link.Click += (_, _) => AppUtil.OpenBrowser(newsEntry.Url)` (line 180).

Eto `LinkButton` renders as a clickable text link; `Click` is `EventHandler<EventArgs>` so the lambda discards sender/args (`AppUtil.cs.md` documents `OpenBrowser`'s `UseShellExecute` process launch).

## 8. Banner click

`bannerImage.MouseDown` (lines 54–59): primary button only (`MouseButtons.Primary`), only when `newsLoaded`, opens `bannerList[currentBanner].Link.ToString()` (`AppUtil.OpenBrowser`). `ImageView` on GTK is a `DrawingArea` — mouse events reach it; the image is scaled by `Math.Min(scaleWidth, scaleHeight)` and centered inside the widget, so with `Size(384, 192)` the click target is the whole widget, not the drawn pixels.

## 9. Consumers

| Consumer | Site | Use |
| --- | --- | --- |
| `MainPage` ctor | `MainPage.cs:51` | `new NewsFrame(app)` — construction starts the network fetch and the timer |
| `MainPage.ReloadNews` | `MainPage.cs:116` | `this.newsFrame.ReloadNews()`, invoked by `LauncherApp` when leaving `LauncherState.Settings` (`LauncherApp.cs.md` §3), so news refreshes after a language change |
| `MainPage` layout | `MainPage.cs:92` | `topRow.Add(this.newsFrame, 1, 0, xscale: true, yscale: false)` in the login/news row |

`NewsFrame` has no `OnShow()` override, so entering `Main` state never re-fetches news; `MainPage.DoAutoLoginIfApplicable` asserts the state (`MainPage.cs:110`) and is separate from news loading.

## 10. Thread / UI rules

- Construction (labels, `ImageView`, `Panel`, `StackLayout`, `UITimer`) must be on the UI thread (`MainPage` ctor path from `LauncherApp`).
- `ReloadNews` deliberately runs off the UI thread, marshalling bitmap creation through `Ui.Run` and the final UI update through `Ui.Post` (`Ui.cs.md`).
- `RotateBanner` and the banner click handler are UI-thread.
- `newsLoaded`, `bannerList`, `banners`, `currentBanner` are plain fields written from both sides; no locking, as described in §4.
- The `UITimer` is never stopped: the timer keeps running while the page is hidden, so a hidden news column continues rotating banners and burning the 8 s tick.

## 11. Quirks

- `this.topicsCaptionLabel` is added to the `listPanel` layout after the news entries (lines 156–157); the outer stack ends at `listPanel`, so the visible topics caption is the one inside the panel, and the outer trailing caption from HEAD's immediate-mode layout is gone.
- `ShowNews` never resets `newsCaptionLabel`/`topicsCaptionLabel` to hidden: once shown, they stay visible even if a later reload has no news.
- `Ui.Run(() => new Bitmap(bannerBytes))` blocks the background loop for every banner, i.e. N blocking UI-thread round-trips during a reload (`Ui.cs.md`).
- No banner image caching: each reload re-fetches and re-decodes all banners; `AppImages.FromBytes` is not used (`AppImages.cs.md`).
- The banner's `Visible` flag stays true after the first load, so if a later reload yields `banners.Length == 0` the image keeps the previous bitmap.
- `headlines` field is stored but never read outside `ShowNews`.
- News entries never show the `News发布日期`/timestamp; the caption labels are the only non-entry text.
- `NewsFrame` has no `OnShow()` override, so entering `Main` state never refreshes news; `LauncherApp` triggers `MainPage.ReloadNews()` when leaving settings (`LauncherApp.cs:92`-adjacent code, `LauncherApp.cs.md` §3).

## 12. Referenced / referenced-by

Uses: `Component` (`Component.cs.md`), `LauncherApp` (`LauncherApp.cs.md`), `Headlines`/`Banner`/`News`/`ClientLanguage` (submodule `XIVLauncher.Common` — out of scope for docs), `Program.HttpClient` (`Program.cs.md`), `Ui` (`Ui.cs.md`), `EtoColors.Grey3` (`EtoColors.cs.md`), `AppUtil.OpenBrowser` (`AppUtil.cs.md`), `Strings`, `Eto.Forms.ImageView`/`LinkButton`/`DynamicLayout`/`UITimer`.
Referenced by: `MainPage.cs` (lines 51, 101, 116).
