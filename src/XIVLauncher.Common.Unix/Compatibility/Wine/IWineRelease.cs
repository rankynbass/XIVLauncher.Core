namespace XIVLauncher.Common.Unix.Compatibility.Wine;

public interface IWineRelease : IToolRelease
{
    string Name { get; }
    string Label { get; }
    string Description { get; }
    string DownloadUrl { get; }
    string Checksum { get; }
    string[] Checksums { get; }
    bool lsteamclient { get; }
}