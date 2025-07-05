**1.2.1.1**
- Refactored against 1.2.1
- launcher.ini values that are different from official XL.Core, or unique to XIVLauncher-RB, have been changed. They will now be prefixed with `RB_`. So `WineVersion` will become `RB_WineVersion`, `DxvkHudType` will become `RB_HudType`, etc.
- MangoHud is now included in the DXVK Overlay menu.

Several features removed for now (most will probably return)
- UI Scaling removed
- built-in compatibility tool installer removed
- command line options removed
- XL_NVNGX_TO_PREFIX and XL_FORCE_DLSS removed
- Dxvk/Nvapi options folded into Wine tab
- Download buttons for wine/proton/dxvk/nvapi/runtime removed
- Wine/Dxvk/Proton/Nvapi list from folder removed. Only built-in options for now. This is the next thing I'm working on.

Planned features
- Loading Wine/Dxvk/Proton/Nvapi from folders into list
- Automatic updating of wine/dxvk/proton/nvapi lists
- Tracking of files from Dxvk/Nvapi so they can be removed safely
- Actual working wayland and x11 UI scaling