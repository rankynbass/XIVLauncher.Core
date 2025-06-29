using System.Numerics;
using System.Runtime.InteropServices;

using ImGuiNET;

using XIVLauncher.Common.Unix.Compatibility.Dxvk;
using XIVLauncher.Common.Unix.Compatibility.Nvapi;
using XIVLauncher.Common.Unix.Compatibility.Wine;
using XIVLauncher.Common.Unix.Compatibility.Proton;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabWine : SettingsTab
{
    private SettingsEntry<RBWineStartupType> startupTypeSetting;

    private ToolSettingsEntry wineVersionSetting;

    private ToolSettingsEntry dxvkVersionSetting;

    public SettingsTabWine()
    {
        Entries = new SettingsEntry[]
        {
            startupTypeSetting = new SettingsEntry<RBWineStartupType>("Wine Install", "Choose how XIVLauncher will start and manage your wine installation.",
                () => Program.Config.RB_WineStartupType ?? RBWineStartupType.Managed, x => Program.Config.RB_WineStartupType = x)
            {
                CheckValidity = x => x == RBWineStartupType.Proton ? "Proton not yet implemented" : null,
            },

            wineVersionSetting = new ToolSettingsEntry("Wine Version", "Choose which Wine version to use.", () => Program.Config.RB_WineVersion ?? Program.WineManager.DEFAULT,
                s => Program.Config.RB_WineVersion = s, Program.WineManager.Version, Program.WineManager.DEFAULT )
            {
                CheckVisibility = () => startupTypeSetting.Value == RBWineStartupType.Managed
            },

            new SettingsEntry<string>("Wine Binary Path",
                "Set the path XIVLauncher will use to run applications via wine.\nIt should be an absolute path to a folder containing wine and/or win64 and wineserver binaries.",
                () => Program.Config.WineBinaryPath, s => Program.Config.WineBinaryPath = s)
            {
                CheckVisibility = () => startupTypeSetting.Value == RBWineStartupType.Custom,
                CheckValidity = s =>
                {
                    if (!WineSettings.IsValidWineBinaryPath(s))
                        return "Invalid wine path.";
                    return null;
                },
            },

            new ToolSettingsEntry("Proton Version", "Choose which Proton version to use.", () => Program.Config.RB_ProtonVersion ?? Program.ProtonManager.DEFAULT,
                s => Program.Config.RB_ProtonVersion = s, Program.ProtonManager.Version, Program.ProtonManager.DEFAULT)
            {
                CheckVisibility = () => startupTypeSetting.Value == RBWineStartupType.Proton
            },

            new SettingsEntry<bool>("Enable Sniper", "Use the Steam sniper runtime container (recommended)", () => Program.Config.RB_UseSniperRuntime ?? true, b => Program.Config.RB_UseSniperRuntime = b)
            {
                CheckVisibility = () => startupTypeSetting.Value == RBWineStartupType.Proton
            },

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

            new ToolSettingsEntry("Dxvk-Nvapi Version (Needed for DLSS)", "Choose which version of Dxvk-Nvapi to use.", () => Program.Config.RB_NvapiVersion ?? Program.NvapiManager.DEFAULT,
                s => Program.Config.RB_NvapiVersion = s, Program.NvapiManager.Version, Program.NvapiManager.DEFAULT)
            {
                CheckVisibility = () => dxvkVersionSetting.Value != "DISABLED",
                CheckWarning = x =>
                {
                    string warning = "";
                    if (dxvkVersionSetting.Value == Program.DxvkManager.LEGACY)
                        warning += "DLSS will not work with Legacy DXVK. Use Stable instead.\n";
                    if (startupTypeSetting.Value == RBWineStartupType.Custom)
                        warning += "DLSS may not work with custom wine versions. Make sure wine is >= 9.0";
                    else if (wineVersionSetting.Value == Program.WineManager.LEGACY)
                        warning += "DLSS will not work with Legacy Wine. Use Stable instead, or Custom Wine >= 9.0";

                    warning = warning.Trim();
                    
                    return string.IsNullOrEmpty(warning) ? null : warning;
                }
            },

            new SettingsEntry<bool>("Enable Feral's GameMode", "Enable launching with Feral Interactive's GameMode CPU optimizations.", () => Program.Config.GameModeEnabled ?? true, b => Program.Config.GameModeEnabled = b)
            {
                CheckVisibility = () => RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
                CheckValidity = b =>
                {
                    var handle = IntPtr.Zero;
                    if (b == true && !NativeLibrary.TryLoad("libgamemodeauto.so.0", out handle))
                        return "GameMode was not detected on your system.";
                    NativeLibrary.Free(handle);
                    return null;
                }
            },

            new SettingsEntry<bool>("Enable ESync", "Enable eventfd-based synchronization.", () => Program.Config.ESyncEnabled ?? true, b => Program.Config.ESyncEnabled = b),
            new SettingsEntry<bool>("Enable FSync", "Enable fast user mutex (futex2).", () => Program.Config.FSyncEnabled ?? true, b => Program.Config.FSyncEnabled = b)
            {
                CheckVisibility = () => RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
                CheckValidity = b =>
                {
                    if (b == true && (Environment.OSVersion.Version.Major < 5 && (Environment.OSVersion.Version.Minor < 16 || Environment.OSVersion.Version.Major < 6)))
                        return "Linux kernel 5.16 or higher is required for FSync.";

                    return null;
                }
            },

            new SettingsEntry<bool>("Set Windows version to 7", "Default for Wine 8.1+ is Windows 10, but this causes issues with some Dalamud plugins. Windows 7 is recommended for now.", () => Program.Config.SetWin7 ?? true, b => Program.Config.SetWin7 = b),

            new SettingsEntry<DxvkHudType>("DXVK Overlay", "Configure how much of the DXVK overlay is to be shown.", () => Program.Config.DxvkHudType, type => Program.Config.DxvkHudType = type),

            new SettingsEntry<string>("WINEDEBUG Variables", "Configure debug logging for wine. Useful for troubleshooting.", () => Program.Config.WineDebugVars ?? string.Empty, s => Program.Config.WineDebugVars = s)
        };
    }

    public override SettingsEntry[] Entries { get; }

    public override bool IsUnixExclusive => true;

    public override string Title => "Wine";

    public override void Draw()
    {
        base.Draw();

        if (!Program.CompatibilityTools.IsToolDownloaded)
        {
            ImGui.BeginDisabled();
            ImGui.Text("Compatibility tool isn't set up. Please start the game at least once.");

            ImGui.Dummy(new Vector2(10));
        }

        if (ImGui.Button("Open prefix"))
        {
            PlatformHelpers.OpenBrowser(Program.CompatibilityTools.Settings.Prefix.FullName);
        }

        ImGui.SameLine();

        if (ImGui.Button("Open Wine configuration"))
        {
            Program.CompatibilityTools.RunInPrefix("winecfg");
        }

        ImGui.SameLine();

        if (ImGui.Button("Open Wine explorer (run apps in prefix)"))
        {
            Program.CompatibilityTools.RunInPrefix("explorer");
        }

        if (ImGui.Button("Kill all wine processes"))
        {
            Program.CompatibilityTools.Kill();
        }

        if (!Program.CompatibilityTools.IsToolDownloaded)
        {
            ImGui.EndDisabled();
        }
    }

    public override void Save()
    {
        base.Save();
        Program.CreateCompatToolsInstance();
    }
}
