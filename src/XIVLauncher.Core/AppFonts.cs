using System.IO;

using Eto.Drawing;

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

    private static FontFamily? textFamily;
    private static FontFamily? iconFamily;
    private static float size = 21f;

    /// <summary>Registers the embedded fonts. Call once on the UI thread before building UI.</summary>
    public static void Initialize(float pxSize)
    {
        size = pxSize;

        textFamily = FontFamily.FromStreams(OpenResource(TEXT_FONT_NAME));
        iconFamily = FontFamily.FromStreams(OpenResource(ICON_FONT_NAME));
    }

    /// <summary>The text font, at the configured launcher font size.</summary>
    public static Font TextFont => new(textFamily ?? throw new InvalidOperationException("AppFonts.Initialize must run first"), size);

    /// <summary>The Font Awesome icon font, at the same size as the text font.</summary>
    public static Font IconFont => new(iconFamily ?? throw new InvalidOperationException("AppFonts.Initialize must run first"), size);

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
}
