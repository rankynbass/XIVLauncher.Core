using System.Numerics;
using ImGuiNET;
using Serilog;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Core.Components;

public class UpdateFailedPage : Page
{
    private readonly TextureWrap updateFailedTexture;

    public UpdateFailedPage(LauncherApp app)
        : base(app)
    {
        this.updateFailedTexture = TextureWrap.Load(AppUtil.GetEmbeddedResourceBytes("xlcore_updatefailed.png"));
    }

    public override async void Draw()
    {
        ImGui.SetCursorPos(new Vector2(0));

        ImGui.Image(this.updateFailedTexture.ImGuiHandle, new Vector2(1280, 800) * ImGuiHelpers.GlobalScale);

        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);

        ImGui.SetCursorPos(new Vector2(316, 481) * ImGuiHelpers.GlobalScale);

        if (ImGui.Button("###continueToMainButton", new Vector2(649, 101) * ImGuiHelpers.GlobalScale))
        {
            App.FinishFromUpdateWarn();
        }

        ImGui.SetCursorPos(new Vector2(316, 598) * ImGuiHelpers.GlobalScale);

        if (ImGui.Button("###closeLauncherButton", new Vector2(649, 101) * ImGuiHelpers.GlobalScale))
        {
            Environment.Exit(0);
        }

        ImGui.PopStyleColor(3);

        base.Draw();
    }
}