using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace XIVLauncher.Core.DesktopEnvironment;

// Patched piece of VeldridStartup to pass SDL_WindowFlags.AllowHighDpi to SDL_CreateWindow
public static class XLVeldridStartup
{
    public static void CreateWindowAndGraphicsDevice(
        WindowCreateInfo windowCI,
        out Sdl2Window window,
        out GraphicsDevice gd)
        => CreateWindowAndGraphicsDevice(
            windowCI,
            new GraphicsDeviceOptions(),
            VeldridStartup.GetPlatformDefaultBackend(),
            out window,
            out gd);

    public static void CreateWindowAndGraphicsDevice(
        WindowCreateInfo windowCI,
        GraphicsDeviceOptions deviceOptions,
        out Sdl2Window window,
        out GraphicsDevice gd)
        => CreateWindowAndGraphicsDevice(windowCI, deviceOptions, VeldridStartup.GetPlatformDefaultBackend(), out window, out gd);

    public static void CreateWindowAndGraphicsDevice(
        WindowCreateInfo windowCI,
        GraphicsDeviceOptions deviceOptions,
        GraphicsBackend preferredBackend,
        out Sdl2Window window,
        out GraphicsDevice gd)
    {
        Sdl2Native.SDL_Init(SDLInitFlags.Video);
        if (preferredBackend == GraphicsBackend.OpenGL || preferredBackend == GraphicsBackend.OpenGLES)
        {
            VeldridStartup.SetSDLGLContextAttributes(deviceOptions, preferredBackend);
        }

        window = CreateWindow(ref windowCI);
        gd = VeldridStartup.CreateGraphicsDevice(window, deviceOptions, preferredBackend);
    }


    public static Sdl2Window CreateWindow(WindowCreateInfo windowCI) => CreateWindow(ref windowCI);

    public static Sdl2Window CreateWindow(ref WindowCreateInfo windowCI)
    {
        SDL_WindowFlags flags = SDL_WindowFlags.OpenGL | SDL_WindowFlags.Resizable | SDL_WindowFlags.AllowHighDpi
                | GetWindowFlags(windowCI.WindowInitialState);
        if (windowCI.WindowInitialState != WindowState.Hidden)
        {
            flags |= SDL_WindowFlags.Shown;
        }
        Sdl2Window window = new Sdl2Window(
            windowCI.WindowTitle,
            windowCI.X,
            windowCI.Y,
            windowCI.WindowWidth,
            windowCI.WindowHeight,
            flags,
            false);

        return window;
    }

    private static SDL_WindowFlags GetWindowFlags(WindowState state)
    {
        switch (state)
        {
            case WindowState.Normal:
                return 0;
            case WindowState.FullScreen:
                return SDL_WindowFlags.Fullscreen;
            case WindowState.Maximized:
                return SDL_WindowFlags.Maximized;
            case WindowState.Minimized:
                return SDL_WindowFlags.Minimized;
            case WindowState.BorderlessFullScreen:
                return SDL_WindowFlags.FullScreenDesktop;
            case WindowState.Hidden:
                return SDL_WindowFlags.Hidden;
            default:
                throw new VeldridException("Invalid WindowState: " + state);
        }
    }
}
