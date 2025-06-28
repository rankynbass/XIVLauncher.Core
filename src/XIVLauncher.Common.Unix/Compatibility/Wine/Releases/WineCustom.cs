namespace XIVLauncher.Common.Unix.Compatibility.Wine.Releases;

public class WineCustomRelease(string label, string desc, string folder, string url, bool lsc, WineReleaseDistro wineDistroId, string[] checksums = null) : IWineRelease
{
    public string Label { get; } = label;
    public string Description { get; } = desc;
    public string Name { get; } = folder;
    public string DownloadUrl { get; } = url.Replace("{wineDistroId}", wineDistroId.ToString());
    public bool lsteamclient { get; } = lsc;
    public string[] Checksums { get; } = checksums ?? ["skip"];
}