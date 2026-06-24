using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Serilog;

using XIVLauncher.Common.Unix.Compatibility.Wine.Releases;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Common.Unix.Compatibility.Wine;

public static class Runtime
{
    public static async Task DownloadRuntime(HttpClient httpClient, DirectoryInfo installDirectory, string url)
    {
        if (string.IsNullOrEmpty(url))
            throw new ArgumentOutOfRangeException("Download URL is null or empty");
        
        var tempPath = PlatformHelpers.GetTempFileName();

        File.WriteAllBytes(tempPath, await httpClient.GetByteArrayAsync(url).ConfigureAwait(false));

        PlatformHelpers.Untar(tempPath, installDirectory.FullName);

        // The umu tarball extracts to a subdirectory, so we need to move the files up one level
        foreach (var file in Directory.GetFiles(Path.Combine(installDirectory.FullName, "umu")))
        {
            File.Move(file, Path.Combine(installDirectory.FullName, Path.GetFileName(file)), true);
        }
        Directory.Delete(Path.Combine(installDirectory.FullName, "umu"), true);

        File.Delete(tempPath);
    }
}

