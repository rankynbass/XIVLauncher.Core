using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Globalization;
using Serilog;

namespace XIVLauncher.Core.UnixCompatibility;

static class Locale
{
    public static List<string> Codes { get; private set; }

    static Locale()
    {
        Codes = new List<string>();
        if (System.OperatingSystem.IsWindows())
        {
            Codes.Add("Disabled");
            return;
        }
        var psi = new ProcessStartInfo("sh");
        psi.Arguments = "-c \"locale -a 2>/dev/null | grep -i utf\"";
        psi.RedirectStandardOutput = true;

        var proc = new Process();
        proc.StartInfo = psi;
        proc.Start();
        var output = proc.StandardOutput.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var code in output)
        {
            Codes.Add(code);
        }
        Codes.Add("Disabled");
    }

    public static bool IsValid(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return Codes.Contains(name);
    }

    public static string GetDefaultCode()
    {
        return "Disabled";
    }
}