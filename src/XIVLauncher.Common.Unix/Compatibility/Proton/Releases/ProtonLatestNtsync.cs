namespace XIVLauncher.Common.Unix.Compatibility.Proton.Releases;

public sealed class ProtonLatestNtsyncRelease : IToolRelease
{
    public string Label { get; } = "XIV-Proton 10-4 NTSync";
    public string Description { get; } = "GE-Proton with XIV patches. Based on 10-4";
    public string Name { get; } = "XIV-Proton10-4-ntsync";
    public string DownloadUrl { get; } = "https://github.com/rankynbass/proton-xiv/releases/download/XIV-Proton10-4/XIV-Proton10-4-ntsync.tar.xz";
    public string Checksum { get; } = "6cffaff4bc36c6fb6de75a9949e061f7ae9c45cdbbf292cad71e2ba39c8ddcd6c295d269f7d1a48c4e742b82f4947e55dc4221a3733be372122e361335690ab0";
}