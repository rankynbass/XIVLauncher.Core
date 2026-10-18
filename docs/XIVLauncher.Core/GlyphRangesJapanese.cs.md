# GlyphRangesJapanese.cs — Specification

**File:** `src/XIVLauncher.Core/GlyphRangesJapanese.cs` · 528 lines · 70,119 bytes
**Namespace:** `XIVLauncher.Core` · **Declaration:** `public static class GlyphRangesJapanese`
**Role:** ImGui glyph-range table — the set of code points the launcher rasterises for its UI text

---

## 1. Purpose

Dear ImGui rasterises only the glyphs listed in a **range table** (an ordered list of `start, end` pairs, `0`-terminated) when a font is added. This file supplies that table, taken from ImGui's Japanese range preset, and `FontManager` attaches it to the UI text face (`NotoSansCJKjp-Regular.otf`).

Despite the name, the table is not Japanese-only: it is the union of every script the launcher's UI may need — Latin, Greek, Cyrillic, punctuation, symbols, box-drawing, kana, and a curated subset of CJK ideographs — which is why it doubles as the general-purpose range for all launcher text.

## 2. Surface

| Member | Kind | Contract |
| --- | --- | --- |
| `GlyphRanges` | `public static ushort[]`, get-only accessor declared as `=> new ushort[] { … }` | the full range table |
| (class is `static`) | — | no instances; no other members |

**The accessor builds a brand-new array on every access.** The literal has 8,210 `ushort` entries ⇒ ~16.4 KB allocated and initialised per read. `FontManager` reads it once (`GCHandle.Alloc(GlyphRangesJapanese.GlyphRanges, Pinned)`), which is the only reason the cost is tolerable. Do not read this accessor inside drawing code; if it must be read more than once, cache it locally (and keep that same array pinned, since ImGui holds the pointer).

## 3. Table structure

| Property | Value (measured) |
| --- | --- |
| `ushort` literals | 8,210 + one trailing `0` terminator (last entry) |
| `(start, end)` pairs | 4,105 |
| Singleton ranges (`start == end`) | 2,680 |
| Total code points covered | 7,325 |
| Span | `0x0020` … `0xFFFF` |
| Ordering | ascending by start (required form for ImGui scanning; not re-validated at runtime) |

## 4. Coverage by block

| Block | Ranges | Code points | Notes |
| --- | --- | --- | --- |
| Basic Latin + Latin-1 (`0x20–0xFF`) | 1 | 224 | one contiguous block — all ASCII plus accented Latin |
| Greek (`0x0391–0x03C9`) | 4 | 48 | split around the unused `0x03A2` and `0x03AA–0x03C0` gaps |
| Cyrillic (`0x0401`, `0x0410–0x044F`, `0x0451`) | 3 | 66 | full Serbian/Russian range plus `Ё`/`ё` |
| General Punctuation (`0x2000–0x206F`) | 1 | 112 | dashes, quotes, bullets |
| Letterlike Symbols (`0x2103`, `0x212B`) | 2 | 2 | ℃, Å |
| Arrows (`0x2190–0x2193`, `0x21D2`, `0x21D4`) | 3 | 6 | |
| Mathematical Symbols | 18 | 32 | `∀ ∂ ∇ ∈ √ ∝ ∞ ⌬ ⌐ ⌒ … ≠ ≡ ≪ ≫ ⊂ ⊃ ⊆ ⊇ ⊥ …` |
| Misc Technical (`0x2312`) | 1 | 1 | ⌒ (arc) |
| Box Drawing (`0x2500–0x254B`) | 17 | 32 | partial: borders/corners used by terminal-style layouts |
| Geometric Shapes | 7 | 12 | ■ □ ▲ ▼ ◆ ● … |
| Miscellaneous Symbols | 6 | 7 | ★ ☆ ♀ ♂ ♪ ♭ ♯ |
| CJK Punctuation + Kana (`0x3000–0x30FF`) | 1 | 256 | one contiguous block: ideographic space, 、。｢｣〜・ and iteration marks, Hiragana, Katakana |
| Hangul Jamo Extended-B area (`0x31F0–0x31FF`) | 1 | 16 | Japanese phonograms ㋰–㋿ (the 0x31F0 block) |
| CJK Ideographs (`0x4E00–0x9FFF`) | 4,031 | 6,356 | **curated kanji subset**, not the whole CJK block |
| Halfwidth/Fullwidth Forms (`0xFF01–0xFFE5`, `0xFFFF`) | 9 | 155 | fullwidth Latin/punctuation, halfwidth katakana, � |

The CJK portion — 4,031 of the 4,105 pairs — dominates the file and is the "kanji used in Japanese text" preset list; it is a subset (`0x4E00`-`0x9FA0` sampled, with gaps), so many Chinese-only ideographs are absent.

## 5. Format contract (ImGui)

- Entries are **inclusive** `(start, end)` pairs; a single glyph is written as `X, X`.
- The list **must be `0`-terminated**; the file ends with `0xFFFF, 0xFFFF, 0,` — the terminator is not part of any pair.
- Pairs must not overlap and must be in non-decreasing order for ImGui's linear merge/scan; nothing in this project validates that, so an edit that breaks ordering silently drops glyphs.
- The array element type is `ushort` while `FontManager` passes `(uint*)handle.AddrOfPinnedObject()` to `AddFontFromMemoryTTF`. That cast is a binding-level pointer cast (the Hexa signature declares `uint*`); correctness depends on ImGui's 16-bit `ImWchar` configuration. **Do not change the array to `uint[]`** without changing the consumer: an `ImWchar16` build expects packed `ushort` entries, and widening the element type would reinterpret the data as half as many, wrong-valued pairs.

## 6. Behavioural consequences

- **Missing glyph ⇒ ImGui's replacement box, not a crash.** Text in an uncovered script (Korean, Thai, Arabic, emoji, most non-Japanese CJK, anything above `0xFFFF` including astral-plane emoji) renders as boxes and is not logged.
- **Atlas cost is proportional to coverage:** 7,325 code points at `Config.FontPxSize` (~22 px) is what drives atlas size; ImGui's `ImFontAtlas` growth and `GetTextureWidth/Height` come directly from this table. Adding a big new block (e.g. full CJK, ~20,000 code points) risks exceeding atlas limits and degrading glyph packing.
- `FontManager` applies this table **only to the text face**; the icon face has its own `0xE000–0xF8FF` range. The two ranges do not intersect, so icons never collide with text glyphs.
- Because the accessor is a factory, `GlyphRanges != GlyphRanges` is `true` on a second read (reference comparison on arrays). If any code ever caches identity of this array, it breaks — compare contents, or change the accessor to a backed field if identity must be stable.

## 7. Provenance and editing

The list is the standard `glyph_ranges_japanese` table from Dear ImGui (`imgui_textmate`/`stb`-era generator), re-formatted as C# with 16 values per line and 4-space indent, comment-free values. Regenerating from an ImGui ranges file should preserve: `ushort[]` element type, inclusive pairs, ascending order, and the single trailing `0`.
