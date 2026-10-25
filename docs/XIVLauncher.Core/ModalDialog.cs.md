# ModalDialog.cs — Specification

**File:** `src/XIVLauncher.Core/ModalDialog.cs` · 52 lines · 1789 bytes
**TargetFramework:** net10.0 (Linux/GTK3 build)
**Role:** single-button modal dialog (`Eto.Forms.Dialog`, https://github.com/picoe/Eto), replacing the ImGui modal
popup; pumps the UI loop while open, which keeps `ShowMessageBlocking` blocking.

---

## 1. Purpose

- `Title = title`, `ClientSize = new Size(500, 300)` (lines 17–18).
- Centered `Button { Text = buttonText, Width = 150 }` (line 20).
- Button click: `this.Close()` then `buttonPressAction()` (lines 21–25).
- `CenterOverMainForm()` — centers the dialog over `Application.Instance.MainForm` using
  `owner.Location` + `owner.ClientSize`, applied from `this.Shown` (lines 29–40).
- Content: `StackLayout`, padding 16, wrapped `Label` (expand) plus centered button (lines 44–50).

## 2. Consumers

| Consumer | Site | Use |
| --- | --- | --- |
| `LauncherApp.cs` | line 197 | `new ModalDialog(text, title, modalButtonText, () => { modalWaitHandle.Set(); pressedAction(); })` |
| `LauncherApp.cs` | line 204 | `dialog.ShowModal(this.form)` when the launcher form is attached |
| `LauncherApp.cs` | line 206 | `dialog.ShowModal()` when no form is attached |

`LauncherApp.ShowMessageBlocking` waits on the `ManualResetEvent` set by the dialog button; the modal
button press closes the dialog and unblocks the caller. Login pipeline verification reached
`Reactivated after Login() != true` after the button's `Close()` ran, confirming the pump/`Set()`/`Close()`
chain.

## 3. Quirks

- **No `CenterScreen` exists in Eto.** Eto `Window`/`Dialog` expose no `CenterScreen` property, and
  grep across `src/Eto/Forms/Window.cs`, `Dialog.cs`, and `Eto.Gtk/Forms/DialogHandler.cs` finds none;
  `strings` on `~/.nuget/packages/eto.forms/2.13.0/lib/net10.0/Eto.dll` reports zero hits.
  A `Location` assigned before the window is realized is ignored by GTK top-level placement, so the
  centering must be applied on `Shown` — the dialog realizes, GTK positions it, then Eto moves it.
- `ShowModal()` in `Eto.Gtk/Forms/DialogHandler.cs` runs a `Control.Run()` loop until `WasClosed`
  (lines 105–121), then resets `WasClosed = false` for reuse. That loop is the UI pump for
  `ShowMessageBlocking`.
- `CenterOverMainForm` uses `owner.ClientSize` (not `owner.Bounds`), so the offset accounts for window
  decorations. Verified: modal window at `887,504`, geometry `789x448`, centered over the main window
  at `640,339` (1280×800).
- The `ClientSize` 500×300 is the old ImGui modal size, not the realized size: GTK reports
  `789x448` for the dialog, because the label text plus `Button` padding expand the client area. The
  centered geometry observed in login runs is the real size.
- The button lambda takes `(_, _)`: Eto `Button.Click` is `EventHandler<EventArgs>`, so a zero-arg
  lambda is not valid.
- `Close()` is called before `buttonPressAction`, so the action runs after the dialog closes; the
  `ManualResetEvent` is set inside the action, guaranteeing the modal loop has exited first.
- `openModals` in `LauncherApp` guards re-entrancy: `ShowMessage` throws
  `InvalidOperationException("Cannot open modal while another modal is open")` when a modal is already
  pumping.

## 4. Cross-references

- `docs/XIVLauncher.Core/LauncherApp.cs.md` — `ShowMessage`, `ShowMessageBlocking`, `ShowExceptionBlocking`,
  `HandleContinuationBlocking`, `modalWaitHandle`, `openModals`.
- `docs/XIVLauncher.Core/LauncherForm.cs.md` — modal owner and window state.
- `docs/XIVLauncher.Core/Ui.cs.md` — `Ui.Post` opens the dialog on the UI thread.
