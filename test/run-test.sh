#!/usr/bin/env bash
# Build the compat DLL and launch RimWorld with an isolated test profile.
# Your real config/saves/modlist are untouched: everything lives in test/SaveData.
#
# Usage:
#   ./test/run-test.sh              build + launch (main menu; use "Dev quicktest")
#   ./test/run-test.sh -quicktest   build + launch straight into a throwaway test map
#   ./test/run-test.sh stage        build + auto-create the CETEST-* staged saves,
#                                   then quit the game and relaunch normally to load them
#   SKIP_BUILD=1 ./test/run-test.sh relaunch without rebuilding
#
# Steam must be running (workshop copies of CE / Simple Sidearms / Harmony are
# resolved through it). Player.log for this profile lands in the usual place:
#   ~/.config/unity3d/Ludeon Studios/RimWorld by Ludeon Studios/Player.log
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd)"
RIMWORLD="$HOME/.local/share/Steam/steamapps/common/RimWorld/RimWorldLinux"
# GS_WRAP: launch inside gamescope's nested compositor — immune to the desktop's
# display state (owner gaming via Proton, mode-list churn, XF86VidMode crashes).
GS=(gamescope -W 1600 -H 900 --)
SAVEDATA="$REPO/test/SaveData"

ARGS=("$@")
if [[ "${1:-}" == "stage" ]]; then
    ARGS=(-quicktest -cestage)
    rm -f "$REPO/test/SaveData/Saves"/CETEST-*.rws   # drop stale staged saves
fi

if [[ "${SKIP_BUILD:-0}" != "1" ]]; then
    dotnet build "$REPO/Source/CESimpleSidearmsCompat/CESimpleSidearmsCompat.csproj" -c Release
    dotnet build "$REPO/test/StagingMod/Source/TestStaging.csproj" -c Release
fi

# Seed the isolated profile on first run (game owns the folder afterwards).
mkdir -p "$SAVEDATA/Config" "$SAVEDATA/Saves"
for f in ModsConfig.xml Prefs.xml; do
    if [[ ! -e "$SAVEDATA/Config/$f" ]]; then
        cp "$REPO/test/Config/$f" "$SAVEDATA/Config/$f"
    fi
done

exec "${GS[@]}" "$RIMWORLD" -savedatafolder="$SAVEDATA" "${ARGS[@]}"
