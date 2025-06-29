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
    private const string WINEDLLOVERRIDES = "msquic=,mscoree=n,b;d3d9,d3d11,d3d10core,dxgi=";
    private const uint DXVK_CLEANUP_THRESHHOLD = 5;
    private const uint NVAPI_CLEANUP_THRESHHOLD = 5;
    private const uint WINE_CLEANUP_THRESHHOLD = 10;

    private readonly DirectoryInfo wineDirectory;
    private readonly DirectoryInfo dxvkDirectory;
    private readonly DirectoryInfo nvapiDirectory;
    private readonly DirectoryInfo gameDirectory;
    private readonly StreamWriter logWriter;

    private string WineBinPath => Settings.WineRelease.Name;
    private string Wine64Path => Settings.GetWineBinary(WineBinPath);
    private string WineServerPath => Path.Combine(WineBinPath, "wineserver");

    private readonly IToolRelease dxvkVersion;
    private readonly DxvkHudType hudType;
    private readonly IToolRelease nvapiVersion;
    private readonly string ExtraWineDLLOverrides;
    private readonly bool gamemodeOn;
    private readonly bool dxvkAsyncOn;
    private readonly bool gplAsyncCacheOn;

    public bool IsToolReady { get; private set; }
    public WineSettings Settings { get; private set; }
    public bool IsToolDownloaded => !string.IsNullOrEmpty(Wine64Path) && Settings.Prefix.Exists;

    public CompatibilityTools(WineSettings wineSettings, IToolRelease dxvkVersion, DxvkHudType hudType, IToolRelease nvapiVersion, bool gamemodeOn, string winedlloverrides, bool dxvkAsyncOn, bool gplAsyncCacheOn, DirectoryInfo toolsFolder, DirectoryInfo gameDirectory)
    {
        this.Settings = wineSettings;
        this.dxvkVersion = dxvkVersion;
        this.hudType = hudType;
        this.nvapiVersion = dxvkVersion.Name != "DISABLED" ? nvapiVersion : new NvapiCustomRelease("Disabled", "Do not use Nvapi", "DISABLED", "");
        this.gamemodeOn = gamemodeOn;
        this.ExtraWineDLLOverrides = WineSettings.WineDLLOverrideIsValid(winedlloverrides) ? winedlloverrides : "";
        this.dxvkAsyncOn = dxvkAsyncOn;
        this.gplAsyncCacheOn = gplAsyncCacheOn;

        this.wineDirectory = new DirectoryInfo(Path.Combine(toolsFolder.FullName, "wine"));
        this.dxvkDirectory = new DirectoryInfo(Path.Combine(toolsFolder.FullName, "dxvk"));
        this.nvapiDirectory = new DirectoryInfo(Path.Combine(toolsFolder.FullName, "nvapi"));
        this.gameDirectory = gameDirectory;

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
        if (!wineSettings.Prefix.Exists)
            wineSettings.Prefix.Create();
    }

    public async Task EnsureTool(DirectoryInfo tempPath)
    {
        if (string.IsNullOrEmpty(Wine64Path))
        {
            Log.Information($"Compatibility tool does not exist, downloading {Settings.WineRelease.DownloadUrl}");
            await DownloadTool(tempPath).ConfigureAwait(false);
        }

        if (!Settings.WineRelease.lsteamclient)
        {
            var lsteamclient = new FileInfo(Path.Combine(Settings.Prefix.FullName, "drive_c", "windows", "system32", "lsteamclient.dll"));
            if (lsteamclient.Exists)
            {
                lsteamclient.Delete();
                Log.Verbose("Using custom wine or non-lsteamclient wine. Deleting lsteamclient.dll from prefix.");
            }
        }

        EnsurePrefix();

        await Dxvk.Dxvk.InstallDxvk(Settings.Prefix, dxvkDirectory, dxvkVersion).ConfigureAwait(false);
        await Nvapi.Nvapi.InstallNvapi(Settings.Prefix, nvapiDirectory, nvapiVersion).ConfigureAwait(false);
        if (nvapiVersion.Name != "DISABLED")
            Nvapi.Nvapi.CopyNvngx(gameDirectory, Settings.Prefix);

        IsToolReady = true;
    }

    private async Task DownloadTool(DirectoryInfo tempPath)
    {
        using var client = new HttpClient();
        var tempFilePath = Path.Combine(tempPath.FullName, $"{Guid.NewGuid()}");
        await File.WriteAllBytesAsync(tempFilePath, await client.GetByteArrayAsync(Settings.WineRelease.DownloadUrl).ConfigureAwait(false)).ConfigureAwait(false);
        if (!CompatUtil.EnsureChecksumMatch(tempFilePath, Settings.WineRelease.Checksums))
        {
            throw new InvalidDataException("SHA512 checksum verification failed");
        }
        PlatformHelpers.Untar(tempFilePath, this.wineDirectory.FullName);
        Log.Information("Compatibility tool successfully extracted to {Path}", this.wineDirectory.FullName);
        File.Delete(tempFilePath);
    }

    public void EnsurePrefix()
    {
        RunInPrefix("cmd /c dir %userprofile%/Documents > nul").WaitForExit();
    }

    public Process RunInPrefix(string command, string workingDirectory = "", IDictionary<string, string> environment = null, bool redirectOutput = false, bool writeLog = false, bool wineD3D = false)
    {
        var psi = new ProcessStartInfo(Wine64Path);
        psi.Arguments = command;

        Log.Verbose("Running in prefix: {FileName} {Arguments}", psi.FileName, command);
        return RunInPrefix(psi, workingDirectory, environment, redirectOutput, writeLog, wineD3D);
    }

    public Process RunInPrefix(string[] args, string workingDirectory = "", IDictionary<string, string> environment = null, bool redirectOutput = false, bool writeLog = false, bool wineD3D = false)
    {
        var psi = new ProcessStartInfo(Wine64Path);
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
                a[keyValuePair.Key] = keyValuePair.Value;
            else
                a.Add(keyValuePair.Key, keyValuePair.Value);
        }
    }

    private Process RunInPrefix(ProcessStartInfo psi, string workingDirectory, IDictionary<string, string> environment, bool redirectOutput, bool writeLog, bool wineD3D)
    {
        psi.RedirectStandardOutput = redirectOutput;
        psi.RedirectStandardError = writeLog;
        psi.UseShellExecute = false;
        psi.WorkingDirectory = workingDirectory;

        var ogl = wineD3D || this.dxvkVersion.Name == "DISABLED";

        var wineEnvironmentVariables = new Dictionary<string, string>
        {
            { "WINEPREFIX", Settings.Prefix.FullName },
            { "WINEDLLOVERRIDES", $"{WINEDLLOVERRIDES}{(ogl ? "b" : "n,b")};{ExtraWineDLLOverrides}" }
        };

        if (!ogl && nvapiVersion.Name != "DISABLED")
            wineEnvironmentVariables.Add("DXVK_ENABLE_NVAPI", "1");

        if (!string.IsNullOrEmpty(Settings.DebugVars))
        {
            wineEnvironmentVariables.Add("WINEDEBUG", Settings.DebugVars);
        }

        wineEnvironmentVariables.Add("XL_WINEONLINUX", "true");
        string ldPreload = Environment.GetEnvironmentVariable("LD_PRELOAD") ?? "";

        string dxvkHud = hudType switch
        {
            DxvkHudType.None => "0",
            DxvkHudType.Fps => "fps",
            DxvkHudType.Full => "full",
            _ => throw new ArgumentOutOfRangeException()
        };

        if (this.gamemodeOn == true && !ldPreload.Contains("libgamemodeauto.so.0"))
        {
            ldPreload = ldPreload.Equals("", StringComparison.OrdinalIgnoreCase) ? "libgamemodeauto.so.0" : ldPreload + ":libgamemodeauto.so.0";
        }

        wineEnvironmentVariables.Add("DXVK_HUD", dxvkHud);
        if (dxvkAsyncOn)
        {
            wineEnvironmentVariables.Add("DXVK_ASYNC", "1");
            wineEnvironmentVariables.Add("DXVK_GPLASYNCCACHE", gplAsyncCacheOn ? "1" : "0");
        }
        wineEnvironmentVariables.Add("WINEESYNC", Settings.EsyncOn ? "1" : "0");
        wineEnvironmentVariables.Add("WINEFSYNC", Settings.FsyncOn ? "1" : "0");

        wineEnvironmentVariables.Add("LD_PRELOAD", ldPreload);

        MergeDictionaries(psi.EnvironmentVariables, wineEnvironmentVariables);
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
        var launchArguments = new string[] { "winepath", "--windows", unixPath };
        var winePath = RunInPrefix(launchArguments, redirectOutput: true);
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
