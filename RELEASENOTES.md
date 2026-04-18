**1.4.0.2**
- Added handling for extra commands. Only gamescope and mangohud supported so far
- Switched MangoHud to use extra command path instead of environment variable
- MangoHud can now be used with WineD3D. Needs --dlsym option for OpenGL, though.
- Added support for gamescope. Only seems to work with Proton, but it may be an nVidia issue. It may work fine with wine and AMD GPU.
