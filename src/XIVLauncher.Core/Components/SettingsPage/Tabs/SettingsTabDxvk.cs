using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using XIVLauncher.Common.Unix.Compatibility;
using XIVLauncher.Core.UnixCompatibility;

namespace XIVLauncher.Core.Components.SettingsPage.Tabs;

public class SettingsTabDxvk : SettingsTab
{
    private DictionarySettingsEntry dxvkVersionSetting;
    private DictionarySettingsEntry nvapiVersionSetting;
    private SettingsEntry<DxvkHud> dxvkHudSetting;
    private SettingsEntry<MangoHud> mangoHudSetting;

    private string dxvkPath = Path.Combine(Program.storage.Root.FullName, "compatibilitytool", "dxvk");

    public SettingsTabDxvk()
    {
        Entries = new SettingsEntry[]
        {
            dxvkVersionSetting = new DictionarySettingsEntry("DXVK Version", $"Choose which version of DXVK to use. Put your custom DXVK in {dxvkPath}\nEntries marked with *Download* will be downloaded when you log in.", Dxvk.Versions, () => Program.Config.DxvkVersion ?? "dxvk-async-1.10.3", s => Program.Config.DxvkVersion = s, Dxvk.GetDefaultVersion())
            {
                CheckWarning = s =>
                {
                    if (s == "DISABLED") return null;
                    if (DxvkSettings.DxvkAllowsNvapi(s))
                        return "May not work with older graphics cards. AMD users may need to use env variable RADV_PERFTEST=gpl";
                    return null;
                },
            },
            new SettingsEntry<bool>("Enable DXVK ASYNC", "Enable DXVK ASYNC patch. May not be available on DXVK >= 2.0", () => Program.Config.DxvkAsyncEnabled ?? true, b => Program.Config.DxvkAsyncEnabled = b)
            {
                CheckVisibility = () => dxvkVersionSetting.Value.Contains("async"),
            },

            new SettingsEntry<bool>("Enable GPL Async Cache", "Enable the GPL Async State Cache. Only available on Dxvk with GPL Async >= 2.2-3. May improve performance.", () => Program.Config.DxvkGPLAsyncCacheEnabled ?? false, b => Program.Config.DxvkGPLAsyncCacheEnabled = b)
            {
                CheckVisibility = () => dxvkVersionSetting.Value.Contains("gplasync"),
            },

            nvapiVersionSetting = new DictionarySettingsEntry("Enable DLSS (Disable for FSR2 mod)", $"Choose which version of dxvk-nvapi to use. Wine >= 9.0 or Valve Wine (wine-ge/valvebe) >= 8.x are needed for DLSS.", DLSS.Versions, () => Program.Config.NvapiVersion ?? DLSS.GetDefaultVersion(), s => Program.Config.NvapiVersion = s, Dxvk.GetDefaultVersion())
            {
                CheckWarning = s =>
                {
                    if (s == "DISABLED") return null;
                    // This should never be seen, but just in case...
                    if (!DLSS.IsDLSSAvailable)
                        return "No nvngx dlls found. Incorrect drivers installed, or non-nvidia GPU. Non-nvidia GPU users should set this to Disabled.";
                    if (!DxvkSettings.DxvkAllowsNvapi(dxvkVersionSetting.Value))
                        return "Nvapi/DLSS requires DXVK 2.0 or greater.";
                    return null;
                },
            },

            dxvkHudSetting = new SettingsEntry<DxvkHud>("DXVK Overlay", "DXVK Hud is included with DXVK. MangoHud must be installed separately.\nFlatpak users need the flatpak version of MangoHud.", () => Program.Config.DxvkHud ?? DxvkHud.None, x => Program.Config.DxvkHud = x)
            {
                CheckVisibility = () => dxvkVersionSetting.Value != "DISABLED",
            },

            new SettingsEntry<string>("DXVK Hud Custom String", "Set a custom string for the built in DXVK Hud. Warning: If it's invalid, the game may hang.", () => Program.Config.DxvkHudCustom ?? Dxvk.DXVK_HUD, s => Program.Config.DxvkHudCustom = s)
            {
                CheckVisibility = () => dxvkHudSetting.Value == DxvkHud.Custom && dxvkVersionSetting.Value != "DISABLED",
                CheckWarning = s =>
                {
                    if(!DxvkSettings.DxvkHudStringIsValid(s))
                        return "That's not a valid hud string";
                    return null;
                },
            },
            mangoHudSetting = new SettingsEntry<MangoHud>("MangoHud Overlay", "MangoHud is installed. It is recommended to set Dxvk Overlay to None if using MangoHud.", () => Program.Config.MangoHud ?? MangoHud.None, x => Program.Config.MangoHud = x)
            {
                CheckVisibility = () => dxvkVersionSetting.Value != "DISABLED" && Dxvk.MangoHudInstalled,
                CheckWarning = x =>
                {
                    if (dxvkHudSetting.Value != DxvkHud.None && x != MangoHud.None)
                        return "Warning! You can run Dxvk Hud and MangoHud at the same time, but you probably shouldn't.\nSet one of them to None.";
                    return null;
                }
            },
            new SettingsEntry<string>("MangoHud Custom String", "Set a custom string for MangoHud config.", () => Program.Config.MangoHudCustomString ?? Dxvk.MANGOHUD_CONFIG, s => Program.Config.MangoHudCustomString = s)
            {
                CheckVisibility = () => mangoHudSetting.Value == MangoHud.CustomString && dxvkVersionSetting.Value != "DISABLED" && Dxvk.MangoHudInstalled,
                CheckWarning = s =>
                {
                    if (s.Contains(' '))
                        return "No spaces allowed in MangoHud config";
                    return null;
                }
            },
            new SettingsEntry<string>("MangoHud Custom Path", "Set a custom path for MangoHud config file.", () => Program.Config.MangoHudCustomFile ?? Dxvk.MANGOHUD_CONFIGFILE, s => Program.Config.MangoHudCustomFile = s)
            {
                CheckVisibility = () => mangoHudSetting.Value == MangoHud.CustomFile && dxvkVersionSetting.Value != "DISABLED" && Dxvk.MangoHudInstalled,
                CheckWarning = s =>
                {
                    if(!File.Exists(s))
                        return "That's not a valid file.";
                    return null;
                },
            },
            new NumericSettingsEntry("Frame Rate Limit", "Set a frame rate limit, and DXVK will try not exceed it. Use 0 for unlimited.", () => Program.Config.DxvkFrameRateLimit ?? 0, i => Program.Config.DxvkFrameRateLimit = i, 0, 1000)
            {
                CheckVisibility = () => dxvkVersionSetting.Value != "DISABLED",
            },
        };
    }

    public override SettingsEntry[] Entries { get; }

    public override bool IsUnixExclusive => true;

    public override string Title => "DXVK";

    public override void Draw()
    {
        ImGui.TextUnformatted("If you chose Proton in the Wine Tab, the version does not matter, except for Disabled. Nvapi Version is completely ignored.");
        ImGui.TextUnformatted("Choose any version of Dxvk, and set the rest of the options as normal. Disabled will attempt to use WineD3D.");
        ImGui.Dummy(SPACER);
        ImGui.Separator();
        ImGui.Dummy(SPACER);

        base.Draw();

        if (Dxvk.Versions[dxvkVersionSetting.Value].ContainsKey("mark") || DLSS.Versions[nvapiVersionSetting.Value].ContainsKey("mark"))
        {
            ImGui.Separator();

            ImGui.Dummy(SPACER);

            if (Dxvk.Versions[dxvkVersionSetting.Value].ContainsKey("mark"))
            {
                if (ImGui.Button($"{Dxvk.Versions[dxvkVersionSetting.Value]["mark"]} dxvk now!"))
                {
                    this.Save();
                    var _ = Task.Run(async () => 
                    {
                        Dxvk.SetMark(dxvkVersionSetting.Value, "Downloading");
                        await Program.CompatibilityTools.DownloadDxvk().ConfigureAwait(false);
                        Dxvk.SetMark(dxvkVersionSetting.Value, null);
                    });
                }
            }

            if (DLSS.Versions[nvapiVersionSetting.Value].ContainsKey("mark"))
            {
                ImGui.SameLine();

                if (ImGui.Button($"{DLSS.Versions[nvapiVersionSetting.Value]["mark"]} dxvk-nvapi now!"))
                {
                    this.Save();
                    var _ = Task.Run(async () => 
                    {
                        DLSS.SetMark(nvapiVersionSetting.Value, "Downloading");
                        await Program.CompatibilityTools.DownloadNvapi().ConfigureAwait(false);
                        DLSS.SetMark(nvapiVersionSetting.Value, null);
                    });
                }
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
