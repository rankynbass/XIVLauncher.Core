using Eto.Drawing;

namespace XIVLauncher.Core;

/// <summary>
/// The launcher palette as Eto colors, taken from the values previously used by the ImGui style.
/// </summary>
/// <remarks>
/// Native widgets take their colours from the active theme; these colors are used only where text is
/// coloured explicitly (errors, descriptions, warnings).
/// </remarks>
public static class EtoColors
{
    public static Color Red { get; } = From(1f, 0f, 0f);
    public static Color Grey { get; } = From(0.7f, 0.7f, 0.7f);
    public static Color Grey2 { get; } = From(0.7f, 0.7f, 0.7f);
    public static Color Grey3 { get; } = From(0.5f, 0.5f, 0.5f);
    public static Color White { get; } = From(1f, 1f, 1f);
    public static Color Orange { get; } = From(1f, 0.709f, 0f);
    public static Color Yellow { get; } = From(1f, 1f, 0.4f);
    public static Color Violet { get; } = From(0.770f, 0.700f, 0.965f);

    /// <summary>Launcher blue (the previous button color).</summary>
    public static Color Blue { get; } = From(0.3647058823529412f, 0.4549019607843137f, 0.6745098039215686f);

    /// <summary>Window background blue shade.</summary>
    public static Color BlueShade0 { get; } = From(0.1686274509803922f, 0.1882352941176471f, 0.2313725490196078f);

    /// <summary>Frame background blue shade.</summary>
    public static Color BlueShade1 { get; } = From(0.2f, 0.2235294117647059f, 0.2784313725490196f);

    public static Color BlueShade2 { get; } = From(0.2705882352941176f, 0.3019607843137255f, 0.3725490196078431f);
    public static Color BlueShade3 { get; } = From(0.2980392156862745f, 0.3843137254901961f, 0.5803921568627451f);

    public static Color Text { get; } = From(0.7843137254901961f, 0.796078431372549f, 0.8196078431372549f);

    /// <summary>The OTP child box: the previous window background (0.06 grey) at 40% alpha.</summary>
    public static Color OtpChildBackground { get; } = Color.FromArgb(102, 15, 15, 15);

    private static Color From(float r, float g, float b)
        => Color.FromArgb((int)Math.Round(r * 255), (int)Math.Round(g * 255), (int)Math.Round(b * 255));
}
