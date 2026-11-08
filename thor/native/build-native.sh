#!/bin/bash
# Builds the native libraries OpenRA needs for arm64 Android (runs inside thor-kknd2-build).
# Output: $OUT/lib{SDL2,soft_oal,freetype6,lua51}.so + SDL2 Java sources in $OUT/java.
set -euo pipefail
WORK=${WORK:-/work/native}; OUT=${OUT:-/work/native/out}; API=26; ABI=arm64-v8a
SDL_V=2.30.11; OAL_V=1.24.3; FT_V=2.13.3; LUA_V=5.1.5
TC=$ANDROID_NDK_HOME/build/cmake/android.toolchain.cmake
CM="-G Ninja -DCMAKE_TOOLCHAIN_FILE=$TC -DANDROID_ABI=$ABI -DANDROID_PLATFORM=android-$API -DCMAKE_BUILD_TYPE=Release"
mkdir -p "$WORK/src" "$OUT/java"; cd "$WORK/src"
get() { [ -d "$2" ] || { curl -fsSL "$1" -o t.tgz && tar xzf t.tgz && rm t.tgz; }; }
get https://github.com/libsdl-org/SDL/releases/download/release-$SDL_V/SDL2-$SDL_V.tar.gz SDL2-$SDL_V
get https://github.com/kcat/openal-soft/archive/refs/tags/$OAL_V.tar.gz openal-soft-$OAL_V
get https://download.savannah.gnu.org/releases/freetype/freetype-$FT_V.tar.gz freetype-$FT_V
get https://www.lua.org/ftp/lua-$LUA_V.tar.gz lua-$LUA_V

[ -f "$OUT/libSDL2.so" ] || { cmake -S SDL2-$SDL_V -B b-sdl $CM -DSDL_STATIC=OFF -DSDL_TEST=OFF && cmake --build b-sdl && cp b-sdl/libSDL2.so "$OUT/"; }
cp -r SDL2-$SDL_V/android-project/app/src/main/java/org "$OUT/java/"
[ -f "$OUT/libsoft_oal.so" ] || { cmake -S openal-soft-$OAL_V -B b-oal $CM -DALSOFT_UTILS=OFF -DALSOFT_EXAMPLES=OFF -DALSOFT_TESTS=OFF \
    -DALSOFT_BACKEND_OPENSL=ON -DALSOFT_BACKEND_OBOE=OFF -DALSOFT_REQUIRE_OPENSL=ON && cmake --build b-oal && cp b-oal/libopenal.so "$OUT/libsoft_oal.so"; }
[ -f "$OUT/libfreetype6.so" ] || { cmake -S freetype-$FT_V -B b-ft $CM -DBUILD_SHARED_LIBS=ON -DFT_DISABLE_ZLIB=ON -DFT_DISABLE_BZIP2=ON \
    -DFT_DISABLE_PNG=ON -DFT_DISABLE_HARFBUZZ=ON -DFT_DISABLE_BROTLI=ON && cmake --build b-ft && cp -L b-ft/libfreetype.so "$OUT/libfreetype6.so"; }
if [ ! -f "$OUT/liblua51.so" ]; then
  CC=$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/bin/aarch64-linux-android$API-clang
  ( cd lua-$LUA_V/src && $CC -O2 -fPIC -shared -DLUA_USE_POSIX -DLUA_USE_DLOPEN -Wl,-soname,liblua51.so -o "$OUT/liblua51.so" \
      $(ls *.c | grep -v -E '^(lua|luac|print)\.c$') -lm -ldl )
fi
# the libs are loaded by their soname from the APK lib dir
$ANDROID_NDK_HOME/toolchains/llvm/prebuilt/linux-x86_64/bin/llvm-strip --strip-unneeded "$OUT"/*.so
ls -la "$OUT"
