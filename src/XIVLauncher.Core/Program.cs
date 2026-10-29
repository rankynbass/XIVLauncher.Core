using System.Net;
using System.Runtime.InteropServices;

using Config.Net;

using Eto.Forms;

using Serilog;

using XIVLauncher.Common;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Support;
using XIVLauncher.Common.Game;
using XIVLauncher.Common.Game.Patch;
using XIVLauncher.Common.Http.HappyEyeballs;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Unix;
using XIVLauncher.Common.Unix.Compatibility;
using XIVLauncher.Common.Unix.Compatibility.Dxvk;
using XIVLauncher.Common.Unix.Compatibility.Wine;
using XIVLauncher.Common.Util;
using XIVLauncher.Common.Windows;
using XIVLauncher.Core.Accounts.Secrets;
using XIVLauncher.Core.Accounts.Secrets.Providers;
using XIVLauncher.Core.Components.LoadingPage;
using XIVLauncher.Core.Configuration;
using XIVLauncher.Core.Configuration.Parsers;
using XIVLauncher.Core.Net;

namespace XIVLauncher.Core;


sealed class Program
{
    private const string APP_NAME = "xlcore";
    private static string[] mainArgs = [];
    private static LauncherApp launcherApp = null!;
    private static LauncherForm launcherForm = null!;
    public static ILauncherConfig Config { get; private set; } = null!;
    public static ISteam? Steam { get; private set; }
    public static DalamudUpdater DalamudUpdater { get; private set; } = null!;
    public static DalamudOverlayInfoProxy DalamudLoadInfo { get; private set; } = null!;
    public static CompatibilityTools CompatibilityTools { get; private set; } = null!;
    public static ISecretProvider Secrets { get; private set; } = null!;
    public static HttpClient HttpClient { get; private set; } = new HttpClient(new SocketsHttpHandler
    {
        ConnectCallback = new SocketsHttpHandler
        {
            UseCookies = false,
            ConnectCallback = new HappyEyeballsCallback().ConnectCallback,
        }.ConnectCallback,
        AutomaticDecompression = DecompressionMethods.All,
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 4,
    });

    public static PatchManager? Patcher { get; set; } = null;
    public static Storage storage = null!;
    public static DirectoryInfo DotnetRuntime => storage.GetFolder("runtime");
    public static string CType = CoreEnvironmentSettings.GetCType();

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
            var compatGamePath = Environment.GetEnvironmentVariable("STEAM_COMPAT_INSTALL_PATH"); // auto-set when using compat tool
            var compatGameId = Environment.GetEnvironmentVariable("STEAM_COMPAT_APP_ID"); // auto-set when using compat tool
            // Set the game path to the game's installation directory if its either the full game or free trial.
            Config.GamePath = !string.IsNullOrWhiteSpace(compatGamePath) &&
                              int.TryParse(compatGameId, out int compatGameIdParsed) &&
                              (compatGameIdParsed == STEAM_APP_ID || compatGameIdParsed == STEAM_APP_ID_FT)
                ? new DirectoryInfo(compatGamePath)
                : storage.GetFolder("ffxiv");
        }

        Config.GameConfigPath ??= storage.GetFolder("ffxivConfig");
        Config.ClientLanguage ??= ClientLanguage.English;
        Config.DpiAwareness ??= DpiAwareness.Unaware;
        Config.IsAutologin ??= false;
        Config.CompletedFts ??= false;
        Config.FontPxSize ??= 22.0f;

        Config.IsEncryptArgs ??= true;
        Config.IsOtpServer ??= false;
        Config.IsIgnoringSteam = CoreEnvironmentSettings.UseSteam.HasValue ? !CoreEnvironmentSettings.UseSteam.Value : Config.IsIgnoringSteam ?? false;

        Config.PatchPath ??= storage.GetFolder("patch");

        // Default the manual injection path so the settings FilePicker row is never empty: Eto's GTK
        // FilePicker writes the chooser's NULL filename into its internal entry on every
        // selection-changed, firing a gtk_entry_set_text assertion; a non-empty path keeps it quiet.
        Config.DalamudManualInjectPath ??= new DirectoryInfo(Path.Combine(storage.Root.FullName, "dalamud", "Hooks", "dev"));

        Config.DalamudEnabled ??= true;
        Config.DalamudLoadMethod ??= DalamudLoadMethod.EntryPoint;

        Config.GlobalScale ??= 1.0f;

        Config.GameModeEnabled ??= false;
        Config.DxvkVersion ??= DxvkVersion.Stable;
        Config.DxvkAsyncEnabled ??= true;

        Config.WineStartupType ??= WineStartupType.Managed;
        Config.WineManagedVersion ??= WineManagedVersion.Stable;
        Config.WineSyncType ??= RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? WineUtility.SystemFsyncSupport() == FsyncSupport.Supported ? WineSyncType.FSync : WineSyncType.ESync
            : WineSyncType.FSync;
        Config.WineBinaryPath ??= "/usr/bin";
        Config.WineDebugVars ??= "-all";

        Config.FixLDP ??= false;
        Config.FixIM ??= false;
        Config.FixLocale ??= false;
        Config.FixError127 ??= false;
        Config.DontUseSystemTz ??= false;
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
        return new DalamudUpdater(HttpClient, storage.GetFolder("dalamud"), storage.GetFolder("runtime"), storage.GetFolder("dalamudAssets"), null, null)
        {
            Overlay = DalamudLoadInfo,
            RunnerOverride = runnerOverride
        };
    }

    [STAThread]
    private static void Main(string[] args)
    {
        mainArgs = args;
        storage = new Storage(APP_NAME);

        if (CoreEnvironmentSettings.ClearAll)
        {
            ClearAll();
        }
        else
        {
            if (CoreEnvironmentSettings.ClearSettings) ClearSettings();
            if (CoreEnvironmentSettings.ClearPrefix) ClearPrefix();
            if (CoreEnvironmentSettings.ClearPlugins) ClearPlugins();
            if (CoreEnvironmentSettings.ClearTools) ClearTools();
            if (CoreEnvironmentSettings.ClearLogs) ClearLogs();
        }

        SetupLogging(mainArgs);
        LoadConfig(storage);

#if DEBUG
        // Eto warns about widget access from a background thread with Debug.WriteLine; hook it so the
        // offending managed stack is logged, which is the only way to identify the call site.
        Ui.CaptureUIThreadWarnings();
#endif

        Secrets = GetSecretProvider(storage);

        Dictionary<uint, string> apps = [];
        if (CoreEnvironmentSettings.SteamAppId != 0)
        {
            apps.Add(CoreEnvironmentSettings.SteamAppId, "XLM");
        }
        if (CoreEnvironmentSettings.AltAppID != 0)
        {
            apps.Add(CoreEnvironmentSettings.AltAppID, "XL_APPID");
        }
        if (!apps.ContainsKey(STEAM_APP_ID))
        {
            apps.Add(STEAM_APP_ID, "FFXIV Retail");
        }
        if (!apps.ContainsKey(STEAM_APP_ID_FT))
        {
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
        DalamudUpdater.Run(Config.DalamudBetaKind, Config.DalamudBetaKey);

        CreateCompatToolsInstance();

        Log.Debug("Creating the Eto application...");

        var version = AppUtil.GetAssemblyVersion();
        var hash = AppUtil.GetGitHash();

        // The launcher forces a dark theme: the previous build shipped a dark ImGui style, and the
        // launcher artwork is dark. Eto 2.13 crashes on GTK3 when asked for Themes.Dark on a system
        // theme with no dark variant, so the theme is chosen through GTK_THEME instead.
        EtoTheme.PreferDarkGtkTheme();

        var application = new Application
        {
            Name = APP_NAME,
        };

        application.Initialized += (_, _) =>
        {
            AppFonts.Initialize(Config.FontPxSize ?? 21.0f);

            var launcherClientConfig = LauncherClientConfig.GetAsync().GetAwaiter().GetResult();
            launcherApp = new LauncherApp(storage, launcherClientConfig.frontierUrl, launcherClientConfig.cutOffBootver);
            Log.Debug("LauncherApp created");

            launcherForm = new LauncherForm(launcherApp, version, hash);
            launcherApp.AttachForm(launcherForm);
            Log.Debug("LauncherForm created");

            application.MainForm = launcherForm;
            launcherForm.Show();
            Log.Debug("Main form shown");

#if DEBUG
            // Debug-only selftest (XLCORE_SELFTEST=1): drives the patching and repair status loop from a
            // worker thread, so the LoadingPage marshalling is exercised without a real patch queue.
            if (Environment.GetEnvironmentVariable("XLCORE_SELFTEST") == "1")
            {
                Task.Run(() =>
                {
                    launcherApp.StartLoading("Working on patch 0", "left to download", "left to install", false, true, true);

                    for (var i = 0; i < 200; i++)
                    {
                        launcherApp.LoadingPage.Line2 = $"Working on patch {i}";
                        launcherApp.LoadingPage.Line3 = $"left to download: {i} patch(es), {(long)i * 33000000} MiB, 200 MiB in 0s\n1.0 MiB/s";
                        launcherApp.LoadingPage.Progress = i / 200f;
                        Thread.Sleep(30);
                    }

                    launcherApp.StartLoading("Now repairing files", "current file", "0/10", false, false, false);

                    // Hide/show: MainPage hides the launcher window when the game starts and restores it
                    // on Reactivate(), so the GTK loop must stay alive with no mapped window.
                    Ui.Run(() => Program.HideWindow());
                    Ui.Run(() => Console.WriteLine($"[selftest] after hide: form.Visible={launcherForm.Visible}"));

                    for (var i = 0; i < 20; i++)
                        Ui.Post(() => launcherApp.LoadingPage.Line2 = $"hidden heartbeat {i}");

                    Thread.Sleep(2000);
                    Ui.Run(() => Console.WriteLine($"[selftest] last UI post while hidden: {launcherApp.LoadingPage.Line2}"));

                    Ui.Run(() => Program.ShowWindow());
                    Ui.Run(() => Console.WriteLine($"[selftest] after restore: form.Visible={launcherForm.Visible}"));

                    Log.Information("selftest complete: patching and repair status driven from a worker thread");
                    Environment.Exit(0);
                });
            }
#endif
        };

        // The Eto UI thread is the thread that calls Application.Run. The login, patching, and startup
        // threads marshal every widget write through Ui, so record the owner before the loop starts;
        // Ui.IsUIThread then detects unmarshalled widget writes in Debug builds.
        Ui.MarkUIThread();

        Log.Debug("Eto OK!");

        application.Run();

        if (Patcher is not null)
        {
            Patcher.StartCancellation();
            // PatchManager.UnInitializeAcquisition() is private but the function bellow is the only call that is in the method and is public accessible
            try
            {
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not uninitialize patch acquisition.");
            }
        }
    }

    public static void CreateCompatToolsInstance()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        var wineLogFile = new FileInfo(Path.Combine(storage.GetFolder("logs").FullName, "wine.log"));
        var winePrefix = storage.GetFolder("wineprefix");
        var wineSettings = new WineSettings(Config.WineStartupType ?? WineStartupType.Custom, Config.WineManagedVersion ?? WineManagedVersion.Stable, Config.WineBinaryPath, Config.WineDebugVars, wineLogFile, winePrefix, Config.WineSyncType ?? WineSyncType.FSync);
        var toolsFolder = storage.GetFolder("compatibilitytool");
        Directory.CreateDirectory(Path.Combine(toolsFolder.FullName, "dxvk"));
        Directory.CreateDirectory(Path.Combine(toolsFolder.FullName, "wine"));
        CompatibilityTools = new CompatibilityTools(wineSettings, Config.DxvkVersion ?? DxvkVersion.Stable, Config.DxvkHudType, Config.GameModeEnabled ?? false, Config.DxvkAsyncEnabled ?? true, toolsFolder);
    }

    /// <summary>Shows the launcher window, marshalled to the UI thread.</summary>
    public static void ShowWindow() => Ui.Post(() => launcherForm.ShowWindow());

    /// <summary>Hides the launcher window, marshalled to the UI thread.</summary>
    public static void HideWindow() => Ui.Post(() => launcherForm.HideWindow());

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
                    Log.Information("Loading keyring secret provider...");
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
    }

    public static void ClearPlugins(bool tsbutton = false)
    {
        storage.GetFolder("dalamud").Delete(true);
        storage.GetFolder("dalamudAssets").Delete(true);
        storage.GetFolder("installedPlugins").Delete(true);
        storage.GetFolder("runtime").Delete(true);
        if (storage.GetFile("dalamudUI.ini").Exists) storage.GetFile("dalamudUI.ini").Delete();
        if (storage.GetFile("dalamudConfig.json").Exists) storage.GetFile("dalamudConfig.json").Delete();
        storage.GetFolder("dalamud");
        storage.GetFolder("dalamudAssets");
        storage.GetFolder("installedPlugins");
        storage.GetFolder("runtime");
        if (tsbutton)
        {
            DalamudLoadInfo = new DalamudOverlayInfoProxy();
            DalamudUpdater = CreateDalamudUpdater();
            DalamudUpdater.Run(Config.DalamudBetaKind, Config.DalamudBetaKey);
        }
    }

    public static void ClearTools(bool tsbutton = false)
    {
        storage.GetFolder("compatibilitytool").Delete(true);
        storage.GetFolder("compatibilitytool/wine");
        storage.GetFolder("compatibilitytool/dxvk");
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
        ClearPlugins(tsbutton);
        ClearTools(tsbutton);
        ClearLogs(true);
    }

    public static void ResetUIDCache(bool tsbutton = false) => launcherApp.UniqueIdCache.Reset();
}
