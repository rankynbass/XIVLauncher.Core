using System.Numerics;
using System.Runtime.InteropServices;

using ImGuiNET;

using XIVLauncher.Common.Unix.Compatibility.Dxvk;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabDxvk : SettingsTab
{
    private ToolSettingsEntry dxvkVersionSetting;

    private ToolSettingsEntry nvapiVersionSetting;

    public SettingsTabDxvk()
    {
        Entries = new SettingsEntry[]
        {
            dxvkVersionSetting = new ToolSettingsEntry("Dxvk Version", "Choose which Dxvk version to use.", () => Program.Config.RB_DxvkVersion ?? Program.DxvkManager.DEFAULT,
            s => Program.Config.RB_DxvkVersion = s, Program.DxvkManager.Version, Program.DxvkManager.DEFAULT),

            new SettingsEntry<bool>("Enable DXVK ASYNC", "Enable DXVK ASYNC patch.", () => Program.Config.DxvkAsyncEnabled ?? true, b => Program.Config.DxvkAsyncEnabled = b)
            {
                CheckVisibility = () => dxvkVersionSetting.Value.Contains("async")
            },

            new SettingsEntry<bool>("Enable GPLAsync Cache", "Enable GPLASync Cache.", () => Program.Config.RB_GPLAsyncCacheEnabled ?? true, b => Program.Config.RB_GPLAsyncCacheEnabled = b)
            {
                CheckVisibility = () => dxvkVersionSetting.Value.Contains("gplasync")
            },

            nvapiVersionSetting = new ToolSettingsEntry("Dxvk-Nvapi Version (Needed for DLSS)", "Choose which version of Dxvk-Nvapi to use. Needs Wine 9.0+ and Dxvk 2.0+", () => Program.Config.RB_NvapiVersion ?? Program.NvapiManager.DEFAULT,
                s => Program.Config.RB_NvapiVersion = s, Program.NvapiManager.Version, Program.NvapiManager.DEFAULT)
            {
                CheckVisibility = () => dxvkVersionSetting.Value != "DISABLED"
            },

            new SettingsEntry<bool>("Enable WineD3D Vulkan Renderer", "Use WineD3D's experimental Vulkan renderer. May be buggy or unstable. Only used if Dxvk is disabled.", () => Program.Config.RB_UseVulkanWineD3D ?? false, b => Program.Config.RB_UseVulkanWineD3D = b)
            {
                CheckVisibility = () => dxvkVersionSetting.Value == "DISABLED"
            },

            new NumericSettingsEntry("Frame Rate Limit (DXVK Only)", "Set a frame rate limit, and DXVK will try not exceed it. Use 0 for unlimited.", () => Program.Config.RB_DxvkFrameRate ?? 0, i => Program.Config.RB_DxvkFrameRate = i, 0, 1000)
            {
                CheckValidity = i =>
                {
                    if (i < 30 && i > 0)
                        return "Frame rate limit must be >= 30, or 0 (unlimited)";
                    return null;
                }
            },
            
            new SettingsEntry<RBHudType>("DXVK Overlay", "Configure how much of the DXVK overlay is to be shown.", () => Program.Config.RB_HudType ?? RBHudType.None, type => Program.Config.RB_HudType = type)
            {
                CheckWarning = s =>
                {
                    if (!Program.IsMangoHudInstalled)
                        return "Could not find MangoHud! MangoHud options may not work.";
                    return null;
                },
            },

            new SettingsEntry<string>("DXVK Hud Custom String", "Custom string for Dxvk Hud", () => Program.Config.RB_DxvkHudCustom ?? "1", s => Program.Config.RB_DxvkHudCustom = s)
            {
                CheckWarning = s =>
                {
                    if (!Dxvk.IsDxvkHudStringValid(s))
                        return "Dxvk string is invalid!";
                    return null;
                }
            },

            new SettingsEntry<string>("MangoHud Custom String", "Custom string for MangoHud", () => Program.Config.RB_MangoHudCustomString ?? "", s => Program.Config.RB_MangoHudCustomString = s),

            new SettingsEntry<string>("MangoHud Custom File", "Custom config file for MangoHud", () => Program.Config.RB_MangoHudCustomFile ?? "", s => Program.Config.RB_MangoHudCustomFile = s)
            {
                CheckWarning = s =>
                {                   
                    if (!File.Exists(s))
                        return "Could not find config file! This setting will not work.";
                    return null;
                }
            },
        };
    }

    public override SettingsEntry[] Entries { get; }

    public override bool IsUnixExclusive => true;

    public override string Title => "Dxvk";

    public override void Draw()
    {
        if (Program.DxvkManager.IsListUpdated)
        {
            Program.DxvkManager.DoneUpdatingDxvkList();
            dxvkVersionSetting.Reset(Program.DxvkManager.Version, Program.DxvkManager.DEFAULT);
        }

        if (Program.NvapiManager.IsListUpdated)
        {
            Program.NvapiManager.DoneUpdatingNvapiList();
            nvapiVersionSetting.Reset(Program.NvapiManager.Version, Program.NvapiManager.DEFAULT);
        }

        base.Draw();
    }

    public override void Save()
    {
        base.Save();
        Program.CreateCompatToolsInstance();
    }
}
