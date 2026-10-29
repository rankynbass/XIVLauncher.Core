using System.IO;
using System.Runtime.InteropServices;

using Eto.Drawing;

using Serilog;

namespace XIVLauncher.Core;

/// <summary>
/// Registers the launcher's embedded fonts with the Eto platform.
/// </summary>
/// <remarks>
/// Replaces the ImGui font manager. Fonts must be registered on the UI thread once, before any control
/// is built that uses them; the resulting families are shared by every control.
/// </remarks>
public static class AppFonts
{
    private const string TEXT_FONT_NAME = "NotoSansCJKjp-Regular.otf";
    private const string ICON_FONT_NAME = "FontAwesome5FreeSolid.otf";

    /// <summary>The family name GDI enumerates the embedded Noto font by (name table ID 1 - GDI uses
    /// the full font name, not the preferred family name ID 16, for InstalledFontCollection).</summary>
    private const string TEXT_FAMILY_NAME = "Noto Sans CJK JP Regular";

    /// <summary>The family name GDI enumerates the embedded Font Awesome font by (name table ID 1).
    /// The file carries only the Solid face.</summary>
    private const string ICON_FAMILY_NAME = "Font Awesome 5 Free Solid";

    private static FontFamily? textFamily;
    private static FontFamily? iconFamily;
    private static float size = 21f;

    /// <summary>The launcher-wide default text size: every control that carries no explicit font
    /// draws with the embedded Noto font at this size.</summary>
    private const float DEFAULT_TEXT_SIZE = 11f;

    /// <summary>Registers the embedded fonts, then replaces the platform default font with the
    /// embedded Noto font. Call once on the UI thread before building UI.</summary>
    public static void Initialize(float pxSize)
    {
        size = pxSize;

#if LINUX
        textFamily = FontFamily.FromStreams(OpenResource(TEXT_FONT_NAME));
        iconFamily = FontFamily.FromStreams(OpenResource(ICON_FONT_NAME));
#else
        textFamily = RegisterWindowsFont(TEXT_FONT_NAME, TEXT_FAMILY_NAME);
        iconFamily = RegisterWindowsFont(ICON_FONT_NAME, ICON_FAMILY_NAME);
#endif
        ApplyDefaultTextFont();
    }

    /// <summary>Replaces the platform default font with the embedded Noto font at 11pt. Runs on the
    /// UI thread before any control is created, so every font-less control picks up the new default.</summary>
    private static void ApplyDefaultTextFont()
    {
#if LINUX
        // The gtk-font-name setting drives the default font of every widget Eto does not give an
        // explicit font; the family is fontconfig-visible because FontFamily.FromStreams added the
        // file through FcConfigAppFontAddFile (Eto.Gtk FontTypefaceHandler).
        var settingsType = Type.GetType("Gtk.Settings, GtkSharp")!;
        var settings = settingsType.GetProperty("Default")!.GetValue(null)!;
        settingsType.GetProperty("FontName")!.SetValue(settings, $"Noto Sans CJK JP {DEFAULT_TEXT_SIZE}");
#else
        // WinForms Control.DefaultFont drives every control created after this point; Eto's WinForms
        // handlers leave a control's font at the default until one is set explicitly. Types are read
        // through reflection: the project references Eto.WinForms, not System.Windows.Forms directly.
        var appType = Type.GetType("System.Windows.Forms.Application, System.Windows.Forms")!;
        var familyType = Type.GetType("System.Drawing.FontFamily, System.Drawing.Common")!;
        var styleType = Type.GetType("System.Drawing.FontStyle, System.Drawing.Common")!;
        var unitType = Type.GetType("System.Drawing.GraphicsUnit, System.Drawing.Common")!;
        try
        {
            var family = Activator.CreateInstance(familyType, TEXT_FAMILY_NAME)!;
            // FontStyle.Regular = 0, GraphicsUnit.Point = 3.
            appType.GetMethod("SetDefaultFont", new[] { familyType, typeof(float), styleType, unitType })!
                .Invoke(null, new object[] { family, DEFAULT_TEXT_SIZE, Enum.GetValues(styleType).GetValue(0), Enum.GetValues(unitType).GetValue(3) });
        }
        catch (Exception e)
        {
            Log.Warning("SetDefaultFont failed for {Family}: {Message}", TEXT_FAMILY_NAME, e.Message);
        }
#endif
    }

    /// <summary>The text font, at the configured launcher font size.</summary>
    public static Font TextFont => new(textFamily ?? throw new InvalidOperationException("AppFonts.Initialize must run first"), size);

    /// <summary>The Font Awesome icon font, at the same size as the text font.</summary>
    public static Font IconFont => new(iconFamily ?? throw new InvalidOperationException("AppFonts.Initialize must run first"), size);

    /// <summary>The Font Awesome icon font at a given size, for buttons whose glyph must fit a fixed box
    /// (WinForms sizes a button's height from its font, so the HEAD 45 px icon buttons need a smaller glyph).</summary>
    public static Font IconFontAt(float size) => new(iconFamily ?? throw new InvalidOperationException("AppFonts.Initialize must run first"), size);

    /// <summary>The glyph for a Font Awesome icon, to be drawn with <see cref="IconFont"/>.</summary>
    public static string Glyph(FontAwesomeIcon icon) => icon.ToIconString();

    /// <summary>Makes a button show an icon glyph, using the Font Awesome font.</summary>
    public static void ApplyGlyph(Eto.Forms.Button button, FontAwesomeIcon icon)
    {
        button.Font = IconFont;
        button.Text = Glyph(icon);
    }

    private static Stream OpenResource(string resourceName)
        => new MemoryStream(AppUtil.GetEmbeddedResourceBytes(resourceName));

#if !LINUX
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern int AddFontResourceEx(string lpszFilename, uint fl, IntPtr pdnlOls);

    /// <summary>Registers an embedded font with the session font table, and returns the family for
    /// its preferred family name.</summary>
    /// <remarks>
    /// Eto's WinForms platform loads stream fonts through a GDI+ PrivateFontCollection
    /// (AddMemoryFont), which GDI cannot see; GDI-based controls - Button, Label - draw text with GDI,
    /// so a glyph font loaded only from memory renders as tofu boxes on buttons. Eto also resolves a
    /// FontFamily by name through InstalledFontCollection, and private fonts (FR_PRIVATE) are not
    /// enumerated there, so the font is registered with the plain session font table: it is then
    /// enumerable by family name and visible to every control. It is removed when the process exits.
    /// </remarks>
    private static FontFamily RegisterWindowsFont(string resourceName, string familyName)
    {
        var dir = Path.Combine(Path.GetTempPath(), "xlcore-fonts");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, resourceName);

        var bytes = AppUtil.GetEmbeddedResourceBytes(resourceName);
        if (!File.Exists(path) || new FileInfo(path).Length != bytes.Length)
            File.WriteAllBytes(path, bytes);

        if (AddFontResourceEx(path, 0, IntPtr.Zero) == 0)
            Log.Warning("AddFontResourceEx failed for {Font}", path);

        var installed = new System.Drawing.Text.InstalledFontCollection();
        var found = installed.Families.Any(f => string.Equals(f.Name, familyName, StringComparison.OrdinalIgnoreCase));
        if (!found)
        {
            Log.Warning("Family '{Family}' not in the font table after registering {Font}; enumerated: {Names}",
                familyName, path,
                string.Join(", ", installed.Families.Select(f => f.Name).Where(n => n.Contains('N') || n.Contains("Font"))));
        }

        if (found)
            return new FontFamily(familyName);

        // GDI refused the font (a font this large can exceed the font table); fall back to Eto's
        // stream registration, which serves the family through GDI+ for the controls that use it.
        Log.Warning("Falling back to stream registration for {Family}", familyName);
        return FontFamily.FromStreams(OpenResource(resourceName));
    }
#endif
}
