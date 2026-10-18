# SteamDeckPromptPage.cs — Specification

**File:** `src/XIVLauncher.Core/Components/SteamDeckPromptPage.cs` · 25 lines · 578 bytes
**Namespace:** `XIVLauncher.Core.Components` · **Declaration:** `public class SteamDeckPromptPage : Page`
**Role:** One fullscreen PNG, nothing else — the "how do I get back to the launcher?" card shown on a Steam Deck after the game launches

---

## 1. Purpose

`SteamDeckPromptPage` displays `Resources/steamdeck_switchprompt.png` and offers **no interaction**. The launcher deliberately hides its window when the game starts on a Deck (`MainPage.cs:762` `Hide()`, guarded by `!Program.IsSteamDeckHardware || CoreEnvironmentSettings.IsSteamCompatTool`, else `App.State = LauncherApp.LauncherState.SteamDeckPrompt` at `MainPage.cs:766`), and this page is what the user sees before the hide takes effect: an explanation of how to return to the launcher (deck overlay / desktop switching).

It is the smallest page: constructed once in `LauncherApp`'s ctor (`LauncherApp.cs:139`, `LauncherApp.cs.md` §ctor), selected by `LauncherApp.CurrentPage` for `LauncherState.SteamDeckPrompt` (`LauncherApp.cs:103`), and reached only from `MainPage.cs.md`'s launch path. Usings: `System.Numerics`, `Hexa.NET.ImGui` — `AppUtil` and `TextureWrap` need no import because this file's namespace (`XIVLauncher.Core.Components`) is nested inside `XIVLauncher.Core`, where both are declared (the same reason `FtsPage.cs.md` compiles without those imports).

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `switchPromptTexture` | `private readonly TextureWrap` | ctor: `TextureWrap.Load(AppUtil.GetEmbeddedResourceBytes("steamdeck_switchprompt.png"))` |
| `SteamDeckPromptPage(LauncherApp app)` | ctor | `: base(app)` (`Page.cs.md` §protected ctor) + texture load |
| `Draw()` | `override` | cursor → `(0,0)`, image at `1280×800`, `base.Draw()` |

No fields beyond the texture: there is **no state at all** — nothing is written after construction, so the class is stateless and cannot be out of sync. There is no `Reset()` (contrast `FtsPage.cs.md`, `OtpEntryPage.cs.md`, `LoadingPage.cs.md`), and `OnShow()` is not overridden, so `LauncherApp.State` setter's `LauncherState.SteamDeckPrompt` branch (`LauncherApp.cs:87`) runs `Page`'s empty hook (`Page.cs.md` §"OnShow contract"): entering this page performs no work whatsoever.

## 3. `Draw()`, line by line

```
ImGui.SetCursorPos(new Vector2(0));
ImGui.Image(switchPromptTexture.ImGuiHandle, new Vector2(1280, 800));
base.Draw();
```

- **`SetCursorPos((0,0))` is mandatory, not cosmetic:** the draw cursor is inherited from whatever the *previous* component left (`Page.cs.md` §"Draw as a chain": `CurrentPage` is drawn inside `LauncherApp`'s content window, after `Background.cs.md` and possibly after other pages touched the cursor). Without it the prompt would be drawn at an arbitrary offset.
- **The size `1280×800` is a hardcoded fit to the Steam Deck portrait viewport** (the same literal pair appears in `FtsPage.cs.md` §5). It is *not* `ImGuiHelpers.ViewportSize` (`ImGuiHelpers.cs.md`), so on any other resolution the art is scaled to a fixed pixel box — stretched or cropped, never letterboxed. Deck-only page, deck-only geometry: keep the two constants in sync with `FtsPage.cs.md` if a viewport-relative variant is ever introduced (neither file exposes a shared helper; `ImGuiHelpers.cs.md` §helpers lists no scaled-image helper).
- **No `PushClipRectFullViewport()`** here, unlike `Background.cs.md` §"clipping": the image is drawn inside the content window that `LauncherApp.Draw()` opens (`LauncherApp.cs.md` §8), and it is not clipped to the viewport. Because it is drawn *first* in the page chain, overflow is covered by later draws in most cases, but on a viewport narrower than 1280 px the prompt art spills beyond the window edge — visible spill, harmless but a known artefact (`Background.cs.md` is the reference fix: push/pop `PushClipRectFullViewport()` around the draw).
- **`base.Draw()` last** (`Page.cs.md`): children would overlay the prompt. None exist, and this page never adds children — unlike `MainPage.cs.md`, which opens child windows inside its `Draw()`, so nothing on this page is clickable. There is **no way to leave this page from the page itself**: see §4.
- **No style push/pop pairs:** the page contributes no colour or style vars, so `LauncherApp.Draw()`'s own theme stack (`LauncherApp.cs.md` §8) is untouched — adding `PushStyleColor` here requires the matching pop before `base.Draw()` (see `Component.cs.md` §"push/pop discipline" for the pattern the leaf components use).

## 4. Exit paths and the "launcher hidden" window of visibility

The page's `State` is entered at `MainPage.cs:766` and nothing in this file can leave it, so the exit is entirely `LauncherApp`-side. Read `LauncherApp.cs.md` §"the `State` setter" and §shutdown for the quit paths; the important constraint for this file is:

- The launcher **hides** its window in the sibling branch (`MainPage.cs:762`), so on Deck the visible sequence is: prompt page drawn (one or more frames) → window hidden → game running. Because hiding happens *after* the state assignment in the same block, the prompt is guaranteed at most to be drawn on the frame that the state change lands on; there is no deliberate "show the prompt for N seconds" timer here and none should be added without changing `MainPage.cs`'s launch sequence (`MainPage.cs.md`).
- The page is never reset or re-used, so it can be visited twice only if `MainPage.cs` transitions to `LauncherState.SteamDeckPrompt` twice — currently it does not (`MainPage.cs:766` is the only write of that state anywhere; `LauncherApp.cs:103` only reads it). Consequence: this class stays free of state; if a second visit ever becomes possible (e.g. a manual "show prompt again" affordance), any state added here must be reset via an `OnShow()` override, per `Page.cs.md` §"OnShow contract".
- `CoreEnvironmentSettings.IsSteamCompatTool` (`CoreEnvironmentSettings.cs.md`) is the switch that sends Deck users down the `Hide()` branch instead — that decision lives in `MainPage.cs`, not here, and this page has no knowledge of compatibility tools. Do not re-test that flag in this file: it would duplicate the decision and desynchronize when `Program.cs` (`Program.cs.md` §env handling) changes how the flag is derived.

## 5. Asset and startup constraints

- **Asset name is a single string literal** `"steamdeck_switchprompt.png"`, looked up by `AppUtil.GetEmbeddedResourceBytes` (`AppUtil.cs.md` §GetEmbeddedResourceBytes: `ArgumentException` on unknown name — see the file listing rules there). The file lives at `src/XIVLauncher.Core/Resources/steamdeck_switchprompt.png`.
- **The load happens at app construction**, in the ctor, unconditionally — including on non-Deck platforms where this page is never drawn. Renaming the asset, deleting it, or adding a platform-conditional copy therefore breaks **startup everywhere**, exactly as it does for the two `FtsPage.cs.md` textures. If a deck-only asset should be optional, the load has to move into `OnShow()`/first `Draw()` in this file (`Page.cs.md` §"OnShow contract" is the hook that fires on entry).
- **`TextureWrap` upload cost:** `TextureWrap.cs.md` §upload timing covers the GL upload of the decoded PNG; this texture is the largest single image on this page and is uploaded once per process. The two `FtsPage.cs.md` textures upload alongside it at the same moment — three deck-only textures loaded on every desktop launch, an accepted cost today.
- **`ImGuiHandle` access is the only ImGui-visible member** (`TextureWrap.cs.md` §surface); this page reads it every frame and never checks for a null handle, which is safe only while the wrap outlives the frame — guaranteed here because the wrap is a `readonly` field of a page held by `LauncherApp` (`LauncherApp.cs:121` field, alive for the process lifetime).

## 6. Referenced / referenced-by

Constructed by: `LauncherApp.cs.md` ctor (`new SteamDeckPromptPage(this)`, `LauncherApp.cs:139`); selected via `LauncherApp.CurrentPage` (`LauncherApp.cs:103`) and `LauncherApp.State` setter (`LauncherApp.cs:87`).
Entered by: `MainPage.cs.md` (`MainPage.cs:766`, guarded by `MainPage.cs:762` `Hide()` / `CoreEnvironmentSettings.IsSteamCompatTool`).
Uses: `Page` (`Page.cs.md`), `Component` (`Component.cs.md`), `TextureWrap.cs.md`, `AppUtil.cs.md` (embedded bytes), `ImGui.Image` (Hexa binding).
Sibling assets: `FtsPage.cs.md` (`steamdeck_fts.png`, `steamdeck_fterror.png`).
