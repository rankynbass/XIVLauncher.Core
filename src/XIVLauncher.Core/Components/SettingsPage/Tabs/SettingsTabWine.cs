using System.Numerics;
using System.Runtime.InteropServices;

using ImGuiNET;
using Serilog;

using XIVLauncher.Common.Unix.Compatibility;
using XIVLauncher.Common.Util;
using XIVLauncher.Core.UnixCompatibility;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabWine : SettingsTab
{
    private SettingsEntry<RunnerType> RunnerTypeSetting;

    private DictionarySettingsEntry wineVersionSetting;

    private DictionarySettingsEntry protonVersionSetting;

    private DictionarySettingsEntry runtimeVersionSetting;

    private readonly string toolDirectory = Path.Combine(Program.storage.Root.FullName, "compatibilitytool", "wine");

    public SettingsTabWine()
    {
        Entries = new SettingsEntry[]
        {
            RunnerTypeSetting = new SettingsEntry<RunnerType>("Installation Type", "Choose how XIVLauncher will start and manage your game installation.",
                () => Program.Config.RunnerType ?? RunnerType.Managed, x => Program.Config.RunnerType = x)
            {
                CheckWarning = x =>
                {
                    return "If you are using wine 9+, valvebe 9+, or Proton 9+, Dalamud with FFXIV 7.2+ *requires* a new patch.\nPlease update to at least Wine-XIV 10.4.1, ValveBE 9-20, or XIV-Proton 9-26.1. Wine 8.5 is still OK.";
                }
            },

            wineVersionSetting = new DictionarySettingsEntry("Wine Version", $"Wine versions in {toolDirectory}\nEntries marked with *Download* will be downloaded when you log in.", Wine.Versions, () => Program.Config.WineVersion, s => Program.Config.WineVersion = s, Wine.GetDefaultVersion())
            {
                CheckVisibility = () => RunnerTypeSetting.Value == RunnerType.Managed
            },
            
            new SettingsEntry<string>("Wine Binary Path",
                "Set the path XIVLauncher will use to run applications via wine.\nIt should be an absolute path to a folder containing wine64 and wineserver binaries.",
                () => Program.Config.WineBinaryPath, st => Program.Config.WineBinaryPath = st)
            {
                CheckVisibility = () => RunnerTypeSetting.Value == RunnerType.Custom
            },

            protonVersionSetting = new DictionarySettingsEntry("Proton Version", "The Wine configuration and Wine explorer buttons below may not function properly with Proton.", Proton.Versions, () => Program.Config.ProtonVersion, s => Program.Config.ProtonVersion = s, Proton.GetDefaultVersion())
            {
                CheckVisibility = () => RunnerTypeSetting.Value == RunnerType.Proton,
                CheckWarning = x =>
                {
                    var warning = "";
                    if (!protonVersionSetting.Value.ToUpper().Contains("XIV"))
                        warning += "Non XIV-Proton versions may crash with the ping plugin. Use XIV-Proton instead.\n";
                    if (protonVersionSetting.Value.Contains("9-11"))
                        warning += "GE-Proton9-11 and XIV-Proton9-11 have a bug that causes issues with Dalamud. You may be able to fix it by clearing the prefix.";
                    return (string.IsNullOrEmpty(warning)) ? null : warning;
                }
            },

            runtimeVersionSetting = new DictionarySettingsEntry("Runtime Version", "Sniper runtime is recommeded for use with Proton.", Runtime.Versions, () => Program.Config.RuntimeVersion, s => Program.Config.RuntimeVersion = s, Runtime.GetDefaultVersion())
            {
                CheckVisibility = () => RunnerTypeSetting.Value == RunnerType.Proton,
                // CheckWarning = x =>
                // {
                //     if (RunnerTypeSetting.Value == RunnerType.Proton)
                //         return "Proton is designed to be run in a container. You may have issues if you use it without Umu-launcher.";
                //     return null;
                // }
            },

            new SettingsEntry<bool>("Enable Feral's GameMode", "Enable launching with Feral Interactive's GameMode CPU optimizations.", () => Program.Config.GameModeEnabled ?? true, b => Program.Config.GameModeEnabled = b)
            {
                CheckVisibility = () => RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
                CheckValidity = b =>
                {
                    if (b == true && !CoreEnvironmentSettings.IsGameModeInstalled())
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

            new SettingsEntry<bool>("Enable Wayland Driver (Experimental)", "Requires compatible wine build. Will probably not work with proton or valvebe wine.", () => Program.Config.WaylandEnabled ?? false, b => Program.Config.WaylandEnabled = b),

            new NumericSettingsEntry("Wayland Desktop Scaling", "Set this equal to your desktop scaling. Needed for Wine Wayland driver.\nUse the \"Update Wine Scaling\" button below to change this.", () => Program.Config.WineScale ?? 100, i => Program.Config.WineScale = (i > 400 || i < 100 || i % 25 !=0) ? 100 : i, 100, 400, 25),

            new SettingsEntry<string>("Extra WINEDLLOVERRIDES", "Add extra WINEDLLOVERRIDES. No spaces, semicolon separated. Do not use msquic, mscoree, d3d11, dxgi. These are already set.", () => Program.Config.WineDLLOverrides ?? "", s => Program.Config.WineDLLOverrides = s)
            {
                CheckValidity = s =>
                {
                    if (!RunnerSettings.WINEDLLOVERRIDEIsValid(s))
                        return "Not a valid WINEDLLOVERRIDE string";
                    
                    return null;
                },
            },

            new SettingsEntry<string>("WINEDEBUG Variables", "Configure debug logging for wine. Useful for troubleshooting.", () => Program.Config.WineDebugVars ?? string.Empty, s => Program.Config.WineDebugVars = s),
        };
    }

    public override SettingsEntry[] Entries { get; }

    public override bool IsUnixExclusive => true;

    public override string Title => "Wine";

    public override void Draw()
    {
        base.Draw();

        ImGui.Separator();

        ImGui.Dummy(SPACER);

        if (RunnerTypeSetting.Value == RunnerType.Managed)
        {
            if (Wine.Versions[wineVersionSetting.Value].ContainsKey("mark"))
            {
                ImGui.BeginDisabled();
                ImGui.Text("Compatibility tool isn't set up. Please start the game at least once.");

                ImGui.Dummy(SPACER);
            }
        }
        else if (RunnerTypeSetting.Value == RunnerType.Proton)
        {
            if (Proton.Versions[protonVersionSetting.Value].ContainsKey("mark"))
            {
                ImGui.BeginDisabled();
                ImGui.Text("Compatibility tool isn't set up. Please start the game at least once.");

                ImGui.Dummy(SPACER);
            }
        }

        if (ImGui.Button("Update Wine Scaling"))
        {
            this.Save();
            Program.CompatibilityTools.RunInPrefix($"reg add \"HKEY_LOCAL_MACHINE\\System\\CurrentControlSet\\Hardware Profiles\\Current\\Software\\Fonts\" /v LogPixels /t REG_DWORD /d {Runner.Dpi} /f").WaitForExit();
        }

        if (RunnerTypeSetting.Value == RunnerType.Proton)
        {
            ImGui.SameLine();
            if (ImGui.Button("Update Steam runtime"))
            {
                this.Save();
                Task.Run(async () =>
                {
                    Runtime.SetMark(runtimeVersionSetting.Value, "Downloading");
                    try
                    {
                        await Program.CompatibilityTools.DownloadRuntime().ConfigureAwait(false);
                        Runtime.SetMark(runtimeVersionSetting.Value, null);
                    }
                    catch (Exception e)
                    {
                        Runtime.SetMark(runtimeVersionSetting.Value, "Download Failed!");
                        Log.Error(e, $"Could not download {Runtime.GetDownloadUrl(runtimeVersionSetting.Value)}");
                    }
                });
            }
        }

        ImGui.Dummy(SPACER);

        if (ImGui.Button("Open prefix"))
        {
            PlatformHelpers.OpenBrowser(Program.CompatibilityTools.Runner.Prefix.FullName);
        }

        ImGui.SameLine();

        if (ImGui.Button("Open Wine configuration"))
        {
            this.Save();
            Program.CompatibilityTools.RunExternalProgram("winecfg");
        }

        ImGui.SameLine();

        if (ImGui.Button("Open Wine explorer (run apps in prefix)"))
        {
            Program.CompatibilityTools.RunExternalProgram("explorer");
        }

        ImGui.SameLine();

        if (ImGui.Button("Open Wine explorer (use WineD3D"))
        {
            Program.CompatibilityTools.RunExternalProgram("explorer", wineD3D: true);

        }

        ImGui.Dummy(SPACER);


        if (ImGui.Button("Set Wine to Windows 7"))
        {
            Program.CompatibilityTools.RunExternalProgram($"winecfg /v win7", redirectOutput: true, writeLog: true);
        }

        ImGui.SameLine();

        if (ImGui.Button("Set Wine to Windows 10"))
        {
            Program.CompatibilityTools.RunExternalProgram($"winecfg /v win10", redirectOutput: true, writeLog: true);
        }

        ImGui.Dummy(SPACER);

        if (ImGui.Button("Kill all wine processes"))
        {
            Program.CompatibilityTools.Kill();
        }

        if (RunnerTypeSetting.Value == RunnerType.Managed)
        {
            if (Wine.Versions[wineVersionSetting.Value].ContainsKey("mark"))
            {
                ImGui.EndDisabled();
            }

            if (Wine.Versions[wineVersionSetting.Value].ContainsKey("mark"))
            {
                ImGui.SameLine();

                if (ImGui.Button($"{Wine.Versions[wineVersionSetting.Value]["mark"]}"))
                {
                    this.Save();
                    Task.Run(async () => 
                    {
                        Wine.SetMark(wineVersionSetting.Value, "Downloading");
                        try
                        {
                            await Program.CompatibilityTools.DownloadWine().ConfigureAwait(false);
                            Wine.SetMark(wineVersionSetting.Value, null);
                        }
                        catch (Exception e)
                        {
                            Wine.SetMark(wineVersionSetting.Value, "Download Failed!");
                            Log.Error(e, $"Could not download {Wine.GetDownloadUrl(wineVersionSetting.Value)}");
                        }
                    });
                }
            }
        }
        else if (RunnerTypeSetting.Value == RunnerType.Proton)
        {
            if (Proton.Versions[protonVersionSetting.Value].ContainsKey("mark"))
            {
                ImGui.EndDisabled();
            }

            if (Proton.Versions[protonVersionSetting.Value].ContainsKey("mark"))
            {
                ImGui.SameLine();

                if (ImGui.Button($"{Proton.Versions[protonVersionSetting.Value]["mark"]}"))
                {
                    this.Save();
                    Task.Run(async () => 
                    {
                        Proton.SetMark(protonVersionSetting.Value, "Downloading");
                        try
                        {
                            await Program.CompatibilityTools.DownloadProton().ConfigureAwait(false);
                            Proton.SetMark(protonVersionSetting.Value, null);
                        }
                        catch (Exception e)
                        {
                            Proton.SetMark(protonVersionSetting.Value, "Download Failed!");
                            Log.Error(e, $"Could not download {Proton.GetDownloadUrl(protonVersionSetting.Value)}");
                        }
                    });
                }
            }
        }

        ImGui.Dummy(SPACER);

        if (Program.IsReshadeEnabled() is not null)
        {
            ImGui.Text($"Reshade is {(Program.IsReshadeEnabled().Value ? "ENABLED" : "DISABLED")}");
        
            if (ImGui.Button("Toggle Reshade"))
            {
                Program.ToggleReshade();
            }
        }
        else
            ImGui.Text($"Reshade is not installed");
    }

    public override void Save()
    {
        base.Save();
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Program.CreateCompatToolsInstance();
    }
}
