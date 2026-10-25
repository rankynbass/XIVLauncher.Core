# LauncherForm.cs — Specification

**File:** `src/XIVLauncher.Core/LauncherForm.cs` · 57 lines · 1941 bytes
**TargetFramework:** net10.0 (Linux/GTK3 build)
**Role:** the launcher window (`Eto.Forms.Form`, https://github.com/picoe/Eto), replacing the SDL window: hosts
the current launcher page over the background artwork, and owns modal dialogs.

---

## 1. Purpose

- Title `XIVLauncher {version} ({hash})` — reproduces the SDL window title, nightly hash included
  (line 22).
- `ClientSize = new Size(1280, 800)` — fixed launcher window size (line 23).
- Content is a `PixelLayout` (line 25). Children:
  1. `Background` added first at `0,0`, sized 1280×800 (lines 31–32).
  2. `pageHost` (`Panel`), sized 1280×800, added second at `0,0` (lines 34–35).
  Because GTK `PixelLayout` is a `Gtk.Fixed`, adding the page host after the background paints it
  over the artwork, reproducing the SDL layer order.
- `ShowPage(Page page)` — `pageHost.Content = page`, replacing the displayed page (line 39).
- `ShowWindow()` — `Visible = true` and `WindowState = WindowState.Normal` (lines 41–45).
- `HideWindow()` — `WindowState = WindowState.Minimized` (lines 47–53).
- `ModalOwner` — returns `this` as the window that owns modal dialogs (line 56).

## 2. Consumers

| Consumer | Site | API |
| --- | --- | --- |
| `Program.cs` | line 278 | `launcherForm = new LauncherForm(launcherApp, version, hash)` |
| `Program.cs` | line 279 | `launcherApp.AttachForm(launcherForm)` |
| `Program.cs` | lines 283–284 | `application.MainForm = launcherForm; launcherForm.Show();` |
| `LauncherApp.cs` | line 179 | `this.PageShown += form.ShowPage` — every state change routes the current page here |
| `LauncherApp.cs` | line 204 | `dialog.ShowModal(this.form)` — the form as modal owner |
| `Program.cs` | line 319 | `Ui.Post(() => launcherForm.ShowWindow())` in `Program.ShowWindow` |
| `Program.cs` | line 322 | `Ui.Post(() => launcherForm.HideWindow())` in `Program.HideWindow` |
| `Components/MainPage/MainPage.cs` | line 1145 | `Program.HideWindow()` in `Hide()` — game-start path |
| `Components/MainPage/MainPage.cs` | line 1153 | `Program.ShowWindow()` in `Reactivate()` |

`ModalOwner` has no consumer; `LauncherApp.ShowMessage` passes `this.form` directly.

## 3. Quirks

- **`HideWindow()` must minimize, not hide.** Under GTK, `GApplication` exits when the last visible
  window closes. `Visible = false` on the main form ends the launcher's GTK loop, which previously
  caused the login test to terminate the process when `Reactivate()` posted a restore. Minimizing is
  the Eto equivalent of the SDL `SDL_HideWindow` used by the login pipeline, and keeps the process
  alive: verified by login runs that reached `Reactivated after Login() != true` with the window
  restored.
- `ShowWindow()` restores `WindowState.Normal` as well as `Visible = true`; a restore on a
  non-minimized window is a no-op, so calling it after `Show()` is safe.
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
