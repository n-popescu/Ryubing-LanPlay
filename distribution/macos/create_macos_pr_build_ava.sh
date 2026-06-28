#!/bin/bash

set -e

if [ "$#" -lt 8 ]; then
    echo "usage <BASE_DIRECTORY> <TEMP_DIRECTORY> <OUTPUT_DIRECTORY> <ENTITLEMENTS_FILE_PATH> <VERSION> <SOURCE_REVISION_ID> <CONFIGURATION>"
    exit 1
fi

mkdir -p "$1"
mkdir -p "$2"
mkdir -p "$3"

BASE_DIRECTORY=$(readlink -f "$1")
TEMP_DIRECTORY=$(readlink -f "$2")
OUTPUT_DIRECTORY=$(readlink -f "$3")
ENTITLEMENTS_FILE_PATH=$(readlink -f "$4")
VERSION=$5
SOURCE_REVISION_ID=$6
CONFIGURATION=$7

echo "Clearing xattr on all dot underscore files"
if [[ "$(uname)" == "Darwin" ]]; then
    find "$BASE_DIRECTORY" -type f -name "._*" -exec sh -c '
    for f; do
        dir=$(dirname "$f")
        base=$(basename "$f")
        orig="$dir/${base#._}"
        [ -f "$orig" ] && xattr -c "$orig" || true
    done
    ' sh {} +
else
    find "$BASE_DIRECTORY" -type f -name "._*" -exec sh -c '
    for f; do
        dir=$(dirname "$f")
        base=$(basename "$f")
        orig="$dir/${base#._}"
        [ -f "$orig" ] && setfattr -x "$orig" || true
    done
    ' sh {} +
fi

RELEASE_FILE_NAME="ryujinx-$CONFIGURATION-$VERSION+$SOURCE_REVISION_ID-macos_universal"
RELEASE_APP_FILE_NAME="$RELEASE_FILE_NAME.app"
RELEASE_DMG_FILE_NAME="$RELEASE_FILE_NAME.dmg"
RELEASE_TAR_FILE_NAME="$RELEASE_FILE_NAME.tar"

ARM64_APP_BUNDLE="$TEMP_DIRECTORY/output_arm64/Ryujinx.app"
X64_APP_BUNDLE="$TEMP_DIRECTORY/output_x64/Ryujinx.app"
UNIVERSAL_APP_BUNDLE="$OUTPUT_DIRECTORY/Ryujinx.app"
EXECUTABLE_SUB_PATH=Contents/MacOS/Ryujinx

rm -rf "$TEMP_DIRECTORY"
mkdir -p "$TEMP_DIRECTORY"

DOTNET_COMMON_ARGS=(-p:DebugType=embedded -p:Version="$VERSION" -p:SourceRevisionId="$SOURCE_REVISION_ID" --self-contained true $EXTRA_ARGS)

echo ""
echo "Publishing .app"
dotnet restore
dotnet build -c "$CONFIGURATION" src/Ryujinx
dotnet publish -c "$CONFIGURATION" -r osx-arm64 -o "$TEMP_DIRECTORY/publish_arm64" "${DOTNET_COMMON_ARGS[@]}" src/Ryujinx
dotnet publish -c "$CONFIGURATION" -r osx-x64 -o "$TEMP_DIRECTORY/publish_x64" "${DOTNET_COMMON_ARGS[@]}" src/Ryujinx

# Get rid of the support library for ARMeilleure for x64 (that's only for arm64).
rm -rf "$TEMP_DIRECTORY/publish_x64/libarmeilleure-jitsupport.dylib"

# Get rid of libsoundio from arm64 builds as we don't have a arm64 variant.
# TODO: remove this once done
rm -rf "$TEMP_DIRECTORY/publish_arm64/libsoundio.dylib"

pushd "$BASE_DIRECTORY/distribution/macos"
./create_app_bundle.sh "$TEMP_DIRECTORY/publish_x64" "$TEMP_DIRECTORY/output_x64" "$ENTITLEMENTS_FILE_PATH"
./create_app_bundle.sh "$TEMP_DIRECTORY/publish_arm64" "$TEMP_DIRECTORY/output_arm64" "$ENTITLEMENTS_FILE_PATH"
popd

rm -rf "$UNIVERSAL_APP_BUNDLE"
mkdir -p "$OUTPUT_DIRECTORY"

# Let's copy one of the two different app bundle and remove the executable.
cp -R "$ARM64_APP_BUNDLE" "$UNIVERSAL_APP_BUNDLE"
rm "$UNIVERSAL_APP_BUNDLE/$EXECUTABLE_SUB_PATH"

# Make its libraries universal.
python3 "$BASE_DIRECTORY/distribution/macos/construct_universal_dylib.py" "$ARM64_APP_BUNDLE" "$X64_APP_BUNDLE" "$UNIVERSAL_APP_BUNDLE" "**/*.dylib"

if ! [ -x "$(command -v lipo)" ];
then
    if ! [ -x "$(command -v llvm-lipo-22)" ];
    then
        LIPO=llvm-lipo
    else
        LIPO=llvm-lipo-22
    fi
else
    LIPO=lipo
fi

# Make the executable universal.
$LIPO "$ARM64_APP_BUNDLE/$EXECUTABLE_SUB_PATH" "$X64_APP_BUNDLE/$EXECUTABLE_SUB_PATH" -output "$UNIVERSAL_APP_BUNDLE/$EXECUTABLE_SUB_PATH" -create

# Patch up the Info.plist to have appropriate version.
sed -r -i.bck "s/\%\%RYUJINX_BUILD_VERSION\%\%/$VERSION/g;" "$UNIVERSAL_APP_BUNDLE/Contents/Info.plist"
sed -r -i.bck "s/\%\%RYUJINX_BUILD_GIT_HASH\%\%/$SOURCE_REVISION_ID/g;" "$UNIVERSAL_APP_BUNDLE/Contents/Info.plist"
rm "$UNIVERSAL_APP_BUNDLE/Contents/Info.plist.bck"

# Set up staging.
echo ""
echo "Staging directory for packaging"

DMG_FOLDER="$OUTPUT_DIRECTORY/dmg"
mkdir "$DMG_FOLDER"

tar -xzf "$BASE_DIRECTORY/distribution/macos/DMG_ASSETS/DMG_Structure.tar.gz" -C "$DMG_FOLDER" --strip-components=1
cp -R "$UNIVERSAL_APP_BUNDLE" "$DMG_FOLDER/Ryujinx.app"

chmod -R 755 "$DMG_FOLDER/Ryujinx.app"
chmod +x "$DMG_FOLDER/Ryujinx.app/Contents/MacOS/Ryujinx"

# Now sign it.
echo ""
echo "Signing .app"
if ! [ -x "$(command -v codesign)" ];
then
    if ! [ -x "$(command -v rcodesign)" ];
    then
        echo "Cannot find rcodesign on your system, please install rcodesign."
        exit 1
    fi

    echo "Using rcodesign for ad-hoc signing"
    rcodesign sign --entitlements-xml-path "$ENTITLEMENTS_FILE_PATH" "$DMG_FOLDER/Ryujinx.app"
else
    echo "Using codesign for ad-hoc signing"
    codesign --entitlements "$ENTITLEMENTS_FILE_PATH" --force --deep --sign - "$DMG_FOLDER/Ryujinx.app"
    
    echo "Using codesign to verify signature"
    spctl -a -vv "$DMG_FOLDER/Ryujinx.app"
fi

# Package it into a disk image.
echo ""
echo "Packaging .dmg"

UNCOMPRESSED_DMG="$OUTPUT_DIRECTORY/UNCOMPRESSED_$RELEASE_DMG_FILE_NAME"
COMPRESSED_DMG="$OUTPUT_DIRECTORY/$RELEASE_DMG_FILE_NAME"

dd if=/dev/zero of="$UNCOMPRESSED_DMG" bs=1M count=100
genisoimage -D -V "Ryujinx" -no-pad -r -apple -file-mode 0777 -o $UNCOMPRESSED_DMG $DMG_FOLDER
dmg dmg -c lzma "$UNCOMPRESSED_DMG" "$COMPRESSED_DMG"
rm -r "$DMG_FOLDER"
rm -f "$UNCOMPRESSED_DMG"

chmod -R 755 "$COMPRESSED_DMG"

# ... And sign it again. Thanks, Apple.
echo ""
echo "Signing .dmg"
if ! [ -x "$(command -v codesign)" ];
then
    if ! [ -x "$(command -v rcodesign)" ];
    then
        echo "Cannot find rcodesign on your system, please install rcodesign."
        exit 1
    fi

    echo "Using rcodesign for ad-hoc signing"
    rcodesign sign "$COMPRESSED_DMG"
else
    echo "Using codesign for ad-hoc signing"
    codesign --force --deep --sign - "$COMPRESSED_DMG"

    echo "Using codesign to verify signature"
    spctl -a -vv "$COMPRESSED_DMG"
fi

echo "Done"
