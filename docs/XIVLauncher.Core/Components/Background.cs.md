# Background.cs — Specification

**File:** `src/XIVLauncher.Core/Components/Background.cs` · 25 lines · 834 bytes
**Namespace:** `XIVLauncher.Core.Components` · **Declaration:** `public class Background : Panel`
**Role:** The decorative FFXIV logo anchored to the bottom-left of the launcher window

---

## 1. Purpose

`Background` renders the embedded `bg_logo.png` at full window size. It is the bottom layer of the launcher window: `LauncherForm` adds it to a `PixelLayout` at `(0, 0)` **before** the page host, so pages paint over it (`LauncherForm.cs.md`).

The artwork lives in the **bottom-left quadrant** of a 1280×800 canvas and the rest is transparent, so a full-window image view places the logo in the bottom-left corner without any positioning logic. The previous immediate-mode version positioned the texture at `(0, viewport.Y - height)` for the same effect.

Usings: `Eto.Drawing`, `Eto.Forms`.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `Background()` | `public` ctor (lines 17–24) | sets `Content` once |
| `Content` | `ImageView` with `Image = AppImages.FromResource("bg_logo.png")`, `Size = new Size(1280, 800)` | lines 19–23 |

`Background` derives from **`Eto.Forms.Panel`**, not from `Component` — it has no `OnShow()`, no `Id`, and is not a page. It is constructed once by `LauncherForm` and never swapped.

## 3. Consumer

| Consumer | Site | Use |
| --- | --- | --- |
| `LauncherForm.cs` | line 31 | `new Background { Size = new Size(1280, 800) }`, then `layout.Add(background, 0, 0)` |

The consumer sets `Size` on the `Panel` itself (the image view already has `Size(1280, 800)`), so both layers are window-sized.

## 4. Rendering behaviour (GTK `ImageView`)

`Eto.Gtk/Forms/Controls/ImageViewHandler.cs` implements the image view as a `Gtk.DrawingArea`:

- the image is scaled by `Math.Min(scaleWidth, scaleHeight)` (uniform scaling, no distortion),
- the scaled image is **centred** within the widget,
- drawing happens in the GTK expose/draw handler.

Here the widget size equals the bitmap size (1280×800), so `scaleWidth == scaleHeight == 1`: the bitmap is drawn 1:1, no scaling, no centring offset. Two consequences:

- If the window is resized larger than 1280×800, the image stays 1280×800 anchored at the widget's top-left; the logo does **not** re-anchor to the bottom-left, because centring is applied inside the fixed-size image view.
- The image view's transparency handling depends on the GTK expose path; `bg_logo.png` is RGBA, so the transparent region shows the window background (the dark GTK theme), not the page content underneath. There is nothing underneath here — the background is added first, so page content is drawn on top of the window, not composited with the logo.

## 5. Image source

`AppImages.FromResource("bg_logo.png")` (`AppImages.cs.md`) resolves the resource through `Bitmap.FromResource(resourceName, typeof(AppUtil).Assembly)`. `bg_logo.png` is embedded as `EmbeddedResource` in `src/XIVLauncher.Core/Resources/bg_logo.png` (`XIVLauncher.Core.csproj.md`), measured as `1280 x 800`, 8-bit RGBA, non-interlaced.

`AppImages` caches per resource name, so the bitmap is decoded once; the page host swaps do not re-decode it.

## 6. Quirks

- **Fixed 1280×800 literal:** the size is hard-coded, matching `LauncherForm.ClientSize`. It is not derived from the bitmap's own `Width`/`Height`, so a future artwork change that alters canvas size needs both files updated.
- **No `Component` inheritance:** `Background` is the only UI class in `Components/` that does not inherit `Component`, so `OnShow()` is unavailable on it.
- The comment claims the image view "scales the bitmap to its own size"; GTK scales uniformly and centres, which is equivalent here only because the sizes match. The wording is the design rationale, not the mechanism.
- Because the image view is a `DrawingArea`, the background artwork does not participate in GTK theme painting (no background fill, no border), so the area outside the artwork is the window's own background.

## 7. Referenced / referenced-by

Uses: `AppImages` (`AppImages.cs.md`), `Eto.Forms.ImageView`, `Eto.Forms.Panel`.
Referenced by: `LauncherForm.cs` (line 31).
