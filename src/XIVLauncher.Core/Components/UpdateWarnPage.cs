using System.Numerics;
using ImGuiNET;

namespace XIVLauncher.Core.Components;

public class UpdateWarnPage : Page
{
    private readonly TextureWrap updateWarnTexture;

    public UpdateWarnPage(LauncherApp app)
        : base(app)
    {
        this.updateWarnTexture = TextureWrap.Load(AppUtil.GetEmbeddedResourceBytes("xlcore_updatewarn.png"));
    }

    public override void Draw()
    {
        ImGui.SetCursorPos(new Vector2(0));

        ImGui.Image(this.updateWarnTexture.ImGuiHandle, new Vector2(1280, 800) * ImGuiHelpers.GlobalScale);

        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);

        ImGui.SetCursorPos(new Vector2(316, 481) * ImGuiHelpers.GlobalScale);

        if (ImGui.Button("###openGuideButton", new Vector2(649, 101) * ImGuiHelpers.GlobalScale))
        {
            Environment.Exit(0);
        }

        ImGui.SetCursorPos(new Vector2(316, 598) * ImGuiHelpers.GlobalScale);

        if (ImGui.Button("###finishFtsButton", new Vector2(649, 101) * ImGuiHelpers.GlobalScale))
        {
            App.FinishFromUpdateWarn();
        }

        ImGui.PopStyleColor(3);

        base.Draw();
    }
}