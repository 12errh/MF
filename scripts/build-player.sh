#!/usr/bin/env bash
# Build the Mate Framework Unity player (Linux x64) into the runtime cache.
#
# Usage: scripts/build-player.sh [version] [out]
#   version  semver to install into (default: 1.0.0)
#   out      destination directory; defaults to ~/.mate-framework/runtimes/$version
#
# Produces <out>/MateRuntime/MateRuntime plus its _Data sibling, matching the
# layout that `mf player_path()` expects.

set -euo pipefail

VERSION="${1:-1.0.0}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="${UNITY:-$HOME/Unity/Hub/Editor/6000.2.6f2/Editor/Unity}"
OUT="${2:-$HOME/.mate-framework/runtimes/$VERSION}"

if [ ! -x "$UNITY" ]; then
  echo "error: Unity editor not found at $UNITY" >&2
  exit 1
fi

mkdir -p "$OUT"
LOG="$OUT/build.log"

echo "Building MateRuntime v$VERSION -> $OUT/MateRuntime/MateRuntime"

# Regenerate the idle AnimatorController asset (single Idle state bound to the
# humanoid idle clip) so a fresh checkout or clip change is picked up.
"$UNITY" -batchmode -nographics -quit \
  -projectPath "$ROOT/unity" \
  -executeMethod Mate.Bootstrap.EditorTools.MateAnimatorBuilder.BuildController \
  -logFile "$OUT/build-controller.log" >/dev/null 2>&1 || true

# Build the player binary. The BuildScripts directory is auto-discovered by
# Unity (Editor folder), so no -executeMethod is required for a default build.
"$UNITY" -batchmode -nographics -quit \
  -projectPath "$ROOT/unity" \
  -buildLinux64Player "$OUT/MateRuntime/MateRuntime" \
  -logFile "$LOG"

echo "Build finished. Player: $OUT/MateRuntime/MateRuntime"
echo "Log: $LOG"