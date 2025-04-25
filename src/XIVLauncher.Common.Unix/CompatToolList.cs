using System;
using System.Collections.Generic;

namespace XIVLauncher.Common.Unix.Compatibility;

public class CompatToolList
{
    public string ID { get; }

    public string Name { get; }

    public string Timestamp { get; }

    public List<CompatToolRelease> Wine { get; private set; }

    public List<CompatToolRelease> Dxvk { get; private set; }

    public List<CompatToolRelease> Nvapi { get; private set; }

    public CompatToolList(string id, string name, string timestamp)
    {
        ID = id;
        Name = name;
        Timestamp = timestamp;
        Wine = new List<CompatToolRelease>();
        Dxvk = new List<CompatToolRelease>();
        Nvapi = new List<CompatToolRelease>();
    }

    public void AddWine(CompatToolRelease tool)
    {
        Wine.Add(tool);
    }

    public void AddDxvk(CompatToolRelease tool)
    {
        Dxvk.Add(tool);
    }

    public void AddNvapi(CompatToolRelease tool)
    {
        Nvapi.Add(tool);
    }
}