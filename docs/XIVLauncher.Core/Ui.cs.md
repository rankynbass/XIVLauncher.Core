# Ui.cs — Specification

**File:** `src/XIVLauncher.Core/Ui.cs` · 75 lines · 3,369 bytes
**TargetFramework:** net10.0 (Linux/GTK3 build)
**Role:** the single marshal point between background threads and the Eto UI thread (`Eto.Forms`, https://github.com/picoe/Eto), plus the Debug-only thread-identity guards that catch unmarshalled widget writes.

---

## 1. Purpose

`Ui` wraps the Eto `Application.Instance` invoke surface. The launcher runs login, patching,
news loading, and OTP listening on background threads; Eto controls may only be mutated from
the UI thread. Every UI mutation performed by those threads goes through `Ui.Post` / `Ui.Run`.

API surface:

- `Ui.Post(Action)` → `Application.Instance.AsyncInvoke(action)` — queue without waiting (line 63).
- `Ui.Run(Action)` → `Application.Instance.Invoke(action)` — block the caller until the action has
  completed on the UI thread (line 66).
- `Ui.Run<T>(Func<T>)` — invoke on the UI thread and return the result, blocking the caller (line 69).
- `Ui.MarkUIThread()` — record the thread that owns the UI loop (line 24).
- `Ui.IsUIThread` — true on that thread (line 28); false before `MarkUIThread` (`uiThreadId = -1`, line 20).
- `Ui.AssertUIThread(site)` — Debug-only (`[Conditional("DEBUG")]`, line 34): logs a warning with
  `Environment.StackTrace` when the call is not on the UI thread (lines 35–42).
- `Ui.CaptureUIThreadWarnings()` — Debug-only (line 47): adds a `TraceListener` that catches Eto's
  "Accessing UI object from a non-UI thread" warning and logs the managed stack that triggered it.

## 2. Thread identity and guards

Eto's check lives in `Application.EnsureUIThread()`, reached through `Control.Handler` on every
widget property: it compares `Thread.CurrentThread.ManagedThreadId` with the thread that started
`Application.Run`, and in the default `UIThreadCheckMode.Warning` mode writes the warning with
`Trace.WriteLine` (no stack, no widget, no call site). `MarkUIThread` is called in `Program.Main`
immediately before `application.Run()` (`Program.cs:320`), so `IsUIThread` reproduces Eto's notion of
the UI thread, and `AssertUIThread` can be placed at the launcher's own widget-write entry points.

The guard sites are the writes that background threads drive:

- `LoadingPage.ApplyState` (`LoadingPage.cs:175`) — the label/bar/visibility writes.
- `LauncherForm.ShowPage` (`LauncherForm.cs:41`), `ShowWindow` (`LauncherForm.cs:47`),
  `HideWindow` (`LauncherForm.cs:54`).

`CaptureUIThreadWarnings` is installed in `Program.Main` under `#if DEBUG` (`Program.cs:195`). It is
what found the login crash call sites: the warning in `launcher.log` came with the stack
`LauncherApp.StartLoading → LoadingPage.Reset() → LoadingPage.Refresh() → TextControl.set_Text` on a
`.NET TP Worker` thread, and `MainPage.ProcessLogin` reading `loginFrame.Username/Password/IsOtp/
IsSteam/IsFreeTrial/IsAutoLogin` inside `Task.Run` (Eto reads are UI-object accesses too, so reads
need marshalling as much as writes).

## 3. Consumers

| Consumer | Site | Call |
| --- | --- | --- |
| `Components/LoadingPage/LoadingPage.cs` | lines 99, 164, 167, 170 | `Ui.Post(this.ApplyState)` from every property setter (`SetStateField`), `Reset()`, `OnShow()`, `Refresh()` |
| `Components/LoadingPage/LoadingPage.cs` | line 175 | `Ui.AssertUIThread("LoadingPage.ApplyState")` |
| `LauncherForm.cs` | lines 41, 47, 54 | `Ui.AssertUIThread` on `ShowPage`, `ShowWindow`, `HideWindow` |
| `Components/MainPage/MainPage.cs` | line 192 | `Ui.Post(() => Application.Instance.Quit())` after a successful `Login()` |
| `Components/MainPage/NewsFrame.cs` | line 133 | `Ui.Run(() => new Bitmap(bannerBytes))` — decoding banners on the UI thread |
| `Components/MainPage/NewsFrame.cs` | line 140 | `Ui.Post(() => this.ShowNews(headlines))` |
| `Components/SettingsPage/DalamudBranchMetaSettingsEntry.cs` | line 29 | `Ui.Post` populates the branch drop-down after the fetch completes |
| `Components/OtpEntryPage.cs` | line 141 | `Ui.Post` resets the OTP input from the OTP listener thread |
| `LauncherApp.cs` | line 62 | `State` setter posts `SetState` |
| `LauncherApp.cs` | line 181 | `Ui.Post(() => this.SetState(LauncherState.Main))` in `AttachForm` |
| `LauncherApp.cs` | line 193 | `Ui.Post` opens the modal dialog on the UI thread |
| `Program.cs` | line 320 | `Ui.MarkUIThread()` before `application.Run()` |
| `Program.cs` | line 195 | `Ui.CaptureUIThreadWarnings()` (`#if DEBUG`) |
| `Program.cs` | line 369 | `Ui.Post(() => launcherForm.ShowWindow())` |
| `Program.cs` | line 372 | `Ui.Post(() => launcherForm.HideWindow())` |

`Program.ShowWindow()` / `Program.HideWindow()` keep the login pipeline's hide/restore calls
(`MainPage.cs` lines 1155/1163) thread-safe, so the game-start path can call them from a worker thread.

## 4. Quirks

- `Ui.Post` on GTK queues through `GtkSynchronizationContext.Post`, which uses GLib idle
  (`Idle.Add(() => { d(state); return false; })` in the Eto source
  `src/Eto.Gtk/Forms/GtkSynchronizationContext.cs`). A `Post` issued from the UI thread runs at the
  next idle, not immediately.
- `Ui.Run` from the UI thread re-enters the Eto invoke path; `GtkSynchronizationContext.Send`
  checks `ApplicationHandler.MainThreadID` and runs inline when already on the UI thread.
- `Ui.Run<T>` mutates a captured `result` inside the invoked closure; nothing else observes the
  intermediate value, so the helper is safe only for single-value returns.
- Blocking the UI thread in a `Run` is what makes `ShowMessageBlocking`'s `ManualResetEvent`
  semantics work (`LauncherApp.cs`), and is exactly what previously dead-locked the `MainPage`
  constructor when a `Task(...).Result` continuation was queued to the GTK loop. Blocking waits on
  background work must never be issued from the UI thread.
- Under GTK, `GApplication` exits when its last window closes, so posts that hide a window
  (`Program.HideWindow` → `LauncherForm.HideWindow`) unmap it **and** hold the `GApplication`
  (`g_application_hold` on `libgio-2.0.so.0`) across the hidden period; `LauncherForm.ShowWindow`
  releases it. Verified in an Eto 2.13/GTK3 probe: after hide the window reports
  `IsMapped=False` for 6s, UI posts keep being processed, restore returns `IsMapped=True`, and the
  loop survives (`LauncherForm.cs.md` §3).
- `AssertUIThread` logs and continues; it does not throw, so a Debug run keeps going after a
  violation and the stack lands in `launcher.log`. Eto's own check has the same policy in the default
  `UIThreadCheckMode.Warning`; setting `Application.Instance.UIThreadCheckMode = Error` makes every
  unmarshalled access throw `UIThreadAccessException` instead, which is the stricter mode to use while
  auditing new code paths.
- `Console.Error` is not where Eto's warning lands; `Trace.Listeners` is, because Eto uses
  `Trace.WriteLine`. Hooking `Console.Out`/`Console.Error` misses it (the first attempt did).

## 5. Cross-references

- `docs/XIVLauncher.Core/Program.cs.md` — creates the Eto `Application`, installs
  `launcherForm`, calls `MarkUIThread`, installs the warning capture, and calls the posts.
- `docs/XIVLauncher.Core/LauncherApp.cs.md` — `State` setter, modal pump, `AttachForm`.
- `docs/XIVLauncher.Core/Components/LoadingPage/LoadingPage.cs.md` §3 — the marshalled setter path
  that fixes the login crash.
- `docs/XIVLauncher.Core/Components/MainPage/MainPage.cs.md` §13 — the login snapshot.
- `docs/XIVLauncher.Core/Components/MainPage/NewsFrame.cs.md`,
  `docs/XIVLauncher.Core/Components/OtpEntryPage.cs.md` — other posting consumers.
- `docs/XIVLauncher.Core/LauncherForm.cs.md` — the `ShowWindow`/`HideWindow` implementations posted
  here, and their guard calls.
