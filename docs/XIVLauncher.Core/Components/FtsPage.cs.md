# FtsPage.cs — Specification

**File:** `src/XIVLauncher.Core/Components/FtsPage.cs` · 92 lines · 2772 bytes
**Namespace:** `XIVLauncher.Core.Components` · **Declaration:** `public class FtsPage : Page`
**Role:** Deck "first-run" screen — a fullscreen PNG with two invisible click rectangles, plus the rule that decides whether the launcher should show it

---

## 1. Purpose

`FtsPage` (FTS = *first-time setup*) is the launcher's deck onboarding screen and, in a second mode, its **Steam-appid-error** screen. Two different assets and two different button semantics share one page, switched by `isSteamDeckAppIdError`.

It is reached from exactly one place: `LauncherApp.RunStartupTasks()` (`LauncherApp.cs:254`, method declared at `:251`) inside `#if FLATPAK`, which calls `this.ftsPage.OpenFtsIfNeeded();` — i.e. **only the Flatpak build** runs the decision logic (see §3). Construction itself happens in `LauncherApp`'s ctor (`LauncherApp.cs.md` §ctor: `new FtsPage(this)`), unconditionally, so both textures load on every platform. Usings: `System.Numerics`, `Hexa.NET.ImGui`; reads `TextureWrap` (`TextureWrap.cs.md`), `AppUtil.GetEmbeddedResourceBytes` (`AppUtil.cs.md`), `CoreEnvironmentSettings` (`CoreEnvironmentSettings.cs.md`), `Program` (`Program.cs.md`), `Environment`.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `steamdeckFtsTexture` | `private readonly TextureWrap` | ctor: `steamdeck_fts.png` |
| `steamdeckAppIdErrorTexture` | `private readonly TextureWrap` | ctor: `steamdeck_fterror.png` |
| `isSteamDeckAppIdError` | `private bool = false` | the **only** state; never reset — §4 |
| `FtsPage(LauncherApp app)` | ctor, `: base(app)` | loads both textures |
| `OpenFtsIfNeeded()` | `public void` | decides whether to enter `LauncherState.Fts`; sets the error flag |
| `FinishFts(bool save)` | `private void` | `State = Main`, optionally persists `CompletedFts = true` |
| `Draw()` | `override` | image + two invisible buttons + `base.Draw()` |

No `OnShow()` override: entering `LauncherState.Fts` calls `Page`'s empty hook (`Page.cs.md` §"OnShow contract"), which is the correct behaviour here because the *decision* lives in `OpenFtsIfNeeded`, not in the transition.

## 3. `OpenFtsIfNeeded()` — the rule table

Ordered, first match wins:

| Condition (in order) | Effect |
| --- | --- |
| `CoreEnvironmentSettings.IsDeckFirstRun` **set**, value `true` | `App.State = Fts` |
| `CoreEnvironmentSettings.IsDeckFirstRun` **set**, value `false` | `return` — nothing else is consulted |
| not set, `!(CompletedFts ?? false)` **and** `Program.IsSteamDeckHardware` | `App.State = Fts` |
| not returned yet, `IsSteamDeckHardware` **and** (`Steam == null` or `!Steam.IsValid`) | `IsIgnoringSteam ?? false` ⇒ `return`; else `App.State = Fts` **and** `isSteamDeckAppIdError = true` |

- **The env override is a hard stop, not a hint:** `CoreEnvironmentSettings.IsDeckFirstRun` (`CoreEnvironmentSettings.cs.md` — the `XIVLAUNCHER24_`-prefixed env namespace) short-circuits both the "incomplete FTS" branch **and** the Steam-error branch. A packager/distro that sets it to `false` therefore also silences the Steam-appid error screen — the reason the second branch is unreachable in that case is documented right there in the source's comment ("This fixes a bug with Steam Deck always showing the Fts Error screen").
- **`App.Settings.IsIgnoringSteam`** (`App.Settings` = `Program.Config`, see `LauncherApp.cs.md` §"Settings" / `Program.cs.md`) is read through `?? false` here, and `SettingsPage/Tabs/SettingsTabGame.cs.md` is the UI that offers it; the two must stay in step for the "skip the error screen" affordance to work.
- **`App.State = ...` from a startup task** is a state transition through `LauncherApp.State`'s setter (`LauncherApp.cs.md` §the `State` setter table): it fires `this.ftsPage.OnShow()` (empty) and, on leaving, `mainPage.ReloadNews()` — i.e. `OpenFtsIfNeeded` runs while `State == Main` and the assignment is a Main→Fts transition. It runs before the first frame (`RunStartupTasks` is pre-frame-loop, `LauncherApp.cs.md` §startup order).
- **`App.State = LauncherApp.LauncherState.Fts`** — the qualified `LauncherApp.LauncherState` rather than bare `LauncherState`: needed because this file's namespace is `XIVLauncher.Core.Components`, where `LauncherState` also resolves through the enclosing namespace; the qualification is not cosmetic when editing (the same pattern appears in `Common/Input.cs.md`'s `Program.Steam` and in `SteamDeckPromptPage.cs.md`'s sibling usage).

## 4. `isSteamDeckAppIdError` — one-way mode switch

Set only in `OpenFtsIfNeeded`'s last branch, **never cleared** anywhere (no `Reset()`, no `OnShow()` override, and `OpenFtsIfNeeded` runs once per process since `RunStartupTasks` is called once — `LauncherApp.cs.md` §startup order). Consequences:

- Normal (non-error) FTS and error FTS cannot both appear in one run; the mode is sticky for the whole process.
- Because `OpenFtsIfNeeded` is the only setter, re-entering `LauncherState.Fts` (nothing in the repo does, but `MainPage` or a future page could) re-uses the previously decided mode.
- The flag gates **both** draw behaviour and the two buttons (§5): in error mode the "finish" button is inert and the "open guide" button **exits the process**. Any new exit path added to this class must respect that the flag may be stale on a later visit.

## 5. `Draw()` — invisible hit rectangles over a fixed-geometry image

```
SetCursorPos(0,0)
Image(isSteamDeckAppIdError ? fterror : fts, size = (1280, 800))
PushStyleColor(Button, Zero); PushStyleColor(ButtonHovered, Zero); PushStyleColor(ButtonActive, Zero)
SetCursorPos(316, 481)   Button("###openGuideButton", 649×101)  → guide / exit
SetCursorPos(316, 598)   Button("###finishFtsButton", 649×101) & !error → FinishFts(true)
PopStyleColor(3)
base.Draw()
```

- **The image is stretched to a hardcoded 1280×800** at `(0,0)` (contrast `Background.cs.md`, which draws at native size bottom-left): the geometry assumes a Steam Deck viewport. On a different viewport the image is scaled (distorted, not letterboxed) while the two button rectangles **keep their absolute pixel coordinates**, so the click areas drift relative to the artwork they are supposed to cover. This is the page's known deck-only assumption; any resize support must convert `316/481/598/649/101` and the `1280×800` to viewport-relative values inside this `Draw()`.
- **Buttons are invisible by construction:** `ImGuiCol.Button`/`ButtonHovered`/`ButtonActive` are pushed to `Vector4.Zero` (`System.Numerics`, so the literal is available) for exactly those two `ImGui.Button` calls, then popped with `PopStyleColor(3)`. No `Label` text is drawn because the id-split form `"###openGuideButton"` makes the display text empty (`###` convention — see `Component.cs.md` §5 and `Common/Button.cs.md` §"Label as ImGui id"): the visible affordance is entirely in the PNG, and the rectangles are the invisible click targets. Note this page uses **raw `ImGui.Button`**, not `Common/Button.cs.md`: the click handler is inline in the draw, so there is no `event Click` to compose onto.
- **`ImGui.Button(...)` returns true on click**, and the second condition `&& !isSteamDeckAppIdError` is short-circuit *after* the click: in error mode, clicking the finish rectangle still registers a button press (and its style state) but performs no action. The first rectangle's branch, by contrast, is inside the `if`, so the mode check happens post-click. Keep these two shapes distinct when editing.
- **Error mode's guide button calls `Environment.Exit(0)`** — process termination from inside the draw pass, with status 0. It bypasses everything (no SDL teardown, no config save, no `LauncherApp` shutdown path); see `LauncherApp.cs.md` §shutdown for what the normal quit path does and why an `Exit(0)` here does not run it. It is also the *only* way out of error mode (finish is disabled), so the deck shows the error image until the user presses that rectangle or kills the process.
- **Guide button in normal mode calls `AppUtil.OpenBrowser("https://goatcorp.github.io/faq/steamdeck")`** (`AppUtil.cs.md` §OpenBrowser: Steam overlay web page on deck, `PlatformHelpers.OpenBrowser` otherwise) — non-blocking, and the page stays on FTS afterwards: there is no "guide opened" state, so the user returns via the finish rectangle.
- **`FinishFts(true)` is the only exit from normal FTS:** it sets `App.State = LauncherApp.LauncherState.Main` (fires `mainPage.OnShow()` and the previous-state side effect in `LauncherApp.cs.md` §"the `State` setter") and, because `save` is `true`, writes `App.Settings.CompletedFts = true`, which the second row of §3 reads back on the next launch. A `FinishFts(false)` call would leave the screen re-triggerable on the next start — currently unreachable from this file (the argument is always `true`), so treat `save` as reserved for a future "skip without completing" flow.
- `PopStyleColor(3)` matches the three pushes, and no `PushStyleVar` appears: the page is style-neutral outside those three colour entries, keeping `LauncherApp.Draw()`'s own push/pop balance (`LauncherApp.cs.md` §8) intact.
- `base.Draw()` last (`Page.cs.md`): children would paint over the image; none exist.

## 6. Invariants and hazards

- **Both button id strings are package-level constants** with no `###`-id uniqueness from `Component.Id` (`Component.cs.md` §5): they collide with any other page using the same names — none currently do (grep). Prefer `Component.Id`-suffixed ids if a second FTS-like instance is ever created.
- **`FtsPage` never reads `ImGuiHelpers.ViewportSize`** (unlike `LoadingPage.cs.md`, `MainPage/NewsFrame.cs.md`, `MainPage/LoginFrame.cs.md`); all geometry is absolute. Deck-only, see §5.
- **No localization anywhere:** the two assets are pre-rendered art with baked-in text (`Resources/steamdeck_fts.png`, `Resources/steamdeck_fterror.png`, listed in `AppUtil.VersionInfo.cs.md`'s asset discussion) and there are no `Strings.*` keys for this page. Localizing it means changing this file to draw art + text separately.
- **`CompletedFts` is a persisted config flag** (declared `bool? CompletedFts { get; set; }` in `src/XIVLauncher.Core/Configuration/ILauncherConfig.cs:11`, defaulted by `Program.cs:116` `Config.CompletedFts ??= false`, stored in `launcher.ini` — see `Program.cs.md` §persistence). It has **no settings UI**: the only ways to change it are this page's `FinishFts(true)`, the ini file, and the `CoreEnvironmentSettings.IsDeckFirstRun` env override (`CoreEnvironmentSettings.cs.md`). `IsIgnoringSteam`, by contrast, *does* have UI: the `SettingsPage/Tabs/SettingsTabGame.cs.md` entry at `:38`, and `Program.cs:121` also derives it from the `UseSteam` env var (`CoreEnvironmentSettings.cs.md`) — read both before changing either interaction.
- **Textures load at app construction, not at page entry** (`TextureWrap.cs.md` §upload timing, `LauncherApp.cs.md` §ctor ordering): a missing asset name in either ctor line is an `ArgumentException` from `AppUtil.GetEmbeddedResourceBytes` during startup (`AppUtil.cs.md` §GetEmbeddedResourceBytes), which kills the launcher on **every** platform, including non-deck ones where this page is never shown. If a non-deck build should tolerate a missing deck asset, the ctor must become lazy (load on first `Draw()`/`OnShow()`), which is the only safe place for that decision.

## 7. Referenced / referenced-by

Constructed by: `LauncherApp.cs.md` ctor (`new FtsPage(this)`); driven by `LauncherApp.RunStartupTasks` (`#if FLATPAK`, `LauncherApp.cs:254`) and `LauncherApp.CurrentPage` (`LauncherState.Fts`).
Uses: `Page` (`Page.cs.md`), `TextureWrap.cs.md`, `AppUtil.cs.md` (embedded bytes, browser), `CoreEnvironmentSettings.cs.md` (`IsDeckFirstRun`, `IsSteamCompatTool` family), `Program.cs.md` (`IsSteamDeckHardware`, `Steam`), `LauncherApp.cs.md` (`State`, `Settings`).
Assets: `steamdeck_fts.png`, `steamdeck_fterror.png` (under `src/XIVLauncher.Core/Resources/`; the sibling asset `steamdeck_switchprompt.png` belongs to `SteamDeckPromptPage.cs.md`).
