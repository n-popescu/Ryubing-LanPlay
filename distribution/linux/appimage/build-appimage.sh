#!/usr/bin/env sh
set -eu

ROOTDIR="$(readlink -f "$(dirname "$0")")"/../../../
cd "$ROOTDIR"

BUILDDIR=${BUILDDIR:-publish}
OUTDIR=${OUTDIR:-publish_appimage}

rm -rf AppDir
mkdir -p AppDir/usr/lib AppDir/usr/bin AppDir/usr/share/metainfo

cp -r "$BUILDDIR"/* AppDir/usr/lib/

cp distribution/linux/appimage/app.ryujinx.Ryujinx.appdata.xml AppDir/usr/share/metainfo/app.ryujinx.Ryujinx.appdata.xml
cp distribution/linux/app.ryujinx.Ryujinx.desktop AppDir/app.ryujinx.Ryujinx.desktop
cp distribution/misc/Logo.svg AppDir/app.ryujinx.Ryujinx.svg

ln -s ./app.ryujinx.Ryujinx.svg AppDir/.DirIcon  # Must be png, swap out later
ln -s ../lib/Ryujinx AppDir/usr/bin/Ryujinx
ln -s ./usr/lib/Ryujinx.sh AppDir/AppRun

# Ensure necessary bins are set as executable
chmod +x AppDir/AppRun AppDir/usr/bin/Ryujinx*

mkdir -p "$OUTDIR"

appimagetool --appimage-extract-and-run -n --comp zstd --mksquashfs-opt -Xcompression-level --mksquashfs-opt 21 \
    AppDir "$OUTDIR"/Ryujinx.AppImage