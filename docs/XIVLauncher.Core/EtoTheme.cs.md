# EtoTheme.cs — Specification

**File:** `src/XIVLauncher.Core/EtoTheme.cs` · 100 lines · 3518 bytes
**TargetFramework:** net10.0 (Linux/GTK3 build)
**Role:** selects a dark GTK3 theme for the launcher through `GTK_THEME` before Eto starts, replacing the
dark `imgui.net` style; avoids the Eto `Themes.Dark` crash path.

---

## 1. Purpose

`EtoTheme.PreferDarkGtkTheme()` sets the `GTK_THEME` environment variable to a dark theme that
exists on disk, before `new Application(...)` realizes GTK settings:

1. Return immediately if `GTK_THEME` is already set (user override, lines 41–43).
2. Read the configured desktop theme name from
   `gsettings get org.gnome.desktop.interface gtk-theme` (line 96), stripping the quote characters
   GNOME prints; `null` when `gsettings` is unavailable.
3. Try, in order, `current + "-dark"`, `current + ":dark"`, then the fallback names
   `Breeze-Dark`, `Adwaita-dark`, `Yaru-dark`, `HighContrastDark` (lines 29–36, 67–76).
4. Test each candidate for a `<theme>/gtk-3.0` directory in the GTK theme search paths:
   `~/.themes`, `~/.local/share/themes`, `/usr/share/themes`, `/usr/local/share/themes` (lines 20–25).
5. Set `GTK_THEME` to the first candidate that exists and return (lines 47–53).

The launcher ships dark artwork, and the previous ImGui style was dark, so GTK's widget colors are
forced dark; native widgets read the theme, not `EtoColors`.

## 2. Consumers

| Consumer | Site |
| --- | --- |
| `Program.cs` | line 263 — `EtoTheme.PreferDarkGtkTheme();` immediately before `new Application { Name = APP_NAME }` |

## 3. Quirks

- **`Application.Theme = Themes.Dark` is unusable on GTK3 in Eto 2.13.**
  `Eto.GtkSharp.Forms.ThemeHandler.SetTheme` computes the dark variant of the current theme name,
  gets `null` when the system theme has no dark variant, then calls `Path.Combine(dir, null)` and
  throws `System.ArgumentNullException: Value cannot be null. (Parameter 'path2')` from
  `ThemeHandler.ThemeExists`. On this machine the configured theme is `Breeze`, so the direct Eto
  path crashed at startup. `PreferDarkGtkTheme` sets `GTK_THEME` before GTK reads its default
  settings, so Eto is never asked to switch themes and the handler path is not reached.
- `gsettings` is GNOME-family only. On non-GNOME desktops the process launch throws and is swallowed
  (line 89), so the fallback theme list is the only path.
- Theme existence is checked for `gtk-3.0` only; GTK4 theme directories are not considered because
  the Eto platform in use is `Eto.Platform.Gtk` (GTK3, `Platforms.Gtk`).
- `gsettings` output is a quoted string (`'Breeze'`); the code strips `'` and `"` only, so a theme
  name containing spaces is preserved.
- If no candidate theme exists, the function leaves `GTK_THEME` unset and the launcher runs with the
  user's theme — the app is usable, only the appearance is light.
- `Process` timeout is `WaitForExit(1000)`; a hung `gsettings` is treated as no theme, not a crash.

## 4. Cross-references

- `docs/XIVLauncher.Core/Program.cs.md` — the only consumer, in the Eto bootstrap.
- `docs/XIVLauncher.Core/EtoColors.cs.md` — explicit text colors used on top of the theme.
- `docs/XIVLauncher.Core/Ui.cs.md` — GTK platform details (`GtkSynchronizationContext`).
