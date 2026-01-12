#!/bin/bash

echo "-- XLM Native RB-beta Auto-Installer --"
echo ""

echo "[Step: 1] Downloading XLM"
curl -L https://github.com/Blooym/xlm/releases/latest/download/xlm-x86_64-unknown-linux-gnu > /tmp/xlm

echo "[Step: 2] Configuring XLM as a Steam Tool using XIVLauncher-RB"
chmod +x /tmp/xlm
mkdir -p /tmp/xlm-rb
/tmp/xlm install-steam-tool --extra-launch-args="--xlcore-web-release-url-base=\"https://github.com/rankynbass/XIVLauncher.Core/releases/download/rb-v1.3.1.1-beta2\"" --steam-compat-path /tmp/xlm-rb
sed -i 's/XLCore/XLCore-RB-beta/' /tmp/xlm-rb/XLM/compatibilitytool.vdf
sed -i 's/"xlm"/"xlm-rb-beta"/' /tmp/xlm-rb/XLM/compatibilitytool.vdf
sed -i 's/"xlm"/"xlm-rb-beta"/' /tmp/xlm-rb/XLM/toolmanifest.vdf
mkdir -p ~/.steam/root/compatibilitytools.d/XLM-RB-beta
mv /tmp/xlm-rb/XLM/* ~/.steam/root/compatibilitytools.d/XLM-RB-beta/

echo "[Step: 3] Cleanup XLM binary"
rm /tmp/xlm

echo ""
echo "-- Auto Installer Complete: Restart Steam and follow the README to continue! --"