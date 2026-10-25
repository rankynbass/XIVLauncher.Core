# ClickArea.cs — Specification

**File:** `src/XIVLauncher.Core/Components/Common/ClickArea.cs` · 29 lines · 955 bytes
**TargetFramework:** net10.0 (Linux/GTK3 build)
**Role:** transparent clickable rectangle over fixed artwork, the replacement for the invisible
transparent ImGui buttons (`imgui.net`) used on the Steam Deck guide screens; built on Eto
(`Eto.Forms`, https://github.com/picoe/Eto) `Drawable`.

---

## 1. Purpose

- `ClickArea(int width, int height)` sets `Size` to the given pixel size (line 17).
- `MouseDown` handler raises `Click` when `e.Buttons == MouseButtons.Primary` (lines 19–23).
- `public event Action? Click` — fires when the left mouse button is pressed inside the area (line 26).

Eto buttons are themed and cannot be made transparent, so the launcher overlays fixed-size
clickable rectangles on artwork, at the same pixel coordinates the ImGui buttons occupied.

## 2. Consumers

| Consumer | Site | Region |
| --- | --- | --- |
| `Components/FtsPage.cs` | line 45 | `new ClickArea(649, 101)` at `316, 481` — open guide / FTS launch |
| `Components/FtsPage.cs` | line 56 | `new ClickArea(649, 101)` at `316, 598` — finish FTS |

`Components/SteamDeckPromptPage.cs` has no `ClickArea`; the whole prompt window is clickable in the
old code, so the page's click behavior lives in the page itself.

`Components/MainPage/NewsFrame.cs` uses `MouseButtons.Primary` in a direct `MouseUp` check for the
news/topic click behavior, not this wrapper.

## 3. Quirks

- Eto `MouseButtons` has `None`, `Primary`, `Alternate`, `Middle`; `MouseButtons.Left` does not exist,
  so the primary button check uses `Primary`.
- `Drawable` paints nothing; the area is transparent, so the artwork underneath (the `ImageView` in the
  same `PixelLayout`) remains visible.
- The rectangle is sized, not scaled: coordinates are hard-coded against the 1280×800 artwork, matching
  the previous ImGui layout positions.
- `MouseDown` fires on press, not on release, and has no drag semantics — a click that starts inside
  the area fires immediately.
- `Click` is `Action?`, so it must be subscribed (`+=`); the wrapper has no command/`Command` support
  as Eto `Button` does.

## 4. Cross-references

- `docs/XIVLauncher.Core/Components/FtsPage.cs.md` — the only consumer, with the fixed geometry.
- `docs/XIVLauncher.Core/Components/SteamDeckPromptPage.cs.md` — full-window artwork page.
- `docs/XIVLauncher.Core/AppImages.cs.md` — supplies the 1280×800 artwork bitmaps.
