# NewsFrame.cs — Specification

**File:** `src/XIVLauncher.Core/Components/MainPage/NewsFrame.cs` · 248 lines · 9,483 bytes
**Namespace:** `XIVLauncher.Core.Components.MainPage` · **Declaration:** `public class NewsFrame : Component`
**Role:** The news column: rotating headline banner, news list, and topics list, loaded from the game API

---

## 1. Purpose

Replaces the immediate-mode news child window. The banner rotates on a `UITimer` (a `System.Threading.Timer` cannot touch widgets, so the old pattern needed a redraw-flag; here the timer runs on the UI thread and sets `ImageView.Image` directly), and clickable news entries become Eto `LinkButton`s, keeping the "title opens the news page" behaviour.

Usings: `Eto.Drawing`, `Eto.Forms`, `XIVLauncher.Common` (`ClientLanguage`), `XIVLauncher.Common.Game` (`Headlines`, `Banner`, `News`), `XIVLauncher.Core.Resources.Localization`.

## 2. Constants and state

| Member | Kind | Notes |
| --- | --- | --- |
| `BANNER_TIME` | `private const int = 8000` (line 20) | banner rotation period in ms; the timer interval is `BANNER_TIME / 1000.0` = 8 s (line 107) |
| `NEWS_ENTRY_WIDTH` | `private const int = 780` (line 23) | initial title width: 800 px column minus 20 px gutter |
| `NEWS_ENTRY_MAX_WIDTH_CHARS` | `private const int = 10` (line 28) | GTK cap on a wrapped label's natural width, so GTK allocates the column width to the label |
| `app` | `private readonly LauncherApp` (line 30) | used for `Launcher` and `Settings.ClientLanguage` |
| `bannerList` | `private IReadOnlyList<Banner>` (line 32) | banner metadata (link URLs), starts as an empty list |
| `banners` | `private Bitmap[]` (line 33) | decoded banner bitmaps, starts empty |
| `headlines` | `private Headlines?` (line 34) | last loaded news set |
| `currentBanner` | `private int` (line 35) | rotation index |
| `newsLoaded` | `private bool` (line 36) | gate for banner clicks and rotation |
| `loadingLabel` | `private readonly Label` (line 38) | `Strings.NewsLoading` |
| `bannerImage` | `private readonly ImageView` (line 39) | `Size(384, 192)`, starts `Visible = false` (lines 57–61) |
| `newsCaptionLabel`, `topicsCaptionLabel` | `private readonly Label` (lines 40–41) | `Strings.NewsCaption` / `Strings.TopicsCaption`, `TextColor = EtoColors.Grey3`, start `Visible = false` (lines 68–80) |
| `listPanel` | `private readonly Panel` (line 42) | `Width = NEWS_ENTRY_WIDTH` (line 84); `SizeChanged → ApplyNewsEntryWidth` (line 89) |
| `newsEntries` | `private readonly List<Control>` (line 45) | the entries currently in `listPanel`, re-widthed on resize |
| `bannerTimer` | `private readonly UITimer` (line 47) | interval 8 s, `Elapsed → RotateBanner` (lines 107–108), `Start()` at line 112 |

## 3. Layout

`this.Content = new StackLayout { Spacing = 15, Items = { loadingLabel, bannerImage, newsCaptionLabel, listPanel, <stretch spacer> } }` (lines 91–104) — vertical stack. `listPanel` sits after the news caption, so both news and topics entries render inside the same panel: `ShowNews` puts the news caption, news entries, topics caption, and topic entries into one `DynamicLayout` and assigns it as the panel content (lines 167–196), i.e. the topics caption appears **inside** the list panel, after the news entries. The outer stack does not include `topicsCaptionLabel`.

Three mechanisms drive the column geometry:

- `bannerImage.Size = new Size(384, 192)` (line 59) keeps HEAD's fixed banner box; the column around it stretches.
- `listPanel.Width = NEWS_ENTRY_WIDTH` (line 84) plus per-entry `Width = NEWS_ENTRY_WIDTH` on every label/link (lines 236, 243) give the column a natural width that does not come from an unwrapped title.
- `listPanel.SizeChanged` (line 89) re-applies `entry.Width = listPanel.Width` for every tracked entry (`ApplyNewsEntryWidth`, lines 201–209), so widths follow the allocation as the window is resized.

GTK reports an unwrapped label's full text width as its natural width, so a label can never be made to wrap at a narrower column. `CapNewsEntryWidth` (lines 214–227) is `#if LINUX`: it sets the native `Gtk.Label.MaxWidthChars` to `NEWS_ENTRY_MAX_WIDTH_CHARS` through the Eto.Gtk handler, which makes GTK allocate the column width to the label. Eto 2.13 has no width-chars property, so the cap reaches GTK via reflection on the handler's internal `Control` property, and a `Gtk.LinkButton`'s text lives in its child label (`link.Children[0]`). The `#else` branch is a no-op: WinForms labels have no width-chars cap, and the entry `Width` set at creation time plus `WrapMode.Word` already keep titles wrapping at the column edge.

The trailing `new StackLayoutItem(new Panel(), VerticalAlignment.Stretch)` (line 103) takes the leftover column height. Eto.Gtk's `StackLayout` packs items at the top with no expand flag, so this expander is what keeps the page's action button row below the columns (`MainPage.cs.md` §4, `LoginFrame.cs.md` §5).

The cap alone is not enough to make titles re-wrap when the window shrinks: GTK re-wraps at a label's own width request, and Eto's width request never decreases below the last allocation, so shrinking clips the label. The column's `NEWS_ENTRY_WIDTH` and the caps make the initial layout correct; dynamic shrink-to-wrap is not supported by Eto.Gtk.

## 4. `ReloadNews()`

`ReloadNews()` (lines 116–142) runs everything inside `Task.Run`, leaving the UI thread:

1. `newsLoaded = false` (line 120)
2. `Headlines.GetWorlds`, `GetBanners`, `GetMessage`, `GetNews`, each with `this.app.Settings.ClientLanguage ?? ClientLanguage.English` and `ConfigureAwait(false)` (lines 122–126)
3. `bitmaps = new Bitmap[banners.Count]` (line 128), then per banner: `Program.HttpClient.GetByteArrayAsync(banners[i].LsbBanner)` and `Ui.Run(() => new Bitmap(bannerBytes))` (lines 130–134) — the bitmap is constructed **on the UI thread** through `Ui.Run` (`Ui.cs.md`), because Eto `Bitmap` creation is platform-bound.
4. `newsLoaded = true`, `bannerList = banners`, `banners = bitmaps` (lines 136–138)
5. `Ui.Post(() => this.ShowNews(headlines))` (line 140)

Consequences:

- `bannerList`/`banners` are written from the background task and read from UI-thread code (`RotateBanner`, banner click), without locks. The rotation timer sees the new arrays as soon as they are assigned; a rotation landing between line 136 and line 138 can read new `banners` against the old `bannerList`, i.e. an index that exists in both lists is fine, and `RotateBanner`'s guard uses `banners.Length` only.
- The list assignments are reference swaps, so a reader in `RotateBanner` may see the new array while `bannerList` is still the old one, and the banner click uses `bannerList[this.currentBanner].Link` — a rotation during a click can pair a new bitmap index with old banner metadata.
- A failed fetch (exception inside `Task.Run`) leaves `newsLoaded = false`, `loadingLabel` visible, and the `Task`'s exception unobserved: nothing awaits the `Task.Run` result, so the fault is silent in this file.

## 5. `RotateBanner()`

Lines 144–151: returns when `!newsLoaded` or `banners.Length == 0` (lines 146–147); otherwise `currentBanner = (currentBanner + 1) % banners.Length` (line 149) and `bannerImage.Image = banners[currentBanner]` (line 150). Runs on the UI timer, i.e. UI thread, so widget writes are safe.

`ShowNews` also sets `bannerImage.Image` when banners exist (lines 159–162), so the first banner is shown without waiting for a rotation tick, and the timer keeps rotating afterwards.

## 6. `ShowNews(Headlines)`

Lines 153–198, executed on the UI thread via `Ui.Post`:

1. `headlines = loadedHeadlines` (line 155), `loadingLabel.Visible = false` (line 157)
2. If `banners.Length > 0`: `bannerImage.Visible = true` and `Image = banners[currentBanner]` (lines 159–162)
3. `layout = new DynamicLayout { Spacing = new Size(0, 5) }` (line 167), `newsEntries.Clear()` (line 168)
4. If `headlines.News.Length > 0`: show `newsCaptionLabel`, add it, then for each news entry `NewsEntry(newsEntry)` → `CapNewsEntryWidth(entry)` → `newsEntries.Add(entry)` → `layout.Add(entry)` (lines 170–181)
5. If `headlines.Topics.Length > 0`: same pattern for topic entries (lines 183–194)
6. `listPanel.Content = layout` (line 196), then `ApplyNewsEntryWidth()` (line 197) — replaces the previous list and widthens the new entries to the current column allocation; old `LinkButton`s are discarded with the panel content.

Note `.Length` (arrays) is used, not `.Count`: `Headlines.News` and `Headlines.Topics` are `News[]` (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Game/Headlines.cs`), while `banners.Count` (line 130) is a `List<Banner>` count. `Headlines.Pinned` is not read here.

## 7. `NewsEntry(News)`

Lines 229–247, static:

- When `newsEntry.Url` is null/empty → plain `Label` with `Wrap = WrapMode.Word`, `Width = NEWS_ENTRY_WIDTH` (lines 231–237).
- Otherwise → `LinkButton` with the same wrap/width (lines 239–244) and `link.Click += (_, _) => AppUtil.OpenBrowser(newsEntry.Url)` (line 245).

Eto `LinkButton` renders as a clickable text link; `Click` is `EventHandler<EventArgs>` so the lambda discards sender/args (`AppUtil.cs.md` documents `OpenBrowser`'s `UseShellExecute` process launch). `CapNewsEntryWidth` is applied by the caller before the entry is added to the layout, so the GTK cap is set before GTK realizes the widget.

## 8. Banner click

`bannerImage.MouseDown` (lines 62–66): primary button only (`MouseButtons.Primary`), only when `newsLoaded`, opens `bannerList[currentBanner].Link.ToString()` (`AppUtil.OpenBrowser`). `ImageView` on GTK is a `DrawingArea` — mouse events reach it; the image is scaled by `Math.Min(scaleWidth, scaleHeight)` and centered inside the widget, so with `Size(384, 192)` the click target is the whole widget, not the drawn pixels.

## 9. Consumers

| Consumer | Site | Use |
| --- | --- | --- |
| `MainPage` ctor | `MainPage.cs:51` | `new NewsFrame(app)` — construction starts the network fetch and the timer |
| `MainPage.ReloadNews` | `MainPage.cs:118` | `this.newsFrame.ReloadNews()`, invoked by `LauncherApp` when leaving `LauncherState.Settings` (`LauncherApp.cs.md` §3), so news refreshes after a language change |
| `MainPage` layout | `MainPage.cs:92` | `topRow.Add(this.newsFrame, 1, 0, xscale: true, yscale: false)` in the login/news row |

`NewsFrame` has no `OnShow()` override, so entering `Main` state never re-fetches news; `MainPage.DoAutoLoginIfApplicable` asserts the state (`MainPage.cs:110`) and is separate from news loading.

## 10. Thread / UI rules

- Construction (labels, `ImageView`, `Panel`, `StackLayout`, `UITimer`) must be on the UI thread (`MainPage` ctor path from `LauncherApp`).
- `ReloadNews` deliberately runs off the UI thread, marshalling bitmap creation through `Ui.Run` and the final UI update through `Ui.Post` (`Ui.cs.md`).
- `RotateBanner` and the banner click handler are UI-thread.
- `newsLoaded`, `bannerList`, `banners`, `currentBanner` are plain fields written from both sides; no locking, as described in §4.
- The `UITimer` is never stopped: the timer keeps running while the page is hidden, so a hidden news column continues rotating banners and burning the 8 s tick.

## 11. Quirks

- `CapNewsEntryWidth` is `#if LINUX` (lines 216–222): reflection on the internal `Control` property of the Eto.Gtk handler (`handler.GetType().GetProperty("Control", ... | NonPublic)`). `Assembly.Load("Eto.GtkSharp")` fails (`System.IO.FileNotFoundException`: the assembly is `Eto.Gtk`), and `Eto.GtkSharp.GtkControl` is not public API, so reflection is the only way to reach `Gtk.Label.MaxWidthChars`. If Eto's handler internals rename `Control`, the cap silently disappears (the `!`-guarded lookup throws on a null handler). The `#else` (lines 223–226) is a no-op comment; WinForms relies on the entry `Width` + `WrapMode.Word`.
- `this.topicsCaptionLabel` is added to the `listPanel` layout after the news entries (lines 183–184); the outer stack ends at `listPanel`, so the visible topics caption is the one inside the panel, and the outer trailing caption from HEAD's immediate-mode layout is gone.
- `ShowNews` never resets `newsCaptionLabel`/`topicsCaptionLabel` to hidden: once shown, they stay visible even if a later reload has no news.
- `Ui.Run(() => new Bitmap(bannerBytes))` blocks the background loop for every banner, i.e. N blocking UI-thread round-trips during a reload (`Ui.cs.md`).
- No banner image caching: each reload re-fetches and re-decodes all banners; `AppImages.FromBytes` is not used (`AppImages.cs.md`).
- The banner's `Visible` flag stays true after the first load, so if a later reload yields `banners.Length == 0` the image keeps the previous bitmap.
- `headlines` field is stored but never read outside `ShowNews`.
- News entries never show the `News发布日期`/timestamp; the caption labels are the only non-entry text.
- `NewsFrame` has no `OnShow()` override, so entering `Main` state never refreshes news; `LauncherApp` triggers `MainPage.ReloadNews()` when leaving settings (`LauncherApp.cs:92`-adjacent code, `LauncherApp.cs.md` §3).

## 12. Referenced / referenced-by

Uses: `Component` (`Component.cs.md`), `LauncherApp` (`LauncherApp.cs.md`), `Headlines`/`Banner`/`News`/`ClientLanguage` (submodule `XIVLauncher.Common` — out of scope for docs), `Program.HttpClient` (`Program.cs.md`), `Ui` (`Ui.cs.md`), `EtoColors.Grey3` (`EtoColors.cs.md`), `AppUtil.OpenBrowser` (`AppUtil.cs.md`), `Strings`, `Eto.Forms.ImageView`/`LinkButton`/`DynamicLayout`/`UITimer`.
Referenced by: `MainPage.cs` (lines 40, 51, 92, 118).
