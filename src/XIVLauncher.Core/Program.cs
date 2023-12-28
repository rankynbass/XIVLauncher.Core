using System.Numerics;
using System.IO;
using CheapLoc;
using Config.Net;
using ImGuiNET;
using XIVLauncher.Core.Style;
using Serilog;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using XIVLauncher.Common;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Game.Patch.Acquisition;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Support;
using XIVLauncher.Common.Windows;
using XIVLauncher.Common.Unix;
using XIVLauncher.Common.Unix.Compatibility;
using XIVLauncher.Common.Util;
using XIVLauncher.Core.Accounts.Secrets;
using XIVLauncher.Core.Accounts.Secrets.Providers;
using XIVLauncher.Core.Components.LoadingPage;
using XIVLauncher.Core.Configuration;
using XIVLauncher.Core.Configuration.Parsers;
using XIVLauncher.Core.UnixCompatibility;

namespace XIVLauncher.Core;

class Program
{
    private static Sdl2Window window;
    private static CommandList cl;
    private static GraphicsDevice gd;
    private static ImGuiBindings bindings;

    public static GraphicsDevice GraphicsDevice => gd;
    public static ImGuiBindings ImGuiBindings => bindings;
    public static ILauncherConfig Config { get; private set; }
    public static CommonSettings CommonSettings => new(Config);
    public static ISteam? Steam { get; private set; }
    public static DalamudUpdater DalamudUpdater { get; private set; }
    public static DalamudOverlayInfoProxy DalamudLoadInfo { get; private set; }
    public static CompatibilityTools CompatibilityTools { get; private set; }
    public static ISecretProvider Secrets { get; private set; }

    private static readonly Vector3 clearColor = new(0.1f, 0.1f, 0.1f);
    private static bool showImGuiDemoWindow = true;

    private static LauncherApp launcherApp;
    public static Storage storage;
    public static DirectoryInfo DotnetRuntime => storage.GetFolder("runtime");

    // TODO: We don't have the steamworks api for this yet.
    public static bool IsSteamDeckHardware => CoreEnvironmentSettings.IsDeck.HasValue ?
        CoreEnvironmentSettings.IsDeck.Value :
        Directory.Exists("/home/deck") || (CoreEnvironmentSettings.IsDeckGameMode ?? false) || (CoreEnvironmentSettings.IsDeckFirstRun ?? false);
    public static bool IsSteamDeckGamingMode => CoreEnvironmentSettings.IsDeckGameMode.HasValue ?
        CoreEnvironmentSettings.IsDeckGameMode.Value :
        Steam != null && Steam.IsValid && Steam.IsRunningOnSteamDeck();

    private const string APP_NAME = "xlcore";

    private static string[] mainargs;

    private static uint invalidationFrames = 0;
    private static Vector2 lastMousePosition;

    public static void Invalidate(uint frames = 100)
    {
        invalidationFrames = frames;
    }

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

        Config.GamePath ??= storage.GetFolder("ffxiv");
        Config.GameConfigPath ??= storage.GetFolder("ffxivConfig");
        Config.ClientLanguage ??= ClientLanguage.English;
        Config.DpiAwareness ??= DpiAwareness.Unaware;
        Config.IsAutologin ??= false;
        Config.CompletedFts ??= false;
        Config.DoVersionCheck ??= true;
        Config.FontPxSize ??= 22.0f;

        Config.IsDx11 ??= true;
        Config.IsEncryptArgs ??= true;
        Config.IsFt ??= false;
        Config.IsOtpServer ??= false;
        Config.IsIgnoringSteam = CoreEnvironmentSettings.UseSteam.HasValue ? !CoreEnvironmentSettings.UseSteam.Value : Config.IsIgnoringSteam ?? false;

        Config.PatchPath ??= storage.GetFolder("patch");
        Config.PatchAcquisitionMethod ??= AcquisitionMethod.Aria;

        Config.DalamudEnabled ??= true;
        Config.DalamudLoadMethod = !OperatingSystem.IsWindows() ? DalamudLoadMethod.DllInject : DalamudLoadMethod.EntryPoint;

        Config.GlobalScale ??= 1.0f;

        Config.GameModeEnabled ??= false;
        Config.ESyncEnabled ??= true;
        Config.FSyncEnabled ??= false;

        Config.WineType ??= WineType.Managed;
        Config.WineVersion = Wine.IsValid(Config.WineVersion) ? Config.WineVersion : Wine.GetDefaultVersion();
        Config.WineBinaryPath ??= "/usr/bin";
        Config.WineDebugVars ??= "-all";

        Config.DxvkVersion = Dxvk.IsValid(Config.DxvkVersion) ? Config.DxvkVersion : Dxvk.GetDefaultVersion();
        Config.DxvkAsyncEnabled ??= true;
        Config.DxvkGPLAsyncCacheEnabled ??= false;
        Config.DxvkFrameRateLimit ??= 0;
        Config.DxvkHud ??= DxvkHud.None;
        Config.DxvkHudCustom ??= Dxvk.DXVK_HUD;
        Config.MangoHud ??= MangoHud.None;
        Config.MangoHudCustomString ??= Dxvk.MANGOHUD_CONFIG;
        Config.MangoHudCustomFile ??= Dxvk.MANGOHUD_CONFIGFILE;

        Config.ProtonVersion ??= "Proton 8.0";
        Config.SteamRuntime ??= OSInfo.IsFlatpak ? "Disabled" : "SteamLinuxRuntime_sniper";

        Config.FixLDP ??= false;
        Config.FixIM ??= false;
        Config.FixLocale = Locale.IsValid(Config.FixLocale) ? Config.FixLocale : Locale.GetDefaultCode();

        var xdg_data_home = (OSInfo.IsFlatpak) ? Path.Combine(CoreEnvironmentSettings.HOME, ".local", "share") : CoreEnvironmentSettings.XDG_DATA_HOME;
        Config.SteamPath ??= Path.Combine(xdg_data_home, "Steam");
        Config.SteamFlatpakPath ??= Path.Combine(CoreEnvironmentSettings.HOME, ".var", "app", "com.valvesoftware.Steam", "data", "Steam" );

        Config.HelperApp1Enabled ??= false;
        Config.HelperApp1 ??= string.Empty;
        Config.HelperApp1WineD3D ??= false;
        Config.HelperApp2Enabled ??= false;
        Config.HelperApp2 ??= string.Empty;
        Config.HelperApp2WineD3D ??= false;
        Config.HelperApp3Enabled ??= false;
        Config.HelperApp3 ??= string.Empty;
        Config.HelperApp3WineD3D ??= false;
    }

    public const uint STEAM_APP_ID = 39210;
    public const uint STEAM_APP_ID_FT = 312060;

    private static void Main(string[] args)
    {
        mainargs = args;

        bool badxlpath = false;
        var badxlpathex = new Exception();
        string? useAltPath = Environment.GetEnvironmentVariable("XL_PATH");
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
        Wine.Initialize();
        Dxvk.Initialize();

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
            if (CoreEnvironmentSettings.ClearDalamud) ClearDalamud();
            if (CoreEnvironmentSettings.ClearPlugins) ClearPlugins();
            if (CoreEnvironmentSettings.ClearTools) ClearTools();
            if (CoreEnvironmentSettings.ClearLogs) ClearLogs();
        }

        SetupLogging(mainargs);
        LoadConfig(storage);
        Proton.Initialize(OSInfo.IsFlatpak && CoreEnvironmentSettings.IsSteamCompatTool ? Config.SteamFlatpakPath : Config.SteamPath);
        Config.ProtonVersion = Proton.VersionExists(Config.ProtonVersion) ? Config.ProtonVersion : Proton.GetDefaultVersion();
        Config.SteamRuntime = Proton.RuntimeExists(Config.SteamRuntime) ? Config.SteamRuntime : Proton.GetDefaultRuntime();
        
        Secrets = GetSecretProvider(storage);

        Loc.SetupWithFallbacks();

        uint appId, altId;
        string appName, altName;
        if (Config.IsFt.Value)
        {
            appId = STEAM_APP_ID_FT;
            altId = STEAM_APP_ID;
            appName = "FFXIV Free Trial";
            altName = "FFXIV Retail";
        }
        else
        {
            appId = STEAM_APP_ID;
            altId = STEAM_APP_ID_FT;
            appName = "FFXIV Retail";
            altName = "FFXIV Free Trial";
        }
        try
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                    Steam = new WindowsSteam();
                    break;

                case PlatformID.Unix:
                    if (CommandLineInstaller()) return;
                    Steam = new UnixSteam();
                    break;

                default:
                    throw new PlatformNotSupportedException();
            }
            if (CoreEnvironmentSettings.IsSteamCompatTool || (!Config.IsIgnoringSteam ?? true))
            {
                try
                {
                    Steam.Initialize(appId);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Couldn't init Steam with AppId={appId} ({appName}), trying AppId={altId} ({altName})");
                    Steam.Initialize(altId);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Steam couldn't load");
        }

        DalamudLoadInfo = new DalamudOverlayInfoProxy();
        DalamudUpdater = new DalamudUpdater(storage.GetFolder("dalamud"), storage.GetFolder("runtime"), storage.GetFolder("dalamudAssets"), storage.Root, null, null)
        {
            Overlay = DalamudLoadInfo
        };
        DalamudUpdater.Run();

        CreateCompatToolsInstance();

        Log.Debug("Creating Veldrid devices...");

#if DEBUG
        var version = AppUtil.GetGitHash();
#else
        var version = $"{AppUtil.GetAssemblyVersion()} ({AppUtil.GetGitHash()})";
#endif

        // Create window, GraphicsDevice, and all resources necessary for the demo.
        VeldridStartup.CreateWindowAndGraphicsDevice(
            new WindowCreateInfo(50, 50, (int)(1280 * ImGuiHelpers.GlobalScale), (int)(800 * ImGuiHelpers.GlobalScale), WindowState.Normal, $"XIVLauncher {version} RB-patched"),
            new GraphicsDeviceOptions(false, null, true, ResourceBindingModel.Improved, true, true),
            out window,
            out gd);

        window.Resized += () =>
        {
            gd.MainSwapchain.Resize((uint)window.Width, (uint)window.Height);
            bindings.WindowResized(window.Width, window.Height);
            Invalidate();
        };
        cl = gd.ResourceFactory.CreateCommandList();
        Log.Debug("Veldrid OK!");

        bindings = new ImGuiBindings(gd, gd.MainSwapchain.Framebuffer.OutputDescription, window.Width, window.Height, storage.GetFile("launcherUI.ini"), (Config.FontPxSize ?? 22.0f) * ImGuiHelpers.GlobalScale);
        Log.Debug("ImGui OK!");

        StyleModelV1.DalamudStandard.Apply();

        launcherApp = new LauncherApp(storage);

        Invalidate(20);

        // Main application loop
        while (window.Exists)
        {
            Thread.Sleep(50);

            InputSnapshot snapshot = window.PumpEvents();

            if (!window.Exists)
                break;

            var overlayNeedsPresent = false;

            if (Steam != null && Steam.IsValid)
                overlayNeedsPresent = Steam.BOverlayNeedsPresent;

            if (!snapshot.KeyEvents.Any() && !snapshot.MouseEvents.Any() && !snapshot.KeyCharPresses.Any() && invalidationFrames == 0 && lastMousePosition == snapshot.MousePosition
                && !overlayNeedsPresent)
            {
                continue;
            }

            if (invalidationFrames == 0)
            {
                invalidationFrames = 10;
            }

            if (invalidationFrames > 0)
            {
                invalidationFrames--;
            }

            lastMousePosition = snapshot.MousePosition;

            bindings.Update(1f / 60f, snapshot);

            launcherApp.Draw();

            cl.Begin();
            cl.SetFramebuffer(gd.MainSwapchain.Framebuffer);
            cl.ClearColorTarget(0, new RgbaFloat(clearColor.X, clearColor.Y, clearColor.Z, 1f));
            bindings.Render(gd, cl);
            cl.End();
            gd.SubmitCommands(cl);
            gd.SwapBuffers(gd.MainSwapchain);
        }

        // Clean up Veldrid resources
        gd.WaitForIdle();
        bindings.Dispose();
        cl.Dispose();
        gd.Dispose();
    }

    public static void CreateCompatToolsInstance()
    {
        var dxvkSettings = new DxvkSettings(Dxvk.FolderName, Dxvk.DownloadUrl, storage.Root.FullName, Dxvk.AsyncEnabled, Dxvk.FrameRateLimit, Dxvk.DxvkHudEnabled, Dxvk.DxvkHudString, Dxvk.MangoHudEnabled, Dxvk.MangoHudCustomIsFile, Dxvk.MangoHudString, Dxvk.Enabled);
        var wineSettings = new WineSettings(Wine.IsManagedWine, Wine.CustomWinePath, Wine.FolderName, Wine.DownloadUrl, storage.Root, Wine.DebugVars, Wine.LogFile, Wine.Prefix, Wine.ESyncEnabled, Wine.FSyncEnabled, Wine.ProtonInfo);
        var toolsFolder = storage.GetFolder("compatibilitytool");
        CompatibilityTools = new CompatibilityTools(wineSettings, dxvkSettings, Config.GameModeEnabled, toolsFolder, OSInfo.IsFlatpak);
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
        storage.GetFolder("wineprefix").Delete(true);
        storage.GetFolder("wineprefix");
        storage.GetFolder("protonprefix").Delete(true);
        storage.GetFolder("protonprefix/pfx");
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
            DalamudUpdater = new DalamudUpdater(storage.GetFolder("dalamud"), storage.GetFolder("runtime"), storage.GetFolder("dalamudAssets"), storage.Root, null, null)
            {
                Overlay = DalamudLoadInfo
            };
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
        foreach (var winetool in Wine.Versions)
        {
            if (winetool.Value.ContainsKey("url"))
                if (!string.IsNullOrEmpty(winetool.Value["url"]))
                    storage.GetFolder($"compatibilitytool/wine/{winetool.Key}").Delete(true);
        }
        foreach (var dxvktool in Dxvk.Versions)
        {
            if (dxvktool.Value.ContainsKey("url"))
                if (!string.IsNullOrEmpty(dxvktool.Value["url"]))
                    storage.GetFolder($"compatibilitytool/dxvk/{dxvktool.Key}").Delete(true);
        }
        // Re-initialize Versions so they get *Download* marks back.
        Wine.Initialize();
        Dxvk.Initialize();

        if (tsbutton) CreateCompatToolsInstance();
    }

    public static void ClearLogs(bool tsbutton = false)
    {
        storage.GetFolder("logs").Delete(true);
        storage.GetFolder("logs");
        string[] logfiles = { "dalamud.boot.log", "dalamud.boot.old.log", "dalamud.log", "dalamud.injector.log"};
        foreach (string logfile in logfiles)
            if (storage.GetFile(logfile).Exists) storage.GetFile(logfile).Delete();
        if (tsbutton)
            SetupLogging(mainargs);
        
    }

    public static void ClearAll(bool tsbutton = false)
    {
        ClearSettings(tsbutton);
        ClearPrefix();
        ClearDalamud(tsbutton);
        ClearPlugins();
        ClearTools(tsbutton);
        ClearLogs(true);
    }

    public static bool? IsReshadeEnabled()
    {
        var gamepath = Path.Combine(Config.GamePath.FullName, "game");
        var dxgiE = Path.Combine(gamepath, "dxgi.dll");
        var dxgiD = Path.Combine(gamepath, "dxgi.dll.disabled");
        var compilerE = Path.Combine(gamepath, "d3dcompiler_47.dll");
        var compilerD = Path.Combine(gamepath, "d3dcompiler_47.dll.disabled");
        if (File.Exists(dxgiE) && File.Exists(compilerE))
            return true;
        if (File.Exists(dxgiD) && File.Exists(compilerD))
            return false;
        if (File.Exists(dxgiE) && File.Exists(compilerD))
        {
            File.Move(compilerD, compilerE);
            return true;
        }
        if (File.Exists(dxgiD) && File.Exists(compilerE))
        {
            File.Move(compilerE, compilerD);
            return false;
        }
        return null;
    }

    public static void ToggleReshade()
    {
        var gamepath = Path.Combine(Config.GamePath.FullName, "game");
        var dxgiE = Path.Combine(gamepath, "dxgi.dll");
        var dxgiD = Path.Combine(gamepath, "dxgi.dll.disabled");
        var compilerE = Path.Combine(gamepath, "d3dcompiler_47.dll");
        var compilerD = Path.Combine(gamepath, "d3dcompiler_47.dll.disabled");
        if (File.Exists(dxgiE))
        {
            if (File.Exists(dxgiD))
                File.Delete(dxgiD);
            if (File.Exists(compilerD))
                File.Delete(compilerD);

            File.Move(dxgiE, dxgiD);
            if (File.Exists(compilerE))
                File.Move(compilerE, compilerD);
        }
        else if (File.Exists(dxgiD))
        {
            if (File.Exists(compilerE))
                File.Delete(compilerE);

            File.Move(dxgiD, dxgiE);
            if (File.Exists(compilerD))
                File.Move(compilerD, compilerE);
        }
        else
        {
            Log.Error("Tried to toggle ReShade, but dxgi.dll or dxgi.dll.disabled not present");
        }
    }

    private static bool CommandLineInstaller()
    {
        foreach (var arg in mainargs)
        {
            if (arg == "--deck-install")
            {
                SteamCompatibilityTool.CreateTool(Path.Combine(CoreEnvironmentSettings.HOME, ".local", "share", "Steam"));
                Console.WriteLine($"Installed as Steam compatibility tool to {Path.Combine(CoreEnvironmentSettings.HOME, ".local", "share", "Steam", "compatibilitytools.d", "xlcore")}");
                return true;
            }
            if (arg.StartsWith("--install="))
            {
                var path = arg.Split('=', 2)[1];
                if (path.StartsWith("~/"))
                    path = CoreEnvironmentSettings.HOME + path.TrimStart('~');
                if (Directory.Exists(path) && (path.EndsWith("/Steam") || path.EndsWith("/Steam/")))
                {
                    SteamCompatibilityTool.CreateTool(path);
                    Console.WriteLine($"Installed as Steam compatibility tool to path {Path.Combine(path, "compatibilitytools.d", "xlcore")}");
                }
                else
                    Console.WriteLine($"Invalid path. Path does not exist or is not a Steam folder: {path}");
                
                return true;
            }
            if (arg == "--deck-remove")
            {
                SteamCompatibilityTool.DeleteTool(Path.Combine(CoreEnvironmentSettings.HOME, ".local", "share", "Steam"));
                Console.WriteLine($"Removed XIVLauncher.Core as a Steam compatibility tool from {Path.Combine(CoreEnvironmentSettings.HOME, ".local", "share", "Steam", "compatibilitytools.d")}");
                return true;
            }
            if (arg.StartsWith("--remove="))
            {
                var path = arg.Split('=', 2)[1];
                if (path.StartsWith("~/"))
                    path = CoreEnvironmentSettings.HOME + path.TrimStart('~');
                if (Directory.Exists(path) && (path.EndsWith("/Steam") || path.EndsWith("/Steam/")))
                {
                    SteamCompatibilityTool.DeleteTool(path);
                    Console.WriteLine($"Removed XIVLauncher.Core as a Steam compatibility tool from {Path.Combine(path, "compatibilitytools.d")}");
                }
                else
                    Console.WriteLine($"Invalid path. Path does not exist or is not a Steam folder: {path}");
                
                return true;
            }
        }
        return false;
    }
}