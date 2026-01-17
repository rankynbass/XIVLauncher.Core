using Hexa.NET.ImGui;

using System.Numerics;

namespace XIVLauncher.Core;

public static class ImGuiHelpers
{
    public static Vector2 ViewportSize => ImGui.GetIO().DisplaySize;
    public static float GlobalScale => x11Scale * ImGui.GetStyle().FontScaleMain;

    // Helper functions to deal with wonky x11 scaling
    private static float x11Scale { get; set; } = 1.0f;

    public static void SetX11Scale(float scale)
    {
        x11Scale = (scale > 0) ? scale : 1.0f;
    }

    public static float ScaleFloat(float pixels)
    {
        return pixels * x11Scale;
    }

    public static float ScaleFloat(int pixels)
    {
        return (float)pixels * x11Scale;
    }

    public static int ScaleInt(float pixels)
    {
        return (int)MathF.Round(pixels * x11Scale, 0);
    }

    public static Vector2 ScaleVector2(Vector2 vec)
    {
        return vec * x11Scale;
    }

    public static Vector2 ScaleVector2(float pixels)
    {
        return new Vector2(pixels) * x11Scale;
    }

    public static Vector2 ScaleVector2(float x, float y)
    {
        return new Vector2(x, y) * x11Scale;
    }

    public static void ScaleDummy(Vector2 vec)
    {
        ImGui.Dummy(ScaleVector2(vec));
    }

    public static void ScaleDummy(float pixels)
    {
        ImGui.Dummy(ScaleVector2(pixels));
    }

    public static void ScaleDummy(float x, float y)
    {
        ImGui.Dummy(ScaleVector2(x, y));
    }

    public static void TextWrapped(string text)
    {
        ImGui.PushTextWrapPos();
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
    }

    public static void CenteredText(string text)
    {
        CenterCursorForText(text);
        ImGui.TextUnformatted(text);
    }

    public static void CenterCursorForText(string text)
    {
        var textWidth = ImGui.CalcTextSize(text).X;
        CenterCursorFor((int)textWidth);
    }

    public static void CenterCursorFor(int itemWidth)
    {
        var window = (int)ImGui.GetWindowWidth();
        ImGui.SetCursorPosX(window / 2 - itemWidth / 2);
    }

    public static void AddTooltip(string text)
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(text);
            ImGui.EndTooltip();
        }
    }
}
