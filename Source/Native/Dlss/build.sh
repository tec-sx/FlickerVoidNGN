#!/usr/bin/env bash
# Builds the engine's DLSS library for Linux into artifacts/native/linux-x64 with NVIDIA's DLSS and Ray Reconstruction runtimes
# beside it, which the engine's Vulkan project then copies beside every program. Fetches the DLSS SDK and the Vulkan headers into
# artifacts/native first; needs git, CMake and a C++ compiler. --development takes the runtimes with NVIDIA's on-screen indicator
# and debug keys.
set -euo pipefail

dlss_version=310.9.16
vulkan_headers_version=vulkan-sdk-1.4.350.0
runtime=rel
if [ "${1:-}" = "--development" ]; then
  runtime=dev
fi

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
root="$(cd "$here/../../.." && pwd)"
native="$root/artifacts/native"
sdk="$native/dlss-sdk-$dlss_version"
headers="$native/vulkan-headers-$vulkan_headers_version"
build="$native/build-linux-x64"
output="$native/linux-x64"
runtime_file="libnvidia-ngx-dlss.so.$dlss_version"
denoising_runtime_file="libnvidia-ngx-dlssd.so.$dlss_version"

# Only the headers, the static library and the runtimes of the SDK's few gigabytes.
if [ ! -d "$sdk" ]; then
  git clone --depth 1 --filter=blob:none --sparse --branch "v$dlss_version" https://github.com/NVIDIA/DLSS.git "$sdk"
fi
git -C "$sdk" sparse-checkout set --no-cone /include/ /LICENSE.txt /lib/Linux_x86_64/libnvsdk_ngx.a "/lib/Linux_x86_64/$runtime/$runtime_file" \
  "/lib/Linux_x86_64/$runtime/$denoising_runtime_file"

if [ ! -d "$headers" ]; then
  git clone --depth 1 --filter=blob:none --sparse --branch "$vulkan_headers_version" https://github.com/KhronosGroup/Vulkan-Headers.git "$headers"
  git -C "$headers" sparse-checkout set include
fi

cmake -S "$here" -B "$build" -DCMAKE_BUILD_TYPE=Release -DDLSS_SDK="$sdk" -DVULKAN_HEADERS="$headers"
cmake --build "$build" --config Release
mkdir -p "$output"
cp "$build/libFlickerVoid.Dlss.so" "$output/"
cp "$sdk/lib/Linux_x86_64/$runtime/$runtime_file" "$output/"
cp "$sdk/lib/Linux_x86_64/$runtime/$denoising_runtime_file" "$output/"
echo "Built the DLSS library and NVIDIA's $runtime runtimes into $output."
