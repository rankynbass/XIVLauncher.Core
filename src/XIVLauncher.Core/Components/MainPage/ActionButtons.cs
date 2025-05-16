using System.Numerics;

using ImGuiNET;

namespace XIVLauncher.Core.Components.MainPage;

public class ActionButtons : Component
{
    public event Action? OnAccountButtonClicked;
    public event Action? OnStatusButtonClicked;
    public event Action? OnSettingsButtonClicked;

    public override void Draw()
    {
        var btnSize = ImGuiHelpers.GetScaled(new Vector2(80));

        ImGui.PushFont(FontManager.IconFont);
        ImGui.BeginDisabled(this.OnAccountButtonClicked == null);
        if (ImGui.Button(FontAwesomeIcon.User.ToIconString(), btnSize))
        {
            this.OnAccountButtonClicked?.Invoke();
        }
        ImGui.PushFont(FontManager.TextFont);
        ImGuiHelpers.AddTooltip("My Account");
        ImGui.PopFont();
        ImGui.EndDisabled();

        ImGui.SameLine();

        ImGui.BeginDisabled(this.OnStatusButtonClicked == null);
        if (ImGui.Button(FontAwesomeIcon.Heartbeat.ToIconString(), btnSize))
        {
            this.OnStatusButtonClicked?.Invoke();
        }
        ImGui.PushFont(FontManager.TextFont);
        ImGuiHelpers.AddTooltip("Service Status");
        ImGui.PopFont();
        ImGui.EndDisabled();

        ImGui.SameLine();

        ImGui.BeginDisabled(this.OnSettingsButtonClicked == null);
        if (ImGui.Button(FontAwesomeIcon.Cog.ToIconString(), btnSize))
        {
            this.OnSettingsButtonClicked?.Invoke();
        }
        ImGui.PushFont(FontManager.TextFont);
        ImGuiHelpers.AddTooltip("Launcher Settings");
        ImGui.PopFont();
        ImGui.EndDisabled();

        ImGui.PopFont();

        base.Draw();
    }
}
