#!/bin/bash
# Dev loop on the build host: builds engine + mod for Linux and runs OpenRA.Utility on openkrush_gen2.
# Original game files (owner's own Steam copy) are mounted read-only as the mod content; never part of the repo.
# Usage: thor/dev/check.sh [utility args...]   (default: --check-yaml)
set -euo pipefail
[ -f "$HOME/.config/thorkknd2/env" ] && . "$HOME/.config/thorkknd2/env"
HOST=${THOR_BUILD_HOST:?}; REMOTE=${THOR_BUILD_DIR:-/mnt/cache/thor-kknd2}
REPO="$(cd "$(dirname "$0")/../.." && pwd)"
ARGS="${*:---check-yaml}"
rsync -rlp --checksum --delete --exclude .git --exclude /dist --exclude /thor/shots --exclude 'bin/' --exclude 'obj/' \
  --exclude /thor/android/native-out --exclude /thor/android/assets-out "$REPO/" "$HOST:$REMOTE/src/"
ssh "$HOST" "docker run --rm -e DOTNET_ROLL_FORWARD=Major -e XDG_CONFIG_HOME=/root/.config -v $REMOTE/src:/work/src -v $REMOTE/nuget:/root/.nuget \
  -v $REMOTE/gamedata:/root/.config/openra/Content/openkrush_gen2:ro -v $REMOTE/devmaps:/root/.config/openra/maps thor-kknd2-build bash -c '
  cd /work/src && dotnet build OpenRA.Mods.OpenKrush -c Release -nologo -v q -p:TreatWarningsAsErrors=false >/tmp/b.log 2>&1 || { grep -E \"error\" /tmp/b.log | sort -u | head -30; exit 1; }
  dotnet build engine/OpenRA.Utility -c Release -nologo -v q -p:TreatWarningsAsErrors=false >/tmp/b2.log 2>&1 || { grep error /tmp/b2.log | head; exit 1; }
  cd engine && ENGINE_DIR=.. MOD_SEARCH_PATHS=/work/src/mods,./mods dotnet bin/OpenRA.Utility.dll openkrush_gen2 $ARGS'"
