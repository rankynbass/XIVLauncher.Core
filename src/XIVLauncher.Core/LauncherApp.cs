using System.Diagnostics;

using Serilog;

using XIVLauncher.Common;
using XIVLauncher.Common.Game;
using XIVLauncher.Common.PlatformAbstractions;
using XIVLauncher.Common.Util;
using XIVLauncher.Core.Accounts;
using XIVLauncher.Core.Components;
using XIVLauncher.Core.Components.LoadingPage;
using XIVLauncher.Core.Components.MainPage;
using XIVLauncher.Core.Components.SettingsPage;
using XIVLauncher.Core.Configuration;
using XIVLauncher.Core.Resources.Localization;

using XIVLauncher.PlatformAbstractions;

namespace XIVLauncher.Core;

/// <summary>
/// The launcher shell: the state machine that swaps pages, the modal dialog helpers, and the shared
/// launcher services (launcher, accounts, storage, news/OTP/FTS pages).
/// </summary>
/// <remarks>
/// Replaces the immediate-mode shell. Pages are Eto controls hosted by
/// <see cref="LauncherForm.ShowPage"/>; every state change is posted to the UI thread because patching,
/// login, and startup tasks mutate state from background threads.
/// <para>
/// The blocking modal semantics are preserved: <see cref="ShowMessage"/> posts an Eto
/// <see cref="Eto.Forms.Dialog"/>, whose <c>ShowModal</c> pumps the UI loop, and
/// <see cref="ShowMessageBlocking"/> waits on the same <see cref="ManualResetEvent"/> the modal button
/// sets.
/// </para>
/// </remarks>
public class LauncherApp
{
    public static bool IsDebug { get; private set; } = Debugger.IsAttached;

    #region Modal State

    private int openModals;
    private readonly ManualResetEvent modalWaitHandle = new(false);

    #endregion

    public enum LauncherState
    {
        Main,
        Settings,
        Loading,
        OtpEntry,
        Fts,
        SteamDeckPrompt,
    }

    private LauncherState state = LauncherState.Main;

    public LauncherState State
    {
        get => this.state;
        set => Ui.Post(() => this.SetState(value));
    }

    private void SetState(LauncherState value)
    {
        // If we are coming from the settings, we should reload the news, as the client language might have changed
        if (this.state == LauncherState.Settings)
            this.mainPage.ReloadNews();

        this.state = value;

        switch (value)
        {
            case LauncherState.Settings:
                this.setPage.OnShow();
                break;

            case LauncherState.Main:
                this.mainPage.OnShow();
                break;

            case LauncherState.Loading:
                this.LoadingPage.OnShow();
                break;

            case LauncherState.OtpEntry:
                this.otpEntryPage.OnShow();
                break;

            case LauncherState.Fts:
                this.ftsPage.OnShow();
                break;

            case LauncherState.SteamDeckPrompt:
                this.steamDeckPromptPage.OnShow();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(value), value, null);
        }

        this.PageShown?.Invoke(this.CurrentPage);
    }

    /// <summary>Raises when a page must be hosted, marshalled to the UI thread.</summary>
    public event Action<Page>? PageShown;

    public Page CurrentPage =>
        this.state switch
        {
            LauncherState.Main => this.mainPage,
            LauncherState.Settings => this.setPage,
            LauncherState.Loading => this.LoadingPage,
            LauncherState.OtpEntry => this.otpEntryPage,
            LauncherState.Fts => this.ftsPage,
            LauncherState.SteamDeckPrompt => this.steamDeckPromptPage,
            _ => throw new ArgumentOutOfRangeException(nameof(this.State), this.State, "CurrentPage: Unknown LauncherState"),
        };

    public ILauncherConfig Settings => Program.Config;
    public Launcher Launcher { get; private set; }
    public ISteam? Steam => Program.Steam;
    public Storage Storage { get; private set; }
    public LoadingPage LoadingPage { get; }
    public AccountManager Accounts;
    public CommonUniqueIdCache UniqueIdCache;

    private readonly MainPage mainPage;
    private readonly SettingsPage setPage;

    private readonly OtpEntryPage otpEntryPage;
    private readonly FtsPage ftsPage;
    private readonly SteamDeckPromptPage steamDeckPromptPage;
    private LauncherForm? form;

    public LauncherApp(Storage storage, string frontierUrl, string? cutOffBootver)
    {
        this.Storage = storage;

        this.Accounts = new AccountManager(this.Storage.GetFile("accounts.json"));
        this.UniqueIdCache = new CommonUniqueIdCache(this.Storage.GetFile("uidCache.json"));

        this.Launcher = new Launcher(Program.Steam, UniqueIdCache, frontierUrl, Program.Config.AcceptLanguage ?? ApiHelpers.GenerateAcceptLanguage());

        this.mainPage = new MainPage(this);
        this.setPage = new SettingsPage(this);
        this.otpEntryPage = new OtpEntryPage(this);
        this.LoadingPage = new LoadingPage(this);
        this.ftsPage = new FtsPage(this);
        this.steamDeckPromptPage = new SteamDeckPromptPage(this);

        if (!EnvironmentSettings.IsNoKillswitch && !string.IsNullOrEmpty(cutOffBootver))
        {
            var bootver = SeVersion.Parse(Repository.Boot.GetVer(Program.Config.GamePath));
            var cutoff = SeVersion.Parse(cutOffBootver);

            if (bootver > cutoff)
            {
                this.ShowMessage("XIVLauncher is unavailable at this time as there were changes to the login process during a recent patch." +
                                "\n\nWe need to adjust to these changes and verify that our adjustments are safe before we can re-enable the launcher." +
                                "\n\nYou can use the Official Launcher instead until XIVLauncher has been updated.",
                                "XIVLauncher", "Close Launcher", () => Environment.Exit(0));
                return;
            }
        }

        this.RunStartupTasks();

#if DEBUG
        IsDebug = true;
#endif
    }

    public void AttachForm(LauncherForm form)
    {
        this.form = form;

        this.PageShown += form.ShowPage;

        Ui.Post(() => this.SetState(LauncherState.Main));
    }

    public void ShowMessage(string text, string title = "XIVLauncher", string modalButtonText = "OK", Action? modalPressedAction = default)
    {
        if (this.openModals > 0)
            throw new InvalidOperationException("Cannot open modal while another modal is open");

        this.modalWaitHandle.Reset();

        var pressedAction = modalPressedAction ?? new Action(() => { });

        Ui.Post(() =>
        {
            this.openModals++;

            var dialog = new ModalDialog(text, title, modalButtonText, () =>
            {
                this.modalWaitHandle.Set();
                pressedAction();
            });

            if (this.form is not null)
                dialog.ShowModal(this.form);
            else
                dialog.ShowModal();

            this.openModals--;
        });
    }

    public void ShowMessageBlocking(string text, string title = "XIVLauncher", bool canContinue = true)
    {
        if (!this.modalWaitHandle.WaitOne(0) && this.openModals > 0)
            throw new InvalidOperationException("Cannot open modal while another modal is open");

        this.modalWaitHandle.Reset();
        this.ShowMessage(text, title);

        this.modalWaitHandle.WaitOne();
    }

    public void ShowExceptionBlocking(Exception ex, string context)
    {
        this.ShowMessageBlocking(context + " failed with exception\nPlease report this error to us.\n\n" + ex.Message, "XIVLauncher has encountered an error.");
    }

    public bool HandleContinuationBlocking(Task t)
    {
        if (t.IsFaulted)
        {
            Log.Error(t.Exception, "Task failed");
            this.ShowMessageBlocking(t.Exception?.InnerException?.Message ?? "Unknown error - please check logs.", "Error");
            return false;
        }

        if (t.IsCanceled)
        {
            this.ShowMessageBlocking("Task was canceled.", "Error");
            return false;
        }

        return true;
    }

    public void AskForOtp()
    {
        this.otpEntryPage.Reset();
        this.State = LauncherState.OtpEntry;
    }

    public async Task<string?> WaitForOtpAsync()
    {
        var result = await this.otpEntryPage.GetResultAsync().ConfigureAwait(false);
        this.otpEntryPage.StopOtpListener();
        return result;
    }

    public void StartLoading(string line1, string line2 = "", string line3 = "", bool isIndeterminate = true, bool canCancel = false, bool canDisableAutoLogin = false)
    {
        this.State = LauncherState.Loading;
        this.LoadingPage.Line1 = line1;
        this.LoadingPage.Line2 = line2;
        this.LoadingPage.Line3 = line3;
        this.LoadingPage.IsIndeterminate = isIndeterminate;
        this.LoadingPage.CanCancel = canCancel;
        this.LoadingPage.CanDisableAutoLogin = canDisableAutoLogin;

        this.LoadingPage.Reset();
    }

    public void StopLoading()
    {
        this.State = LauncherState.Main;
    }

    /// <summary>
    /// Temporary login smoke test: runs the login pipeline without launching the game, so the login,
    /// boot check, patch check, loading page, and modal can be exercised from the command line.
    /// </summary>

    public void RunStartupTasks()
    {
#if FLATPAK
        this.ftsPage.OpenFtsIfNeeded();
#endif
        this.mainPage.DoAutoLoginIfApplicable();
    }
}
