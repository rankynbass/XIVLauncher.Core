using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using XIVLauncher.Common.Unix.Compatibility;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabDXVK : SettingsTab
{
    public SettingsTabDXVK()
    {
        Entries = new SettingsEntry[]
        {
            new SettingsEntry<Dxvk.DxvkVersion>("DXVK Version", "Choose which version of DXVK to use.", () => Program.Config.DxvkVersion, type => Program.Config.DxvkVersion = type),
            new SettingsEntry<bool>("Enable DXVK ASYNC", "Enable DXVK ASYNC patch.", () => Program.Config.DxvkAsyncEnabled ?? true, b => Program.Config.DxvkAsyncEnabled = b),
            new SettingsEntry<Dxvk.DxvkHudType>("DXVK Overlay", "DXVK Hud is included. MangoHud must be installed separately. ", () => Program.Config.DxvkHudType, type => Program.Config.DxvkHudType = type),
            new NumericSettingsEntry("Frame Rate Limit", "Set a frame rate limit. DXVK will try not exceed it. Use 0 for no limit.", () => Program.Config.DxvkFrameRate ?? 0, i => Program.Config.DxvkFrameRate = i, 0, 1000),
        };
    }

    public override SettingsEntry[] Entries { get; }

    public override bool IsUnixExclusive => true;

    public override string Title => "DXVK";

    public override void Save()
    {
        base.Save();
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Program.CreateCompatToolsInstance();
    }
}
