#!/bin/bash

set -e

INSTALL_DIRECTORY=$1
NEW_APP_DIRECTORY=$2
APP_PID=$3
APP_ARGUMENTS=("${@:4}")

error_handler() {
    local lineno="$1"

    script="""
    set alertTitle to \"Ryujinx - Updater error\"
    set alertMessage to \"An error occurred during Ryujinx update (updater.sh:$lineno)\n\nPlease download the update manually from our website if the problem persists.\"
    display dialog alertMessage with icon caution with title alertTitle buttons {\"Open Download Page\", \"Exit\"}
    set the button_pressed to the button returned of the result

    if the button_pressed is \"Open Download Page\" then
        open location \"https://ryujinx.app/download\"
    end if
    """

    osascript -e "$script"
    exit 1
}

trap 'error_handler ${LINENO}' ERR

# Wait for Ryujinx to exit.
# If the main process is still acitve, we wait for 1 second and check it again.
# After the fifth time checking, this script exits with status 1.

attempt=0
while true; do
    if lsof -p "$APP_PID" +r 1 &>/dev/null || ps -p "$APP_PID" &>/dev/null; then
        if [ "$attempt" -eq 4 ]; then
            echo "Error: Update failed. Ryujinx was still open after 5 seconds."
            exit 1
        fi
        sleep 1
    else
        break
    fi
    (( attempt++ ))
done

sleep 1

# Mount the .dmg.
MOUNT_POINT=$(mktemp -d "/tmp/dmg-mount.$APP_PID")
hdiutil attach "$NEW_APP_DIRECTORY" -mountpoint "$MOUNT_POINT" -nobrowse -quiet

# Find the location of the updated Ryujinx.app bundle.
# If it can't be found in the downloaded update, this script exits with status 2.

NEW_APP=$(find "$MOUNT_POINT" -maxdepth 1 -name "Ryujinx.app" | head -n 1)
if [ -z "$NEW_APP" ];
then
    echo "Error: Updated failed. Cannot find Ryujinx.app, is the update corrupted?"
    hdiutil detach "$MOUNT_POINT" -force -quiet
    rm -rf "$MOUNT_POINT"
    exit 2
fi

# Replace.
rm -rf "$INSTALL_DIRECTORY"
cp -R "$NEW_APP" "$INSTALL_DIRECTORY"

# Cleanup.
hdiutil detach "$MOUNT_POINT" -force -quiet
rm -rf "$MOUNT_POINT"
rm -rf "$NEW_APP_DIRECTORY"

# Relaunch.
if [ "$#" -le 3 ]; then
    open -a "$INSTALL_DIRECTORY"
else
    open -a "$INSTALL_DIRECTORY" --args "${APP_ARGUMENTS[@]}"
fi

echo "Done"
