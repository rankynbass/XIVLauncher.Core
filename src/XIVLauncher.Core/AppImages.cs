using Eto.Drawing;

namespace XIVLauncher.Core;

/// <summary>
/// Loads Eto images from the launcher's embedded resources, caching them.
/// </summary>
/// <remarks>
/// Replaces the SDL GPU texture wrapper. Images are decoded by the platform (GDK/Pixbuf on GTK);
/// each resource is decoded once per process.
/// </remarks>
public static class AppImages
{
    private static readonly Dictionary<string, Bitmap> cache = [];

    /// <summary>Gets the embedded resource as an image, decoding it on first use.</summary>
    public static Bitmap FromResource(string resourceName)
    {
        if (!cache.TryGetValue(resourceName, out var image))
            cache[resourceName] = image = Bitmap.FromResource(resourceName, typeof(AppUtil).Assembly);

        return image;
    }

    /// <summary>Decodes downloaded image bytes (e.g. news banners) into an image.</summary>
    public static Bitmap FromBytes(byte[] bytes) => new(bytes);
}
