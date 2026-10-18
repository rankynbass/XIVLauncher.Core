# StyleModel.cs — Specification

**File:** `src/XIVLauncher.Core/Style/StyleModel.cs` · 42 lines · 1121 bytes
**Namespace:** `XIVLauncher.Core.Style` · **Declaration:** `public abstract class StyleModel`
**Role:** Abstract shape of a serialisable Dalamud theme — JSON-named `Name`/`Version` identity plus the `Apply`/`Push`/`Pop` verb triple

---

## 1. Purpose

`StyleModel` is the abstract base of the theme object model: a theme is a **named, versioned, serialisable** bundle of ImGui style values that can be *applied* to the global style, *pushed* as a delta onto the style stack, and *popped* off it. `StyleModelV1.cs.md` is the only concrete subtype in the fork; `StyleHandle.cs.md` is a separate, also-unused style-var-stack client that shares only the directory (see `StyleHandle.cs.md` §5 — no code path connects the two).

Usings: `Newtonsoft.Json` (for the `[JsonProperty]` attribute on the two identity fields) and `XIVLauncher.Core.Style`.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `Name` | `public string { get; set; }`, initialised `"Unknown"` (`:14`) | `[JsonProperty("name")]` — serialises as `"name"` |
| `Version` | `public int { get; set; }`, **no initialiser** (`:20`) | `[JsonProperty("ver")]` — serialises as `"ver"`; defaults to `0` |
| `GetFromCurrent()` | `public static StyleModel` **method** (arrow body, `:26`) | delegates to `StyleModelV1.Get()` |
| `Apply()` | `public abstract void` (`:31`) | implemented by `StyleModelV1` |
| `Push()` / `Pop()` | `public abstract void` (`:36`/`:41`) | implemented (as throwing stubs) by `StyleModelV1` |

## 3. Identity and JSON shape

Both identity fields carry `[JsonProperty]`, so a concrete model round-trips through `JsonConvert.{SerializeObject,DeserializeObject}` as `{"name": "...", "ver": 0}`. `Version` is a plain `int` counter, and the subtype carries a separate `SerializedPrefix => "DS1"` (`StyleModelV1.cs:228`, doc comment "Gets the version prefix for this version.") — but the prefix is never concatenated, compared or serialised anywhere in the fork, so the versioning scheme is aspirational: a `StyleModelV2` would be a **new class**, not a bumped `Version`.

`Name` defaults to `"Unknown"`, and that is also what `StyleModelV1.Get()` leaves in place (the concrete subtype's private ctor at `StyleModelV1.cs:20` re-assigns `this.Name = "Unknown"`, and `Get()` never sets `Name`) — so every model built by `Get()`/`GetFromCurrent()` carries `Name == "Unknown"` regardless of which theme dict it was filled from. **`Name` is therefore not a discriminator between Standard and Classic**; only the two static fields `StyleModelV1.DalamudStandard`/`DalamudClassic` carry real names (`"Dalamud Standard"`, `"Dalamud Classic"`), and both of those are compile-time constants, never parsed back.

`Version` is `int` here while the concrete subtype's theme values are `float`/`ushort`-shaped ImGui fields; nothing constrains `Version` to the prefix in `SerializedPrefix` ("DS**1**" vs `Version == 0` already disagree).

## 4. `GetFromCurrent()` — the entry point of this base

```csharp
public static StyleModel GetFromCurrent() => StyleModelV1.Get();
```

* It is a **method** (arrow body), so the call site must be `StyleModel.GetFromCurrent()`; writing it as a property access without parens (`StyleModel.GetFromCurrent`) is an "expected method, not a property" diagnostic — the same class of slip recorded in the Components specs for accessor-vs-method mixups.
* The return type is the **base** `StyleModel`, so the static type hides that a `StyleModelV1` instance is returned; callers that want V1's theme fields must downcast.
* It is `static` on an `abstract` class — legal, and the only sane way to have an entry point without instantiating the base.
* It is unreferenced in the fork (no call sites), so that arity is currently only visible to the compiler. Its mirror image on the subtype is `StyleModelV1.Get()` (`StyleModelV1.cs:328`), which the base can name because `StyleModelV1` is a sibling file in the same namespace — a **downward** reference from base to subtype, which is what forces `GetFromCurrent()` to be written that way instead of the reverse.

The declaration order (`GetFromCurrent` before the abstract verbs) has no semantic effect; the abstract trio is what `StyleModelV1` must — and does — override.

## 5. Invariants and extension points

* **Abstract, never instantiated.** `new StyleModel()` is impossible; every instance reachable in the fork is a `StyleModelV1`, including the results of `GetFromCurrent()`.
* **`Apply()` is the only verb the fork uses** (`../Program.cs.md` §6, `unsafe` block step 8 — `StyleModelV1.DalamudStandard.Apply()` at `Program.cs:315`, before the first frame). `Push`/`Pop` are **unreachable**: no caller invokes the model's stack verbs, and `StyleHandle.cs.md`'s `Push`/`Pop` are its *own* members driving the style-**var** stack, so `StyleModelV1`'s throwing stubs (`StyleModelV1.cs.md` §6) never fire today. The fork's theme therefore has no push/pop support in practice, and adding a caller of `StyleModel.Push()` would be a crash until the subtype implements the pair.
* **No registry of models.** There is no list, no `switch` on `Name`/`Version`, and no ini path for the theme: `ImGuiBindings.cs.md` persists a *separate* ini (`launcherUI.ini`, `Program.cs:312`) that is unaware of this class. The `[JsonProperty]` hooks exist for the upstream Dalamud theme protocol, which this fork never sends or receives — see `StyleModelV1.cs.md` §5 for the concrete key list and §4 for the colour-dict shape.
* **Extension point for a new theme:** add a sibling class (`StyleModelV2 : StyleModel`) and repoint `GetFromCurrent()`; the base deliberately names no subtypes, so it needs no other change. Keeping `Name`/`Version` meaningful (i.e. actually setting them in the subtype's ctor) is the prerequisite for any future theme negotiation.

## 6. Referenced / referenced-by

* References: `Newtonsoft.Json` (attribute only), `XIVLauncher.Core.Style` sibling `StyleModelV1.cs` (`Get()`).
* Referenced by: `StyleModelV1.cs` (base clause + its overridden verbs), `StyleHandle.cs.md` (adjacent domain, no code link), `../Program.cs.md` §6 (`Program.cs:315` — the fork's single theme application, reached through `StyleModelV1` directly, not through this base).
* Sibling specs: `StyleModelV1.cs.md`, `StyleHandle.cs.md`.
