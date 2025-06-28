    namespace XIVLauncher.Common.Unix.Compatibility.Wine.Releases;
    
    public class WineCustomRelease(string name, string url, string[] checksums, bool lsc, WineReleaseDistro wineDistroId) : IWineRelease
    {
        public string Name { get; } = name;
        public string DownloadUrl { get; } = url.Replace("{wineDistroId}", wineDistroId.ToString());
        public string[] Checksums { get; } = checksums;
        public bool lsteamclient { get; } = lsc;
    }