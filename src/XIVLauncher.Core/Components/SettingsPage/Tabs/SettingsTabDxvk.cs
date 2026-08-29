using Hexa.NET.ImGui;

using System.Numerics;
using System.Runtime.InteropServices;

using XIVLauncher.Common.Unix.Compatibility.Dxvk;
using XIVLauncher.Core.Resources.Localization;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabDxvk : SettingsTab
{
    private ToolSettingsEntry dxvkVersionSetting;

    private ToolSettingsEntry nvapiVersionSetting;

    private SettingsEntry<bool> protonDxvkSetting;

    public SettingsTabDxvk()
    {
        Entries = new SettingsEntry[]
        {
            dxvkVersionSetting = new ToolSettingsEntry("Wine: " + Strings.DXVKVersionSetting, Strings.DXVKVersionSettingDescription, () => Program.Config.RB_DxvkVersion ?? Program.DxvkManager.DEFAULT,
            s => Program.Config.RB_DxvkVersion = s, Program.DxvkManager.Version, Program.DxvkManager.DEFAULT),

            new SettingsEntry<bool>(Strings.DXVKEnableAsyncSetting, Strings.DXVKEnableAsyncSettingDescription, () => Program.Config.DxvkAsyncEnabled ?? true, b => Program.Config.DxvkAsyncEnabled = b)
            {
                CheckVisibility = () => dxvkVersionSetting.Value.Contains("async")
            },

            new SettingsEntry<bool>(Strings.GPLAsyncSetting, Strings.GPLAsyncSettingDescription, () => Program.Config.RB_GPLAsyncCacheEnabled ?? true, b => Program.Config.RB_GPLAsyncCacheEnabled = b)
            {
                CheckVisibility = () => dxvkVersionSetting.Value.Contains("gplasync")
            },

            nvapiVersionSetting = new ToolSettingsEntry(Strings.NvapiVersionSetting, Strings.NvapiVersionSettingDescription, () => Program.Config.RB_NvapiVersion ?? Program.NvapiManager.DEFAULT,
                s => Program.Config.RB_NvapiVersion = s, Program.NvapiManager.Version, Program.NvapiManager.DEFAULT)
            {
                CheckVisibility = () => dxvkVersionSetting.Value != "DISABLED"
            },

            new SettingsEntry<bool>("Wine: " + Strings.WineD3DSetting, Strings.WineD3DSettingDescription, () => Program.Config.RB_UseVulkanWineD3D ?? false, b => Program.Config.RB_UseVulkanWineD3D = b)
            {
                CheckVisibility = () => dxvkVersionSetting.Value == "DISABLED"
            },

            protonDxvkSetting = new SettingsEntry<bool>("Proton: " + Strings.ProtonDXVKSetting, Strings.ProtonDXVKSettingDescription, () => Program.Config.RB_DxvkEnabled ?? true, b => Program.Config.RB_DxvkEnabled = b),

            new SettingsEntry<bool>("Proton: " + Strings.ProtonNvapiSetting, Strings.ProtonNvapiSettingDescription, () => Program.Config.RB_NvapiEnabled ?? true, b => Program.Config.RB_NvapiEnabled = b)
            {
                CheckVisibility = () => protonDxvkSetting.Value == true
            },

            new SettingsEntry<bool>("Proton: " + Strings.WineD3DSetting, Strings.WineD3DSettingDescription, () => Program.Config.RB_ProtonUseVulkanWineD3D ?? false, b => Program.Config.RB_ProtonUseVulkanWineD3D = b)
            {
                CheckVisibility = () => protonDxvkSetting.Value == false
            },

            new NumericSettingsEntry(Strings.FrameRateSetting, Strings.FrameRateSettingDescription, () => Program.Config.RB_DxvkFrameRate ?? 0, i => Program.Config.RB_DxvkFrameRate = i, 0, 1000, 0)
            {
                CheckValidity = i =>
                {
                    if (i < 30 && i > 0)
                        return Strings.FrameRateInvalid;
                    return null;
                }
            },
            
            new SettingsEntry<RBHudType>(Strings.DXVKOverlaySetting, Strings.DXVKOverlaySettingDescription, () => Program.Config.RB_HudType ?? RBHudType.None, type => Program.Config.RB_HudType = type)
            {
                CheckWarning = s =>
                {
                    if (!CoreEnvironmentSettings.IsMangoHudInstalled)
                        return Strings.MangoHudNotFound;
                    return null;
                },
            },

            new SettingsEntry<string>(Strings.DXVKHudCustomSetting, Strings.DXVKHudCustomSettingDescription, () => Program.Config.RB_DxvkHudCustom ?? "1", s => Program.Config.RB_DxvkHudCustom = s)
            {
                CheckWarning = s =>
                {
                    if (!Dxvk.IsDxvkHudStringValid(s))
                        return Strings.DXVKCustomInvalid;
                    return null;
                }
            },

            new SettingsEntry<string>(Strings.MangoHudStringSetting, Strings.MangoHudStringSettingDescription, () => Program.Config.RB_MangoHudCustomString ?? "", s => Program.Config.RB_MangoHudCustomString = s),

            new SettingsEntry<string>(Strings.MangoHudFileSetting, Strings.MangoHudFileSettingDescription, () => Program.Config.RB_MangoHudCustomFile ?? "", s => Program.Config.RB_MangoHudCustomFile = s)
            {
                CheckWarning = s =>
                {                   
                    if (!File.Exists(s))
                        return Strings.MangoHudFileNotFound;
                    return null;
                }
            },

            new SettingsEntry<string>(Strings.MangoHudExtraArgs, Strings.MangoHudExtraArgsDescription, () => Program.Config.RB_MangoHudArguments ?? "", s => Program.Config.RB_MangoHudArguments = s),
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
