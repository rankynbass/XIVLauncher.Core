# StyleModelV1.cs — Specification

**File:** `src/XIVLauncher.Core/Style/StyleModelV1.cs` · 435 lines · 17923 bytes
**Namespace:** `XIVLauncher.Core.Style` · **Declaration:** `public class StyleModelV1 : StyleModel`
**Role:** The concrete Dalamud theme — 28 ImGui style fields + a colour dictionary, two compile-time theme constants, and the snapshot/restore pair (`Get()`/`Apply()`) against ImGui's global style

---

## 1. Purpose

`StyleModelV1` is the only theme the fork can render. Its `Apply()` overwrites ImGui's **global** style (the single `ImGuiStyle` struct behind `ImGui.GetStyle()`), and it is invoked once during startup — `../Program.cs.md` §6, step 8 inside the `unsafe` block (`Program.cs:315`), right after `ImGuiBindings.cs.md` is constructed (`:312`) and before `launcherApp` exists (`:318`). Everything in `../Components/` that pushes style deltas (`PushStyleVar`/`PushStyleColor`, see e.g. `../Components/Common/Button.cs.md`) writes *relative to the values set here*.

Usings: `System.Numerics` (`Vector2`/`Vector4`), `Hexa.NET.ImGui` (`ImGui`, `ImGuiCol`, `ImGuiDir`), `Newtonsoft.Json` (`[JsonProperty]`), plus the sibling `StyleModel.cs.md` base.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `private StyleModelV1()` | ctor (`:17`) | `Colors = new Dictionary<string, Vector4>()`; `Name = "Unknown"`; **never callable from outside the class** (see §4) |
| `DalamudStandard` | `public static StyleModelV1` field-like member, `=> new() { … }` (`:26`–`:127`) | 28 style values + **64** colour keys (`:60`–`:126`, keys at `:62`–`:125`) |
| `DalamudClassic` | same shape (`:132`–`:223`) | 28 style values (near-identical) + **55** colour keys (`:165`–`:222`, keys at `:167`–`:221`) |
| `SerializedPrefix` | `public static string => "DS1"` (`:228`) | never read anywhere — see `StyleModel.cs.md` §3 |
| 28 style members | `public T { get; set; }` (`:233`–`:314`) | `float`/`Vector2`/`ImGuiDir`, each `[JsonProperty(...)]` — see §5 |
| `Colors` | `public Dictionary<string, Vector4> { get; set; }`, `[JsonProperty("col")]` (`:322`) | keys are **`ImGuiCol` member names as strings** |
| `Get()` | `public static StyleModelV1` (`:328`–`:375`) | snapshot of the live ImGui style into a fresh model |
| `Apply()` | `public override void` (`:380`–`:422`) | writes the model onto the live ImGui style |
| `Push()` / `Pop()` | `public override void` (`:425`/`:431`) | **`throw new NotImplementedException()`** at `:427`/`:433` — see §6 |

## 3. Theme constants and the `new()` idiom

`new() { Name = "Dalamud Standard", … }` is the C# 13 shorthand for `new StyleModelV1() { … }` combined with designated-initialiser syntax. Two consequences:

* `new()` resolves to the class's **own private ctor** — legal because the initialisers sit inside the class, and this is precisely why that ctor is `private`: the theme constants are constructible only from within this file, so no other module can mint a `StyleModelV1` with the *wrong* defaults. `Get()` (`:330`) constructs `new StyleModelV1()` the long way, also inside the class.
* The designated initialisers assign `Name` and every style property, but **not** `Version` (so `Version == 0`) and **not** `Colors` as a whole — `Colors` is assigned a *new dict literal* in the initialiser list, overwriting what the ctor allocated. The ctor's `Colors = new Dictionary…` is therefore only observable for instances built by `Get()` (which also overwrites it at `:362`) — i.e. the ctor initialisation is defensive, not load-bearing.

`Name` is the only real discriminator between the two themes at runtime (`"Dalamud Standard"`/`"Dalamud Classic"`), and no code branches on it: `DalamudClassic` has **zero references** in the fork, so the theme choice is a compile-time one and the classic palette is a dead-but-editable constant.

## 4. Colour dictionaries — the load-bearing part

Both dicts are keyed by `ImGuiCol` **member-name strings**, because `Apply()`/`Get()` bridge an enum to a stringly-keyed dict:

```csharp
foreach (var imGuiCol in Enum.GetValues<ImGuiCol>())
{
    if (imGuiCol == ImGuiCol.Count)
        continue;

    style.Colors[(int)imGuiCol] = this.Colors[imGuiCol.ToString()];   // Apply, :420
    model.Colors[imGuiCol.ToString()] = style.Colors[(int)imGuiCol];  // Get,  :371
}
```

`ImGuiCol.Count` (ordinal `60`) is a sentinel, not a colour — it is skipped in both loops; `ImGuiStyle.Colors` is an `unsafe Span<Vector4>` of exactly 60 slots (the pinned `Hexa.NET.ImGui` 2.2.9 / ImGui 1.92.2 binding declares ordinals 0–59 plus `Count`).

**Standard (64 keys) covers all 60 colour members.** The four surplus keys — `TabActive`, `TabUnfocused`, `TabUnfocusedActive` (`:97`–`:99`), `NavHighlight` (`:113`) — are *legacy ImGui 1.8x names* that are not `ImGuiCol` members of this binding: they are never looked up by `Apply()`, never copied by `Get()`, but they **do** appear in the serialised JSON. Renaming a real colour key into one of these four silently disables that colour.

**Classic (55 keys) is missing 9 colour members**: `InputTextCursor` (33), `TabSelected` (36), `TabSelectedOverline` (37), `TabDimmed` (38), `TabDimmedSelected` (39), `TabDimmedSelectedOverline` (40), `TextLink` (52), `TreeLines` (54), `NavCursor` (56) — it carries the same four legacy keys instead. Because `Apply()` iterates the enum in ascending ordinal order, **`DalamudClassic.Apply()` throws `KeyError` at `InputTextCursor`** before the remaining colour writes run — the 28 scalars have already been copied by then, so the global style is left half-classic/half-default and the uncaught exception aborts startup rather than falling back (§7's cross-ref to `Program.cs:315`). Restoring classic as a usable option means supplying those nine keys (and the legacy four should be renamed to their modern equivalents, e.g. `Tab*` → `TabSelected*`/`TabDimmed*`).

Standard's colour values mix literals with six references into the shared table `../ImGuiColors.cs.md` — `ImGuiColors.BlueShade3` at `:84`/`:96`, `ImGuiColors.Blue` at `:85`/`:97`/`:99`/`:118` (the `Button*`/`Tab*` family): the theme is *coupled* to that table, so retinting those constants retints the theme. Classic uses **literals only** — it stays fully decoupled from `ImGuiColors.cs`. Alpha is part of each vector (`WindowBg` `0.93f`, `PopupBg` `0.94f`, `TitleBgCollapsed` `0/0/0/0.51f`), i.e. translucency is expressed per-colour, not via `Style.Alpha`.

## 5. The 28 style fields

Declared as `public T { get; set; }` (accessor-pair properties with implicit storage) between `#pragma warning disable SA1600` (`:230`) and `… restore` (`:316`) — the pragma silences the doc-comment rule for these terse single-word members, so they carry `[JsonProperty]` names but no doc comments. Each has a one-letter JSON key assigned in declaration order (`:232` `"a"` → `Alpha`, then `"b"`,`"c"`,… continuing alphabetically to `"z"`, with two insertions `"ab"` for `PopupBorderSize` (`:259`) and `"aa"` for `DisplaySafeAreaPadding` (`:313`)), plus `Colors` under `"col"` (`:321`) and `StyleModel.cs.md`'s `"name"`/`"ver"` — so a serialised theme is `{"name","ver", a…z/ab/aa, "col": {…}}`. The keys carry no mnemonic value (`"v"` is `LogSliderDeadzone`, not `Version`; `Version` itself is `"ver"` on the base), so a hand-written theme payload must be built from this file's key list, not guessed.

Field order here **is** load-bearing in one sense: `Get()` and `Apply()` are hand-mirrored lists in the same order, so adding a style field means editing three places (declaration, `Get()`, `Apply()`) — forget `Apply()` and the theme constant becomes inert; forget `Get()` and a snapshot loses it. The 28 fields are **a subset of the style struct, not all of it**: `ImGuiStyle` (Hexa 2.2.9 / ImGui 1.92.2) exposes 63 scalar fields plus the `Colors` span, so this model covers 28 and never reads or writes the other 35 — `FontSizeBase`, `FontScaleMain`, `FontScaleDpi`, `DisabledAlpha`, `WindowMinSize`, `WindowBorderHoverPadding`, `ColumnsMinSpacing`, `ImageBorderSize`, the `TabMinWidth*`/`TabCloseButtonMinWidth*`/`TabBar*` family, `TableAngledHeaders*`, `TreeLinesFlags`/`TreeLinesSize`/`TreeLinesRounding`, `ColorButtonPosition`, `SeparatorText*`, `DisplayWindowPadding`, `DockingSeparatorSize`, `MouseCursorScale`, `CurveTessellationTol`, `CircleTessellationMaxError`, `HoverStationaryDelay`/`HoverDelayShort`/`HoverDelayNormal`/`HoverFlagsForTooltip*`, `MainScale`, `NextFrameFontSizeBase`. Consequences: `Apply()` leaves those at whatever ImGui or `ImGuiBindings.cs.md` last set (font size and `MainScale` come from the *bindings*, `Program.cs:312`, so they survive theme application), and `Get()` cannot round-trip them — the snapshot/restore pair is only faithful for these 28. All 28 do exist in `ImGuiStyle` (no dead fields). Notably there is **no `ImGuiStyleVar` list**: this model is not a "set of style vars" (that is `StyleHandle.cs.md`'s domain), which is why its `Push`/`Pop` are stubs.

Values worth pinning (both themes agree): `WindowPadding (8,8)`, `FramePadding (4,3)`, `ItemSpacing (8,4)`, `ItemInnerSpacing (4,4)`, `CellPadding (4,2)`, `IndentSpacing 21`, `ScrollbarSize 16`, `ScrollbarRounding 9`, `ButtonTextAlign (0.5,0.5)`, `WindowMenuButtonPosition ImGuiDir.Right`. The two themes differ in **exactly three scalars**: `FrameRounding` `0` (`:42`) vs `4` (`:147`), `GrabMinSize` `13` vs `10`, `TabRounding` `1` vs `4` — everything else in the two initialisers is byte-identical, and the colour dicts are where the themes really diverge. `FrameRounding` being `0` has a preserved migration note: `//FrameRounding = 4,` sits commented just above at `:41` (the value Classic still carries), i.e. Dalamud's older theme rounded frames and this fork's does not.

Those numbers are the **baseline** the components assume: `../Components/MainPage/MainPage.cs:78` (also `LoginFrame.cs:97`, `NewsFrame.cs:74`) push `WindowPadding (32,32)` over the `8,8` default, `../Components/MainPage/AccountSwitcher.cs:32` pushes `Vector2(5)`, and `../Components/Common/Button.cs:31` pushes `FramePadding (16,16)` over `4,3`. `../Components/SettingsPage/SettingsPage.cs:122` instead pushes `FrameRounding 100f` over the `0` baseline — the pill-shaped panel that would have been invisible under the old `FrameRounding = 4`.

## 6. `Push()`/`Pop()` — deliberate stubs

`throw new NotImplementedException()` on both. **Nothing calls them**: `StyleHandle.cs.md` has its *own* `Push`/`Pop` members (it drives ImGui's style-**var** stack keyed by `ImGuiStyleVar`, and never mentions `StyleModel`), so no caller reaches the model's stack verbs — the base contract in `StyleModel.cs.md` (`Apply`/`Push`/`Pop`) is exercised only through `Apply()` (`../Program.cs.md` §6, `unsafe` block step 8, `Program.cs:315`). The exception is therefore unreachable today; it would fire the moment a caller holds a `StyleModel` (e.g. from `GetFromCurrent()`) and invokes the verb. If a theme-driven push/pop is ever wanted, the model needs an `ImGuiStyleVar`-shaped payload (see `StyleHandle.cs.md` §4) rather than the 28-field/colour-pair shape used by `Apply()`.

## 7. Invariants and hazards

* **`Apply()` is destructive and non-composable.** It overwrites the global style; it must run before any code that pushes deltas, and it must run **once** — `../Program.cs.md` §6 (`unsafe` block step 8, `Program.cs:315`), and `../Program.cs.md` §12 ("Thread-safety") covers the ordering guarantee: after `ImGuiBindings` exists and before `launcherApp`. Calling it again mid-frame would clobber deltas pushed earlier in that frame (and would *not* restore the 35 uncovered fields of §5, which `Apply()` never touches).
* **`Apply()` on `DalamudClassic` throws** (§4) — the only reachable way to hit that is editing `Program.cs:315` to swap the constant, which is also the only live reference to any theme constant in the fork.
* **`Colors` keys are strings**: no compile-time check ties them to the enum. Renaming/typo-ing a key yields `KeyError` at runtime, at the first missing member in ascending ordinal order (§4's diagnostic is the same one for Classic).
* **`Get()` is unreferenced** in the fork, and `ImGuiBindings.cs.md` persists the style through its own ini (`launcherUI.ini`), not through this class — so the snapshot half of the pair exists for callers outside the fork (upstream Dalamud theme negotiation).
* **`ImGuiStyle.Colors` is an `unsafe` span** aliasing native memory; the fork writes through it without bounds checks beyond `(int)imGuiCol`, which is only safe because the loop is driven by the same enum that sizes the span.
* The model's `Version` is never set (always `0`) while `SerializedPrefix` says `"DS1"` — a version mismatch already present in the data (§`StyleModel.cs.md` §3 for the scheme).

## 8. Referenced / referenced-by

* References: `StyleModel.cs.md` (base clause, verb contract), `../ImGuiColors.cs.md` (palette constants inside the Standard dict), `Hexa.NET.ImGui` (`ImGui.GetStyle()`, `ImGuiCol`, `ImGuiDir`), `System.Numerics` vectors, `Newtonsoft.Json` attributes.
* Referenced by: `../Program.cs.md` §6 (`unsafe` block step 8, `Program.cs:315` — `StyleModelV1.DalamudStandard.Apply()`, the fork's single theme application and its only reference to any of this class's members), `StyleModel.cs.md` §4 (`GetFromCurrent()` → `StyleModelV1.Get()`), and implicitly by every spec that pushes style deltas (baseline values).
* Sibling specs: `StyleModel.cs.md`, `StyleHandle.cs.md`.
