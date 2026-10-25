# AppImages.cs — Specification

**File:** `src/XIVLauncher.Core/AppImages.cs` · 27 lines · 938 bytes
**TargetFramework:** net10.0 (Linux/GTK3 build)
**Role:** cached Eto `Bitmap` loader for the launcher's embedded artwork; the replacement for the SDL GPU
`TextureWrap` (deleted) in the `Eto.Forms` (https://github.com/picoe/Eto) UI.

---

## 1. Purpose

- `AppImages.FromResource(string resourceName)` — decodes an embedded resource once per process
  (`Bitmap.FromResource(resourceName, typeof(AppUtil).Assembly)`, cached in a static dictionary, line 15).
- `AppImages.FromBytes(byte[] bytes)` — `new Bitmap(bytes)` for downloaded image bytes, uncached (line 25).

Resource names are the embedded resource paths from `src/XIVLauncher.Core/Resources` — the same names
the SDL path uploaded to the GPU.

Embedded launcher resources (measured with `file`):

| Resource | Format |
| --- | --- |
| `bg_logo.png` | PNG, 1280×800, RGBA |
| `logo.png` | PNG, 1000×999, RGBA |
| `steamdeck_fts.png` | PNG, 1280×800, RGB |
| `steamdeck_fterror.png` | PNG, 1280×800, RGBA |
| `steamdeck_switchprompt.png` | PNG, 1280×800, RGBA |

## 2. Consumers

| Consumer | Site | Resource |
| --- | --- | --- |
| `Components/Background.cs` | line 21 | `bg_logo.png` — full-size `ImageView` in the window |
| `Components/FtsPage.cs` | line 28 | `steamdeck_fts.png` |
| `Components/FtsPage.cs` | line 35 | `steamdeck_fterror.png` |
| `Components/SteamDeckPromptPage.cs` | line 21 | `steamdeck_switchprompt.png` |
| `Components/SettingsPage/Tabs/SettingsTabAbout.cs` | line 38 | `logo.png` |

`Components/MainPage/NewsFrame.cs` line 110 does *not* use `AppImages`; it calls
`Ui.Run(() => new Bitmap(bannerBytes))` directly because `Bitmap` construction must be done on the
UI thread, and banners are per-download (uncacheable).

## 3. Quirks

- `FromResource` is called from page constructors, which run on the UI thread; the cache is not
  thread-safe, and no consumer calls it off the UI thread.
- `Bitmap` decoding is done by the GTK platform handler (GDK/GdkPixbuf); decoded bitmaps are retained,
  so each resource holds its decoded size for the process lifetime.
- `FromBytes` is not cached: news banners rotate every `BANNER_TIME` (8000 ms, `NewsFrame.cs`), and the
  bytes for each banner are downloaded once, so caching bytes would leak.
- Eto `ImageView` scales the image by `Math.Min(scaleWidth, scaleHeight)` and centers it
  (`Eto.Gtk/Forms/Controls/ImageViewHandler.cs`), so the fixed-size pages (1280×800) reproduce the SDL
  full-window draw only if the `ImageView`'s `Size` is set to the window size — the consumers do set it.

## 4. Cross-references

- `docs/XIVLauncher.Core/Components/Background.cs.md` — uses `bg_logo.png` under the page host.
- `docs/XIVLauncher.Core/Components/FtsPage.cs.md`, `docs/XIVLauncher.Core/Components/SteamDeckPromptPage.cs.md`
  — full-window artwork pages with `ClickArea` hot zones.
- `docs/XIVLauncher.Core/Ui.cs.md` — `Ui.Run<T>` for banner bitmaps.
- `docs/XIVLauncher.Core/AppUtil.cs.md` — provides the assembly used as the resource lookup root.
