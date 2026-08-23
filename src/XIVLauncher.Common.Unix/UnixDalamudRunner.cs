using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Serilog;

using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Unix.Compatibility;

namespace XIVLauncher.Common.Unix;

public class UnixDalamudRunner : IDalamudRunner
{
    private readonly CompatibilityTools compatibility;
    private readonly DirectoryInfo dotnetRuntime;
    private readonly bool ProtonLogging;

    public UnixDalamudRunner(CompatibilityTools compatibility, DirectoryInfo dotnetRuntime, bool protonLogging = false)
    {
        this.compatibility = compatibility;
        this.dotnetRuntime = dotnetRuntime;
        this.ProtonLogging = protonLogging;
    }

    public Process? Run(FileInfo runner, bool fakeLogin, bool noPlugins, bool noThirdPlugins, FileInfo gameExe, string gameArgs, IDictionary<string, string> environment, DalamudLoadMethod loadMethod, DalamudStartInfo startInfo)
    {
        var gameExePath = "";
        var dotnetRuntimePath = "";
        var oldLoggingPath = startInfo.LoggingPath;

        Parallel.Invoke(
            () => { gameExePath = compatibility.UnixToWinePath(gameExe.FullName); },
            () => { dotnetRuntimePath = compatibility.UnixToWinePath(dotnetRuntime.FullName); },
            () => { startInfo.LoggingPath = compatibility.UnixToWinePath(startInfo.LoggingPath); },
            () => { startInfo.WorkingDirectory = compatibility.UnixToWinePath(startInfo.WorkingDirectory); },
            () => { startInfo.ConfigurationPath = compatibility.UnixToWinePath(startInfo.ConfigurationPath); },
            () => { startInfo.PluginDirectory = compatibility.UnixToWinePath(startInfo.PluginDirectory); },
            () => { startInfo.AssetDirectory = compatibility.UnixToWinePath(startInfo.AssetDirectory); }
        );

        var prevDalamudRuntime = Environment.GetEnvironmentVariable("DALAMUD_RUNTIME");
        if (!string.IsNullOrWhiteSpace(prevDalamudRuntime))
            dotnetRuntimePath = prevDalamudRuntime;

        environment.Add("DALAMUD_RUNTIME", dotnetRuntimePath);
        environment.Add("DOTNET_ROOT", dotnetRuntimePath);
        environment.Add("DOTNET_ROOT_X64", dotnetRuntimePath);
        environment.Add("WINEDOTNET_ROOT", dotnetRuntimePath);

        var launchArguments = new List<string>
        {
            $"\"{runner.FullName}\"",
            DalamudInjectorArgs.LAUNCH,
            DalamudInjectorArgs.Mode(loadMethod == DalamudLoadMethod.EntryPoint ? "entrypoint" : "inject"),
            DalamudInjectorArgs.Game(gameExePath),
            DalamudInjectorArgs.WorkingDirectory(startInfo.WorkingDirectory),
            DalamudInjectorArgs.ConfigurationPath(startInfo.ConfigurationPath),
            DalamudInjectorArgs.LoggingPath(startInfo.LoggingPath),
            DalamudInjectorArgs.PluginDirectory(startInfo.PluginDirectory),
            DalamudInjectorArgs.AssetDirectory(startInfo.AssetDirectory),
            DalamudInjectorArgs.ClientLanguage((int)startInfo.Language),
            DalamudInjectorArgs.DelayInitialize(startInfo.DelayInitializeMs),
            DalamudInjectorArgs.TsPackB64(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(startInfo.TroubleshootingPackData))),
        };

        if (loadMethod == DalamudLoadMethod.ACLonly)
            launchArguments.Add(DalamudInjectorArgs.WITHOUT_DALAMUD);

        if (fakeLogin)
            launchArguments.Add(DalamudInjectorArgs.FAKE_ARGUMENTS);

        if (noPlugins)
            launchArguments.Add(DalamudInjectorArgs.NO_PLUGIN);

        if (noThirdPlugins)
            launchArguments.Add(DalamudInjectorArgs.NO_THIRD_PARTY);

        launchArguments.Add("--");
        launchArguments.Add(gameArgs);

        var dalamudProcess = compatibility.RunTheGame(string.Join(" ", launchArguments), environment: environment, redirectOutput: true, writeLog: true);

        DalamudConsoleOutput dalamudConsoleOutput = null;
        int invalidJsonCount = 0;
 
        // Keep checking for valid json output, but only 5 times. If it's still erroring out at that point, give up.
        while (dalamudConsoleOutput == null && invalidJsonCount < 5)
        {
            Console.WriteLine("Waiting for Dalamud output...");
            if (ProtonLogging)
                break; // Don't wait for Dalamud output if we're logging proton, as it will be in the proton log instead
            var output = dalamudProcess.StandardOutput.ReadLine();
            if (output == null)
            {
                if (ProtonLogging)
                    break;
                else
                    throw new DalamudRunnerException("An internal Dalamud error has occured");
            }
            Console.WriteLine(output);

            try
            {
                dalamudConsoleOutput = JsonConvert.DeserializeObject<DalamudConsoleOutput>(output);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, $"Couldn't parse Dalamud output: {output}");
            }
            invalidJsonCount++;
        }

        new Thread(() =>
        {
            while (!dalamudProcess.StandardOutput.EndOfStream)
            {
                var output = dalamudProcess.StandardOutput.ReadLine();
                if (output != null)
                    Console.WriteLine(output);
            }

        }).Start();

        if (ProtonLogging)
        {
            try
            {
                Process gameProcess;

                int counter = 0;
                var ffxivPid = compatibility.GetUnixProcessIdByName(gameExe.Name);
                Log.Information($"Starting check for {gameExe.Name} [Proton Logging]");
                while (ffxivPid == 0 && counter < 300) // After 30 seconds (300 * 100 ms), assume something went wrong and end the loop.
                {
                    ffxivPid = compatibility.GetUnixProcessIdByName(gameExe.Name);
                    counter++;
                    Thread.Sleep(100);
                }
                if (ffxivPid == 0)
                {
                    Log.Error($"Could not find unix process id for {gameExe.Name} [Proton Logging]");
                    return null;
                }

                gameProcess = Process.GetProcessById(ffxivPid);
                Log.Information($"It took about {((decimal)counter / 10)} seconds to find the unix pid for {gameExe.Name}");
                Log.Information($"Got game process handle {gameProcess.Handle} with Unix pid {gameProcess.Id} [Proton Logging]");
                return gameProcess;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Could not retrieve game Process information");
                return null;
            }
        }

        try
        {
            var unixPid = compatibility.GetUnixProcessId(dalamudConsoleOutput.Pid);

            if (unixPid == 0)
            {
                Log.Error("Could not retrieve Unix process ID");
                return null;
            }

            var gameProcess = Process.GetProcessById(unixPid);
            Log.Verbose($"Got game process handle {gameProcess.Handle} with Unix pid {gameProcess.Id} and Wine pid {dalamudConsoleOutput.Pid}");
            return gameProcess;
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Could not retrieve game Process information");
            return null;
        }
    }
}