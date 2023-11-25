using System.Diagnostics;
using System.Numerics;
using ImGuiNET;
using XIVLauncher.Common.Unix.Compatibility;
using XIVLauncher.Common.Util;
using XIVLauncher.Core.Support;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabAbout : SettingsTab
{
    private readonly TextureWrap logoTexture;

    public override SettingsEntry[] Entries { get; } =
    {
        new NumericSettingsEntry("Global Scale Percent 100% - 400% (Needs Restart)","", () => (int)((Program.Config.GlobalScale ?? 1.0f) * 100), i => Program.Config.GlobalScale = (float)i / 100f, 100, 400, 25),
    };

    public override string Title => "About";

    public SettingsTabAbout()
    {
        this.logoTexture = TextureWrap.Load(AppUtil.GetEmbeddedResourceBytes("logo.png"));
    }

    public override void Draw()
    {
        ImGui.Image(this.logoTexture.ImGuiHandle, new Vector2(256) * ImGuiHelpers.GlobalScale);

        ImGui.Text($"XIVLauncher-RB v{AppUtil.GetAssemblyVersion()}({AppUtil.GetGitHash()})");
        ImGui.Text("By goaaats, with patches by Rankyn Bass");

        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            AppUtil.OpenBrowser("https://github.com/goaaats");

        ImGui.Dummy(new Vector2(20) * ImGuiHelpers.GlobalScale);

        if (ImGui.Button("Open Repository"))
        {
            AppUtil.OpenBrowser("https://github.com/rankynbass/XIVLauncher.Core/tree/RB-patched");
        }

        ImGui.Dummy(new Vector2(20) * ImGuiHelpers.GlobalScale);

        if (ImGui.Button("Join our Discord"))
        {
            AppUtil.OpenBrowser("https://discord.gg/3NMcUV5");
        }

        ImGui.Dummy(new Vector2(20) * ImGuiHelpers.GlobalScale);

        if (ImGui.Button("See Software Licenses"))
        {
            PlatformHelpers.OpenBrowser(Path.Combine(AppContext.BaseDirectory, "license.txt"));
        }

        ImGui.Dummy(new Vector2(20) * ImGuiHelpers.GlobalScale);

        base.Draw();
    }
}