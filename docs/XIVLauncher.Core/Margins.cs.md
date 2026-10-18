# Margins.cs — Specification

**File:** `src/XIVLauncher.Core/Margins.cs` · 17 lines · 375 bytes
**Namespace:** `XIVLauncher.Core` · **Declaration:** `public struct Margins`
**Role:** Layout primitive — four-sided padding for ImGui windows/regions

---

## 1. Purpose

A value type carrying `left/top/right/bottom` insets in pixels, used when laying out ImGui elements (page padding, framed content areas, image insets) so that call sites can pass one object instead of four integers.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `Left`, `Top`, `Right`, `Bottom` | `int`, get/set | four independent, unclamped pixel insets |
| `Margins(int left, int top, int right, int bottom)` | constructor | positional, **declaration-order** convention (`L, T, R, B`), matching CSS edge order |

Because the type is a `struct`, it is a value type: assignment copies, `==` compares by value (field-wise) for the built-in comparison, and it can be used as a `const`-style literal via `new(0, 0, 0, 0)`.

## 3. Contract

- **Units:** device pixels (same space as ImGui window sizes / cursor positions), not scaled floats. ImGui's own padding is `float`; callers converting to `float` must do so themselves.
- **Sign:** negative insets are permitted (content grows outward); nothing validates a non-negative range.
- **No semantics attached to which edge carries which value** — it is purely geometric.
- **No `Width`/`Height`/`TotalX` helpers**: callers compute `Right + Left` themselves. Add such accessors if the pattern spreads, rather than duplicating arithmetic in each page.
- **No default/uniform convenience ctor** (`Margins(int all)`) and no `Parse`/`FromStyle` helper; `ImGui.GetStyle().WindowPadding` (a `Vector2`) is the fallback used elsewhere in the project.
- **Unmanaged:** no `==`/`Equals`/`HashCode` overrides are declared, so identity comparison uses implicit value semantics — two instances with equal edges are interchangeable, including as map keys.

## 4. Usage shape

```csharp
var m = new Margins(16, 24, 16, 24);            // L T R B
var w = viewport.X - m.Left - m.Right;          // caller does the arithmetic
```

## 5. Referenced by

- `Components/Component.cs` — the base `Component` class exposes `public Margins Margins { get; set; } = new();`, so every component inherits a zeroed `Margins` that pages may set per-component.
- Not used for page padding: `Page.Padding` is a `Vector2` (e.g. `MainPage.cs` sets `new Vector2(32f, 32f)`), consumed by `LauncherApp.Draw()` as `ImGuiStyleVar.WindowPadding`. `Margins` and `Padding` are therefore parallel but distinct layout concepts — `Margins` is four-sided, `Padding` is two-sided.
- Sub-directory details are deliberately out of scope for this spec pass.
