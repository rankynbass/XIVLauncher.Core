using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace XIVLauncher.Core;

public static class CoreEnvironmentSettings
{
    public static bool? IsDeck => CheckEnvBoolOrNull("XL_DECK");
    public static bool? IsDeckGameMode => CheckEnvBoolOrNull("XL_GAMEMODE");
    public static bool? IsDeckFirstRun => CheckEnvBoolOrNull("XL_FIRSTRUN");
    public static bool ClearSettings => CheckEnvBool("XL_CLEAR_SETTINGS");
    public static bool ClearPrefix => CheckEnvBool("XL_CLEAR_PREFIX");
    public static bool ClearPlugins => CheckEnvBool("XL_CLEAR_PLUGINS");
    public static bool ClearDalamud => CheckEnvBool("XL_CLEAR_DALAMUD");
    public static bool ClearTools => CheckEnvBool("XL_CLEAR_TOOLS");
    public static bool ClearLogs => CheckEnvBool("XL_CLEAR_LOGS");
    public static bool ClearNvngx => CheckEnvBool("XL_CLEAR_NVNGX");
    public static bool ClearAll => CheckEnvBool("XL_CLEAR_ALL");
    public static bool? UseSteam => CheckEnvBoolOrNull("XL_USE_STEAM"); // Fix for Steam Deck users who lock themselves out
    public static bool IsSteamCompatTool => CheckEnvBool("XL_SCT");
    public static uint SteamAppId => GetAppId(Environment.GetEnvironmentVariable("SteamAppId"));
    public static uint AltAppID => GetAppId(Environment.GetEnvironmentVariable("XL_APPID"));
    public static string? WinePrefix => System.Environment.GetEnvironmentVariable("WINEPREFIX");
    public static string? ProtonPrefix => System.Environment.GetEnvironmentVariable("PROTONPREFIX");

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

    public static uint GetAppId(string? appid)
    {
        uint.TryParse(appid, out var result);

        // Will return 0 if appid is invalid (or zero).
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


    private static bool? mangoHudFound = null;

    public static bool IsMangoHudInstalled()
    {
        if (mangoHudFound is null)
        {
            mangoHudFound = false;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                string[] libraryPaths = [ "/app/lib", "/usr/lib64", "/usr/lib", "/lib64", "/lib", "/var/lib/snapd/hostfs/usr/lib64", "/var/lib/snapd/hostfs/usr/lib" ];
                var options = new EnumerationOptions();
                options.RecurseSubdirectories = true;
                options.MaxRecursionDepth = 8;
                foreach (var path in libraryPaths)
                {
                    if (!Directory.Exists(path))
                        continue;

                    if (Directory.GetFiles(path, "libMangoHud.so", options).Length > 0)
                    {
                        mangoHudFound = true;
                        break;
                    }
                }
            }
        }
        return mangoHudFound ?? false;
    }
}
