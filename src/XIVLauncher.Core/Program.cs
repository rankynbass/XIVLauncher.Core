using System.Numerics;

using CheapLoc;

using Config.Net;

using Serilog;

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
using XIVLauncher.Common.Util;
using XIVLauncher.Common.Windows;
using XIVLauncher.Common.Unix;
using XIVLauncher.Core.Accounts.Secrets;
using XIVLauncher.Core.Accounts.Secrets.Providers;
using XIVLauncher.Core.Components.LoadingPage;
using XIVLauncher.Core.Configuration;
using XIVLauncher.Core.Configuration.Parsers;
using XIVLauncher.Core.UnixCompatibility;
using XIVLauncher.Core.DesktopEnvironment;
using XIVLauncher.Core.Style;

namespace XIVLauncher.Core;

sealed class Program
{
    private static Sdl2Window window = null!;
    private static CommandList cl = null!;
    private static GraphicsDevice gd = null!;
    private static ImGuiBindings bindings = null!;

    public static GraphicsDevice GraphicsDevice => gd;
    public static ImGuiBindings ImGuiBindings => bindings;
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

    private static readonly Vector3 ClearColor = new(0.1f, 0.1f, 0.1f);

    private static LauncherApp launcherApp = null!;
    public static Storage storage = null!;
    public static DirectoryInfo DotnetRuntime => storage.GetFolder("runtime");

    // TODO: We don't have the steamworks api for this yet.
    // SteamDeck=1 on Steam Deck by default. SteamGamepadUI=1 in Big Picture / Gaming Mode.
    public static bool IsSteamDeckHardware => CoreEnvironmentSettings.IsDeck.HasValue ?
        CoreEnvironmentSettings.IsDeck.Value :
        CoreEnvironmentSettings.IsSteamDeckVar || (CoreEnvironmentSettings.IsDeckGameMode ?? false) || (CoreEnvironmentSettings.IsDeckFirstRun ?? false);
    public static bool IsSteamDeckGamingMode => CoreEnvironmentSettings.IsDeckGameMode.HasValue ?
        CoreEnvironmentSettings.IsDeckGameMode.Value :
        Steam != null && Steam.IsValid && Steam.IsRunningOnSteamDeck() && CoreEnvironmentSettings.IsSteamGamepadUIVar;

    private const string APP_NAME = "xlcore";

    private static string[] mainArgs = { };

    private static uint invalidationFrames = 0;
    private static Vector2 lastMousePosition = Vector2.Zero;


    public static string CType = CoreEnvironmentSettings.GetCType();

    public static Version CoreVersion { get; } = Version.Parse(AppUtil.GetAssemblyVersion());

    public const string CoreRelease = "RB-Patched";

    public static string CoreHash = AppUtil.GetGitHash() ?? "";

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

        Config.IsEncryptArgs ??= true;
        Config.IsFt ??= false;
        Config.IsOtpServer ??= false;
        Config.IsIgnoringSteam = CoreEnvironmentSettings.UseSteam.HasValue ? !CoreEnvironmentSettings.UseSteam.Value : Config.IsIgnoringSteam ?? false;

        Config.PatchPath ??= storage.GetFolder("patch");
        Config.PatchAcquisitionMethod ??= AcquisitionMethod.Aria;

        Config.DalamudEnabled ??= true;
        Config.DalamudLoadMethod ??= DalamudLoadMethod.EntryPoint;

        Config.GlobalScale ??= 1.0f;

        Config.GameModeEnabled ??= false;
        Config.ESyncEnabled ??= true;
        Config.FSyncEnabled ??= false;

        Config.RunnerType ??= RunnerType.Managed;
        Config.WineVersion ??= Wine.DEFAULT;
        Config.WineBinaryPath ??= "/usr/bin";
        Config.ProtonVersion ??= Proton.DEFAULT;
        Config.RuntimeVersion ??= Runtime.DEFAULT;
        Config.WineDLLOverrides ??= "";
        Config.WineDebugVars ??= "-all";

        Config.DxvkVersion ??= Dxvk.DEFAULT;
        Config.NvapiVersion ??= DLSS.DEFAULT;
        Config.DxvkAsyncEnabled ??= true;
        Config.DxvkGPLAsyncCacheEnabled ??= false;
        Config.DxvkFrameRateLimit ??= 0;
        Config.DxvkHud ??= DxvkHud.None;
        Config.DxvkHudCustom ??= Dxvk.DXVK_HUD;
        Config.MangoHud ??= MangoHud.None;
        Config.MangoHudCustomString ??= Dxvk.MANGOHUD_CONFIG;
        Config.MangoHudCustomFile ??= Dxvk.MANGOHUD_CONFIGFILE;

        Config.HelperApp1Enabled ??= false;
        Config.HelperApp1 ??= string.Empty;
        Config.HelperApp1Args ??= string.Empty;
        Config.HelperApp1WineD3D ??= false;
        Config.HelperApp2Enabled ??= false;
        Config.HelperApp2 ??= string.Empty;
        Config.HelperApp2Args ??= string.Empty;
        Config.HelperApp2WineD3D ??= false;
        Config.HelperApp3Enabled ??= false;
        Config.HelperApp3 ??= string.Empty;
        Config.HelperApp3Args ??= string.Empty;
        Config.HelperApp3WineD3D ??= false;

        Config.WineScale = (Config.WineScale is null) ? (int)(Config.GlobalScale * 100) : 25 * (Config.WineScale / 25);
        Config.WineScale = (Config.WineScale < 100) ? 100 : (Config.WineScale > 400) ? 400 : Config.WineScale;
        Config.WaylandEnabled ??= false;

        Config.FixLDP ??= false;
        Config.FixIM ??= false;
        Config.FixLocale ??= false;

        Config.SteamPath ??= Path.Combine(CoreEnvironmentSettings.HOME, ".local", "share", "Steam");
        Config.SteamFlatpakPath ??= Path.Combine(CoreEnvironmentSettings.HOME, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam" );
        Config.SteamSnapPath ??= Path.Combine(CoreEnvironmentSettings.HOME, "snap", "steam", "common", ".local", "share", "Steam");

        // Fix bad paths from previous versions.
        if (Config.SteamPath == Path.Combine(CoreEnvironmentSettings.HOME, ".local", "share"))
            Config.SteamPath = Path.Combine(CoreEnvironmentSettings.HOME, ".local", "share", "Steam");
        if (Config.SteamFlatpakPath == Path.Combine(CoreEnvironmentSettings.HOME, ".var", "app", "com.valvesoftware.Steam", ".local", "share"))
            Config.SteamFlatpakPath = Path.Combine(CoreEnvironmentSettings.HOME, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam" );
        if (Config.SteamSnapPath == Path.Combine(CoreEnvironmentSettings.HOME, "snap", "steam", "common", ".local", "share"))
            Config.SteamSnapPath = Path.Combine(CoreEnvironmentSettings.HOME, "snap", "steam", "common", ".local", "share", "Steam");
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
        FileInfo runnerOverride = null;
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

        SetupLogging(mainArgs);

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

        LoadConfig(storage);

        Runner.Initialize();

        Secrets = GetSecretProvider(storage);

        Loc.SetupWithFallbacks();

        if (System.OperatingSystem.IsLinux())
        {
            if (mainArgs.Length > 0)
                Log.Information("Command Line option selected: {args}", string.Join(' ', mainArgs));
            bool? exitValue = null;
            Task.Run(async() => 
            {
                exitValue = await LinuxCommandLineOptions().ConfigureAwait(false);
            });
            while (exitValue is null)
            { 
                // wait
            }
            if (exitValue == true)
            {
                Log.Information("Exiting...");
                return;
            }
        }

        Dictionary<uint, string> apps = new Dictionary<uint, string>();
        uint[] ignoredIds = { 0, STEAM_APP_ID, STEAM_APP_ID_FT};
        if (!ignoredIds.Contains(CoreEnvironmentSettings.SteamAppId))
        {
            apps.Add(CoreEnvironmentSettings.SteamAppId, "XLM");
        }
        if (!ignoredIds.Contains(CoreEnvironmentSettings.AltAppID))
        {
            apps.Add(CoreEnvironmentSettings.AltAppID, "XL_APPID");
        }
        if (Config.IsFt == true)
        {
            apps.Add(STEAM_APP_ID_FT, "FFXIV Free Trial");
            apps.Add(STEAM_APP_ID, "FFXIV Retail");
        }
        else
        {
            apps.Add(STEAM_APP_ID, "FFXIV Retail");
            apps.Add(STEAM_APP_ID_FT, "FFXIV Free Trial");
        }
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
                var initialized = false;
                foreach (var app in apps)
                {
                    try
                    {
                        Steam.Initialize(app.Key);
                        Log.Information($"Successfully initialized Steam entry {app.Key} - {app.Value}");
                        initialized = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Failed to initialize Steam entry {app.Key} - {app.Value}");
                    }
                }
                if (!initialized)
                    Log.Error("Failed to initialize Steam. Please attach XLM to a valid steam game or set XL_APPID to a valid steam app id in your library.");
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
        var version = CoreHash;
#else
        var version = $"{CoreVersion} ({CoreHash})";
#endif

        // Create window, GraphicsDevice, and all resources necessary for the demo.
        Sdl2Native.SDL_Init(SDLInitFlags.Video);
        ImGuiHelpers.GlobalScale = DesktopHelpers.GetDesktopScaleFactor();

        var windowWidth = (int)ImGuiHelpers.GetScaled(1280);
        var windowHeight = (int)ImGuiHelpers.GetScaled(800);

        VeldridStartup.CreateWindowAndGraphicsDevice(
            new WindowCreateInfo(50, 50, windowWidth, windowHeight, WindowState.Normal, $"XIVLauncher {version}"),
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

        bindings = new ImGuiBindings(gd, gd.MainSwapchain.Framebuffer.OutputDescription, window.Width, window.Height, storage.GetFile("launcherUI.ini"), ImGuiHelpers.GetScaled(Config.FontPxSize ?? 21.0f));
        Log.Debug("ImGui OK!");

        StyleModelV1.DalamudStandard.Apply();

        var needUpdate = false;

        if (LinuxInfo.Container == LinuxContainer.flatpak && (Config.DoVersionCheck ?? false))
        {
            var versionCheckResult = UpdateCheck.CheckForUpdate().GetAwaiter().GetResult();

            if (versionCheckResult.Success)
                needUpdate = versionCheckResult.NeedUpdate;
        }

        needUpdate = CoreEnvironmentSettings.IsUpgrade ? true : needUpdate;

        var launcherClientConfig = LauncherClientConfig.GetAsync().GetAwaiter().GetResult();
        launcherApp = new LauncherApp(storage, needUpdate, launcherClientConfig.frontierUrl, launcherClientConfig.cutOffBootver);

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
            {
                try
                {
                    overlayNeedsPresent = Steam.BOverlayNeedsPresent;
                }
                catch (NullReferenceException ex)
                {
                    Log.Error(ex, "Could not get Steam.BOverlayNeedsPresent. This probably doesn't matter.");
                }
            }

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
            cl.ClearColorTarget(0, new RgbaFloat(ClearColor.X, ClearColor.Y, ClearColor.Z, 1f));
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

        HttpClient.Dispose();

        if (Patcher is not null)
        {
            Patcher.CancelAllDownloads();
            Task.Run(async() =>
            {
                await PatchManager.UnInitializeAcquisition().ConfigureAwait(false);
                Environment.Exit(0);
            });
        }
    }

    public static void CreateCompatToolsInstance()
    {
        RunnerSettings runnerSettings;
        DxvkSettings dxvkSettings;
        DLSSSettings dlssSettings;
        if (Runner.IsProton)
        {
            runnerSettings = new RunnerSettings(Runner.FullName, Runner.DownloadUrl, Runner.RuntimeFullName, Runner.RuntimeDownloadUrl, Runner.WineDLLOverrides, Runner.DebugVars, Runner.LogFile, Runner.Prefix, Runner.ESyncEnabled, Runner.FSyncEnabled);
            dxvkSettings = new DxvkSettings(Dxvk.Enabled, storage.Root.FullName, Dxvk.FrameRateLimit, Dxvk.DxvkHudEnabled, Dxvk.DxvkHudString, Dxvk.MangoHudEnabled, Dxvk.MangoHudCustomIsFile, Dxvk.MangoHudString);
            dlssSettings = new DLSSSettings(DLSS.IsDLSSAvailable);
        }
        else
        {
            runnerSettings = new RunnerSettings(Runner.FullName, Runner.DownloadUrl, Runner.WineDLLOverrides, Runner.DebugVars, Runner.LogFile, Runner.Prefix, Runner.ESyncEnabled, Runner.FSyncEnabled);
            dxvkSettings = new DxvkSettings(Dxvk.Enabled, Dxvk.Folder, Dxvk.DownloadUrl, storage.Root.FullName, Dxvk.AsyncEnabled, Dxvk.GPLAsyncCacheEnabled, Dxvk.FrameRateLimit, Dxvk.DxvkHudEnabled, Dxvk.DxvkHudString, Dxvk.MangoHudEnabled, Dxvk.MangoHudCustomIsFile, Dxvk.MangoHudString);
            dlssSettings = new DLSSSettings(DLSS.Enabled, CoreEnvironmentSettings.ForceDLSS, DLSS.Folder, DLSS.DownloadUrl, DLSS.NvngxPath);
        }
        var gameSettings = new GameSettings(Config.GameModeEnabled, storage.GetFolder("compatibilitytool"), new DirectoryInfo(Runner.Steam), Config.GamePath, Config.GameConfigPath, LinuxInfo.Container == LinuxContainer.flatpak);
        CompatibilityTools = new CompatibilityTools(gameSettings, runnerSettings, dxvkSettings, dlssSettings);
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
        var protonprefix = storage.GetFolder("protonprefix");
        File.CreateSymbolicLink(Path.Combine(protonprefix.FullName, "pfx"), protonprefix.FullName);

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
        foreach (var winetool in Wine.Versions)
        {
            storage.GetFolder($"compatibilitytool/wine/{winetool.Key}").Delete(true);
        }
        foreach (var dxvktool in Dxvk.Versions)
        {
            storage.GetFolder($"compatibilitytool/dxvk/{dxvktool.Key}").Delete(true);
        }
        foreach (var nvapitool in DLSS.Versions)
        {
            storage.GetFolder($"compatibilitytool/dxvk/{nvapitool.Key}").Delete(true);
        }
        // Re-initialize Versions so they get *Download* marks back.
        Wine.ReInitialize();
        Dxvk.ReInitialize();
        DLSS.ReInitialize();

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

    private static async Task<bool> LinuxCommandLineOptions()
    {
        bool exit = false;

        if (mainArgs.Contains("-v"))
        {
            Console.WriteLine("Verbose Logging enabled...");
        }

        if (mainArgs.Contains("--info"))
        {
            Console.WriteLine($"This program: XIVLauncher.Core {CoreVersion.ToString()} - {CoreRelease}");
            Console.WriteLine($"Steam compatibility tool {(SteamCompatibilityTool.IsSteamToolInstalled ? "is installed." : "is not installed.")}");
            Console.WriteLine($"Steam flatpak compatibility tool {(SteamCompatibilityTool.IsSteamFlatpakToolInstalled ? "is installed." : "is not installed.")}");
            Console.WriteLine($"Steam snap compatibility tool {(SteamCompatibilityTool.IsSteamSnapToolInstalled ? "is installed." : "is not installed.")}");
            exit = true;
        }

        if (mainArgs.Contains("--deck-install") && mainArgs.Contains("--deck-remove"))
        {
            SteamCompatibilityTool.UninstallXLM(Program.Config.SteamPath);
            Console.WriteLine("Using both --deck-install and --deck-remove. Doing --deck-remove first");
            Console.WriteLine($"Removed XIVLauncher.Core as a Steam compatibility tool from {Program.Config.SteamPath ?? ""}");
            await SteamCompatibilityTool.InstallXLM(Program.Config.SteamPath).ConfigureAwait(false);
            Console.WriteLine($"Installed as Steam compatibility tool to {Program.Config.SteamPath ?? ""}");
            exit = true;
        }
        else if (mainArgs.Contains("--deck-install"))
        {
            await SteamCompatibilityTool.InstallXLM(Program.Config.SteamPath).ConfigureAwait(false);
            Console.WriteLine($"Installed as Steam compatibility tool to {Program.Config.SteamPath ?? ""}");
            exit = true;
        }
        else if (mainArgs.Contains("--deck-remove"))
        {
            SteamCompatibilityTool.UninstallXLM(Program.Config.SteamPath);
            Console.WriteLine($"Removed XIVLauncher.Core as a Steam compatibility tool from {Program.Config.SteamPath ?? ""}");
            exit = true;
        }

        if (mainArgs.Contains("--flatpak-install") && mainArgs.Contains("--flatpak-remove"))
        {
            Console.WriteLine("Using both --flatpak-install and --flatpak-remove. Doing --flatpak-remove first");
            SteamCompatibilityTool.UninstallXLM(Program.Config.SteamFlatpakPath);
            Console.WriteLine($"Removed XIVLauncher.Core as a Steam compatibility tool from {Program.Config.SteamFlatpakPath ?? ""}");
            await SteamCompatibilityTool.InstallXLM(Program.Config.SteamFlatpakPath).ConfigureAwait(false);
            Console.WriteLine($"Installed as Steam compatibility tool to {Program.Config.SteamFlatpakPath ?? ""}");
            exit = true;
        }
        else if (mainArgs.Contains("--flatpak-install"))
        {
            await SteamCompatibilityTool.InstallXLM(Program.Config.SteamFlatpakPath).ConfigureAwait(false);
            Console.WriteLine($"Installed as Steam compatibility tool to {Program.Config.SteamFlatpakPath ?? ""}");
            exit = true;
        }
        else if (mainArgs.Contains("--flatpak-remove"))
        {
            SteamCompatibilityTool.UninstallXLM(Program.Config.SteamFlatpakPath);
            Console.WriteLine($"Removed XIVLauncher.Core as a Steam compatibility tool from {Program.Config.SteamFlatpakPath ?? ""}");
            exit = true;
        }

        if (mainArgs.Contains("--snap-install") && mainArgs.Contains("--snap-remove"))
        {
            Console.WriteLine("Using both --snap-install and --snap-remove. Doing --snap-remove first");
            SteamCompatibilityTool.UninstallXLM(Program.Config.SteamSnapPath);
            Console.WriteLine($"Removed XIVLauncher.Core as a Steam compatibility tool from {Program.Config.SteamSnapPath ?? ""}");
            await SteamCompatibilityTool.InstallXLM(Program.Config.SteamSnapPath).ConfigureAwait(false);
            Console.WriteLine($"Installed as Steam compatibility tool to {Program.Config.SteamSnapPath ?? ""}");
            exit = true;
        }
        else if (mainArgs.Contains("--snap-install"))
        {
            await SteamCompatibilityTool.InstallXLM(Program.Config.SteamSnapPath).ConfigureAwait(false);
            Console.WriteLine($"Installed as Steam compatibility tool to {Program.Config.SteamSnapPath ?? ""}");
            exit = true;
        }
        else if (mainArgs.Contains("--snap-remove"))
        {
            SteamCompatibilityTool.UninstallXLM(Program.Config.SteamSnapPath);
            Console.WriteLine($"Removed XIVLauncher.Core as a Steam compatibility tool from {Program.Config.SteamSnapPath ?? ""}");
            exit = true;
        }

        if (mainArgs.Contains("--delete-old"))
        {
            Console.WriteLine($"Deleting old compatibility tools.");
            SteamCompatibilityTool.DeleteOldTools();
            exit = true;
        }

        if (mainArgs.Contains("--version"))
        {
            Console.WriteLine($"XIVLauncher.Core {CoreVersion.ToString()} - {CoreRelease}");
            Console.WriteLine("Copyright (C) 2024 goatcorp.\nLicense GPLv3+: GNU GPL version 3 or later <https://gnu.org/licenses/gpl.html>.");
            exit = true;
        }

        if (mainArgs.Contains("-V"))
        {
            Console.WriteLine(CoreVersion.ToString());
            Console.WriteLine(CoreRelease);
            exit = true;
        }

        if (mainArgs.Contains("--help") || mainArgs.Contains("-h"))
        {
            Console.WriteLine($"XIVLauncher.Core {CoreVersion.ToString()} ({CoreRelease})\nA third-party launcher for Final Fantasy XIV.\n\nOptions (use only one):");
            Console.WriteLine("  -v                 Turn on verbose logging, then run the launcher.");
            Console.WriteLine("  -h, --help         Display this message.");
            Console.WriteLine("  -V                 Display brief version info.");
            Console.WriteLine("  --version          Display version and copywrite info.");
            Console.WriteLine("  --info             Display Steam compatibility tool install status");
            Console.WriteLine("\nFor Steam Deck and native Steam");
            Console.WriteLine("  --deck-install     Install as a compatibility tool in the default location.");
            Console.WriteLine($"                     Path: {Program.Config.SteamPath ?? ""}");
            Console.WriteLine("  --deck-remove      Remove compatibility tool install from the defualt location.");
            Console.WriteLine("\nFor Flatpak Steam");
            Console.WriteLine("  --flatpak-install  Install as a compatibility tool to flatpak Steam.");
            Console.WriteLine($"                     Path: {Program.Config.SteamFlatpakPath ?? ""}");
            Console.WriteLine("  --flatpak-remove   Remove compatibility tool from flatpak Steam.");
            Console.WriteLine("  --snap-install     Install as a compatibility tool to snap Steam (Ubuntu 24.04+ default)");
            Console.WriteLine($"                     Path: {Program.Config.SteamSnapPath ?? ""}");
            Console.WriteLine("\nTo delete old versions of the Tool");
            Console.WriteLine("  --delete-old       Remove old versions of the compatibility tool (1.1.0.13 and earlier)");
            Console.WriteLine("");
            exit = true;
        }
        
        return exit;
    }
}
