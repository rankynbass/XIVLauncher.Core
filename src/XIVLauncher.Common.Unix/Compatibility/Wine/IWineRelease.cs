namespace XIVLauncher.Common.Unix.Compatibility.Wine;

public interface IWineRelease
{
    string Name { get; }
    string ParentFolder { get; }
    string Label { get; }
    string Description { get; }
    string DownloadUrl { get; }
    string[] Checksums { get; }
    bool Lsteamclient { get; }
    bool IsProton { get; }
}