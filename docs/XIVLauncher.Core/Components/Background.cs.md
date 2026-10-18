# Background.cs — Specification

**File:** `src/XIVLauncher.Core/Components/Background.cs` · 22 lines · 541 bytes
**Namespace:** `XIVLauncher.Core.Components` · **Declaration:** `public class Background : Component`
**Role:** The fullscreen backdrop image, drawn beneath every page

---

## 1. Purpose

`Background` paints one embedded PNG (`bg_logo.png`) as the launcher's wallpaper. It is instantiated by `LauncherApp` as `private readonly Background background = new();` and drawn inside the separate, first fullscreen ImGui window (`LauncherApp.cs.md` §8, the `"Background"` window with `ImGuiWindowFlags.NoBackground`) — which is why the content window can carry a 0.75 alpha background and still show this image underneath.

Usings: `System.Numerics`, `Hexa.NET.ImGui` (plus implicit project-wide `AppUtil`, `TextureWrap`, `Component` from the `XIVLauncher.Core` namespace).

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `bgTexture` | `private TextureWrap` | no initializer; assigned in ctor → definite-assignment style field |
| `Background()` | `public` ctor, **no parameters, no explicit `: base(...)`** | `TextureWrap.Load(AppUtil.GetEmbeddedResourceBytes("bg_logo.png"))` |
| `Draw()` | `override` | positions the image, draws it, then `base.Draw()` |

The ctor is `public` (unlike `Page`'s `protected` one) — `Background` is a plain `Component`, so it can be created anywhere; in practice it exists exactly once, as a `LauncherApp` field initializer.

## 3. Construction contract

- `AppUtil.GetEmbeddedResourceBytes("bg_logo.png")` reads a resource embedded in the built binary (see `AppUtil.cs.md`); the name is a **string literal, compile-time unchecked**: renaming `Resources/binaries`-adjacent assets or the PNG itself yields a runtime failure at app startup, not a build error. The asset lives under `src/XIVLauncher.Core/Resources/` alongside the fonts/icon assets used by `SettingsTabAbout` (`logo.png`) and `SteamDeckPromptPage` (`steamdeck_switchprompt.png`).
- `TextureWrap.Load` decodes and uploads to the GPU device immediately (`TextureWrap.cs.md`), so the cost is paid once, during `LauncherApp` construction — inside the `"Background"` window's owner, before the first frame.
- `bgTexture` is not `readonly`: the field is reassignable in principle but nothing reassigns it; treat the handle as lifetime-long. ImGui handles are only valid for the lifetime of the `TextureWrap`, so **do not** let it be garbage-reclaimed or re-loaded per frame.
- `new Background()` runs as a field initializer on `LauncherApp`; field initializers run before the ctor body, so the texture is loaded before the killswitch check and before `RunStartupTasks()` (`LauncherApp.cs.md` §4/§5) — an image failure therefore precedes the modal system.

## 4. `Draw()` geometry

```csharp
ImGui.SetCursorPos(new Vector2(0, ImGuiHelpers.ViewportSize.Y - bgTexture.Height));
ImGui.Image(bgTexture.ImGuiHandle, new Vector2(bgTexture.Width, bgTexture.Height));
base.Draw();
```

- The cursor is set to the **bottom-left** corner of the window: `x = 0`, `y = viewportHeight − imageHeight`. `ImGuiHelpers.ViewportSize` is a live accessor (`ImGuiHelpers.cs.md` §"Both are accessors"), so the image tracks window resizes immediately — correct mid-frame.
- `ImGui.Image` is given the image's **native size** (`Width`, `Height`), not a viewport-relative size and not scaled by `ImGuiHelpers.GlobalScale`. The wallpaper is therefore unscaled: on a Steam Deck 1280×800 window it fills the frame only if the asset is 1280×800; on a larger monitor it stays anchored bottom-left and leaves empty space above/right, and on a window shorter than the image the top is clipped by the window and the negative `y` pushes the top off-screen.
- This is the intended contrast with the other full-bleed images in the package (`FtsPage.cs.md`, `SteamDeckPromptPage.cs.md`), which pass a *fixed* `new Vector2(1280, 800)` and thus stretch.
- The image is drawn into whichever window is current when called: `LauncherApp.Draw()` opens `"Background"` first, so it is window-scoped to that fullscreen, `NoBackground`/`NoScrollbar` window. If `Background.Draw()` were called inside another window, clipping would follow that window instead.
- `ImGui.Image` registers an item, so `ImGui.IsItemClicked`-style hit tests issued right afterwards would target the image; `Background` performs none.
- `base.Draw()` runs last, so any children would paint above the image (in this project the collection is empty — `Component.cs.md` §2).

## 5. Interaction with the rest of the draw order

`LauncherApp.Draw()`'s sequence is: background window (this component) → content window (`CurrentPage.Draw()` with `SetNextWindowBgAlpha(0.75f)` and `ImGuiCol.WindowBg = ImGuiColors.BlueShade0`) → `DrawModal()`. `Background` is thus the only component drawn *outside* the content window and the only one that can be seen through the content window's 0.75 alpha — that contrast is the visual contract, so changing either the alpha (in `LauncherApp`) or the image's own opacity changes the perceived theme. The theme's background colour pairing is documented in `ImGuiColors.cs.md` (`BlueShade0`).

## 6. Invariants and hazards

- **No `Enabled` gate, no DPI scaling, no aspect handling:** the class assumes "one asset, one size". Any future "fit to window"/"cover" behaviour must come from this file — the geometry is entirely local.
- **No push/pop of ImGui style state:** `Draw()` is style-neutral, which is required because the enclosing `LauncherApp.Draw()` balances its own `PushStyleVar(2)`/`PopStyleVar(2)` around the window (`LauncherApp.cs.md` §8 note on push/pop balance). Do not add unbalanced pushes here.
- The whole component is stateless beyond the texture handle, so it is trivially re-drawable every frame; nothing about it depends on `OnShow()`, and it never participates in page transitions.
- If the asset were replaced by a transparent image, this file still occupies the whole first window — the backdrop window exists regardless of content. There is no fallback draw (e.g. solid colour), so an image failure is a black or garbage backdrop rather than a visible error.
- `bg_logo.png` is not referenced by any other file, so the asset name appears only here; a rename must be paired with the resource embedding list, not just this string.

## 7. Referenced / referenced-by

Referenced by: `LauncherApp.cs.md` (field `background`, drawn in §8).
Uses: `Component` (`Component.cs.md`), `TextureWrap` (`TextureWrap.cs.md`), `AppUtil.GetEmbeddedResourceBytes` (`AppUtil.cs.md`), `ImGuiHelpers.ViewportSize` (`ImGuiHelpers.cs.md`), `Hexa.NET.ImGui` cursor/image calls.
