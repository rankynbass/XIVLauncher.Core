** 1.4.0.10 **
- Fix: Allow use of `PROTON_LOG=1`. `PROTON_LOG_DIR` will not work; logs will be in the xlcore log folder with all the other logs. wine.log will be empty when using proton logging. Multiboxing with proton logging enabled may not work. I cannot fix this! The normal launcher detection routine relies on reading stdout, which gets redirected by proton logging. The fallback routine I used just searches for an "ffxiv_dx11.exe" process, and may not select the correct one if there are multiple.
- Add: Options within Wine tab to enable Proton logging and verbose Proton logging. Non-verbose logging will use the WINEDEBUG vars, while verbose logging will use `+timestamp,+pid,+tid,+seh,+unwind,+threadname,+debugstr,+loaddll,+mscoree`. Enabling Proton Logging is the same as adding `PROTON_LOG=1` as an environment variable.
- Cleaned up a few of the launcher.log messages, and shuffled others to verbose logging where they belonged.

