using System;
using System.Collections.Generic;
using System.IO;

using XIVLauncher.Common.Unix.Compatibility.Wine;

namespace XIVLauncher.Common.Unix.Compatibility;

internal class RemoteWine()
{
    public string Name { get; set; }
    public string Label { get; set; }
    public string Description { get; set; }
    public string DownloadUrl { get; set; }
    public string[] Checksums { get; set; }
    public bool Lsteamclient { get; set; }
    public bool IsProton { get; set; }
}

internal class RemoteTool()
{
    public string Name { get; set; }
    public string Label { get; set;  }
    public string Description { get; set; }
    public string DownloadUrl { get; set; }
    public string Checksum { get; set; }
}

internal class WineList()
{
    public DateTime ReleaseDate { get; set; }

    public List<RemoteWine> WineVersions { get; set; }

    public List<RemoteWine> ProtonVersions { get; set; }

    public string UmuLauncherUrl { get; set; }

    public string DefaultWine { get; set; }

    public string DefaultProton { get; set; }
}

internal class DxvkList()
{
    public DateTime ReleaseDate { get; set; }

    public List<RemoteTool> DxvkVersions { get; set; }

    public string Latest { get; set; }
}

internal class NvapiList()
{
    public DateTime ReleaseDate { get; set; }

    public List<RemoteTool> NvapiVersions {get; set; }

    public string Latest { get; set; }
}