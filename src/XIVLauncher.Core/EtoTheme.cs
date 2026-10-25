using System.Diagnostics;
using System.IO;

namespace XIVLauncher.Core;

/// <summary>
/// Picks the dark GTK theme for the launcher before the Eto application is created.
/// </summary>
/// <remarks>
/// The launcher ships dark artwork and previously shipped a dark ImGui style, so the GTK theme is
/// forced dark. Eto 2.13 cannot be asked for a dark theme directly on GTK3:
/// <c>Application.Theme = Themes.Dark</c> runs <c>ThemeHandler.SetTheme</c>, which computes the dark
/// variant of the current theme name, gets <c>null</c> when the system theme has no <c>-dark</c>
/// variant, and then crashes in <c>Path.Combine(dir, null)</c> (an
/// <c>ArgumentNullException</c> for <c>path2</c>).
/// <para>
/// Setting <c>GTK_THEME</c> before GTK realises its default settings avoids that code path; GTK reads
/// the variable, and Eto is never asked to switch themes. If the variable is already set by the user,
/// it is left alone.
/// </para>
/// </remarks>
public static class EtoTheme
{
    private static readonly string[] themeDirs =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".themes"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "themes"),
        "/usr/share/themes",
        "/usr/local/share/themes",
    ];

    /// <summary>Dark theme names tried in order when the system theme has no dark variant.</summary>
    private static readonly string[] fallbackThemes =
    [
        "Breeze-Dark",
        "Adwaita-dark",
        "Yaru-dark",
        "HighContrastDark",
    ];

    /// <summary>Sets <c>GTK_THEME</c> to a dark theme, if a suitable one is installed.</summary>
    public static void PreferDarkGtkTheme()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GTK_THEME")))
            return;

        // The theme name the desktop is configured with, e.g. 'Breeze'.
        var current = ReadGSettingsTheme();

        foreach (var candidate in DarkCandidates(current))
        {
            if (ThemeExists(candidate))
            {
                Environment.SetEnvironmentVariable("GTK_THEME", candidate);
                return;
            }
        }
    }

    private static IEnumerable<string> DarkCandidates(string? current)
    {
        if (!string.IsNullOrEmpty(current))
        {
            yield return current + "-dark";
            yield return current + ":dark";
        }

        foreach (var theme in fallbackThemes)
            yield return theme;
    }

    private static bool ThemeExists(string themeName)
        => themeDirs.Any(dir => Directory.Exists(Path.Combine(dir, themeName, "gtk-3.0")));

    /// <summary>Reads the configured GTK theme name, returning <c>null</c> when gsettings is unavailable.</summary>
    private static string? ReadGSettingsTheme()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("gsettings", "get org.gnome.desktop.interface gtk-theme")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
            });

            if (process is null)
                return null;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(1000);

            return output.Trim().Trim('\'', '"');
        }
        catch
        {
            // gsettings is not installed outside GNOME-family desktops; the fallback list is used.
            return null;
        }
    }
}
