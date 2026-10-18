# DalamudOverlayInfoProxy.cs — Specification

**File:** `src/XIVLauncher.Core/Components/LoadingPage/DalamudOverlayInfoProxy.cs` · 30 lines · 659 bytes
**Namespace:** `XIVLauncher.Core.Components.LoadingPage` · **Declaration:** `public class DalamudOverlayInfoProxy : IDalamudLoadingOverlay`
**Role:** In-memory "what is Dalamud doing right now" record — the Core-side implementation of the shared overlay interface; **not** a UI component despite its location

---

## 1. Purpose

`DalamudOverlayInfoProxy` is the launcher's *observation sink* for Dalamud's download/update phase. `lib/FFXIVQuickLauncher`'s `DalamudUpdater` and `DalamudLauncher` push state into it; this class just remembers it. It is the Core (ImGui) counterpart of the Windows version's `XIVLauncher/Windows/DalamudLoadingOverlay.xaml.cs`, which is the same interface but actually renders.

Note the placement quirk: the class lives under `Components/LoadingPage/` (so its namespace is `XIVLauncher.Core.Components.LoadingPage`) but **does not extend `Component`** — it implements `IDalamudLoadingOverlay` (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common/PlatformAbstractions/IDalamudLoadingOverlay.cs`). It never draws and never touches ImGui. Its folder position records *intent* ("the loading page would show this"), which no page currently fulfils: grep shows no reader of `IsVisible` or `Step` anywhere in `src/`, so today the whole class is write-only state.

## 2. Surface

| Member | Kind | Notes |
| --- | --- | --- |
| `IsVisible` | `bool`, `get` + **`private set`** | externally readable, only writable from inside the class |
| `Step` | `IDalamudLoadingOverlay.DalamudUpdateStep`, `get` + `private set` | same access shape; enum declared *in the interface* |
| `SetStep(DalamudUpdateStep step)` | `public` | assigns `Step` |
| `SetVisible()` | `public` | `IsVisible = true` |
| `SetInvisible()` | `public` | **`IsVisible = true`** — see §3 (bug) |
| `ReportProgress(long? size, long downloaded, double? progress)` | `public` | **empty body, `// TODO`** — see §4 |

`{ get; private set; }` is the C# 13 accessor form: the backing field is class-private, so `IsVisible`/`Step` are a read-only public view with private mutation. The four method signatures are exactly the interface's — the class compiles only because it implements all four (interface `IDalamudLoadingOverlay` declares `SetStep`, `SetVisible`, `SetInvisible`, `ReportProgress`).

## 3. The `SetInvisible` bug (live)

```csharp
public void SetVisible()   { this.IsVisible = true; }
public void SetInvisible() { this.IsVisible = true; }   // should be false
```

Call path (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common/Dalamud/DalamudLauncher.cs`):

| Line | Call | Intent |
| --- | --- | --- |
| `:55` | `ShowOverlay()` | overlay should appear during an update |
| `:61` | `CloseOverlay()` → `SetInvisible()` | overlay should go away when the updater fails |
| `:75` | `ShowOverlay()` | re-show on "version unavailable" |
| `:129` | `CloseOverlay()` → `SetInvisible()` | hide once the runner has started |

So after either `CloseOverlay()`, `IsVisible` stays `true`. It is currently harmless *only because nothing reads `IsVisible`* (see §1); as soon as a page renders from this record, closing is broken and the fix belongs **in this file**, not in `lib/FFXIVQuickLauncher` (which owns the calls). Keep the method name and flip the assignment.

## 4. `ReportProgress` is a stub

The interface parameter set is `(long? size, long downloaded, double? progress)` and `DalamudUpdater.cs:120-122`'s `ReportOverlayProgress` forwards `downloader.ProgressChanged` events (`DalamudUpdater.cs:555` binds them) into it. This implementation discards all three arguments (`// TODO`), so no byte-level progress is available to Core today.

Where a byte-level bar *is* wanted, note that `LoadingPage.cs.md` already has a progress channel — `Progress` (`float`, `get`/`set`) plus `ProgressText` — and `Progress` already has writers: `MainPage/MainPage.cs.md`'s patching and repair loops assign `App.LoadingPage.Progress` directly from the task (`MainPage.cs:955`, `:1065`, `:1071`, `:1077`), never through `LauncherApp.StartLoading` (which has no progress argument, `LauncherApp.cs.md` §start-loading). `ProgressText`, by contrast, has no writer at all. So the intended fix is to bridge `ReportProgress` into `LoadingPage.Progress`/`ProgressText` following that same direct-assignment pattern, which requires a reference from this class to the page (or moving the record onto the page). Either direction is a deliberate design choice, so it is left unimplemented here rather than guessed.

## 5. Instance lifecycle (owner side)

The single handle is `Program.DalamudLoadInfo` (`Program.cs.md`), declared `public static DalamudOverlayInfoProxy DalamudLoadInfo { get; private set; } = null!;` and re-assigned **before each `DalamudUpdater` build**:

```csharp
DalamudLoadInfo = new DalamudOverlayInfoProxy();     // Program.cs:259 and :450
DalamudUpdater  = CreateDalamudUpdater();            // Program.cs:176: { Overlay = DalamudLoadInfo, ... }
DalamudUpdater.Run(Config.DalamudBetaKind, Config.DalamudBetaKey);
```

Consequences:

- Instances are **fresh per game launch**, so `Step` starts at the enum's implicit first value (`Dalamud`) and `IsVisible` at `false` for each launch — no cross-launch leakage, and no reset logic is needed here.
- `Overlay` is `IDalamudLoadingOverlay? { get; set; }` in `DalamudUpdater` and is dereferenced with `Overlay!` in the four helpers above, so a `null` `Program.DalamudLoadInfo` at `CreateDalamudUpdater()` time is an NPE risk in `lib/`; the `null!` initial value in `Program.cs` is the reason ordering (assign then build) matters.
- The class is *not* owned by the app's UI tree, so it is not drawn or re-drawn; the app cannot rely on it being tied to a page instance.

## 6. Thread and mutation model

`SetStep`/`SetVisible`/`SetInvisible` run inside the login/launch task chain (`MainPage/MainPage.cs.md` §launch → `App.Launcher.Login` → `DalamudLauncher.HoldForUpdate`), while ImGui state is touched by the frame loop. Because `IsVisible`/`Step` are read by nobody, the missing synchronisation is currently invisible; if a page starts reading them in `Draw()`, they are plain (non-`volatile`) accessors written from another thread. Compare `Common/Input.cs.md` §`isSteamDeckInputActive`, where the same cross-thread pattern **is** marked `volatile` — that is the precedent to follow in this file.

## 7. Invariants and extension points

- The class is a *pure* record: no side effects beyond the two fields, no defaults worth, no validation. `SetStep` blindly accepts any enum value.
- `DalamudUpdateStep` is declared inside the interface (`IDalamudLoadingOverlay.cs`), so this class cannot narrow it; new phases (e.g. a "verifying" step) are an `XIVLauncher.Common` change plus a `switch` here.
- `IsVisible`/`Step` are `private set`, so the intended mutation path is *only* the three setter methods — that is what keeps `lib/`'s calls compile-compatible and is why the bug in §3 must be fixed inside a setter rather than by adding an overload.
- A natural extension is a `GetStepText()`/`GetProgressText()` helper mapping `DalamudUpdateStep` to `Strings.*` localized lines, mirroring what the Windows variant does at `XIVLauncher/Windows/DalamudLoadingOverlay.xaml.cs:41-57`. That helper belongs here (it is the class that owns the enum→meaning mapping), and the page that draws (`LoadingPage.cs.md`) then only consumes it.
- `IsVisible` and `Step` are independent: a step change does not make the record visible, and the callers show that explicitly (`SetOverlayProgress(...Unavailable)` then `ShowOverlay()`). A page that renders "always" must therefore not use `IsVisible` as a proxy for "a step was set".

## 8. Referenced / referenced-by

Owned by: `Program.cs.md` (`DalamudLoadInfo` static, assigned at `Program.cs:259`/`:450`, read at `Program.cs:176`).
Consumer (lib side): `DalamudUpdater.cs` (`Overlay` field, `SetOverlayProgress`/`ShowOverlay`/`CloseOverlay`/`ReportOverlayProgress`), `DalamudLauncher.cs` (`:55/:61/:75/:129`, `:74` step `Unavailable`).
Interface: `IDalamudLoadingOverlay.cs` (`lib/FFXIVQuickLauncher/src/XIVLauncher.Common/PlatformAbstractions/`).
Related: `LoadingPage.cs.md` (the page this record was meant to feed — currently unconnected), `Program.cs.md`.
