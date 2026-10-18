# StyleHandle.cs — Specification

**File:** `src/XIVLauncher.Core/Style/StyleHandle.cs` · 41 lines · 785 bytes
**Namespace:** `XIVLauncher.Core.Style` · **Declaration:** `public class StyleHandle : Dictionary<ImGuiStyleVar, object>, IDisposable`
**Role:** RAII-style scoped style-var pusher — a dictionary of style vars that pushes itself on construction and pops itself in `Dispose()` (unused in this fork)

---

## 1. Purpose

`StyleHandle` is the deterministic-scoping counterpart to hand-written `PushStyleVar`/`PopStyleVar` pairs: build a dictionary keyed by `ImGuiStyleVar`, push it, and rely on a `using var` declaration's implicit `Dispose()` to pop exactly the same number of entries at scope exit. It is **entirely unreferenced in the fork** — every page pushes and pops by hand (see `../Components/Common/Button.cs.md` §push/pop ledger, `../Components/SettingsPage/SettingsPage.cs.md`, `../Components/MainPage/MainPage.cs.md`). Separately, `StyleModelV1`'s own `Push`/`Pop` are throwing stubs (`StyleModelV1.cs.md` §6), so this handle is not wired to the theme model at all: `StyleHandle.cs` never mentions `StyleModel`/`StyleModelV1`, and neither class references the other.

Usings: `System.Numerics` (`Vector2`), `Hexa.NET.ImGui` (`ImGui.PushStyleVar`/`PopStyleVar`, `ImGuiStyleVar`).

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| base clause | `Dictionary<ImGuiStyleVar, object>` + `IDisposable` | the instance **is** a dictionary: `Count`, `[]`, `Add` are inherited members; `IDisposable` is satisfied by the `Dispose()` below |
| ctor | `public StyleHandle(bool applyNow = true)` (`:9`–`:13`) | default argument: `new StyleHandle()` pushes immediately (against an always-empty dict — see §4), `applyNow = false` defers |
| `Push()` | `public void` (`:15`–`:30`) | iterates the dictionary, type-tests each value, calls one of two `PushStyleVar` overloads |
| `Pop()` | `public void` (`:32`–`:35`) | `ImGui.PopStyleVar(this.Count)` |
| `Dispose()` | `public void` (`:37`–`:40`) | `this.Pop()` |

## 3. How it is meant to be used

The only sound pattern is **deferred push** — populate the dict, push explicitly, then let a `using` declaration own the pop:

```csharp
using var style = new StyleHandle(applyNow: false);
style[ImGuiStyleVar.FrameRounding] = 5f;
style[ImGuiStyleVar.FramePadding] = new Vector2(16f, 16f);
style.Push();
// ... draws with those vars ...
// scope exit -> Dispose() -> PopStyleVar(2)
```

The `using` here must be a **using-declaration** (`using var x = …`, or the older `using (var x = …) { … }`) — C# has no `using (existingVariable)` form, so a handle created earlier can only be disposed by an explicit `style.Dispose()` call or by re-binding it in a `using` declaration.

`using var` guarantees the handle's `Dispose()` runs at scope exit (deterministic, not GC — the same mechanism the fork already leans on at `../Components/MainPage/MainPage.cs.md` `using var process`/`mutex`/`installer`, and at `../Support/PackGenerator.cs.md` `using var archive`). Balance matters absolutely: ImGui's style stack is per-frame global state, and an unbalanced push/pop corrupts every subsequent draw in that frame. Note also that the entries must be populated **before** `Push()`, which forces the `applyNow: false` form — see §4's first bullet for why the default cannot work.

## 4. The push/pop ledger — where it can desync

`Push()` and `Pop()` do not track the same thing:

* **The default `applyNow = true` can never push anything useful.** The ctor body (`this.Push()`, `:12`) runs while `this` is still the freshly-constructed, **empty** dictionary — there is no way to have inserted entries before the ctor runs — so the push loop iterates zero entries, while `Dispose()` later pops `this.Count`, i.e. however many entries a caller added after construction. Net effect: a guaranteed `PopStyleVar(n)` with `n` fewer pushes than pops (ImGui asserts the over-pop and clamps), i.e. the *default* argument is a live bug. Only `new StyleHandle(applyNow: false)` + an explicit `Push()` is sound (see §3), so the default is worth inverting before the class is ever adopted.
* **Push is per-value-type, Pop is per-entry.** `switch (entry.Value)` has exactly two patterns, `case Vector2 vec2:` and `case float num:`, matching the two Hexa overloads (`Generated/Functions/Functions.000.cs:3767 PushStyleVar(ImGuiStyleVar, float)`, `:3788 (ImGuiStyleVar, Vector2)`). A value of any other runtime type (`int`, `double`, `Vector3`, `bool`, `null`) matches nothing and pushes **nothing** — while `Pop()` still pops one slot for that entry (`this.Count` counts entries, not pushes). Storing `{ ImGuiStyleVar.FrameRounding, 5 }` (an `int` literal, i.e. not `5f`) therefore corrupts the style stack for the rest of the frame. This is the class's most dangerous property and it is invisible at compile time because the value type is `object`.
* **`Push()` is not idempotent.** Nothing clears or marks entries, so a second `Push()` (or a later manual call) pushes again while `Pop()` pops once — hence `applyNow`'s `false` branch has no legal partner here: a handle constructed with `applyNow = false` must be pushed exactly once manually, and nothing in the class records that it happened.
* **`Dispose()` is not compiler-magic here.** `Dispose()` is a plain method; the implicit `Dispose()` call for `using var` is emitted because the static type has a applicable `Dispose()` member, which this one does. The C# 13 `disposable` attribute is *not* used, so nothing warns about a handle that never gets disposed.
* **`Pop()` never checks `Count`**, so a handle that was never pushed still issues `PopStyleVar(0)` (harmless) — but one that pushed with a `false` `applyNow` and was then pushed twice pops once (see the bullet above).

## 5. Interaction with `StyleModelV1.cs.md`

The two classes sit in different domains and must not be conflated:

* `StyleModelV1.Apply()` writes ImGui's **global style struct** once (`../Program.cs.md` §6, `unsafe` block step 8, `Program.cs:315`) — it is a baseline, not a stack operation, which is why its `Push`/`Pop` overrides throw (`StyleModelV1.cs.md` §6).
* `StyleHandle` manipulates the **style-var stack** for a scoped region. Its dictionary is keyed by `ImGuiStyleVar`, which has no overlap with `StyleModelV1`'s 28-property/`Colors` shape.
* Practical consequence: to use `StyleHandle` at all, call sites must build their own `ImGuiStyleVar`-keyed dictionaries — `StyleModelV1` cannot feed it (its payload is 28 named style fields + a `Colors` dict keyed by `ImGuiCol` names, a different domain).

## 6. Invariants and extension points

* **Dead code today.** No construction sites exist (`grep StyleHandle` → this file only), so none of §4's desyncs are live. Treat the class as a tool that exists for *future* component code, and note that components currently maintain their own ledgers.
* **Balance discipline is per-frame.** ImGui's style stack is per-frame global state; a handle whose scope spans a frame boundary (e.g. stored in a long-lived field, as `../Components/Component.cs.md` instances are) would push once and pop once — leaving one entry on the stack — so this class is only sound for *lexical* scopes inside a single `Draw()`, i.e. inside a component's per-frame method.
* **Extension point:** make `Push()`/`Pop()` symmetric — e.g. count actual pushes, or store the pushed count next to each entry — before adopting the class in components; and add a `disposable` annotation if the project's build enables the C# 13 warning.
* The base clause `Dictionary<ImGuiStyleVar, object>` is a public API detail: the handle *is* the payload. If the payload type is ever tightened (e.g. a `union`-like `Vector2OrFloat` wrapper or a `sealed` payload type), the `object`-typed switch in §4 disappears in favour of exhaustive patterns, and the "silent no-push" hazard above becomes a compile error — that is the recommended fix direction.

## 7. Referenced / referenced-by

* References: `Hexa.NET.ImGui` (`PushStyleVar`/`PopStyleVar`/`ImGuiStyleVar`), `System.Numerics` (`Vector2` pattern), `StyleModel.cs.md`/`StyleModelV1.cs.md` (same directory, different domain — see §5).
* Referenced by: nothing in the fork. Nearest live analogues: hand-written `PushStyleVar`/`PopStyleVar` ledgers in `../Components/Common/Button.cs.md`, `../Components/Common/Checkbox.cs.md`, `../Components/Common/Input.cs.md`, `../Components/SettingsPage/SettingsPage.cs.md`, `../Components/MainPage/*.cs.md`.
* Sibling specs: `StyleModel.cs.md`, `StyleModelV1.cs.md`.
