#!/usr/bin/env bash
# Fetches Valve's Steam Audio (Apache 2.0) release and puts its Linux library, libphonon.so, into artifacts/native/linux-x64, where
# the engine's audio project finds it and copies it beside programs. Needs curl and unzip.
set -euo pipefail

version=4.6.1
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/../../.." && pwd)"
native="$root/artifacts/native"
output="$native/linux-x64"
work="$native/steamaudio-$version"

if [ -f "$output/libphonon.so" ]; then
  exit 0
fi

mkdir -p "$work" "$output"
if [ ! -f "$work/steamaudio_$version.zip" ]; then
  curl -fsSL -o "$work/steamaudio_$version.zip" "https://github.com/ValveSoftware/steam-audio/releases/download/v$version/steamaudio_$version.zip"
fi

unzip -q -o -j "$work/steamaudio_$version.zip" "steamaudio/lib/linux-x64/libphonon.so" -d "$output"
