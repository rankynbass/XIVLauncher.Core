using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using XIVLauncher.Common.Unix.Compatibility;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabWine : SettingsTab
{
    private SettingsEntry<WineStartupType> startupTypeSetting;

    public SettingsTabWine()
    {
        Entries = new SettingsEntry[]
        {
            startupTypeSetting = new SettingsEntry<WineStartupType>("Installation Type", "Choose how XIVLauncher will start and manage your game installation.",
                () => Program.Config.WineStartupType ?? WineStartupType.Managed, x => Program.Config.WineStartupType = x)
                {
                    CheckValidity = x =>
                    {
                        if (x == WineStartupType.Proton && !ProtonManager.IsValid())
                        {
                            var userHome = System.Environment.GetEnvironmentVariable("HOME") ?? "/home/username";
                            return $"No proton version found! Check launcher.ini and make sure that SteamPath points your Steam root\nUsually this is {userHome}/.steam/root or {userHome}/.local/share/Steam";
                        }
                        return null;
                    }
                },

            new SettingsEntry<string>("Wine Binary Path",
                "Set the path XIVLauncher will use to run applications via wine.\nIt should be an absolute path to a folder containing wine64 and wineserver binaries.",
                () => Program.Config.WineBinaryPath, s => Program.Config.WineBinaryPath = s)
            {
                CheckVisibility = () => startupTypeSetting.Value == WineStartupType.Custom
            },
            new DictionarySettingsEntry("Proton Version", "", ProtonManager.Versions, () => Program.Config.ProtonVersion, s => Program.Config.ProtonVersion = s)
            {
                CheckVisibility = () => startupTypeSetting.Value == WineStartupType.Proton,
            },

            new SettingsEntry<bool>("Enable Feral's GameMode", "Enable launching with Feral Interactive's GameMode CPU optimizations.", () => Program.Config.GameModeEnabled ?? true, b => Program.Config.GameModeEnabled = b)
            {
                CheckVisibility = () => RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
                CheckValidity = b =>
                {
                    if (b == true && (!File.Exists("/usr/lib/libgamemodeauto.so.0") && !File.Exists("/app/lib/libgamemodeauto.so.0")))
                        return "GameMode not detected.";

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

            new SettingsEntry<string>("WINEDEBUG Variables", "Configure debug logging for wine. Useful for troubleshooting.", () => Program.Config.WineDebugVars ?? string.Empty, s => Program.Config.WineDebugVars = s),
            new SettingsEntry<bool>("Use WineD3D (Disable DXVK)",
                                    "Don't check this unless you know what you're doing.\nIf you check this, XIVLauncher will try to use WineD3D instead of DXVK, and nothing in the DXVK tab will work.\nCustom wine versions may not work (especially proton-based wine). This will also break GShade.",
                                    () => Program.Config.WineD3DEnabled ?? false, b => Program.Config.WineD3DEnabled = b)
            {
                CheckWarning = b =>
                {
                    if(startupTypeSetting.Value != WineStartupType.Managed && startupTypeSetting.Value != WineStartupType.Official7_10 && startupTypeSetting.Value != WineStartupType.Proton)
                        return "Dalamud and WineD3D have only been tested with the Managed Wine install. Other options may or may not work.";
                    return null;
                },
                CheckValidity = b =>
                {
                    if(startupTypeSetting.Value == WineStartupType.Proton && b)
                        return "WineD3D does not currently work with Proton.";
                    return null;
                },
            },
        };
    }

    public override SettingsEntry[] Entries { get; }

    public override bool IsUnixExclusive => true;

    public override string Title => "Wine";

    public override void Draw()
    {
        base.Draw();

        if (!Program.CompatibilityTools.IsToolDownloaded || Program.CompatibilityTools.useProton)
        {
            ImGui.BeginDisabled();
            if (Program.CompatibilityTools.useProton)
                ImGui.Text("These options do not work properly with Proton yet.");
            else
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

        ImGui.SameLine();

        if (ImGui.Button("Open Wine explorer (use WineD3D)"))
        {
            Program.CompatibilityTools.RunInPrefix("explorer", "", null, false, false, true);
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
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Program.CreateCompatToolsInstance();
    }
}
