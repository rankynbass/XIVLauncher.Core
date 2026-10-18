# SettingsEntry.cs — Specification

**File:** `src/XIVLauncher.Core/Components/SettingsPage/SettingsEntry.cs` · 16 lines · 346 bytes
**Namespace:** `XIVLauncher.Core.Components.SettingsPage` · **Declaration:** `public abstract class SettingsEntry : Component`
**Role:** The tiny abstract contract every settings row obeys — name / visibility / validity / load / save

---

## 1. Purpose

`SettingsEntry` is the vocabulary of "one user-facing setting" for the whole Settings page. It is a `Component` (`Component.cs.md`) so that `SettingsTab.cs.md` and `SettingsPage.cs.md` can treat entries as drawable things, and it is abstract so that *only* concrete entry classes (see §2) define what a row actually looks like.

Everything in the class is a declaration — there is no behaviour. The class exists to make the *pair* of things a setting needs explicit: a way to read the persisted value (`Load`) and a way to write it (`Save`), plus two display flags (`IsVisible`, `IsValid`).

## 2. Surface

| Member | Anchor | Form | Contract |
| --- | --- | --- | --- |
| `InternalValue` | `:5` | `protected object?` plain field | type-erased store of the row's current value |
| `Name` | `:7` | `public abstract string { get; }` | abstract accessor — every concrete row overrides it (or sets the backing field in its ctor) |
| `IsValid` | `:9` | `public bool { get; protected set; }` | readable by anyone, writable only by derived classes |
| `IsVisible` | `:11` | `public abstract bool { get; }` | abstract accessor |
| `Load()` / `Save()` | `:13` / `:15` | `public abstract void` | must be overridden by every concrete row |

Inherited (not declared here): `Enabled`, `Margins`, `Children`, `Id`, `Draw()` from `Component.cs.md` — `Component.Draw()` is the children loop, so a row that does not override `Draw()` draws nothing but its children. No entry class does that today; all four concretes override `Draw()` (`SettingsEntry{T}.cs.md`, `NumericSettingsEntry.cs.md`, `DalamudBranchMetaSettingsEntry.cs.md`, and via `SettingsEntry<T>`'s own override).

The tab side of the pair — `public override SettingsEntry[] Entries …` — is satisfied in three accessor shapes; see `SettingsTab.cs.md` §3.1 for which tabs use which.

## 3. The type-erased store

`InternalValue` is `object?` rather than a generic parameter because the generic layer sits *below* this one: `SettingsEntry<T>` (`SettingsEntry{T}.cs.md` §2) casts it back with `(T)this.InternalValue` in its `Value` accessor, and subclass rows cast to their own type (`NumericSettingsEntry.cs.md`, `DalamudBranchMetaSettingsEntry.cs.md`, and the lambdas in the tab specs). Consequences to keep:

- Any new value type must be reference/boxed-compatible with `object?` (so `int`/`bool`/`enum` all work through boxing) — and must tolerate `null`, because `InternalValue` is `null` before the first `Load()` and whenever a user clears a value.
- It is `protected`, i.e. only classes deriving from `SettingsEntry` may touch it. `IsValid`'s setter is `protected` for the same reason: external code may *read* validity (`SettingsPage.cs.md` §finish button computes `…Any(y => y.IsVisible && !y.IsValid)`), but only rows may *write* it.

## 4. `IsValid` starts out `false`

`public bool IsValid { get; protected set; }` has no initializer, so an entry that has never had `Load()` or `Draw()` run reports **invalid**, which `SettingsPage.cs.md` §"finish button" renders as a red ban icon and a disabled button. That is deliberate fail-closed behaviour: the only way to leave Settings is through rows that have asserted validity, and the page's `OnShow()` calls `SettingsTab.Load()` (`SettingsTab.cs.md` §Load) before the first draw precisely to establish it. Do not "default it to true" to silence a spurious ban icon; fix the missing `Load()` call instead — the fail-closed default is what protects the `Save()` side.

## 5. `Name`, `IsVisible`, `Draw`, `Load`, `Save` — who calls what

```
SettingsPage.cs   OnShow()  → tab.Load()        → entry.Load()      (page.cs §OnShow)
SettingsTab.cs    Draw()    → entry.Draw()      if IsVisible        (tab.cs §Draw)
SettingsPage.cs   finish    → tab.Save()        → entry.Save()      (page.cs §finish button)
SettingsPage.cs   search    → entry.Draw()      if IsVisible        (page.cs §search mode)
```

- `Load`/`Save` being `abstract` (not optional virtuals) is what makes the compiler enforce the read/write pair; e.g. `DalamudBranchMetaSettingsEntry.cs.md` §Save overrides `Save()` itself and therefore must still supply the ctor's `save` lambda (it passes an empty one).
- `IsVisible` is abstract, not defaulted `true`: a row that wants to hide itself must override it, and `SettingsEntry<T>` supplies the default (`CheckVisibility?.Invoke() ?? true`, see `SettingsEntry{T}.cs.md` §CheckVisibility).
- Both the tab loop and the search loop consult `IsVisible` independently — that is why hiding is a *draw-time* decision (`SettingsTab.cs.md` §Draw and `SettingsPage.cs.md` §search mode note the same call site twice), and why per-frame visibility predicates are allowed to change mid-session (e.g. `SettingsPage/Tabs/SettingsTabWine.cs.md` §dependency chain).

## 6. Referenced / referenced-by

Implemented by: `SettingsEntry{T}.cs.md` (the generic renderer, base of `NumericSettingsEntry.cs.md` and `DalamudBranchMetaSettingsEntry.cs.md`).
Consumed by: `SettingsTab.cs.md` (`Entries[]`, `IsVisible`, `Draw`, `Load`, `Save`) and `SettingsPage.cs.md` (`IsVisible`, `IsValid`, `Name` in the search filter).
Concrete rows live in: `Tabs/SettingsTabAbout.cs.md` (none — empty), `Tabs/SettingsTabGame.cs.md`, `Tabs/SettingsTabPatching.cs.md`, `Tabs/SettingsTabWine.cs.md`, `Tabs/SettingsTabDalamud.cs.md`, `Tabs/SettingsTabDebug.cs.md` (none), `Tabs/SettingsTabTroubleshooting.cs.md`.
Also uses: `Component.cs.md` (`Draw` children loop, `Id` used in ImGui id strings by `SettingsEntry{T}.cs.md`).
