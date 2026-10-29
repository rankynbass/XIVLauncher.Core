# AppFonts.cs — Specification

**File:** `src/XIVLauncher.Core/AppFonts.cs` · 155 lines · 8,008 bytes
**TargetFramework:** conditional per RID (`XIVLauncher.Core.csproj` lines 5–6): `net10.0-windows` on win-x64, `net10.0` on Linux builds
**Role:** registers the launcher's embedded Noto CJK and Font Awesome fonts with Eto
(`Eto.Forms`, https://github.com/picoe/Eto); the replacement for the SDL/ImGui `FontManager` (deleted).

---

## 1. Purpose

- `Initialize(float pxSize)` — stores the configured size; `#if LINUX` loads `NotoSansCJKjp-Regular.otf`
  and `FontAwesome5FreeSolid.otf` from embedded resources via `FontFamily.FromStreams(OpenResource(...))`,
  `#else` registers both through `RegisterWindowsFont` (lines 40–52).
- `DEFAULT_TEXT_SIZE` = `11f` — the launcher-wide default text size for every control that carries no explicit font (line 36).
- `ApplyDefaultTextFont()` — called at the end of `Initialize` (line 51), on the UI thread before any control is built. `#if LINUX` (lines 58–64) sets `Gtk.Settings.Default.FontName` to `"Noto Sans CJK JP 11"` (interpolated from `DEFAULT_TEXT_SIZE`) through reflection on GtkSharp: `gtk-font-name` drives the default font of every widget Eto does not give an explicit font, and the family is fontconfig-visible because `FontFamily.FromStreams` registered the file through `FcConfigAppFontAddFile`. `#else` (lines 65–84) calls WinForms `Application.SetDefaultFont` (FontFamily, size, FontStyle.Regular, GraphicsUnit.Point) via reflection on `System.Windows.Forms`/`System.Drawing.Common` — types the project does not reference directly — with the family `TEXT_FAMILY_NAME`, after `RegisterWindowsFont` has put it in the session font table; a failed `FontFamily` lookup is caught and logged as a warning, leaving the platform default in place.
- `TEXT_FAMILY_NAME` / `ICON_FAMILY_NAME` — the family names GDI enumerates the embedded fonts by
  ("Noto Sans CJK JP Regular", "Font Awesome 5 Free Solid"): name-table ID 1, the full font name, not the
  preferred family name ID 16 that `InstalledFontCollection` reports for installed fonts (lines 22–28).
  `FontAwesome5FreeSolid.otf` carries only the Solid face.
- `TextFont` — `new Font(textFamily, size)`; throws if `Initialize` has not run (line 88).
- `IconFont` — `new Font(iconFamily, size)`; throws if `Initialize` has not run (line 91).
- `IconFontAt(float size)` — the icon family at an explicit size, for glyphs that must fit a fixed-size
  button (line 95).
- `Glyph(FontAwesomeIcon icon)` — `icon.ToIconString()`, the same private-use glyph string the ImGui
  font atlas rendered (line 98).
- `ApplyGlyph(Button, FontAwesomeIcon)` — sets `Font = IconFont` and `Text = Glyph(icon)` on an Eto
  `Button` (lines 101–105).
- `OpenResource(string)` — `new MemoryStream(AppUtil.GetEmbeddedResourceBytes(resourceName))` (lines 107–108).
- `RegisterWindowsFont(resourceName, familyName)` — `#if !LINUX` block, lines 110–154: writes the embedded
  bytes to `%TEMP%\xlcore-fonts\<name>.otf`, calls gdi32 `AddFontResourceEx(path, 0, IntPtr.Zero)` (plain
  flags, not `FR_PRIVATE`), verifies the family appears in `InstalledFontCollection`, returns
  `new FontFamily(familyName)`; on failure logs a warning and falls back to `FontFamily.FromStreams`.

Fonts must be registered once, on the UI thread, before any control is built that uses them.

## 2. Consumers

| Consumer | Site | API |
| --- | --- | --- |
| `Program.cs` | line 286 | `AppFonts.Initialize(Config.FontPxSize ?? 21.0f)` inside `application.Initialized`, before `LauncherApp`/`LauncherForm` are built |
| `Components/Common/Button.cs` | lines 55–58 | `AppFonts.IconFont` / `AppFonts.IconFontAt(size)` and `AppFonts.Glyph(icon)` in `ShowIcon(FontAwesomeIcon, float?)` |

No consumer uses `TextFont` or `ApplyGlyph`: native Eto controls take their font from the GTK theme,
so the launcher text font is registered to make CJK glyphs resolvable in themed labels, and
`ApplyGlyph` is an extension point for callers that need icon buttons without the `Common.Button`
wrapper.

## 3. Quirks

- Eto `FontFamily.FromStreams` registers the family with the platform font manager (Pango/Freetype on
  GTK). The launcher loads exactly the two families the old `FontManager` loaded, in the same order,
  and never unregisters them.
- Windows: Eto's WinForms platform loads stream fonts through a GDI+ `PrivateFontCollection`
  (`AddMemoryFont`), which GDI cannot see; GDI-based controls — `Button`, `Label` — draw text with GDI,
  so a glyph font loaded only from memory renders as tofu boxes on buttons. Private fonts
  (`FR_PRIVATE`) are not enumerated by `InstalledFontCollection`, so `RegisterWindowsFont` registers
  with the plain session font table: the font is then enumerable by family name and visible to every
  control, and is removed when the process exits. A font as large as Noto CJK can exceed the font
  table, in which case `AddFontResourceEx` returns 0, a warning is logged, and registration falls back
  to `FontFamily.FromStreams` (GDI+ serves the family for the controls that use it).
- `Glyph` returns the private-use codepoint as a string. Rendering depends on the family being
  registered first; if `Initialize` is skipped, `IconFont` throws `InvalidOperationException`.
- The old ImGui font manager applied glyph ranges (Japanese ranges, `GlyphRangesJapanese.cs`) and an
  icon font at a configured pixel size. Eto/GTK has no glyph-range concept: the whole family is
  registered, so glyph coverage comes from the `.otf` itself.
- `Config.FontPxSize` is a float from `launcher.ini`; the same value feeds both `TextFont` and
  `IconFont`, so the icon buttons scale with the launcher font-size setting. `IconFontAt` is the
  override for fixed-size buttons: both GTK and WinForms size a button's height from its font, so
  HEAD's 45 px caret and account buttons need a 14 pt glyph (`LoginFrame.cs.md`).
- `ApplyGlyph` takes `Eto.Forms.Button` fully qualified, so the file compiles with the launcher's own
  `Components/Common/Button.cs` wrapper in scope (`docs/XIVLauncher.Core/Components/Common/Button.cs.md`).

## 4. Cross-references

- `docs/XIVLauncher.Core/AppUtil.cs.md` — `GetEmbeddedResourceBytes` feeds `OpenResource`.
- `docs/XIVLauncher.Core/Components/Common/Button.cs.md` — `ShowIcon` consumer.
- `docs/XIVLauncher.Core/Program.cs.md` — Eto bootstrap and the `Initialized` hook ordering.
- `docs/XIVLauncher.Core/FontAwesomeIcon.cs.md` — `ToIconString` source for the glyph string.
