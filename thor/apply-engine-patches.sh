#!/bin/sh
# Applies the Thor (Android) changes to the OpenRA engine fetched by fetch-engine.sh.
set -e
cd "$(dirname "$0")/../engine" && patch -p1 -N < ../thor/engine.patch
