# FontManager.cs — Specification

**File:** `src/XIVLauncher.Core/FontManager.cs` · 64 lines · 1,964 bytes
**Namespace:** `XIVLauncher.Core` · **Declaration:** `public class FontManager` (with `private const` config fields)
**Role:** Builds the two ImGui fonts (UI text + Font Awesome icons) from embedded resources

---

## 1. Purpose

Registers the launcher's fonts into `ImGui.GetIO().Fonts`. Two faces are loaded:

| Font | Resource | Glyph range |
| --- | --- | --- |
| `TextFont` | `NotoSansCJKjp-Regular.otf` (embedded) | `GlyphRangesJapanese.GlyphRanges` (see `GlyphRangesJapanese.cs.md`) |
| `IconFont` | `FontAwesome5FreeSolid.otf` (embedded) | `0xE000–0xF8FF`, terminator `0` |

`GlyphRangesJapanese` doubles as the *general* glyph range set: it is the Japanese range list (Latin/Greek/Cyrillic/symbols/CJK/Halfwidth katakana/fullwidth forms included) used for **all** text, so Latin UI text and CJK both rasterise from the single Noto Sans CJK JP face.

## 2. Members

| Member | Kind | Notes |
| --- | --- | --- |
| `FONT_GAMMA` | `private const float = 1.4f` | **declared but never applied** — no `Rounding`/gamma field is set; dead constant |
| `TEXT_FONT_NAME` | `private const string = "NotoSansCJKjp-Regular.otf"` | embedded resource logical name |
| `ICON_FONT_NAME` | `private const string = "FontAwesome5FreeSolid.otf"` | embedded resource logical name |
| `TextFont` | `public static ImFontPtr` (`get`, `private set`) | handle of the UI font |
| `IconFont` | `public static ImFontPtr` (`get`, `private set`) | handle of the icon font |
| `public unsafe void SetupFonts(float pxSize)` | instance method | the only behaviour in this class |

The class itself holds no instance state; it exists so it can be constructed and asked to do work (`new FontManager().SetupFonts(...)` inside `ImGuiBindings`' constructor). `TextFont`/`IconFont` are process-global statics.

## 3. `SetupFonts` procedure (exact order)

1. `ioFonts = ImGui.GetIO().Fonts`; `Fonts.Clear()` — **any previously registered font is discarded**.
2. Create one `ImFontConfig` with `PixelSnapH = true` (horizontal pixel snapping; crisper text, no sub-pixel kerning).
3. Load both font blobs via `AppUtil.GetEmbeddedResourceBytes(name)` (throws if the resource is missing).
4. `Marshal.AllocHGlobal(len)` + `Marshal.Copy` for each blob → two native buffers holding a *copy* of the resource bytes.
5. Pin a `GCHandle` on `GlyphRangesJapanese.GlyphRanges`; pin a second `GCHandle` on a freshly built `ushort[]{ 0xE000, 0xF8FF, 0 }`.
6. `TextFont = ioFonts.AddFontFromMemoryTTF(fontDataTextPtr, len, pxSize, /*font config*/ null, japaneseRanges)`.
7. `IconFont = ioFonts.AddFontFromMemoryTTF(fontDataIconsPtr, len, pxSize, fontConfig, iconRanges)`.
8. `fontConfig.Destroy()`; `japaneseRangeHandle.Free()`; `iconRangeHandle.Free()`.

## 4. Contract details and hazards

- **`fontConfig` argument asymmetry:** the text font is loaded with `null` config (ImGui default: `PixelSnapH` false, pixel snap off) while the icon font uses the `PixelSnapH = true` config. If uniform snapping is wanted, the same config must be passed to both.
- **Ownership of the font blobs:** `AddFontFromMemoryTTF` *takes ownership* of the passed buffer (ImGui frees it when the atlas is destroyed). Here those buffers come from `AllocHGlobal` and are **never freed explicitly** — they are only released if ImGui's atlas teardown frees them. The pin `GCHandle`s are freed in step 8; the two byte buffers are not. This is the deliberate ImGui ownership convention, not a leak to "fix".
- **Range arrays must be pinned** (`GCHandleType.Pinned`) because ImGui consumes `(uint*)AddrOfPinnedObject()` synchronously but the API is marked as a raw pointer; the `0` terminator is mandatory (the Japanese list ends with `0,`; the icon list explicitly appends `0`).
- **No `GlyphRangesBuilder` / no fallback font:** glyphs outside `GlyphRangesJapanese` (e.g. emoji, Korean, Thai) are not rasterised and will render as the missing-glyph box.
- **Icon range `0xE000–0xF8FF`** is the Private Use Area. `FontAwesomeIcon.cs` enumerates `0xF000`–`0xF957`: 1,397 of its 1,407 icons fall inside the loaded band, but **10 lie above `0xF8FF`** (`FirefoxBrowser 0xF907`, `Ideal 0xF913`, `Microblog 0xF91A`, `PiedPiperSquare 0xF91E`, `Trailer 0xF941`, `Unity 0xF949`, `Dailymotion 0xF952`, `InstagramSquare 0xF955`, `Mixer 0xF956`, `Shopify 0xF957`) and will not rasterise. Widen the range to `0xF9FF` here if those become used.
- The method is `unsafe` (raw pointers, `Marshal`) and must run inside/after successful SDL+ImGui context creation; it is invoked once, from the `ImGuiBindings` constructor, with `pxSize = Program.Config.FontPxSize ?? 21.0f`. Since `Program.LoadConfig` already applies `Config.FontPxSize ??= 22.0f`, the `21.0f` fallback here is unreachable in practice but numerically different from the configured default — keep the two in sync if the nullable ever changes.
- `Fonts.Clear()` before adding means calling `SetupFonts` twice replaces the fonts (leaking the previous atlases' resources per ImGui's own bookkeeping) rather than duplicating them.
- `pxSize` is a raw pixel size; DPI scaling is applied separately in `ImGuiBindings` (`ConfigDpiScaleFonts`, `FontScaleDpi`), so do not pre-scale the value here.

## 5. Consumers

| Site | Use |
| --- | --- |
| `ImGuiBindings` ctor | `new FontManager().SetupFonts(fontPxSize)` after SDL-GPU backend init |
| `Components/*` (e.g. `ActionButtons.cs`, `LoginFrame.cs`, `SettingsPage.cs`, `OtpEntryPage.cs`) | `ImGui.PushFont(FontManager.IconFont, 0.0f)` for icon glyphs, then `ImGui.PushFont(FontManager.TextFont, 0.0f)` to restore the text face |
| `FontAwesomeIcon.cs` | `ToIconChar`/`ToIconString` produce code points intended to be drawn with `IconFont` |

## 6. Changing fonts

To swap a face: replace the embedded resource `LogicalName` in `XIVLauncher.Core.csproj` (`Resources/fonts/*.otf`) and update the matching `*_FONT_NAME` constant. Both must match exactly, since `GetEmbeddedResourceBytes` looks up the resource by logical name and throws otherwise at startup.
