using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using XIVLauncher.Common.Unix.Compatibility;
using XIVLauncher.Common.Util;
using XIVLauncher.Core.UnixCompatibility;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabWine : SettingsTab
{
    private SettingsEntry<WineType> startupTypeSetting;

    public SettingsTabWine()
    {
        Entries = new SettingsEntry[]
        {
            startupTypeSetting = new SettingsEntry<WineType>("Installation Type", "Choose how XIVLauncher will start and manage your game installation.",
                () => Program.Config.WineType ?? WineType.Managed, x => Program.Config.WineType = x)
            {
                CheckValidity = x =>
                {
                    if (x == WineType.Proton && !ProtonManager.IsValid())
                    {
                        var userHome = System.Environment.GetEnvironmentVariable("HOME") ?? "/home/username";
                        return $"No proton version found! Check launcher.ini and make sure that SteamPath points your Steam root\nUsually this is {userHome}/.steam/root or {userHome}/.local/share/Steam";
                    }
                    return null;
                }
            },
            new SettingsEntry<WineVersion>("Wine Version", "Choose a patched wine version.", () => Program.Config.WineVersion ?? WineVersion.Wine8_5, x => Program.Config.WineVersion = x)
            {
                CheckVisibility = () => startupTypeSetting.Value == WineType.Managed
            },

            new SettingsEntry<RBWineVersion>("Wine Version", "Choose an XIV-patched version of Wine-tkg", () => Program.Config.RBWineVersion ?? RBWineVersion.Wine8_12, x => Program.Config.RBWineVersion = x)
            {
                CheckVisibility = () => startupTypeSetting.Value == WineType.RB_Wine
            },

            new SettingsEntry<RBProtonVersion>("Wine Version", "Choose an XIV-patched version of Wine-GE.", () => Program.Config.RBProtonVersion ?? RBProtonVersion.Proton8_10, x => Program.Config.RBProtonVersion = x)
            {
                CheckVisibility = () => startupTypeSetting.Value == WineType.RB_Proton
            },
            
            new SettingsEntry<string>("Wine Binary Path",
                "Set the path XIVLauncher will use to run applications via wine.\nIt should be an absolute path to a folder containing wine64 and wineserver binaries.",
                () => Program.Config.WineBinaryPath, s => Program.Config.WineBinaryPath = s)
            {
                CheckVisibility = () => startupTypeSetting.Value == WineType.Custom
            },
            new SettingsEntry<string>("Steam Path", "Set the location of your steam folder (requires restart)", () => Program.Config.SteamPath, s => Program.Config.SteamPath = s)
            {
                CheckVisibility = () => startupTypeSetting.Value == WineType.Proton,
            },
            new DictionarySettingsEntry("Proton Version", "The Wine configuration and Wine explorer buttons below may not function properly with Proton.", ProtonManager.Versions, () => Program.Config.ProtonVersion, s => Program.Config.ProtonVersion = s, ProtonManager.GetDefaultVersion())
            {
                CheckVisibility = () => startupTypeSetting.Value == WineType.Proton,
            },
            new DictionarySettingsEntry("Steam Container Runtime", "Use Steam's container system. Proton is designed with this in mind, but may run without it. Slow to launch.", ProtonManager.Runtimes, () => Program.Config.SteamRuntime, s => Program.Config.SteamRuntime = s, ProtonManager.GetDefaultRuntime())
            {
                CheckVisibility = () => startupTypeSetting.Value == WineType.Proton && !Distro.IsFlatpak,
            },
            new SettingsEntry<bool>("Enable Feral's GameMode", "Enable launching with Feral Interactive's GameMode CPU optimizations.", () => Program.Config.GameModeEnabled ?? true, b => Program.Config.GameModeEnabled = b)
            {
                CheckVisibility = () => RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
                CheckValidity = b =>
                {
                    if (b == true && (!File.Exists("/usr/lib/libgamemodeauto.so.0") && !File.Exists("/app/lib/libgamemodeauto.so.0")))
                        return "GameMode was not detected on your system.";

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
            PlatformHelpers.OpenBrowser(Program.CompatibilityTools.Prefix.FullName);
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

        if (ImGui.Button("Open Wine explorer (use WineD3D"))
        {
            Program.CompatibilityTools.RunInPrefix("explorer", wineD3D: true);

        }

        if (ImGui.Button("Set Wine to Windows 7"))
        {
            Program.CompatibilityTools.RunInPrefix($"winecfg /v win7", redirectOutput: true, writeLog: true);
        }

        ImGui.SameLine();

        if (ImGui.Button("Set Wine to Windows 10"))
        {
            Program.CompatibilityTools.RunInPrefix($"winecfg /v win10", redirectOutput: true, writeLog: true);
        }

        if (ImGui.Button("Kill all wine processes"))
        {
            Program.CompatibilityTools.Kill();
        }

        ImGui.SameLine();

        if (!Program.CompatibilityTools.IsToolDownloaded)
        {
            ImGui.EndDisabled();
        }

        if (new [] {WineType.Managed, WineType.RB_Wine, WineType.RB_Wine}.Contains(startupTypeSetting.Value))
        {

            if (ImGui.Button("Download now!"))
            {
                this.Save();
                Program.CompatibilityTools.EnsureTool(Program.storage.GetFolder("temp"));
            }
        }
    }

    public override void Save()
    {
        base.Save();
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Program.CreateCompatToolsInstance();
    }
}
