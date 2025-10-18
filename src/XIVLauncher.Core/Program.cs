using CheapLoc;

using Config.Net;

using ImGuiNET;

using Serilog;

using System.Numerics;
using System.Runtime.InteropServices;

using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

using XIVLauncher.Common;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Game.Patch;
using XIVLauncher.Common.Game.Patch.Acquisition;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Support;
using XIVLauncher.Common.Unix;
using XIVLauncher.Common.Unix.Compatibility;
using XIVLauncher.Common.Unix.Compatibility.Dxvk;
using XIVLauncher.Common.Unix.Compatibility.Dxvk.Releases;
using XIVLauncher.Common.Unix.Compatibility.Nvapi;
using XIVLauncher.Common.Unix.Compatibility.Nvapi.Releases;
using XIVLauncher.Common.Unix.Compatibility.Wine;
using XIVLauncher.Common.Unix.Compatibility.Wine.Releases;
using XIVLauncher.Common.Util;
using XIVLauncher.Common.Windows;
using XIVLauncher.Core.Accounts.Secrets;
using XIVLauncher.Core.Accounts.Secrets.Providers;
using XIVLauncher.Core.Components.LoadingPage;
using XIVLauncher.Core.Configuration;
using XIVLauncher.Core.Configuration.Parsers;
using XIVLauncher.Core.Style;

namespace XIVLauncher.Core;

sealed class Program
{
    private const string APP_NAME = "xlcore";
    private static readonly Vector3 ClearColor = new(0.1f, 0.1f, 0.1f);
    private static string[] mainArgs = [];
    private static LauncherApp launcherApp = null!;
    private static Sdl2Window window = null!;
    private static CommandList commandList = null!;
    private static GraphicsDevice graphicsDevice = null!;
    private static ImGuiBindings guiBindings = null!;

    public static GraphicsDevice GraphicsDevice => graphicsDevice;
    public static ImGuiBindings ImGuiBindings => guiBindings;
    public static ILauncherConfig Config { get; private set; } = null!;
    public static CommonSettings CommonSettings => new(Config);
    public static ISteam? Steam { get; private set; }
    public static DalamudUpdater DalamudUpdater { get; private set; } = null!;
    public static DalamudOverlayInfoProxy DalamudLoadInfo { get; private set; } = null!;
    public static CompatibilityTools CompatibilityTools { get; private set; } = null!;
    public static ISecretProvider Secrets { get; private set; } = null!;
    public static HttpClient HttpClient { get; private set; } = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };
    public static PatchManager Patcher { get; set; } = null!;
    public static Storage storage = null!;
    public static DirectoryInfo DotnetRuntime => storage.GetFolder("runtime");
    public static string CType = CoreEnvironmentSettings.GetCType();

    // RB-specific properties
    public static WineManager WineManager { get; private set; }
    public static DxvkManager DxvkManager { get; private set; }
    public static NvapiManager NvapiManager { get; private set; }
    public static bool GameHasClosed { get; set; } = false;
    public static bool IsGameModeInstalled = CoreEnvironmentSettings.IsGameModeInstalled();
    public static bool IsMangoHudInstalled = CoreEnvironmentSettings.IsMangoHudInstalled();

    // TODO: We don't have the steamworks api for this yet.
    public static bool IsSteamDeckHardware => CoreEnvironmentSettings.IsDeck.HasValue ?
        CoreEnvironmentSettings.IsDeck.Value :
        Directory.Exists("/home/deck") || (CoreEnvironmentSettings.IsDeckGameMode ?? false) || (CoreEnvironmentSettings.IsDeckFirstRun ?? false);


    private static void SetupLogging(string[] args)
    {
        LogInit.Setup(Path.Combine(storage.GetFolder("logs").FullName, "launcher.log"), args);

        Log.Information("========================================================");
        Log.Information("Starting a session(v{Version} - {Hash})", AppUtil.GetAssemblyVersion(), AppUtil.GetGitHash());
    }

    private static void LoadConfig(Storage storage)
    {
        Config = new ConfigurationBuilder<ILauncherConfig>()
                 .UseCommandLineArgs()
                 .UseIniFile(storage.GetFile("launcher.ini").FullName)
                 .UseTypeParser(new DirectoryInfoParser())
                 .UseTypeParser(new AddonListParser())
                 .Build();

        if (string.IsNullOrEmpty(Config.AcceptLanguage))
        {
            Config.AcceptLanguage = ApiHelpers.GenerateAcceptLanguage();
        }

        if (Config.GamePath == null)
        {
            var envPath = Environment.GetEnvironmentVariable("STEAM_COMPAT_INSTALL_PATH"); // auto-set when using compat tool
            Config.GamePath = !string.IsNullOrWhiteSpace(envPath)
                ? new DirectoryInfo(envPath)
                : storage.GetFolder("ffxiv");
        }

        Config.GameConfigPath ??= storage.GetFolder("ffxivConfig");
        Config.ClientLanguage ??= ClientLanguage.English;
        Config.DpiAwareness ??= DpiAwareness.Unaware;
        Config.IsAutologin ??= false;
        Config.CompletedFts ??= false;
        Config.DoVersionCheck ??= true;
        Config.FontPxSize ??= 22.0f;

        Config.IsEncryptArgs ??= true;
        Config.IsOtpServer ??= false;
        Config.IsIgnoringSteam = CoreEnvironmentSettings.UseSteam.HasValue ? !CoreEnvironmentSettings.UseSteam.Value : Config.IsIgnoringSteam ?? false;

        Config.PatchPath ??= storage.GetFolder("patch");
        Config.PatchAcquisitionMethod ??= AcquisitionMethod.Aria;

        Config.DalamudEnabled ??= true;
        Config.DalamudLoadMethod ??= DalamudLoadMethod.EntryPoint;

        Config.GlobalScale ??= 1.0f;

        Config.GameModeEnabled ??= false;
        // Config.DxvkVersion ??= DxvkVersion.Stable;
        Config.DxvkAsyncEnabled ??= true;
        // Config.DxvkHudType ??= DxvkHudType.None;
        // Config.NvapiVersion ??= NvapiVersion.Stable;
        Config.ESyncEnabled ??= true;
        Config.FSyncEnabled ??= true;
        Config.NTSyncEnabled ??= false;
        Config.WaylandEnabled ??= false;
        Config.SetWin7 ??= false;

        // Config.WineStartupType ??= WineStartupType.Managed;
        // Config.WineManagedVersion ??= WineManagedVersion.Stable;
        // Config.WineBinaryPath ??= "/usr/bin";
        Config.WineDebugVars ??= "-all";
        Config.WineDLLOverrides = WineSettings.WineDLLOverrideIsValid(Config.WineDLLOverrides) ? Config.WineDLLOverrides : "";

        Config.FixLDP ??= false;
        Config.FixIM ??= false;
        Config.FixLocale ??= false;
        Config.FixError127 ??= false;
        Config.FixHideWineExports ??= true;
        Config.FixBrokenLsteamclient ??= false;

        // RB-patched replacement vars
        Config.RB_WineStartupType ??= RBWineStartupType.Managed;
        Config.RB_WineVersion = WineManager.GetWineVersionOrDefault(Config.RB_WineVersion);
        Config.RB_WineBinaryPath ??= "/usr/bin";
        Config.RB_ProtonVersion = WineManager.GetProtonVersionOrDefault(Config.RB_ProtonVersion);
        Config.RB_DxvkEnabled ??= true;
        Config.RB_NvapiEnabled ??= true;
        Config.RB_UmuLauncher ??= RBUmuLauncherType.System;
        Config.RB_DxvkVersion = DxvkManager.GetVersionOrDefault(Config.RB_DxvkVersion);
        Config.RB_NvapiVersion = NvapiManager.GetVersionOrDefault(Config.RB_NvapiVersion);
        Config.RB_GPLAsyncCacheEnabled ??= true;
        Config.RB_HudType ??= RBHudType.None;
        Config.RB_DxvkHudCustom ??= "1";
        Config.RB_MangoHudCustomFile ??= "";
        Config.RB_MangoHudCustomString ??= Dxvk.MANGOHUD_DEFAULT_STRING;
        Config.RB_DxvkFrameRate ??= 0;
        Config.RB_UseVulkanWineD3D ??= false;
        Config.RB_ProtonUseVulkanWineD3D ??= false;
        Config.RB_KeepToolsUpdated ??= true;

        // RB-patched App launcher
        Config.RB_App1 ??= "";
        Config.RB_App1Enabled ??= false;
        Config.RB_App1Args ??= "";
        Config.RB_App1WineD3D ??= false;
        Config.RB_App2 ??= "";
        Config.RB_App2Enabled ??= false;
        Config.RB_App2Args ??= "";
        Config.RB_App2WineD3D ??= false;
        Config.RB_App3 ??= "";
        Config.RB_App3Enabled ??= false;
        Config.RB_App3Args ??= "";
        Config.RB_App3WineD3D ??= false;
    }

    public const uint STEAM_APP_ID = 39210;
    public const uint STEAM_APP_ID_FT = 312060;

    /// <summary>
    ///     The name of the Dalamud injector executable file.
    /// </summary>
    // TODO: move this somewhere better.
    public const string DALAMUD_INJECTOR_NAME = "Dalamud.Injector.exe";

    /// <summary>
    ///     Creates a new instance of the Dalamud updater.
    /// </summary>
    /// <remarks>
    ///     If <see cref="ILauncherConfig.DalamudManualInjectionEnabled"/> is true and there is an injector at <see cref="ILauncherConfig.DalamudManualInjectPath"/> then
    ///     manual injection will be used instead of a Dalamud branch.
    /// </remarks>
    /// <returns>A <see cref="DalamudUpdater"/> instance.</returns>
    private static DalamudUpdater CreateDalamudUpdater()
    {
        FileInfo? runnerOverride = null;
        if (Config.DalamudManualInjectPath is not null &&
            Config.DalamudManualInjectionEnabled == true &&
            Config.DalamudManualInjectPath.Exists &&
            Config.DalamudManualInjectPath.GetFiles().FirstOrDefault(x => x.Name == DALAMUD_INJECTOR_NAME) is not null)
        {
            runnerOverride = new FileInfo(Path.Combine(Config.DalamudManualInjectPath.FullName, DALAMUD_INJECTOR_NAME));
        }
        return new DalamudUpdater(storage.GetFolder("dalamud"), storage.GetFolder("runtime"), storage.GetFolder("dalamudAssets"), storage.Root, null, null)
        {
            Overlay = DalamudLoadInfo,
            RunnerOverride = runnerOverride
        };
    }

    private static void Main(string[] args)
    {
        mainArgs = args;

        bool badxlpath = false;
        var badxlpathex = new Exception();
        string? useAltPath = CoreEnvironmentSettings.GetAltUserDir();
        
        try
        {
            storage = new Storage(APP_NAME, useAltPath);
        }
        catch (Exception e)
        {
            storage = new Storage(APP_NAME);
            badxlpath = true;
            badxlpathex = e;
        }

        SetupLogging(mainArgs);
        if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
            // This needs to be above LoadConfig so it can properly set defaults.
            WineManager = new WineManager(storage.Root.FullName, CoreEnvironmentSettings.IgnoreLists, CoreEnvironmentSettings.DisableListUpdate);
            DxvkManager = new DxvkManager(storage.Root.FullName, CoreEnvironmentSettings.IgnoreLists, CoreEnvironmentSettings.DisableListUpdate);
            NvapiManager = new NvapiManager(storage.Root.FullName, CoreEnvironmentSettings.IgnoreLists, CoreEnvironmentSettings.DisableListUpdate);
            LoadConfig(storage);
            WineManager.DownloadWineList(Config.RB_KeepToolsUpdated ?? true);
            DxvkManager.DownloadDxvkList(Config.RB_KeepToolsUpdated ?? true);
            NvapiManager.DownloadNvapiList(Config.RB_KeepToolsUpdated ?? true);
        }
        else
            LoadConfig(storage);

        if (badxlpath)
        {
            Log.Error(badxlpathex, $"Bad value for XL_PATH: {useAltPath}. Using ~/.xlcore instead.");
        }

        if (CoreEnvironmentSettings.ClearAll)
        {
            ClearAll();
        }
        else
        {
            if (CoreEnvironmentSettings.ClearSettings) ClearSettings();
            if (CoreEnvironmentSettings.ClearPrefix) ClearPrefix();
            if (CoreEnvironmentSettings.ClearPlugins) ClearPlugins();
            if (CoreEnvironmentSettings.ClearDalamud) ClearDalamud();
            if (CoreEnvironmentSettings.ClearTools) ClearTools();
            if (CoreEnvironmentSettings.ClearLogs) ClearLogs(true);
            if (CoreEnvironmentSettings.ClearNvngx) ClearNvngx();
        }

        Secrets = GetSecretProvider(storage);

        Loc.SetupWithFallbacks();

        Dictionary<uint, string> apps = [];
        uint[] ignoredIds = [0, STEAM_APP_ID, STEAM_APP_ID_FT];
        if (!ignoredIds.Contains(CoreEnvironmentSettings.SteamAppId))
        {
            apps.Add(CoreEnvironmentSettings.SteamAppId, "XLM");
        }
        if (!ignoredIds.Contains(CoreEnvironmentSettings.AltAppID))
        {
            apps.Add(CoreEnvironmentSettings.AltAppID, "XL_APPID");
        }
        apps.Add(STEAM_APP_ID, "FFXIV Retail");
        apps.Add(STEAM_APP_ID_FT, "FFXIV Free Trial");
        try
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                    Steam = new WindowsSteam();
                    break;

                case PlatformID.Unix:
                    Steam = new UnixSteam();
                    break;

                default:
                    throw new PlatformNotSupportedException();
            }
            if (Config.IsIgnoringSteam != true || CoreEnvironmentSettings.IsSteamCompatTool)
            {
                foreach (var app in apps)
                {
                    try
                    {
                        Steam.Initialize(app.Key);
                        Log.Information($"Successfully initialized Steam entry {app.Key} - {app.Value}");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Failed to initialize Steam Steam entry {app.Key} - {app.Value}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Steam couldn't load");
        }

        // Manual or auto injection setup.
        DalamudLoadInfo = new DalamudOverlayInfoProxy();
        DalamudUpdater = CreateDalamudUpdater();
        DalamudUpdater.Run();

        CreateCompatToolsInstance();

        Log.Debug("Creating Veldrid devices...");

#if DEBUG
        var version = AppUtil.GetGitHash();
#else
        var version = $"{AppUtil.GetAssemblyVersion()} ({AppUtil.GetGitHash()})";
#endif

        // Initialise SDL, as that's needed to figure out where to spawn the window.
        Sdl2Native.SDL_Init(SDLInitFlags.Video);

        // For now, just spawn the window on the primary display, which in SDL2 has displayIndex 0.
        // Maybe we may want to save the window location or the preferred display in the config at some point?
        if (!GetDisplayBounds(displayIndex: 0, out var bounds))
            Log.Warning("Couldn't figure out the bounds of the primary display, falling back to previous assumption that (0,0) is the top left corner of the left-most monitor.");

        // Create the window and graphics device separately, because Veldrid would have reinitialised SDL if done with their combined method.
        window = VeldridStartup.CreateWindow(new WindowCreateInfo(50 + bounds.X, 50 + bounds.Y, 1280, 800, WindowState.Normal, $"XIVLauncher-RB {version}"));
        graphicsDevice = VeldridStartup.CreateGraphicsDevice(window, new GraphicsDeviceOptions(false, null, true, ResourceBindingModel.Improved, true, true));

        window.Resized += () =>
        {
            graphicsDevice.MainSwapchain.Resize((uint)window.Width, (uint)window.Height);
            guiBindings.WindowResized(window.Width, window.Height);
        };
        commandList = graphicsDevice.ResourceFactory.CreateCommandList();
        Log.Debug("Veldrid OK!");

        guiBindings = new ImGuiBindings(graphicsDevice, graphicsDevice.MainSwapchain.Framebuffer.OutputDescription, window.Width, window.Height, storage.GetFile("launcherUI.ini"), Config.FontPxSize ?? 21.0f);
        Log.Debug("ImGui OK!");

        StyleModelV1.DalamudStandard.Apply();
        ImGui.GetIO().FontGlobalScale = Config.GlobalScale ?? 1.0f;

        var launcherClientConfig = LauncherClientConfig.GetAsync().GetAwaiter().GetResult();
        launcherApp = new LauncherApp(storage, launcherClientConfig.frontierUrl, launcherClientConfig.cutOffBootver);

        // Main application loop
        while (window.Exists)
        {
            Thread.Sleep(30);
            var snapshot = window.PumpEvents();
            if (!window.Exists)
                break;

            guiBindings.Update(1 / 60f, snapshot);
            launcherApp.Draw();
            commandList.Begin();
            commandList.SetFramebuffer(graphicsDevice.MainSwapchain.Framebuffer);
            commandList.ClearColorTarget(0, new RgbaFloat(ClearColor.X, ClearColor.Y, ClearColor.Z, 1f));
            guiBindings.Render(graphicsDevice, commandList);
            commandList.End();
            graphicsDevice.SubmitCommands(commandList);
            graphicsDevice.SwapBuffers(graphicsDevice.MainSwapchain);
        }

        // Clean up Veldrid resources
        // FIXME: Veldrid doesn't clean up after SDL though, so some leakage may have been happening for all this time.
        graphicsDevice.WaitForIdle();
        guiBindings.Dispose();
        commandList.Dispose();
        // This causes segfault for some reason
        if (GameHasClosed)
            graphicsDevice.Dispose();

        HttpClient.Dispose();

        if (Patcher is not null)
        {
            Patcher.CancelAllDownloads();
            Task.Run(async () =>
            {
                await PatchManager.UnInitializeAcquisition().ConfigureAwait(false);
                Environment.Exit(0);
            });
        }
    }

    public static void CreateCompatToolsInstance()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        WineManager.SetUmuLauncher(CoreEnvironmentSettings.UseBuiltinUmu || Config.RB_UmuLauncher == RBUmuLauncherType.Builtin);
        var wineLogFile = new FileInfo(Path.Combine(storage.GetFolder("logs").FullName, "wine.log"));
        var isProton = (Config.RB_WineStartupType == RBWineStartupType.Proton) ||
                        (Config.RB_WineStartupType == RBWineStartupType.Custom && WineSettings.IsValidProtonBinaryPath(Config.RB_WineBinaryPath));
        var winePrefix = isProton ? (new DirectoryInfo(CoreEnvironmentSettings.ProtonPrefix ?? Path.Combine(storage.Root.FullName, "protonprefix"))) :
                                    (new DirectoryInfo(CoreEnvironmentSettings.WinePrefix ?? Path.Combine(storage.Root.FullName, "wineprefix")));
        var toolsFolder = storage.GetFolder("compatibilitytool");
        var wineRelease = Config.RB_WineStartupType switch
        {
            RBWineStartupType.Custom => WineSettings.IsValidWineBinaryPath(Config.RB_WineBinaryPath) ? 
                new WineCustomRelease("CUSTOM", "Custom Wine", Config.RB_WineBinaryPath, "", "", WineSettings.HasLsteamclient(Config.RB_WineBinaryPath)) :
                new ProtonCustomRelease("CUSTOM", "Custom Proton", Config.RB_WineBinaryPath, "", ""),
            RBWineStartupType.Managed => WineManager.GetWine(Config.RB_WineVersion),
            RBWineStartupType.Proton => WineManager.GetProton(Config.RB_ProtonVersion),
            _ => throw new ArgumentOutOfRangeException(nameof(RBWineStartupType), $"Not an expected RBWineStartupType: {Config.RB_WineStartupType}")
        };
        var dxvkRelease = isProton ?
            (Config.RB_DxvkEnabled == true ? new DxvkCustomRelease("Enabled", "Enabled", "", "") : DxvkManager.GetDxvk("DISABLED") ) :
            DxvkManager.GetDxvk(Config.RB_DxvkVersion);
        var nvapiRelease = isProton ?
            (Config.RB_NvapiEnabled == true ? new NvapiCustomRelease("Enabled", "Enabled", "", "") : NvapiManager.GetNvapi("DISABLED") ) :
            NvapiManager.GetNvapi(Config.RB_NvapiVersion);
        var async = Config.RB_DxvkVersion.Contains("async") && Config.DxvkAsyncEnabled == true;
        var gplcache = Config.RB_DxvkVersion.Contains("gplasync") && Config.RB_GPLAsyncCacheEnabled == true;
        var paths = new XLCorePaths(winePrefix, toolsFolder, Config.GamePath, Config.GameConfigPath, WineManager.SteamFolder);
        var useUmu = Config.RB_UmuLauncher != RBUmuLauncherType.Disabled;
        var wineSettings = new WineSettings(wineRelease, useUmu ? WineManager.Runtime : null, Config.WineDLLOverrides ?? "", paths, Config.WineDebugVars, wineLogFile, Config.ESyncEnabled ?? true, Config.FSyncEnabled ?? true, Config.NTSyncEnabled ?? false, Config.WaylandEnabled ?? false);
        toolsFolder.CreateSubdirectory("wine");
        toolsFolder.CreateSubdirectory("dxvk");
        toolsFolder.CreateSubdirectory("nvapi");
        var customHud = Config.RB_HudType switch
        {
            RBHudType.None => "0",
            RBHudType.Fps => "fps",
            RBHudType.Full => "full",
            RBHudType.Custom => Config.RB_DxvkHudCustom ?? "1",
            RBHudType.MHCustomFile => Config.RB_MangoHudCustomFile ?? "",
            RBHudType.MHCustomString => Config.RB_MangoHudCustomString ?? "",
            RBHudType.MHDefault => "",
            RBHudType.MHFull => "full",
            _ => throw new ArgumentOutOfRangeException(nameof(RBHudType), $"Not an expected RBHudType: {Config.RB_HudType}")

        };
        CompatibilityTools = new CompatibilityTools(wineSettings, dxvkRelease, Config.RB_DxvkFrameRate ?? 0, Config.RB_HudType ?? RBHudType.None, customHud, nvapiRelease, Config.GameModeEnabled ?? false, async, gplcache);
    }

    public static void ShowWindow()
    {
        window.Visible = true;
    }

    public static void HideWindow()
    {
        window.Visible = false;
    }

    private static ISecretProvider GetSecretProvider(Storage storage)
    {
        var secretsFilePath = Environment.GetEnvironmentVariable("XL_SECRETS_FILE_PATH") ?? "secrets.json";

        var envVar = Environment.GetEnvironmentVariable("XL_SECRET_PROVIDER") ?? "KEYRING";
        envVar = envVar.ToUpper();

        switch (envVar)
        {
            case "FILE":
                return new FileSecretProvider(storage.GetFile(secretsFilePath));

            case "KEYRING":
                {
                    var keyChain = new KeychainSecretProvider();

                    if (!keyChain.IsAvailable)
                    {
                        Log.Error("An org.freedesktop.secrets provider is not available - no secrets will be stored");
                        return new DummySecretProvider();
                    }

                    return keyChain;
                }

            case "NONE":
                return new DummySecretProvider();

            default:
                throw new ArgumentException($"Invalid secret provider: {envVar}");
        }
    }

    public static void ClearSettings(bool tsbutton = false)
    {
        if (storage.GetFile("launcher.ini").Exists) storage.GetFile("launcher.ini").Delete();
        if (tsbutton)
        {
            LoadConfig(storage);
            launcherApp.State = LauncherApp.LauncherState.Settings;
        }
    }

    public static void ClearPrefix()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        storage.GetFolder("wineprefix").Delete(true);
        storage.GetFolder("wineprefix");
        storage.GetFolder("protonprefix").Delete(true);
        storage.GetFolder("protonprefix");
    }

    public static void ClearDalamud(bool tsbutton = false)
    {
        storage.GetFolder("dalamud").Delete(true);
        storage.GetFolder("dalamudAssets").Delete(true);
        storage.GetFolder("runtime").Delete(true);
        if (storage.GetFile("dalamudUI.ini").Exists) storage.GetFile("dalamudUI.ini").Delete();
        storage.GetFolder("dalamud");
        storage.GetFolder("dalamudAssets");
        storage.GetFolder("runtime");
        if (tsbutton)
        {
            DalamudLoadInfo = new DalamudOverlayInfoProxy();
            DalamudUpdater = CreateDalamudUpdater();
            DalamudUpdater.Run();
        }
    }

    public static void ClearPlugins()
    {
        storage.GetFolder("installedPlugins").Delete(true);
        storage.GetFolder("installedPlugins");
        if (storage.GetFile("dalamudConfig.json").Exists) storage.GetFile("dalamudConfig.json").Delete();
    }

    public static void ClearTools(bool tsbutton = false)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        storage.GetFolder("compatibilitytool").Delete(true);
        storage.GetFolder("compatibilitytool/wine");
        storage.GetFolder("compatibilitytool/dxvk");
        storage.GetFolder("compatibilitytool/nvapi");
        storage.GetFolder("compatibilitytool/umu");
        if (tsbutton) CreateCompatToolsInstance();
    }

    public static void ClearLogs(bool tsbutton = false)
    {
        storage.GetFolder("logs").Delete(true);
        storage.GetFolder("logs");
        string[] logfiles = { "dalamud.boot.log", "dalamud.boot.old.log", "dalamud.log", "dalamud.injector.log" };
        foreach (string logfile in logfiles)
            if (storage.GetFile(logfile).Exists) storage.GetFile(logfile).Delete();
        if (tsbutton)
            SetupLogging(mainArgs);

    }

    public static void ClearNvngx()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        var nvngx = new FileInfo(Path.Combine(Config.GamePath.FullName, "game", "nvngx.dll"));
        var _nvngx = new FileInfo(Path.Combine(Config.GamePath.FullName, "game", "_nvngx.dll"));
        var nvngxdlssg = new FileInfo(Path.Combine(Config.GamePath.FullName, "game", "nvngx_dlssg.dll"));
        if (nvngx.Exists) nvngx.Delete();
        if (_nvngx.Exists) _nvngx.Delete();
        if (nvngxdlssg.Exists) nvngxdlssg.Delete();
    }

    public static void ClearAll(bool tsbutton = false)
    {
        ClearSettings(tsbutton);
        ClearPrefix();
        ClearPlugins();
        ClearDalamud(tsbutton);
        ClearTools(tsbutton);
        ClearLogs(true);
        ClearNvngx();
    }

    public static void ResetUIDCache(bool tsbutton = false) => launcherApp.UniqueIdCache.Reset();

    private static unsafe bool GetDisplayBounds(int displayIndex, out Rectangle bounds)
    {
        bounds = new Rectangle();
        fixed (Rectangle* rectangle = &bounds)
        {
            if (Sdl2Native.SDL_GetDisplayBounds(displayIndex, rectangle) != 0)
            {
                return false;
            }
        }
        return true;
    }
}
