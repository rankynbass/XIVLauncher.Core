using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace XIVLauncher.Core;

public static class CoreEnvironmentSettings
{
    public static bool? IsDeck => CheckEnvBoolOrNull("XL_DECK");
    public static bool IsSteamDeckVar => CheckEnvBool("SteamDeck");
    public static bool? IsDeckGameMode => CheckEnvBoolOrNull("XL_GAMEMODE");
    public static bool IsSteamGamepadUIVar => CheckEnvBool("SteamGamepadUI");
    public static bool? IsDeckFirstRun => CheckEnvBoolOrNull("XL_FIRSTRUN");
    public static bool IsUpgrade => CheckEnvBool("XL_SHOW_UPGRADE");
    public static bool ClearSettings => CheckEnvBool("XL_CLEAR_SETTINGS");
    public static bool ClearPrefix => CheckEnvBool("XL_CLEAR_PREFIX");
    public static bool ClearDalamud => CheckEnvBool("XL_CLEAR_DALAMUD");
    public static bool ClearPlugins => CheckEnvBool("XL_CLEAR_PLUGINS");
    public static bool ClearTools => CheckEnvBool("XL_CLEAR_TOOLS");
    public static bool ClearLogs => CheckEnvBool("XL_CLEAR_LOGS");
    public static bool ClearAll => CheckEnvBool("XL_CLEAR_ALL");
    public static bool? UseSteam => CheckEnvBoolOrNull("XL_USE_STEAM"); // Fix for Steam Deck users who lock themselves out
    public static bool IsSteamCompatTool => CheckEnvBool("XL_SCT");
    public static string HOME => System.Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public static string XDG_CONFIG_HOME => string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")) ? Path.Combine(HOME, ".config") : System.Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? "";
    public static string XDG_DATA_HOME => string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("XDG_DATA_HOME")) ? Path.Combine(HOME, ".local", "share") : System.Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? "";
    public static uint AltAppID => GetAltAppId(System.Environment.GetEnvironmentVariable("XL_APPID"));

    public static bool ForceDLSS => CheckEnvBool("XL_FORCE_DLSS"); // Don't search for nvngx.dll. Assume it's already in the game directory.

    private static bool CheckEnvBool(string key)
    {
        string val = (Environment.GetEnvironmentVariable(key) ?? string.Empty).ToLower();
        if (val == "1" || val == "true" || val == "yes" || val == "y" || val == "on") return true;
        return false;
    }

    private static bool? CheckEnvBoolOrNull(string key)
    {
        string val = (Environment.GetEnvironmentVariable(key) ?? string.Empty).ToLower();
        if (val == "1" || val == "true" || val == "yes" || val == "y" || val == "on") return true;
        if (val == "0" || val == "false" || val == "no" || val == "n" || val == "off") return false;
        return null;
    }

    public static string GetCleanEnvironmentVariable(string envvar, string badstring = "", string separator = ":")
    {
        string dirty = Environment.GetEnvironmentVariable(envvar) ?? "";
        if (badstring.Equals("", StringComparison.Ordinal)) return dirty;
        return string.Join(separator, Array.FindAll<string>(dirty.Split(separator, StringSplitOptions.RemoveEmptyEntries), s => !s.Contains(badstring)));
    }

    public static uint GetAltAppId(string? appid)
    {
        uint.TryParse(appid, out var result);
        
        // Will return 0 if appid is invalid (or zero).
        return result;
    }

    public static string GetCType()
    {
        if (System.OperatingSystem.IsWindows())
            return "";
        var psi = new ProcessStartInfo("sh");
        psi.Arguments = "-c \"locale -a 2>/dev/null | grep -i utf\"";
        psi.RedirectStandardOutput = true;

        var proc = new Process();
        proc.StartInfo = psi;
        proc.Start();
        var output = proc.StandardOutput.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return Array.Find(output, s => s.ToUpper().StartsWith("C.")) ?? string.Empty;
    }
    
    static private bool? gameModeInstalled = null;

    static public bool IsGameModeInstalled()
    {
        if (gameModeInstalled is not null)
            return gameModeInstalled ?? false;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var handle = IntPtr.Zero;
            gameModeInstalled = NativeLibrary.TryLoad("libgamemodeauto.so.0", out handle);
            NativeLibrary.Free(handle);
        }
        else
            gameModeInstalled = false;
        return gameModeInstalled ?? false;
    }

    static private string? nvngxPath = ForceDLSS ? "" : Environment.GetEnvironmentVariable("XL_NVNGXPATH");

    static public bool IsDLSSAvailable => !string.IsNullOrEmpty(NvidiaWineDLLPath()) || ForceDLSS;

    static public string NvidiaWineDLLPath()
    {
        if (nvngxPath is not null)
        {
            if (!File.Exists(Path.Combine(nvngxPath, "nvngx.dll")))
                nvngxPath = "";
            return nvngxPath;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            string[] targets = { "/lib", Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".xlcore", "compatibilitytool", "nvidia") };
            foreach (var target in targets)
            {
                var psi = new ProcessStartInfo("/bin/find");
                psi.Arguments = $"-L {target} -name \"nvngx.dll\"";
                psi.RedirectStandardOutput = true;
                var findCmd = new Process();
                findCmd.StartInfo = psi;
                findCmd.Start();

                var output = findCmd.StandardOutput.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(output))
                {
                    var nvngx = new FileInfo(output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault());
                    nvngxPath = nvngx.DirectoryName;
                    break;
                }
            }
        }
        else
            nvngxPath = "";
        nvngxPath ??= ""; // If nvngxPath is still null, set it to empty string to prevent an infinite loop.
        return nvngxPath ?? "";
    }
}
