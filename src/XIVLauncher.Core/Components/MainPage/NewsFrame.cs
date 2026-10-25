using Eto.Drawing;
using Eto.Forms;

using XIVLauncher.Common;
using XIVLauncher.Common.Game;
using XIVLauncher.Core.Resources.Localization;

namespace XIVLauncher.Core.Components.MainPage;

/// <summary>
/// The news column: the rotating headline banner, the news list, and the topics list.
/// </summary>
/// <remarks>
/// Replaces the immediate-mode news child window. The banner rotates on a UI timer (the old
/// <see cref="System.Threading.Timer"/> could not touch widgets) and the clickable news entries become
/// Eto link buttons, which keep the "title opens the news page" behaviour.
/// </remarks>
public class NewsFrame : Component
{
    private const int BANNER_TIME = 8000;

    /// <summary>News title width inside the news column: 800 px column, 20 px gutter.</summary>
    private const int NEWS_ENTRY_WIDTH = 780;

    /// <summary>GTK cap on a wrapped label's natural width, so the label reports a small natural width and
    /// GTK allocates the column width to it. Without the cap, the label reports the unwrapped title as its
    /// natural size, and the column can never shrink it.</summary>
    private const int NEWS_ENTRY_MAX_WIDTH_CHARS = 10;

    private readonly LauncherApp app;

    private IReadOnlyList<Banner> bannerList = new List<Banner>();
    private Bitmap[] banners = [];
    private Headlines? headlines;
    private int currentBanner;
    private bool newsLoaded;

    private readonly Label loadingLabel;
    private readonly ImageView bannerImage;
    private readonly Label newsCaptionLabel;
    private readonly Label topicsCaptionLabel;
    private readonly Panel listPanel;

    /// <summary>The entries in <see cref="listPanel"/>, so their widths follow the column when it is resized.</summary>
    private readonly System.Collections.Generic.List<Control> newsEntries = [];

    private readonly UITimer bannerTimer = new();

    public NewsFrame(LauncherApp app)
    {
        this.app = app;

        this.loadingLabel = new Label { Text = Strings.NewsLoading };

        // The banner keeps the fixed box the ImGui version drew it in; the news column around it
        // stretches with the window.
        this.bannerImage = new ImageView
        {
            Size = new Size(384, 192),
            Visible = false,
        };
        this.bannerImage.MouseDown += (_, e) =>
        {
            if (e.Buttons == MouseButtons.Primary && this.newsLoaded)
                AppUtil.OpenBrowser(this.bannerList[this.currentBanner].Link.ToString());
        };

        this.newsCaptionLabel = new Label
        {
            Text = Strings.NewsCaption,
            TextColor = EtoColors.Grey3,
            Visible = false,
        };

        this.topicsCaptionLabel = new Label
        {
            Text = Strings.TopicsCaption,
            TextColor = EtoColors.Grey3,
            Visible = false,
        };

        // The news column's natural width comes from its widest child, so the list gets an explicit
        // width: labels wrap inside it instead of reporting the unwrapped title as their natural size.
        this.listPanel = new Panel { Width = NEWS_ENTRY_WIDTH };

        // The column scales with the window, so entries are re-widthed from the panel's allocation. GTK
        // wraps a label at its allocation, which only equals the column when the label's natural width is
        // capped (CapNewsEntryWidth).
        this.listPanel.SizeChanged += (_, _) => this.ApplyNewsEntryWidth();

        this.Content = new StackLayout
        {
            Spacing = 15,
            Items =
            {
                this.loadingLabel,
                this.bannerImage,
                this.newsCaptionLabel,
                this.listPanel,
                // The news column scales with the window in the page's table. Eto.Gtk packs items at the
                // top, so an empty stretch item at the end takes the remaining height and keeps the action
                // button row at the bottom of the page.
                new StackLayoutItem(new Panel(), VerticalAlignment.Stretch),
            },
        };

        this.bannerTimer.Interval = BANNER_TIME / 1000.0;
        this.bannerTimer.Elapsed += (_, _) => this.RotateBanner();

        this.ReloadNews();

        this.bannerTimer.Start();
    }

    /// <summary>Fetches worlds, banners, message and news, then shows them on the UI thread.</summary>
    public void ReloadNews()
    {
        Task.Run(async () =>
        {
            this.newsLoaded = false;

            await Headlines.GetWorlds(this.app.Launcher, this.app.Settings.ClientLanguage ?? ClientLanguage.English).ConfigureAwait(false);

            var banners = await Headlines.GetBanners(this.app.Launcher, this.app.Settings.ClientLanguage ?? ClientLanguage.English).ConfigureAwait(false);
            await Headlines.GetMessage(this.app.Launcher, this.app.Settings.ClientLanguage ?? ClientLanguage.English).ConfigureAwait(false);
            var headlines = await Headlines.GetNews(this.app.Launcher, this.app.Settings.ClientLanguage ?? ClientLanguage.English).ConfigureAwait(false);

            var bitmaps = new Bitmap[banners.Count];

            for (var i = 0; i < banners.Count; i++)
            {
                var bannerBytes = await Program.HttpClient.GetByteArrayAsync(banners[i].LsbBanner).ConfigureAwait(false);
                bitmaps[i] = Ui.Run(() => new Bitmap(bannerBytes));
            }

            this.newsLoaded = true;
            this.bannerList = banners;
            this.banners = bitmaps;

            Ui.Post(() => this.ShowNews(headlines));
        });
    }

    private void RotateBanner()
    {
        if (!this.newsLoaded || this.banners.Length == 0)
            return;

        this.currentBanner = (this.currentBanner + 1) % this.banners.Length;
        this.bannerImage.Image = this.banners[this.currentBanner];
    }

    private void ShowNews(Headlines loadedHeadlines)
    {
        this.headlines = loadedHeadlines;

        this.loadingLabel.Visible = false;

        if (this.banners.Length > 0)
        {
            this.bannerImage.Visible = true;
            this.bannerImage.Image = this.banners[this.currentBanner];
        }

        // News titles wrap inside the news column; GTK reports the unwrapped title as a label's
        // natural width, so the width is set explicitly.
        var layout = new DynamicLayout { Spacing = new Size(0, 5) };
        this.newsEntries.Clear();

        if (this.headlines.News.Length > 0)
        {
            this.newsCaptionLabel.Visible = true;
            layout.Add(this.newsCaptionLabel);
            foreach (var newsEntry in this.headlines.News)
            {
                var entry = NewsEntry(newsEntry);
                CapNewsEntryWidth(entry);
                this.newsEntries.Add(entry);
                layout.Add(entry);
            }
        }

        if (this.headlines.Topics.Length > 0)
        {
            this.topicsCaptionLabel.Visible = true;
            layout.Add(this.topicsCaptionLabel);
            foreach (var topic in this.headlines.Topics)
            {
                var entry = NewsEntry(topic);
                CapNewsEntryWidth(entry);
                this.newsEntries.Add(entry);
                layout.Add(entry);
            }
        }

        this.listPanel.Content = layout;
        this.ApplyNewsEntryWidth();
    }

    /// <summary>Widths the news entries to the column allocation, so titles wrap at the column edge.</summary>
    private void ApplyNewsEntryWidth()
    {
        var width = this.listPanel.Width;
        if (width <= 0)
            return;

        foreach (var entry in this.newsEntries)
            entry.Width = width;
    }

    /// <summary>Caps a news entry's natural width on the native Gtk label. Eto exposes no width-chars
    /// property, so the cap is applied through the Eto.Gtk handler, and a Gtk.LinkButton keeps its text in
    /// a child label.</summary>
    private static void CapNewsEntryWidth(Control entry)
    {
        var handler = ((Eto.IHandlerSource)entry).Handler;
        var controlProp = handler.GetType().GetProperty("Control", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!;
        var widget = (Gtk.Widget)controlProp.GetValue(handler)!;

        var label = widget is Gtk.LinkButton link ? (Gtk.Label)link.Children[0]! : (Gtk.Label)widget;
        label.MaxWidthChars = NEWS_ENTRY_MAX_WIDTH_CHARS;
    }

    private static Control NewsEntry(News newsEntry)
    {
        if (string.IsNullOrEmpty(newsEntry.Url))
            return new Label
            {
                Text = newsEntry.Title,
                Wrap = WrapMode.Word,
                Width = NEWS_ENTRY_WIDTH,
            };

        var link = new LinkButton
        {
            Text = newsEntry.Title,
            Wrap = WrapMode.Word,
            Width = NEWS_ENTRY_WIDTH,
        };
        link.Click += (_, _) => AppUtil.OpenBrowser(newsEntry.Url);
        return link;
    }
}
