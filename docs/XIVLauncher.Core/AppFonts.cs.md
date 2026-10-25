# AppFonts.cs — Specification

**File:** `src/XIVLauncher.Core/AppFonts.cs` · 54 lines · 2400 bytes
**TargetFramework:** net10.0 (Linux/GTK3 build)
**Role:** registers the launcher's embedded Noto CJK and Font Awesome fonts with Eto
(`Eto.Forms`, https://github.com/picoe/Eto); the replacement for the SDL/ImGui `FontManager` (deleted).

---

## 1. Purpose

- `Initialize(float pxSize)` — loads `NotoSansCJKjp-Regular.otf` and `FontAwesome5FreeSolid.otf`
  from embedded resources via `FontFamily.FromStreams(OpenResource(...))`, stores the configured size
  (lines 25–28).
- `TextFont` — `new Font(textFamily, size)`; throws if `Initialize` has not run (line 33).
- `IconFont` — `new Font(iconFamily, size)`; throws if `Initialize` has not run (line 36).
- `IconFontAt(float size)` — the icon family at an explicit size, for glyphs that must fit a fixed-size
  button (line 40).
- `Glyph(FontAwesomeIcon icon)` — `icon.ToIconString()`, the same private-use glyph string the ImGui
  font atlas rendered (line 42).
- `ApplyGlyph(Button, FontAwesomeIcon)` — sets `Font = IconFont` and `Text = Glyph(icon)` on an Eto
  `Button` (line 46).
- `OpenResource(string)` — `new MemoryStream(AppUtil.GetEmbeddedResourceBytes(resourceName))` (line 52).

Fonts must be registered once, on the UI thread, before any control is built that uses them.

## 2. Consumers

| Consumer | Site | API |
| --- | --- | --- |
| `Program.cs` | line 272 | `AppFonts.Initialize(Config.FontPxSize ?? 21.0f)` inside `application.Initialized`, before `LauncherApp`/`LauncherForm` are built |
| `Components/Common/Button.cs` | line 57 | `AppFonts.IconFont` / `AppFonts.IconFontAt(size)` and `AppFonts.Glyph(icon)` in `ShowIcon(FontAwesomeIcon, float?)` |

No consumer uses `TextFont` or `ApplyGlyph`: native Eto controls take their font from the GTK theme,
so the launcher text font is registered to make CJK glyphs resolvable in themed labels, and
`ApplyGlyph` is an extension point for callers that need icon buttons without the `Common.Button`
wrapper.

## 3. Quirks

- Eto `FontFamily.FromStreams` registers the family with the platform font manager (Pango/Freetype on
  GTK). The launcher loads exactly the two families the old `FontManager` loaded, in the same order,
  and never unregisters them.
- `Glyph` returns the private-use codepoint as a string. Rendering depends on the family being
  registered first; if `Initialize` is skipped, `IconFont` throws `InvalidOperationException`, which is
  what the launcher's debug log (`Log.Debug("Fonts initialised")` path in `Program.cs`) guards.
- The old ImGui font manager applied glyph ranges (Japanese ranges, `GlyphRangesJapanese.cs`) and an
  icon font at a configured pixel size. Eto/GTK has no glyph-range concept: the whole family is
  registered, so glyph coverage comes from the `.otf` itself.
- `Config.FontPxSize` is a float from `launcher.ini`; the same value feeds both `TextFont` and
  `IconFont`, so the icon buttons scale with the launcher font-size setting. `IconFontAt` is the
  override for fixed-size buttons: GTK sizes a button's height from its font, so HEAD's 45 px caret and
  account buttons need a 14 pt glyph (`LoginFrame.cs.md`).
- `ApplyGlyph` takes `Eto.Forms.Button` fully qualified, so the file compiles with the launcher's own
  `Components/Common/Button.cs` wrapper in scope (`docs/XIVLauncher.Core/Components/Common/Button.cs.md`).

## 4. Cross-references

- `docs/XIVLauncher.Core/AppUtil.cs.md` — `GetEmbeddedResourceBytes` feeds `OpenResource`.
- `docs/XIVLauncher.Core/Components/Common/Button.cs.md` — `ShowIcon` consumer.
- `docs/XIVLauncher.Core/Program.cs.md` — Eto bootstrap and the `Initialized` hook ordering.
- `docs/XIVLauncher.Core/FontAwesomeIcon.cs.md` — `ToIconString` source for the glyph string.
