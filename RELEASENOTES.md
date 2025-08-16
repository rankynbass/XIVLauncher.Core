**1.2.1.1**
Changes and Added Features
- Refactored against 1.2.1
- launcher.ini values that are different from official XL.Core, or unique to XIVLauncher-RB, have been changed. They will now be prefixed with `RB_`. So `WineVersion` will become `RB_WineVersion`, `DxvkHudType` will become `RB_HudType`, etc.
- Proton Dxvk/Nvapi options have their own settings.
- Wine 7/10 toggle buttons removed, back to the Win7 checkbox. Updated to track windows version, so after the first run, it won't update unless you change it or delete the prefix. Speeds up launching slightly after first launch.
- WineD3D can now be enabled with the experimental vulkan renderer. Tracked, so the registry won't be updated on every launch unless you change it or delete the prefix.
- Extra WINEDLLOVERRIDES moved to Game tab below Additional Arguments.
- NTSync checkbox. It only works for very new Proton and ValveBE wine. Other wine/proton releases need to have ntsync baked in; the checkbox does nothing for those versions.
- Custom Wine can now be pointed at a proton release as well. Proton or wine will be detected correctly.
- "Auto-start" tab renamed to "Apps"

Removed Features
- UI Scaling removed. It was mostly broken as of SDL3.
- Built-in compatibility tool installer removed (Use XLM instead)
- Command line options removed
- XL_NVNGX_TO_PREFIX and XL_FORCE_DLSS removed

Planned features
- Tracking of files from Dxvk/Nvapi so they can be removed safely
- Actual working wayland and x11 UI scaling
