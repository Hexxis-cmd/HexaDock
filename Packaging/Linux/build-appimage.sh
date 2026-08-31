#!/bin/sh
set -eu

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)"
OUTPUT="${1:-$ROOT/Release/HexaDock-1.0.0-linux-x86_64.AppImage}"
APPDIR="$(mktemp -d)"
trap 'rm -rf "$APPDIR"' EXIT

dotnet publish "$ROOT/HexaDock.Linux/HexaDock.Linux.csproj" -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o "$ROOT/publish/linux-x64"
mkdir -p "$APPDIR/usr/bin" "$APPDIR/usr/share/icons/hicolor/256x256/apps" "$APPDIR/usr/share/metainfo" "$(dirname "$OUTPUT")"
cp "$ROOT/publish/linux-x64/HexaDock" "$APPDIR/usr/bin/HexaDock"
cp "$ROOT/Packaging/Linux/AppRun" "$APPDIR/AppRun"
cp "$ROOT/Packaging/Linux/hexadock.desktop" "$APPDIR/hexadock.desktop"
cp "$ROOT/Packaging/Linux/hexadock.appdata.xml" "$APPDIR/usr/share/metainfo/hexadock.appdata.xml"
cp "$ROOT/Assets/UserLogo.png" "$APPDIR/hexadock.png"
cp "$ROOT/Assets/UserLogo.png" "$APPDIR/usr/share/icons/hicolor/256x256/apps/hexadock.png"
chmod +x "$APPDIR/AppRun" "$APPDIR/usr/bin/HexaDock"

APPIMAGETOOL="${APPIMAGETOOL:-appimagetool}"
ARCH=x86_64 "$APPIMAGETOOL" "$APPDIR" "$OUTPUT"
