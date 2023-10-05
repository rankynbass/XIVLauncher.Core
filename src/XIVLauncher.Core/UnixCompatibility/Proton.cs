using System.IO;
using Serilog;
using XIVLauncher.Core;

namespace XIVLauncher.Core.UnixCompatibility;

public static class Proton
{
    public static Dictionary<string, Dictionary<string, string>> Versions { get; private set; }
    public static Dictionary<string, Dictionary<string, string>> Runtimes { get; private set; }

    static Proton()
    {
        Versions = new Dictionary<string, Dictionary<string, string>>();
        Runtimes = new Dictionary<string, Dictionary<string, string>>();
    }

    public static void Initialize(string steamRoot)
    {
        var steamLibraries = new List<DirectoryInfo>();
        string steamlibs = Environment.GetEnvironmentVariable("STEAM_COMPAT_LIBRARY_PATHS") ?? "";
        if (!string.IsNullOrEmpty(steamlibs))
        {
            foreach (string steamlib in steamlibs.Split(':'))
                if (Directory.Exists(steamlib)) steamLibraries.Add(new DirectoryInfo(steamlib));
        }

        var commonDir = new DirectoryInfo(Path.Combine(steamRoot, "steamapps","common"));
        var compatDir = new DirectoryInfo(Path.Combine(steamRoot, "compatibilitytools.d"));
        var steamRootExists = true;

        try
        {
            if (Directory.Exists(steamRoot))
            {
                Log.Information($"Steam Root is {steamRoot}");
                Log.Verbose($"Steam Common Directory is {commonDir.FullName}");
                Log.Verbose($"Steam Compatibility Tools Directory is {compatDir.FullName}");
                foreach (var steamlib in steamLibraries)
                    Log.Verbose($"Another Steam Library at {steamlib.FullName}");
            }
            else
            {
                throw new DirectoryNotFoundException($"Steam Root directory \"{steamRoot}\" does not exist or is not a directory.");
            }
        }
        catch (DirectoryNotFoundException ex)
        {
            Log.Error(ex, "No Steam directory found. Proton disabled.");
            steamRootExists = false;
        }
        
        if (steamRootExists)
        {
            try
            {

                var commonDirs = commonDir.GetDirectories("*Proton*");
                foreach (var dir in commonDirs)
                    if (File.Exists(Path.Combine(dir.FullName,"proton"))) Versions[dir.Name] = new Dictionary<string, string>() { {"path", dir.FullName} };
            }
            catch (DirectoryNotFoundException ex)
            {
                Log.Verbose(ex, $"Couldn't find any Proton versions in {commonDir}. Check launcher.ini and make sure that SteamPath points to your local Steam root. This is probably something like /home/deck/.steam/root or /home/deck/.local/share/Steam.");
            }
            try
            {
                var commonDirs2 = commonDir.GetDirectories("SteamLinuxRuntime_*");
                foreach (var dir in commonDirs2)
                    if (File.Exists(Path.Combine(dir.FullName,"_v2-entry-point"))) Runtimes[dir.Name] = new Dictionary<string, string>() { {"path", dir.FullName} };
                    else Log.Verbose($"Couldn't find runtime at {dir.FullName}.");
            }
            catch (DirectoryNotFoundException ex)
            {
                Log.Verbose(ex, $"Couldn't find any container Runtimes in {commonDir}. Check launcher.ini and make sure that SteamPath points to your local Steam root. This is probably something like /home/deck/.steam/root or /home/deck/.local/share/Steam.");
            }
            try
            {
                var compatDirs = compatDir.GetDirectories("*Proton*");
                foreach (var dir in compatDirs)
                    if (File.Exists(Path.Combine(dir.FullName,"proton"))) Versions[dir.Name] = new Dictionary<string, string>() { {"path", dir.FullName} };
            }
            catch (DirectoryNotFoundException ex)
            {
                Log.Verbose(ex, $"Couldn't find any Proton versions {compatDir}.");
            }
            try
            {
                var compatDirs2 = compatDir.GetDirectories("SteamLinuxRuntime_*");
                foreach (var dir in compatDirs2)
                    if (File.Exists(Path.Combine(dir.FullName,"_v2-entry-point"))) Runtimes[dir.Name] = new Dictionary<string, string>() { {"path", dir.FullName} };
            }
            catch (DirectoryNotFoundException ex)
            {
                Log.Verbose(ex, $"Couldn't find any container Runtimes in {compatDir}.");
            }
            foreach (var steamlib in steamLibraries)
            {
                try
                {
                    var compatDirs = steamlib.GetDirectories("*Proton*");
                    foreach (var dir in compatDirs)
                        if (File.Exists(Path.Combine(dir.FullName,"proton"))) Versions[dir.Name] = new Dictionary<string, string>() { {"path", dir.FullName} };
                }
                catch (DirectoryNotFoundException ex)
                {
                    Log.Verbose(ex, $"Couldn't find any Proton versions {steamlib}.");
                }
                try
                {
                    var compatDirs2 = steamlib.GetDirectories("SteamLinuxRuntime_*");
                    foreach (var dir in compatDirs2)
                        if (File.Exists(Path.Combine(dir.FullName,"_v2-entry-point"))) Runtimes[dir.Name] = new Dictionary<string, string>() { {"path", dir.FullName} };
                }
                catch (DirectoryNotFoundException ex)
                {
                    Log.Verbose(ex, $"Couldn't find any container Runtimes in {compatDir}.");
                }
            }
        }

        if (Versions.Count == 0)
            Versions["DISABLED"] = new Dictionary<string, string>() { {"name", "DISABLED - No valid Proton verions found. Bad SteamPath or Steam not installed."}, {"path", ""} };
        Runtimes["Disabled"] =  new Dictionary<string, string>() { {"name", "DISABLED - Don't use a container runtime"}, {"path", ""} };
    }

    public static string GetVersionPath(string? name)
    {
        name ??= GetDefaultVersion();
        if (Versions.ContainsKey(name))
            return Versions[name]["path"];
        return Versions[GetDefaultVersion()]["path"];
    }

    public static string GetDefaultVersion()
    {
        if (VersionExists("Proton 8.0")) return "Proton 8.0";
        if (VersionExists("Proton 7.0")) return "Proton 7.0";
        return Versions.First().Key;
    }

    public static bool VersionExists(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return Versions.ContainsKey(name);
    }

    public static string GetRuntimePath(string? name)
    {
        name ??= GetDefaultRuntime();
        if (Runtimes.ContainsKey(name))
        {
            Log.Verbose($"Runtime found: {name} - {Runtimes[name]}");
            return Runtimes[name]["path"];
        }
        Log.Verbose($"Runtime not found, using default: {GetDefaultRuntime()} - {Runtimes[GetDefaultRuntime()]}");
        return Runtimes[GetDefaultRuntime()]["path"];
    }

    public static string GetDefaultRuntime()
    {
        if (RuntimeExists("SteamLinuxRuntime_sniper")) return "SteamLinuxRuntime_sniper";
        if (RuntimeExists("SteamLinuxRuntime_soldier")) return "SteamLinuxRuntime_soldier";
        return "Disabled";
    }

    public static bool RuntimeExists(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return Runtimes.ContainsKey(name);
    }

    public static bool IsValid()
    {
        return !Versions.ContainsKey("DISABLED");
    }

}