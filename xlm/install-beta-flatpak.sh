#!/bin/bash

echo "-- XLM Flatpak RB-Beta Auto-Installer --"
echo ""

echo "[Step: 1] Downloading XLM"
curl -L https://github.com/Blooym/xlm/releases/latest/download/xlm-x86_64-unknown-linux-gnu > /tmp/xlm

echo "[Step: 2] Configuring XLM as a Steam Tool using XIVLauncher-RB"
chmod +x /tmp/xlm
mkdir -p /tmp/xlm-eto
/tmp/xlm install-steam-tool --extra-launch-args="--xlcore-web-release-url-base=\"https://github.com/rankynbass/XLCoreTestReleases/releases/download/eto-2.0.0-beta1/\"" --steam-compat-path /tmp/xlm-eto
sed -i 's/XLCore/XLCore-Eto-beta/' /tmp/xlm-eto/XLM/compatibilitytool.vdf
sed -i 's/"xlm"/"xlm-eto-beta"/' /tmp/xlm-eto/XLM/compatibilitytool.vdf
sed -i 's/"xlm"/"xlm-eto-beta"/' /tmp/xlm-eto/XLM/toolmanifest.vdf
mkdir -p ~/.var/app/com.valvesoftware.Steam/.steam/root/compatibilitytools.d/XLM-Eto-beta
mv /tmp/xlm-eto/XLM/* ~/.var/app/com.valvesoftware.Steam/.steam/root/compatibilitytools.d/XLM-Eto-beta/

echo "[Step: 3] Cleanup XLM binary"
rm /tmp/xlm

echo ""
echo "-- Auto Installer Complete: Restart Steam and follow the README to continue! --"