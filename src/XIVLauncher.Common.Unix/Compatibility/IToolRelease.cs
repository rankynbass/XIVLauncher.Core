namespace XIVLauncher.Common.Unix.Compatibility;

public interface IToolRelease
{
    public string Folder { get; }
    
    public string DownloadUrl { get; }
    
    // True if the tarball has a folder at the top level This should be the same as the Folder.
    // If not, set to false, and create Folder to extract into.
    public bool TopLevelFolder { get; }
    
    // Name should be unique for a particular tool type. So each wine version should have a different name,
    // each dxvk version should have a different name. But a wine version and a dxvk version can share a name.
    public string Name { get; }
    
    public string Description { get; }
}