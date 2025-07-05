#!/bin/bash

echo "-- XLM Native Auto-Installer --"
echo ""

echo "[Step: 1] Create necessary paths"
echo "Making ~/.local/share/xivlauncher-rb"
mkdir -p ~/.local/share/xivlauncher-rb/xlcore

echo "[Step: 2] Downloading XLM"
curl -L https://github.com/Blooym/xlm/releases/latest/download/xlm-x86_64-unknown-linux-gnu > ~/.local/share/xivlauncher-rb/xlm

echo "[Step: 3] Configuring XLM as a local launcher using XIVLauncher-RB"
chmod +x ~/.local/share/xivlauncher-rb/xlm
~/.local/share/xivlauncher-rb launch --xlcore-repo-owner rankynbass --install-directory ~/.local/share/xivlauncher-rb/xlcore

echo "[Step: 4] Create launcher script"
cat > ~/.local/share/xivlauncher-rb/start.sh << EOL
#!/bin/bash
$HOME/.local/share/xivlauncher-rb/xlm launch --xlcore-repo-owner rankynbass --install-directory ~/.local/share/xivlauncher-rb/xlcore
EOL
chmod +x ~/.local/share/xivlauncher-rb/start.sh

echo "[Step: 5] Download launcher icon"
mkdir -p ~/.local/share/icons
curl -L https://raw.githubusercontent.com/rankynbass/XIVLauncher.Core/a04f704f378a87f8cde33a44a5525a46ba19d2b3/misc/linux_distrib/512.png > ~/.local/share/icons/xivlauncher-rb.png

echo "[Step: 6] Create Desktop entry"
mkdir -p ~/.local/share/applications
cat > ~/.local/share/applications/xivlauncher-rb.desktop << EOL
[Desktop Entry]
Name=XIVLauncher-RB (Local)
Comment=Custom launcher for the most critically acclaimed MMO
Exec=$HOME/.local/share/xivlauncher-rb/start.sh
Icon=xivlauncher-rb
Terminal=false
Type=Application
Categories=Game;
StartupWMClass=XIVLauncher.Core
EOL

xdg-desktop-menu forceupdate

echo ""
echo "-- Auto Installer Complete. XIVLauncher-RB should be in your application menu now."