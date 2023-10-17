using System.Numerics;
using ImGuiNET;
using Serilog;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Core.Components;

public class UpdateCompletePage : Page
{
    private readonly TextureWrap updateCompleteTexture;

    public UpdateCompletePage(LauncherApp app)
        : base(app)
    {
        this.updateCompleteTexture = TextureWrap.Load(AppUtil.GetEmbeddedResourceBytes("xlcore_updatecomplete.png"));
    }

    public override async void Draw()
    {
        ImGui.SetCursorPos(new Vector2(0));

        ImGui.Image(this.updateCompleteTexture.ImGuiHandle, new Vector2(1280, 800) * ImGuiHelpers.GlobalScale);

        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);

        ImGui.SetCursorPos(new Vector2(316, 481) * ImGuiHelpers.GlobalScale);

        if (ImGui.Button("###closeLauncherButton", new Vector2(649, 101) * ImGuiHelpers.GlobalScale))
        {
            Environment.Exit(0);
        }

        ImGui.PopStyleColor(3);

        base.Draw();
    }
}