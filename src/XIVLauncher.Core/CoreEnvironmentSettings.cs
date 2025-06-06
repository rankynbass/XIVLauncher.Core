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
    public static string? WinePrefix => System.Environment.GetEnvironmentVariable("WINEPREFIX");
    public static string? ProtonPrefix => System.Environment.GetEnvironmentVariable("PROTONPREFIX");
    public static string? NvngxPath => System.Environment.GetEnvironmentVariable("XL_NVNGXPATH"); // We want this null if unset.
    public static uint SteamAppId => GetUInt(System.Environment.GetEnvironmentVariable("SteamAppId"));
    public static uint AltAppID => GetUInt(System.Environment.GetEnvironmentVariable("XL_APPID"));
    public static float? Scale
    { 
        get
        {
            float? scale = GetFloat(System.Environment.GetEnvironmentVariable("XL_SCALE"));
            if (scale is null)
                return null;
            float scale2 = scale ?? 1.0f;
            return scale2 < 1.0f ? 1.0f : (scale2 > 4.0f) ? 4.0f : scale2; 
        }
    }
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

    public static uint GetUInt(string? number)
    {
        uint.TryParse(number, out var result);
        
        // Will return 0 if appid is invalid (or zero).
        return result;
    }

    public static float? GetFloat(string? number)
    {

        var test = Single.TryParse(number, out var result);
        if (!test)
            return null;
        return result;
    }

    public static string GetCType()
    {
        if (OperatingSystem.IsWindows())
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
}
