# Ui.cs — Specification

**File:** `src/XIVLauncher.Core/Ui.cs` · 29 lines · 1210 bytes
**TargetFramework:** net10.0 (Linux/GTK3 build)
**Role:** the single marshal point between background threads and the Eto UI thread (`Eto.Forms`, https://github.com/picoe/Eto).

---

## 1. Purpose

`Ui` wraps the Eto `Application.Instance` invoke surface. The launcher runs login, patching,
news loading, and OTP listening on background threads; Eto controls may only be mutated from
the UI thread. Every UI mutation performed by those threads goes through `Ui.Post` / `Ui.Run`.

API surface:

- `Ui.Post(Action)` → `Application.Instance.AsyncInvoke(action)` — queue without waiting (line 17).
- `Ui.Run(Action)` → `Application.Instance.Invoke(action)` — block the caller until the action has
  completed on the UI thread (line 20).
- `Ui.Run<T>(Func<T>)` — invoke on the UI thread and return the result, blocking the caller (line 24).

## 2. Consumers

| Consumer | Site | Call |
| --- | --- | --- |
| `Components/LoadingPage/LoadingPage.cs` | line 53 | `Ui.Post(this.Refresh)` when `Progress` changes |
| `Components/MainPage/MainPage.cs` | line 180 | `Ui.Post(() => Application.Instance.Quit())` after a successful `Login()` |
| `Components/MainPage/NewsFrame.cs` | line 110 | `Ui.Run(() => new Bitmap(bannerBytes))` — decoding banners on the UI thread |
| `Components/MainPage/NewsFrame.cs` | line 117 | `Ui.Post(() => this.ShowNews(headlines))` |
| `Components/SettingsPage/DalamudBranchMetaSettingsEntry.cs` | line 29 | `Ui.Post` populates the branch drop-down after the fetch completes |
| `Components/OtpEntryPage.cs` | line 120 | `Ui.Post` resets the OTP input from the OTP listener thread |
| `LauncherApp.cs` | line 62 | `State` setter posts `SetState` |
| `LauncherApp.cs` | line 181 | `Ui.Post(() => this.SetState(LauncherState.Main))` in `AttachForm` |
| `LauncherApp.cs` | line 193 | `Ui.Post` opens the modal dialog on the UI thread |
| `Program.cs` | line 319 | `Ui.Post(() => launcherForm.ShowWindow())` |
| `Program.cs` | line 322 | `Ui.Post(() => launcherForm.HideWindow())` |

`Program.ShowWindow()` / `Program.HideWindow()` keep the login pipeline's hide/restore calls
(`MainPage.cs` lines 1145/1153) thread-safe, so the game-start path can call them from a worker thread.

## 3. Quirks

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
- Under GTK, `GApplication` exits when the last visible window closes; posts that hide a window
  (`Program.HideWindow`) must minimize, not hide (`LauncherForm.HideWindow`).

## 4. Cross-references

- `docs/XIVLauncher.Core/Program.cs.md` — creates the Eto `Application`, installs
  `launcherForm`, calls the posts.
- `docs/XIVLauncher.Core/LauncherApp.cs.md` — `State` setter, modal pump, `AttachForm`.
- `docs/XIVLauncher.Core/Components/LoadingPage/LoadingPage.cs.md`,
  `docs/XIVLauncher.Core/Components/MainPage/NewsFrame.cs.md`,
  `docs/XIVLauncher.Core/Components/OtpEntryPage.cs.md` — main posting consumers.
- `docs/XIVLauncher.Core/LauncherForm.cs.md` — the `ShowWindow`/`HideWindow` implementations posted here.
