using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Serilog;

using XIVLauncher.Common.Unix.Compatibility.Dxvk;
using XIVLauncher.Common.Unix.Compatibility.Nvapi;
using XIVLauncher.Common.Unix.Compatibility.Nvapi.Releases;
using XIVLauncher.Common.Unix.Compatibility.Wine;
using XIVLauncher.Common.Unix.Compatibility.Wine.Releases;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Unix.Compatibility;

public class CompatibilityTools
{
    private const uint DXVK_CLEANUP_THRESHHOLD = 5;
    private const uint NVAPI_CLEANUP_THRESHHOLD = 5;
    private const uint WINE_CLEANUP_THRESHHOLD = 10;

    private readonly DirectoryInfo wineDirectory;
    private readonly DirectoryInfo dxvkDirectory;
    private readonly DirectoryInfo nvapiDirectory;
    private readonly DirectoryInfo gameDirectory;
    private readonly DirectoryInfo configDirectory;
    private readonly DirectoryInfo steamDirectory;
    private readonly StreamWriter logWriter;

    // Wine64 will be wine64/wine for wine, proton for proton
    private string Wine64Path => Settings.WinePath;
    private string WineServerPath => Settings.WineServerPath;
    private string RunInPrefixVerb => Settings.IsProton ? "runinprefix " : "";
    private string RunVerb => Settings.IsProton ? "run " : "";

    // Runtime will only be used with proton, and can be disabled. If not using runtime,
    // RuntimePath will be the same as Wine64Path, and we extra args won't be needed, so will be empty.
    private string RuntimePath => Settings.IsUsingRuntime ? Path.Combine(Settings.RuntimeRelease.Name, "_v2-entry-point") : Wine64Path;
    private string RuntimeArgs => Settings.IsUsingRuntime ? $"--verb=waitforexitandrun -- \"{Wine64Path}\" " : "";
    private string[] RuntimeArgsArray => Settings.IsUsingRuntime ? [ "--verb=waitforexitandrun", "--", Wine64Path] : new string [] { };

    private readonly IToolRelease dxvkVersion;
    private readonly RBHudType hudType;
    private readonly string customHud;
    private readonly IToolRelease nvapiVersion;
    private readonly bool gamemodeOn;
    private readonly bool dxvkAsyncOn;
    private readonly bool gplAsyncCacheOn;
    private bool isDxvkEnabled => dxvkVersion.Label != "Disabled";
    private bool isNvapiEnabled => isDxvkEnabled && (nvapiVersion.Label != "Disabled");
    
    public bool IsToolReady { get; private set; }
    public WineSettings Settings { get; private set; }
    public bool IsToolDownloaded => File.Exists(RuntimePath) && File.Exists(Wine64Path) && Settings.Prefix.Exists;

    public CompatibilityTools(WineSettings wineSettings, IToolRelease dxvkVersion, RBHudType hudType, string customHud, IToolRelease nvapiVersion, bool gamemodeOn, bool dxvkAsyncOn, bool gplAsyncCacheOn)
    {
        this.Settings = wineSettings;
        this.dxvkVersion = dxvkVersion;
        this.hudType = hudType;
        this.customHud = customHud;
        this.nvapiVersion = dxvkVersion.Name != "DISABLED" ? nvapiVersion : new NvapiCustomRelease("Disabled", "Do not use Nvapi", "DISABLED", "");
        this.gamemodeOn = gamemodeOn;
        this.dxvkAsyncOn = dxvkAsyncOn;
        this.gplAsyncCacheOn = gplAsyncCacheOn;

        this.wineDirectory = new DirectoryInfo(Path.Combine(Settings.Paths.ToolsFolder.FullName, "wine"));
        this.dxvkDirectory = new DirectoryInfo(Path.Combine(Settings.Paths.ToolsFolder.FullName, "dxvk"));
        this.nvapiDirectory = new DirectoryInfo(Path.Combine(Settings.Paths.ToolsFolder.FullName, "nvapi"));
        this.gameDirectory = Settings.Paths.GameFolder;
        this.configDirectory = Settings.Paths.ConfigFolder;
        this.steamDirectory = Settings.Paths.SteamFolder;

        // TODO: Replace these with a nicer way of preventing a pileup of compat tools,
        // This implementation is just a hack.
        if (Directory.GetFiles(dxvkDirectory.FullName).Length >= DXVK_CLEANUP_THRESHHOLD)
        {
            Directory.Delete(dxvkDirectory.FullName, true);
            Directory.CreateDirectory(dxvkDirectory.FullName);
        }
        if (Directory.GetFiles(nvapiDirectory.FullName).Length >= NVAPI_CLEANUP_THRESHHOLD)
        {
            Directory.Delete(nvapiDirectory.FullName, true);
            Directory.CreateDirectory(nvapiDirectory.FullName);
        }
        if (Directory.GetFiles(wineDirectory.FullName).Length >= WINE_CLEANUP_THRESHHOLD)
        {
            Directory.Delete(wineDirectory.FullName, true);
            Directory.CreateDirectory(wineDirectory.FullName);
        }

        this.logWriter = new StreamWriter(wineSettings.LogFile.FullName);

        if (!this.wineDirectory.Exists)
            this.wineDirectory.Create();
        if (!this.dxvkDirectory.Exists)
            this.dxvkDirectory.Create();
        if (!this.nvapiDirectory.Exists)
            this.nvapiDirectory.Create();
        if (!this.steamDirectory.Exists && this.Settings.IsUsingRuntime);
        {
            this.steamDirectory.Create();
            this.steamDirectory.CreateSubdirectory(Path.Combine("steamapps", "common"));
            this.steamDirectory.CreateSubdirectory(Path.Combine("compatibilitytools.d"));
        }

        var pfx = new FileInfo(Path.Combine(Settings.Prefix.FullName, "pfx"));

        if (!Settings.Prefix.Exists)
            Settings.Prefix.Create();

        // Do proton prefixes like umu, with pfx symlinked back to prefix folder.
        if (Settings.IsProton)
        {
            if (pfx.Exists)
            {
                if (pfx.ResolveLinkTarget(false) is null) // File exists, is not a symlink
                    pfx.Delete();
                if (pfx.ResolveLinkTarget(true).FullName != Settings.Prefix.FullName) // symlink is wrong
                {
                    pfx.Delete();
                    pfx.CreateAsSymbolicLink(Settings.Prefix.FullName);
                }
            }
            else if (!Directory.Exists(pfx.FullName)) // pfx is not a directory, does not exist
            {
                pfx.CreateAsSymbolicLink(Settings.Prefix.FullName);
            }
        }
    }

    public async Task EnsureTool()
    {
        // Download sniper container if it's missing.
        if (Settings.IsUsingRuntime && !File.Exists(RuntimePath))
        {
            if (string.IsNullOrEmpty(Settings.RuntimeRelease.DownloadUrl))
                throw new ArgumentNullException("Steam runtime selected, but is not present, and no download url provided.");
            Log.Information($"Steam Sniper runtime does not exist, downloading {Settings.RuntimeRelease.DownloadUrl} to {Settings.Paths.SteamFolder.FullName}/steamapps/common");
            await DownloadRuntime().ConfigureAwait(false);
        }

        if (Settings.IsProton)
        {
            if (!File.Exists(Wine64Path))
            {
                if (string.IsNullOrEmpty(Settings.WineRelease.DownloadUrl))
                    throw new ArgumentNullException($"Proton not found at {Wine64Path}, and no download url provided.");
                Log.Information($"{Settings.WineRelease.Label} does not exist. Downloading {Settings.WineRelease.DownloadUrl} to {Settings.WineRelease.ParentFolder}");
                await DownloadTool(new DirectoryInfo(Settings.WineRelease.ParentFolder)).ConfigureAwait(false);
            }
            EnsurePrefix();
            IsToolReady = true;
            return;
        }

        if (!File.Exists(Wine64Path))
        {
            if (string.IsNullOrEmpty(Settings.WineRelease.DownloadUrl))
                throw new ArgumentNullException($"Wine not found at the given path: {Wine64Path}, and no download url provided.");
            Log.Information($"Wine release \"{Settings.WineRelease.Label}\" does not exist. Downloading {Settings.WineRelease.DownloadUrl} to {wineDirectory.FullName}");
            await DownloadTool(wineDirectory).ConfigureAwait(false);
            Settings.SetWineOrWine64(new FileInfo(Wine64Path).Directory.FullName);
        }

        if (!Settings.WineRelease.Lsteamclient)
        {
            var lsteamclient = new FileInfo(Path.Combine(Settings.Prefix.FullName, "drive_c", "windows", "system32", "lsteamclient.dll"));
            if (lsteamclient.Exists)
            {
                lsteamclient.Delete();
                Log.Verbose("Using custom wine or non-lsteamclient wine. Deleting lsteamclient.dll from prefix.");
            }
        }

        EnsurePrefix();

        if (isDxvkEnabled)
            await Dxvk.Dxvk.InstallDxvk(Settings.Prefix, dxvkDirectory, dxvkVersion).ConfigureAwait(false);
        if (isNvapiEnabled)
        {
            await Nvapi.Nvapi.InstallNvapi(Settings.Prefix, nvapiDirectory, nvapiVersion).ConfigureAwait(false);
            Nvapi.Nvapi.CopyNvngx(Settings.Paths.GameFolder, Settings.Prefix);
        }

        IsToolReady = true;
    }

    private async Task DownloadTool(DirectoryInfo targetPath)
    {
        using var client = HappyEyeballsHttp.CreateHttpClient();
        var tempPath = PlatformHelpers.GetTempFileName();
        await File.WriteAllBytesAsync(tempPath, await client.GetByteArrayAsync(Settings.WineRelease.DownloadUrl).ConfigureAwait(false)).ConfigureAwait(false);
        if (!CompatUtil.EnsureChecksumMatch(tempPath, Settings.WineRelease.Checksums))
        {
            throw new InvalidDataException("SHA512 checksum verification failed");
        }
        PlatformHelpers.Untar(tempPath, targetPath.FullName);
        Log.Information("Compatibility tool {Name} successfully extracted to {Path}", Settings.WineRelease.Label, targetPath.FullName);
        File.Delete(tempPath);
    }

    private async Task DownloadRuntime()
    {
        var targetPath = new DirectoryInfo(Path.Combine(Settings.Paths.SteamFolder.FullName, "steamapps", "common"));
        using var client = HappyEyeballsHttp.CreateHttpClient();
        var tempPath = PlatformHelpers.GetTempFileName();
        await File.WriteAllBytesAsync(tempPath, await client.GetByteArrayAsync(Settings.RuntimeRelease.DownloadUrl).ConfigureAwait(false)).ConfigureAwait(false);
        if (!CompatUtil.EnsureChecksumMatch(tempPath, [Settings.RuntimeRelease.Checksum]))
        {
            throw new InvalidDataException("SHA512 checksum verification failed");
        }
        PlatformHelpers.Untar(tempPath, targetPath.FullName);
        Log.Information("Steam Sniper runtime successfully extracted to {Path}", targetPath.FullName);
        File.Delete(tempPath);
    }

    public void EnsurePrefix()
    {
        bool runinprefix = true;
        // For proton, if the prefix hasn't been initialized, we need to use "proton run" instead of "proton runinprefix"
        // That will generate these files.
        if (!File.Exists(Path.Combine(Settings.Prefix.FullName, "config_info")) &&
            !File.Exists(Path.Combine(Settings.Prefix.FullName, "pfx.lock")) &&
            !File.Exists(Path.Combine(Settings.Prefix.FullName, "tracked_files")) &&
            !File.Exists(Path.Combine(Settings.Prefix.FullName, "version")))
        {
            runinprefix = false;
        }
        RunWithoutRuntime("cmd /c dir %userprofile%/Documents > nul", runinprefix, false).WaitForExit();
    }

    public Process RunWithoutRuntime(string command, bool runinprefix = true, bool redirect = true)
    {
        if (!Settings.IsProton)
            return RunInPrefix(command, redirectOutput: redirect, writeLog: redirect);
        var psi = new ProcessStartInfo(Wine64Path);
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;
        // Need to set these or proton will refuse to run.
        foreach (var kvp in Settings.EnvVars)
            psi.Environment.Add(kvp);
        psi.Environment.Add("WINEDLLOVERRIDES", Settings.WineDLLOverrides + (isDxvkEnabled ? "n,b" : "b"));
        psi.Arguments = runinprefix ? RunInPrefixVerb + command : RunVerb + command;
        var quickRun = new Process();
        quickRun.StartInfo = psi;
        quickRun.Start();
        Log.Verbose("Running without runtime: {FileName} {Arguments}", psi.FileName, psi.Arguments);
        return quickRun;
    }

    public Process RunInPrefix(string command, string workingDirectory = "", IDictionary<string, string> environment = null, bool redirectOutput = false, bool writeLog = false, bool wineD3D = false)
    {
        var psi = new ProcessStartInfo(RuntimePath);
        psi.Arguments = RuntimeArgs + RunInPrefixVerb + command;
        Log.Verbose("Running in prefix: {FileName} {Arguments}", psi.FileName, psi.Arguments);
        return RunInPrefix(psi, workingDirectory, environment, redirectOutput, writeLog, wineD3D);
    }

    public Process RunInPrefix(string[] args, string workingDirectory = "", IDictionary<string, string> environment = null, bool redirectOutput = false, bool writeLog = false, bool wineD3D = false)
    {
        var psi = new ProcessStartInfo(RuntimePath);
        if (Settings.IsUsingRuntime)
        {
            foreach (var arg in RuntimeArgsArray)
                psi.ArgumentList.Add(arg);
        }
        if (Settings.IsProton)
            psi.ArgumentList.Add(RunInPrefixVerb.Trim());
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        Log.Verbose("Running in prefix: {FileName} {Arguments}", psi.FileName, psi.ArgumentList.Aggregate(string.Empty, (a, b) => a + " " + b));
        return RunInPrefix(psi, workingDirectory, environment, redirectOutput, writeLog, wineD3D);
    }

    private void MergeDictionaries(StringDictionary a, IDictionary<string, string> b)
    {
        if (b is null)
            return;

        foreach (var keyValuePair in b)
        {
            if (a.ContainsKey(keyValuePair.Key))
            {
                if (keyValuePair.Key == "LD_PRELOAD")
                    a[keyValuePair.Key] = MergeLDPreload(a[keyValuePair.Key], keyValuePair.Value);
                else
                    a[keyValuePair.Key] = keyValuePair.Value;
            }
            else
                a.Add(keyValuePair.Key, keyValuePair.Value);
        }
    }

    private string MergeLDPreload(string a, string b)
    {
        a ??= "";
        b ??= "";
        return (a.Trim(':') + ":" + b.Trim(':')).Trim(':');
    }

    private Process RunInPrefix(ProcessStartInfo psi, string workingDirectory, IDictionary<string, string> environment, bool redirectOutput, bool writeLog, bool wineD3D)
    {
        psi.RedirectStandardOutput = redirectOutput;
        psi.RedirectStandardError = writeLog;
        psi.UseShellExecute = false;
        psi.WorkingDirectory = workingDirectory;

        var ogl = wineD3D || !isDxvkEnabled;

        var wineEnvironmentVariables = new Dictionary<string, string>();

        wineEnvironmentVariables.Add("WINEDLLOVERRIDES", Settings.WineDLLOverrides + (ogl ? "b" : "n,b"));

        if (!ogl)
        {
            if (!Settings.IsProton && isNvapiEnabled)
                wineEnvironmentVariables.Add("DXVK_ENABLE_NVAPI", "1");
            else if (Settings.IsProton && !isNvapiEnabled)
                wineEnvironmentVariables.Add("PROTON_DISABLE_NVAPI", "1");
        }

        if (!string.IsNullOrEmpty(Settings.DebugVars))
        {
            wineEnvironmentVariables.Add("WINEDEBUG", Settings.DebugVars);
        }

        wineEnvironmentVariables.Add("XL_WINEONLINUX", "true");
        string ldPreload = Environment.GetEnvironmentVariable("LD_PRELOAD") ?? "";

        var dxvkHud = hudType switch
        {
            RBHudType.None => "",
            RBHudType.Fps => "fps",
            RBHudType.Full => "full",
            RBHudType.Custom => customHud,
            _ => "",
        };

        var mangoHud = hudType switch
        {
            RBHudType.MHCustomFile => customHud,
            RBHudType.MHCustomString => customHud,
            RBHudType.MHDefault => "",
            RBHudType.MHFull => "full",
            _ => "0",
        };

        if (!string.IsNullOrEmpty(dxvkHud))
            wineEnvironmentVariables.Add("DXVK_HUD", dxvkHud);
        if (mangoHud != "0")
        {
            wineEnvironmentVariables.Add("MANGOHUD", "1");
            if (hudType == RBHudType.MHCustomFile)
                wineEnvironmentVariables.Add("MANGOHUD_CONFIGFILE", mangoHud);
            else
                wineEnvironmentVariables.Add("MANGOHUD_CONFIG", mangoHud);
        }

        if (this.gamemodeOn == true && !ldPreload.Contains("libgamemodeauto.so.0"))
        {
            ldPreload = ldPreload.Equals("", StringComparison.OrdinalIgnoreCase) ? "libgamemodeauto.so.0" : ldPreload + ":libgamemodeauto.so.0";
        }

        if (dxvkAsyncOn)
        {
            wineEnvironmentVariables.Add("DXVK_ASYNC", "1");
            wineEnvironmentVariables.Add("DXVK_GPLASYNCCACHE", gplAsyncCacheOn ? "1" : "0");
        }

        wineEnvironmentVariables.Add("LD_PRELOAD", ldPreload);

        MergeDictionaries(psi.EnvironmentVariables, wineEnvironmentVariables);
        MergeDictionaries(psi.EnvironmentVariables, Settings.EnvVars);
        MergeDictionaries(psi.EnvironmentVariables, environment);

        Process helperProcess = new();
        helperProcess.StartInfo = psi;
        helperProcess.ErrorDataReceived += new DataReceivedEventHandler((_, errLine) =>
        {
            if (string.IsNullOrEmpty(errLine.Data))
                return;

            try
            {
                logWriter.WriteLine(errLine.Data);
                Console.Error.WriteLine(errLine.Data);
            }
            catch (Exception ex) when (ex is ArgumentOutOfRangeException ||
                                       ex is OverflowException ||
                                       ex is IndexOutOfRangeException)
            {
                // very long wine log lines get chopped off after a (seemingly) arbitrary limit resulting in strings that are not null terminated
                //logWriter.WriteLine("Error writing Wine log line:");
                //logWriter.WriteLine(ex.Message);
            }
        });

        helperProcess.Start();
        if (writeLog)
            helperProcess.BeginErrorReadLine();

        return helperProcess;
    }

    public int[] GetProcessIds(string executableName)
    {
        var wineDbg = RunInPrefix("winedbg --command \"info proc\"", redirectOutput: true);
        var output = wineDbg.StandardOutput.ReadToEnd();
        var matchingLines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(l => l.Contains(executableName));
        return matchingLines.Select(l => int.Parse(l.Substring(1, 8), System.Globalization.NumberStyles.HexNumber)).ToArray();
    }

    public int GetProcessId(string executableName)
    {
        return GetProcessIds(executableName).FirstOrDefault();
    }

    public Int32 GetUnixProcessId(Int32 winePid)
    {
        var wineDbg = RunInPrefix("winedbg --command \"info procmap\"", redirectOutput: true);
        var output = wineDbg.StandardOutput.ReadToEnd();
        if (output.Contains("syntax error\n") || output.Contains("Exception c0000005")) // valve wine changed the error message
        {
            var processName = GetProcessName(winePid);
            return GetUnixProcessIdByName(processName);
        }
        var matchingLines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).Where(
            l => int.Parse(l.Substring(1, 8), System.Globalization.NumberStyles.HexNumber) == winePid);
        var unixPids = matchingLines.Select(l => int.Parse(l.Substring(10, 8), System.Globalization.NumberStyles.HexNumber)).ToArray();
        return unixPids.FirstOrDefault();
    }

    private string GetProcessName(Int32 winePid)
    {
        var wineDbg = RunInPrefix("winedbg --command \"info proc\"", redirectOutput: true);
        var output = wineDbg.StandardOutput.ReadToEnd();
        var matchingLines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).Where(
            l => int.Parse(l.Substring(1, 8), System.Globalization.NumberStyles.HexNumber) == winePid);
        var processNames = matchingLines.Select(l => l.Substring(20).Trim('\'')).ToArray();
        return processNames.FirstOrDefault();
    }

    private Int32 GetUnixProcessIdByName(string executableName)
    {
        int closest = 0;
        int early = 0;
        var currentProcess = Process.GetCurrentProcess(); // Gets XIVLauncher.Core's process
        bool nonunique = false;
        foreach (var process in Process.GetProcessesByName(executableName))
        {
            if (process.Id < currentProcess.Id)
            {
                early = process.Id;
                continue;  // Process was launched before XIVLauncher.Core
            }
            // Assume that the closest PID to XIVLauncher.Core's is the correct one. But log an error if more than one is found.
            if ((closest - currentProcess.Id) > (process.Id - currentProcess.Id) || closest == 0)
            {
                if (closest != 0) nonunique = true;
                closest = process.Id;
            }
            if (nonunique) Log.Error($"More than one {executableName} found! Selecting the most likely match with process id {closest}.");
        }
        // Deal with rare edge-case where pid rollover causes the ffxiv pid to be lower than XLCore's.
        if (closest == 0 && early != 0) closest = early;
        if (closest != 0) Log.Verbose($"Process for {executableName} found using fallback method: {closest}. XLCore pid: {currentProcess.Id}");
        return closest;
    }

    public string UnixToWinePath(string unixPath)
    {
        var launchArguments = $"winepath --windows \"{unixPath}\"";
        var winePath = RunWithoutRuntime(launchArguments);
        var output = winePath.StandardOutput.ReadToEnd();
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
    }

    public void AddRegistryKey(string key, string value, string data)
    {
        var args = new string[] { "reg", "add", key, "/v", value, "/d", data, "/f" };
        var wineProcess = RunInPrefix(args);
        wineProcess.WaitForExit();
    }

    public void Kill()
    {
        var psi = new ProcessStartInfo(WineServerPath)
        {
            Arguments = "-k"
        };
        psi.EnvironmentVariables.Add("WINEPREFIX", Settings.Prefix.FullName);

        Process.Start(psi);
    }
}
