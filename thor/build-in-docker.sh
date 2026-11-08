#!/bin/bash
# Runs inside the thor-kknd2-build image with the repo at /work/src and keys at /work/keys.
set -euo pipefail
SRC=/work/src; A=$SRC/thor/android
WORK=/work/native OUT=$A/native-out bash $SRC/thor/native/build-native.sh >/work/native.log 2>&1 || { tail -40 /work/native.log; exit 1; }
CC=$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/bin/aarch64-linux-android26-clang
$CC -O2 -fPIC -shared -Wl,-soname,libmain.so -o $A/native-out/libmain.so $A/native/main.c

# engine + mod assemblies (net6.0 IL, loaded by MonoVM on Android)
cd $SRC
dotnet build OpenRA.Mods.OpenKrush -c Release -nologo -v q -p:TreatWarningsAsErrors=false
dotnet build engine/OpenRA.Platforms.Default -c Release -nologo -v q -p:TreatWarningsAsErrors=false

# game data (no original game files: those are imported on the device)
mkdir -p $A/assets-out && rm -f $A/assets-out/data.zip
python3 - <<'PY'
import os, zipfile
src='/work/src'; z=zipfile.ZipFile(src+'/thor/android/assets-out/data.zip','w',zipfile.ZIP_DEFLATED)
def add(path, arc):
    if os.path.isfile(path): z.write(path, arc); return
    for r,_,fs in os.walk(path):
        for f in fs: z.write(os.path.join(r,f), os.path.join(arc, os.path.relpath(os.path.join(r,f), path)))
for p in ['glsl','lua','mods/common','mods/modcontent','global mix database.dat','VERSION','AUTHORS','COPYING']:
    add(f'{src}/engine/{p}', 'engine/'+p)
for m in os.listdir(f'{src}/mods'):
    if m != 'openkrush_gen1': add(f'{src}/mods/{m}', 'mods/'+m)  # gen1 (KKND1) is not shipped
z.close()
PY

cd $A && rm -rf obj bin  # incremental .NET Android builds left stale JNI registrations
. /work/keys/signing.env
dotnet publish -c Release -nologo -v q -p:AndroidKeyStore=true -p:AndroidSigningKeyStore=/work/keys/thorkknd2.jks \
  -p:AndroidSigningKeyAlias=thorkknd2 -p:AndroidSigningKeyPass="$KEYPASS" -p:AndroidSigningStorePass="$KEYPASS" -o /work/out
ls -la /work/out/*.apk
