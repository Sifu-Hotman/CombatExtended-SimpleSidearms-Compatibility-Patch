#!/usr/bin/env bash
# Automated acceptance run for the CETEST saves: load a staged save, execute a
# scenario's in-game assertions (CETestRunner.cs), write
#   test/SaveData/test-results-<scenario>.json
# and self-exit.
#
# Usage:
#   ./test/run-assert.sh cetest1 CETEST-1-pickup
#   ./test/run-assert.sh cetest2 CETEST-2-selection
#   ./test/run-assert.sh cetest3 CETEST-3-combat
#   ./test/run-assert.sh cetest4 CETEST-4-generation
#   SKIP_BUILD=1 ./test/run-assert.sh ...
set -euo pipefail

SCENARIO="${1:?scenario (cetest1..4)}"
SAVE="${2:?save name}"

REPO="$(cd "$(dirname "$0")/.." && pwd)"
RIMWORLD="$HOME/.local/share/Steam/steamapps/common/RimWorld/RimWorldLinux"
# GS_WRAP: launch inside gamescope's nested compositor — immune to the desktop's
# display state (owner gaming via Proton, mode-list churn, XF86VidMode crashes).
GS=(gamescope -W 1600 -H 900 --)
SAVEDATA="$REPO/test/SaveData"
RESULT="$SAVEDATA/test-results-$SCENARIO.json"

if [[ "${SKIP_BUILD:-0}" != "1" ]]; then
    dotnet build "$REPO/Source/CESimpleSidearmsCompat/CESimpleSidearmsCompat.csproj" -c Release
    dotnet build "$REPO/test/StagingMod/Source/TestStaging.csproj" -c Release
fi

rm -f "$RESULT"
timeout --signal=TERM 20m "${GS[@]}" "$RIMWORLD" -savedatafolder="$SAVEDATA" \
    "-celoadsave=$SAVE" "-ceassert=$SCENARIO" || true

if [[ -f "$RESULT" ]]; then
    echo "== results: $RESULT =="
    cat "$RESULT"
else
    echo "== NO RESULTS FILE — runner never finished; check Player.log ==" >&2
    exit 1
fi
