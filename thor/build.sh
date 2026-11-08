#!/bin/bash
# Builds dist/ThorKKND2.apk on a Linux host with Docker (image thor-kknd2-build, see thor/docker/Dockerfile).
# Signing key lives only on the build host ($REMOTE/keys). Optional: --install
set -euo pipefail
[ -f "$HOME/.config/thorkknd2/env" ] && . "$HOME/.config/thorkknd2/env"
HOST=${THOR_BUILD_HOST:?set THOR_BUILD_HOST=user@linux-host-with-docker}
REMOTE=${THOR_BUILD_DIR:-/mnt/cache/thor-kknd2}
REPO="$(cd "$(dirname "$0")/.." && pwd)"
[ -d "$REPO/engine/OpenRA.Game" ] || (cd "$REPO" && ./fetch-engine.sh && sh thor/apply-engine-patches.sh)

rsync -rlp --checksum --delete --exclude .git --exclude /dist --exclude /thor/shots --exclude 'bin/' --exclude 'obj/' \
  --exclude /thor/android/native-out --exclude /thor/android/assets-out "$REPO/" "$HOST:$REMOTE/apk-src/"
ssh "$HOST" "docker run --rm -v $REMOTE/apk-src:/work/src -v $REMOTE/keys:/work/keys:ro -v $REMOTE/native:/work/native \
  -v $REMOTE/out:/work/out -v $REMOTE/nuget:/root/.nuget thor-kknd2-build bash /work/src/thor/build-in-docker.sh > $REMOTE/build.log 2>&1; \
  rc=\$?; tail -25 $REMOTE/build.log; exit \$rc"
mkdir -p "$REPO/dist"
scp -q "$HOST:$REMOTE/out/de.harmssi.thorkknd2-Signed.apk" "$REPO/dist/ThorKKND2.apk"
echo "APK: $REPO/dist/ThorKKND2.apk"
[ "${1:-}" = "--install" ] && adb install -r "$REPO/dist/ThorKKND2.apk"
exit 0
