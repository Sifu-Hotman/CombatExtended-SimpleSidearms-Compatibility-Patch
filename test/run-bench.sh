#!/usr/bin/env bash
# In-game benchmark of the weapon-scoring path (CE's convention: benchmark inside
# RimWorld, not a desktop harness). Loads CETEST-2-selection, times the calls SS
# makes during a warmup tick with the patch active and again with its Harmony
# patches removed, and writes test/SaveData/bench-results-<label>.json.
#
# Usage:
#   ./test/run-bench.sh                 # measure the current build
#   ./test/run-bench.sh cebench-before  # label the run (for A/B against another build)
set -euo pipefail

LABEL="${1:-cebench}"

REPO="$(cd "$(dirname "$0")/.." && pwd)"
RIMWORLD="$HOME/.local/share/Steam/steamapps/common/RimWorld/RimWorldLinux"
GS=(gamescope -W 1600 -H 900 --)
SAVEDATA="$REPO/test/SaveData"
RESULT="$SAVEDATA/bench-results-$LABEL.json"

if [[ "${SKIP_BUILD:-0}" != "1" ]]; then
    dotnet build "$REPO/Source/CESimpleSidearmsCompat/CESimpleSidearmsCompat.csproj" -c Release
    dotnet build "$REPO/test/StagingMod/Source/TestStaging.csproj" -c Release
fi

rm -f "$RESULT"
timeout --signal=TERM 20m "${GS[@]}" "$RIMWORLD" -savedatafolder="$SAVEDATA" \
    "-celoadsave=CETEST-2-selection" "-ceassert=$LABEL" || true

if [[ -f "$RESULT" ]]; then
    echo "== results: $RESULT =="
    cat "$RESULT"
else
    echo "== NO RESULTS FILE — bench never finished; check Player.log ==" >&2
    exit 1
fi
