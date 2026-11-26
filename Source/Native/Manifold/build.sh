#!/usr/bin/env bash
# Builds Manifold (github.com/elalish/manifold, Apache-2.0), the mesh Boolean library the editor's mesh tools cut with, for Linux
# into artifacts/native/linux-x64, where the editor and its tests find it. Fetches its source into artifacts/native first; needs
# git, CMake and a C++ compiler. Its C binding (manifoldc) is the only part the editor calls.
set -euo pipefail

manifold_version=v3.5.5

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/../../.." && pwd)"
native="$root/artifacts/native"
source="$native/manifold-$manifold_version"
build="$native/build-manifold-linux-x64"
output="$native/linux-x64"

if [ ! -d "$source" ]; then
  git clone --depth 1 --branch "$manifold_version" https://github.com/elalish/manifold.git "$source"
fi

cmake -S "$source" -B "$build" -DCMAKE_BUILD_TYPE=Release -DBUILD_SHARED_LIBS=ON -DMANIFOLD_CBIND=ON -DMANIFOLD_TEST=OFF \
  -DMANIFOLD_PAR=OFF -DMANIFOLD_PYBIND=OFF -DMANIFOLD_JSBIND=OFF -DCMAKE_BUILD_WITH_INSTALL_RPATH=ON -DCMAKE_INSTALL_RPATH='$ORIGIN'
cmake --build "$build" --config Release --target manifoldc --parallel
mkdir -p "$output"
find "$build" -name 'lib*.so*' -exec cp -P {} "$output/" \;
echo "Built Manifold into $output."
