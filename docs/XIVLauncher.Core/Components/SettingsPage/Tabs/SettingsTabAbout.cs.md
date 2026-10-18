# SettingsTabAbout.cs — Specification

**File:** `src/XIVLauncher.Core/Components/SettingsPage/Tabs/SettingsTabAbout.cs` · 52 lines · 1661 bytes
**Namespace:** `XIVLauncher.Core.Components.SettingsPage.Tabs` · **Declaration:** `public class SettingsTabAbout : SettingsTab`
**Role:** The "About" tab — logo, version string, contributor credit, repo/Discord/license links; no settings rows

---

## 1. Purpose

`SettingsTabAbout` is a tab that carries **no entries**: `Entries` is `Array.Empty<SettingsEntry>()` and everything visible is hand-drawn in `Draw()`. It pairs with `Tabs/SettingsTabDebug.cs.md` (also entry-less) as the two "informational tabs" in the package, and it is the tab whose job is to be a link farm rather than a settings surface.

## 2. Surface

| Member | Anchor | Notes |
| --- | --- | --- |
| `logoTexture` | `:12` | `private readonly TextureWrap` — loaded in the ctor, never reloaded |
| `Entries` | `:14` | `public override SettingsEntry[] => Array.Empty<SettingsEntry>()` |
| `Title` | `:16` | `Strings.AboutTitle` |
| ctor | `:18-:21` | `public SettingsTabAbout()` — loads `logo.png` (`:20`) |
| `Draw()` | `:23-:51` | `override`, `base.Draw()` **last** (`:50`) |

Uses: `docs/XIVLauncher.Core/TextureWrap.cs.md` (`Load`, `ImGuiHandle`), `docs/XIVLauncher.Core/AppUtil.cs.md` (`GetEmbeddedResourceBytes`, `GetAssemblyVersion`, `GetGitHash`, `OpenBrowser`), `docs/XIVLauncher.Core/AppUtil.VersionInfo.cs.md`, `docs/XIVLauncher.Core/ImGuiHelpers.cs.md` (`GlobalScale`, `CalcTextSize`), `lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Util/PlatformHelpers.cs` (`OpenBrowser`), `src/XIVLauncher.Core/Resources/Localization/Strings.Designer.cs`.

## 3. Construction — the texture is uploaded once

`SettingsTabAbout()` is constructed by `SettingsPage.cs.md` §tabs (`private readonly SettingsTab[] tabs = { … }` at `:21 new SettingsTabAbout()`), itself built during `LauncherApp.cs:135`. `TextureWrap.Load(AppUtil.GetEmbeddedResourceBytes("logo.png"))` therefore runs once per process, during `LauncherApp` construction — a GPU/texture resource acquired at page-construction time, outside any frame. Consequences: `logoTexture` is never `null` (no lazy path exists); there is no teardown, so the wrap lives for the process lifetime (see `docs/XIVLauncher.Core/TextureWrap.cs.md` §lifecycle for the release model); and renaming the resource means editing the `GetEmbeddedResourceBytes("logo.png")` key and the embedded resource together, not a draw-guard.

## 4. `Draw()` sequence

| Step | Anchor | What |
| --- | --- | --- |
| Logo | `:25` | `ImGui.Image(logoTexture.ImGuiHandle, new Vector2(256) * ImGuiHelpers.GlobalScale)` |
| Version line | `:27` | `ImGui.Text($"XIVLauncher Core v{AppUtil.GetAssemblyVersion()}({AppUtil.GetGitHash()})")` |
| Credit | `:29-:32` | `string.Format(Strings.XLCoreCreatedBy, "goaaats, Blooym, rankynbass")` then `ImGui.Selectable(contribText, false, default, ImGui.CalcTextSize(contribText))` → `AppUtil.OpenBrowser("…/graphs/contributors")` |
| Spacing | `:33` | `ImGui.Dummy(new Vector2(20))` |
| Repo button | `:35-:38` | `Button(Strings.OpenRepositoryButton)` → `AppUtil.OpenBrowser("https://github.com/goatcorp/XIVLauncher.Core")` |
| Discord button | `:40-:43` | `ImGui.SameLine()` then `Button(Strings.JoinDiscordButton)` → `AppUtil.OpenBrowser("https://discord.gg/3NMcUV5")` |
| License button | `:45-:49` | `ImGui.SameLine()` then `Button(Strings.SeeSoftwareLicensesButton)` → `PlatformHelpers.OpenBrowser(Path.Combine(AppContext.BaseDirectory, "license.txt"))` |
| `base.Draw()` | `:50` | `SettingsTab.Draw()` over an empty `Entries` (then `Component.Draw()` — see `SettingsTab.cs.md` §Draw/§base) |

- **The credit row's hit area uses the `CalcTextSize` width trick** — `Selectable(text, default, ImGui.CalcTextSize(text))` makes the whole line clickable instead of just the trailing hit-mark (the same trick recorded in `MainPage/AccountSwitcher.cs.md` §width trick and `MainPage/ActionButtons.cs.md` §tooltip adjacency). Keep the size argument if the credit row is restyled: without it the row looks like a paragraph and behaves like a 1-pixel target.
- **The contributor names are a hardcoded string literal** (`:29`, `"goaats, Blooym, rankynbass"` as the format argument of `Strings.XLCoreCreatedBy`), while everything else in the tab is a `Strings` key. The link goes to the GitHub contributors graph, so the literal is a decorative, deliberately-stale credit; the format key is the localizable part. Do not "move the names into the key" without checking the key's `{0}` placeholder (`src/XIVLauncher.Core/Resources/Localization/Strings.Designer.cs`, `XLCoreCreatedBy`).
- **Buttons are laid out with `ImGui.SameLine()` between them** (repo → Discord → license on one row), the same flow-control idiom as `MainPage.cs.md` §4 and `SettingsPage.cs.md` §5.1/§5.4; the row's content width is `Strings`-dependent, so the three buttons can wrap on a narrow viewport — that's ImGui's behaviour, not something to guard here.
- **`base.Draw()` is last** (contrast `Tabs/SettingsTabGame.cs.md`, `Tabs/SettingsTabDebug.cs.md`, `Tabs/SettingsTabTroubleshooting.cs.md`, `Tabs/SettingsTabWine.cs.md` which put it first/earlier). With `Entries` empty, position is irrelevant *today* but the choice still records intent ("rows, if ever added, appear after the links"). If rows are added to this tab, decide the order explicitly per `SettingsTab.cs.md` §position list.

## 5. The two browser helpers

Three buttons use `AppUtil.OpenBrowser(url)` and the license button uses `PlatformHelpers.OpenBrowser(path)`. `AppUtil.cs.md` §`OpenBrowser` is the Steam-Deck-aware wrapper (`Program.Steam.ActivateGameOverlayToWebPage(url)` when the Deck + Steam are live) and it *delegates* to `PlatformHelpers.OpenBrowser` otherwise. The direct call here is therefore deliberate: `license.txt` is a local path opened through the OS file handler, which the overlay cannot express (`AppContext.BaseDirectory` is the *executable's* directory, i.e. the packaged/installed tree — on a Flatpak build that is the app root, see `docs/XIVLauncher.Core/Program.cs.md` §packaging and `AppUtil.cs.md` §`GetEmbeddedResourceBytes`). Keep the split; routing the license button through `AppUtil.OpenBrowser` would hand a path to `ActivateGameOverlayToWebPage`.

## 6. Invariants and extension points

- **No entries ⇒ this tab cannot affect `SettingsPage.cs.md` §finish button or search mode**: it contributes a tab header in browse mode (`SettingsPage.cs.md` §5.1) and nothing in search results (`SettingsEntry.cs.md` §Name filter has no rows to match). A "search finds nothing in About" complaint is a consequence of this, not a bug to fix in the tab.
- **Hardcoded URLs are the tab's literals** (`:31` contributors graph, `:37` repo, `:42` Discord). Labels are localized, targets are not — the package keeps links in code (contrast `Tabs/SettingsTabTroubleshooting.cs.md`, where the actions are local code paths). Adding a link: literal URL + `Strings` label + one of the two browser helpers per §5.
- **The version string is read per frame** (`GetAssemblyVersion()`/`GetGitHash()`), so it follows the build stamped at process start (see `docs/XIVLauncher.Core/AppUtil.VersionInfo.cs.md` §source of the stamp, `AppUtil.cs.md` §`GetGitHash`). That is the mechanism for "what build am I running" reports; do not add a re-read on a timer — the value is build-constant.
- **`Entries` uses the accessor-block form (`=> Array.Empty<SettingsEntry>()`, a classic accessor)** matching `SettingsTab.cs.md` §Entries override style; `Tabs/SettingsTabDebug.cs.md` uses the same. Keep that form for entry-less tabs (rather than `get { return … }` blocks) so the tab specs stay diff-able.
