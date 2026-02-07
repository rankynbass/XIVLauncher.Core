**1.3.1.2**
- Added WINEDOTNET_ROOT to dalamud initialization, in addition to DOTNET_ROOT_X64. This should allow dalamud to initialize even if proton unsets DOTNET_ROOT and DOTNET_ROOT_X64. May fix future edge proton-cachyos and ge-proton releases.
- Changed the UnixToWine() function to always use winepath instead of using getcompatpath for proton. Fixes Internal Dalamud error with cachy proton 20260203.
