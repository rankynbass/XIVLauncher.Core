# LauncherForm.cs — Specification

**File:** `src/XIVLauncher.Core/LauncherForm.cs` · 117 lines · 3,944 bytes
**TargetFramework:** net10.0 (Linux/GTK3 build); net10.0-windows7.0 for `win-x64` (`Eto.Platform.Windows`)
**Role:** the launcher window (`Eto.Forms.Form`, https://github.com/picoe/Eto), replacing the SDL window: hosts
the current launcher page over the background artwork, and owns modal dialogs.

---

## 1. Purpose

- Title `XIVLauncher {version} ({hash})` — reproduces the SDL window title, nightly hash included
  (line 24).
- `ClientSize = new Size(1280, 800)` — fixed launcher window size (line 25).
- Content is a `PixelLayout` (line 27). Children:
  1. `Background` added first at `0,0`, sized 1280×800 (lines 33–34).
  2. `pageHost` (`Panel`), sized 1280×800, added second at `0,0` (lines 36–37).
  Because GTK `PixelLayout` is a `Gtk.Fixed`, adding the page host after the background paints it
  over the artwork, reproducing the SDL layer order.
- `ShowPage(Page page)` — `Ui.AssertUIThread("LauncherForm.ShowPage")` (line 43) then `pageHost.Content = page` (line 44), replacing the displayed page. UI thread only.
- `ShowWindow()` — `Ui.AssertUIThread("LauncherForm.ShowWindow")` (line 49), then `Visible = true` (line 50), `WindowState = WindowState.Normal` (line 51), `ReleaseApplication()` (line 52).
- `HideWindow()` — `Ui.AssertUIThread("LauncherForm.HideWindow")` (line 57), then `HoldApplication()` (line 62) and `Visible = false` (line 63): true hide (the window is unmapped), not a minimise.
- `ModalOwner` — returns `this` as the window that owns modal dialogs (line 67).
- `#if LINUX` GApplication helpers (`appHolds` line 70, `DllImport` `g_application_hold`/`g_application_release` lines 72–76, `GApplicationHandle()` line 80, `HoldApplication()` line 87, `ReleaseApplication()` line 97): Eto keeps `Gtk.Application` on its internal handler, so the native pointer is read through reflection; the counter keeps holds and releases balanced. The `#else` branch (lines 109–116) compiles `HoldApplication()`/`ReleaseApplication()` as no-op stubs on Windows/macOS: WinForms app lifetime is not tied to a window count, so hiding the form is safe with no hold.

## 2. Consumers

| Consumer | Site | API |
| --- | --- | --- |
| `Program.cs` | line 287 | `launcherForm = new LauncherForm(launcherApp, version, hash)` |
| `Program.cs` | line 288 | `launcherApp.AttachForm(launcherForm)` |
| `Program.cs` | lines 290–291 | `application.MainForm = launcherForm; launcherForm.Show();` |
| `LauncherApp.cs` | line 179 | `this.PageShown += form.ShowPage` — every state change routes the current page here (posted, because `State` is set from background threads) |
| `LauncherApp.cs` | line 204 | `dialog.ShowModal(this.form)` — the form as modal owner |
| `Program.cs` | line 372 | `Ui.Post(() => launcherForm.ShowWindow())` in `Program.ShowWindow` |
| `Program.cs` | line 375 | `Ui.Post(() => launcherForm.HideWindow())` in `Program.HideWindow` |
| `Components/MainPage/MainPage.cs` | line 1157 | `Program.HideWindow()` in `Hide()` — game-start path |
| `Components/MainPage/MainPage.cs` | line 1165 | `Program.ShowWindow()` in `Reactivate()` |
| `Program.cs` | lines 316, 325 | `Ui.Run(() => Program.HideWindow())` / `Ui.Run(() => Program.ShowWindow())` in the Debug selftest |

`ModalOwner` has no consumer; `LauncherApp.ShowMessage` passes `this.form` directly.

## 3. Quirks

- **`HideWindow()` hides the window, it does not minimize it.** Under GTK, `GApplication` exits when
  its last window closes, and `Visible = false` unmaps the main window (`GtkWindow.IsMapped` becomes
  false), dropping that window count. So `HideWindow()` holds the `GApplication`
  (`g_application_hold`, in `libgio-2.0.so.0`) before unmap, and `ShowWindow()` releases it
  (`g_application_release`) after remap; the launcher loop keeps running with no mapped window, as the
  game-start path requires. Verified with an Eto 2.13/GTK3 probe: after hide, `Visible=False` and
  `IsMapped=False` for 6s, 30 UI posts processed while hidden, restore back to
  `Visible=True`/`IsMapped=True`, app loop alive; and in the launcher's Debug selftest
  (`Program.cs.md` §6), which hides, posts 20 UI writes, and restores.
  In the login run, the launcher window disappears at game launch, the process stays alive through
  `Waiting for game to exit`, and exits after `Stopping addons...`.
  The P/Invoke names are Linux-only (`#if LINUX`); Windows/macOS builds compile `HoldApplication()`
  and `ReleaseApplication()` as no-op stubs (lines 109–116), so on Windows `HideWindow()` is just
  the form hide and the loop survives by virtue of WinForms' own message loop.
- `ShowWindow()` restores `WindowState.Normal` as well as `Visible = true`; a restore on a
  non-minimized window is a no-op, so calling it after `Show()` is safe. `ReleaseApplication()` is a
  no-op when no holds are outstanding, so an unmatched restore is harmless.
- **The three `Ui.AssertUIThread` calls are Debug-only** (`[Conditional("DEBUG")]`, `Ui.cs.md` §1), so
  release builds compile them away. They log the offending managed stack to `launcher.log` instead of
  throwing, so a Debug login run keeps going and the violation is visible in the log. Eto's own check
  (`Application.EnsureUIThread`, reached through `Control.Handler`) fires on every widget access, read
  or write, so it catches sites that have no launcher guard.
- `Eto.Forms.Window` has no `CenterScreen`; Eto 2.13.0 exposes `WindowState`
  (`Normal`/`Maximized`/`Minimized`) and `Topmost` only (`src/Eto/Forms/Window.cs`).
  Modal centering lives in `ModalDialog.Shown`, not here.
- `PixelLayout` positions children at fixed pixel coordinates. The window size is fixed, so both
  children fill the window. Resizing the window would break the layout — the launcher has no resize
  path today.
- `Background` is a `Panel`, sized here, not by `Background.cs`
  (`docs/XIVLauncher.Core/Components/Background.cs.md` sizes its own `ImageView` to 1280×800 as well).
- The hidden 10×10 `XIVLauncher.Core` window that `xdotool` reports alongside the launcher window is
  Eto's GTK application helper window (`Gtk.Application`), not a launcher page.

## 4. Cross-references

- `docs/XIVLauncher.Core/LauncherApp.cs.md` — `LauncherState` machine, `PageShown`, `AttachForm`, modal owner.
- `docs/XIVLauncher.Core/Program.cs.md` — creates the form, shows it, and posts `ShowWindow`/`HideWindow`.
- `docs/XIVLauncher.Core/Components/Page.cs.md` — page contract hosted here.
- `docs/XIVLauncher.Core/ModalDialog.cs.md` — centers over `Application.Instance.MainForm`.
- `docs/XIVLauncher.Core/Ui.cs.md` — thread marshalling for window visibility.
