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

        this.Content = new StackLayout
        {
            Spacing = 15,
            Items = { this.loadingLabel, this.bannerImage, this.newsCaptionLabel, this.listPanel, this.topicsCaptionLabel },
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

        if (this.headlines.News.Length > 0)
        {
            this.newsCaptionLabel.Visible = true;
            layout.Add(this.newsCaptionLabel);
            foreach (var newsEntry in this.headlines.News)
                layout.Add(NewsEntry(newsEntry));
        }

        if (this.headlines.Topics.Length > 0)
        {
            this.topicsCaptionLabel.Visible = true;
            layout.Add(this.topicsCaptionLabel);
            foreach (var topic in this.headlines.Topics)
                layout.Add(NewsEntry(topic));
        }

        this.listPanel.Content = layout;
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
