# Builds the engine's DLSS library for Windows into artifacts/native/win-x64 with NVIDIA's DLSS and Ray Reconstruction runtimes
# beside it, which the engine's Vulkan project then copies beside every program. Fetches the DLSS SDK and the Vulkan headers into
# artifacts/native first; needs git, CMake and Visual Studio's C++ tools (the "Desktop development with C++" workload, which
# brings CMake too). -Development takes the runtimes with NVIDIA's on-screen indicator and debug keys.
param([switch]$Development)

$ErrorActionPreference = 'Stop'
$dlssVersion = '310.9.16'
$vulkanHeadersVersion = 'vulkan-sdk-1.4.350.0'
$runtime = 'rel'
if ($Development) {
    $runtime = 'dev'
}

$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$native = Join-Path $root 'artifacts/native'
$sdk = Join-Path $native "dlss-sdk-$dlssVersion"
$headers = Join-Path $native "vulkan-headers-$vulkanHeadersVersion"
$build = Join-Path $native 'build-win-x64'
$output = Join-Path $native 'win-x64'

function Invoke-Native {
    param([string]$Command, [string[]]$Arguments)
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Command $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

# CMake from the path, else the one Visual Studio's C++ workload installs.
function Find-CMake {
    $onPath = Get-Command cmake -ErrorAction SilentlyContinue
    if ($onPath) {
        return $onPath.Source
    }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    if (Test-Path $vswhere) {
        $studio = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
        if ($studio) {
            $bundled = Join-Path $studio 'Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe'
            if (Test-Path $bundled) {
                return $bundled
            }
        }
    }

    throw 'CMake was not found: install Visual Studio''s "Desktop development with C++" workload, or CMake and the C++ build tools.'
}

# Only the headers, the static library and the runtimes of the SDK's few gigabytes.
if (-not (Test-Path $sdk)) {
    Invoke-Native git @('clone', '--depth', '1', '--filter=blob:none', '--sparse', '--branch', "v$dlssVersion", 'https://github.com/NVIDIA/DLSS.git', $sdk)
}
Invoke-Native git @('-C', $sdk, 'sparse-checkout', 'set', '--no-cone', '/include/', '/LICENSE.txt', '/lib/Windows_x86_64/x64/nvsdk_ngx_s.lib', "/lib/Windows_x86_64/$runtime/nvngx_dlss.dll", "/lib/Windows_x86_64/$runtime/nvngx_dlssd.dll")

if (-not (Test-Path $headers)) {
    Invoke-Native git @('clone', '--depth', '1', '--filter=blob:none', '--sparse', '--branch', $vulkanHeadersVersion, 'https://github.com/KhronosGroup/Vulkan-Headers.git', $headers)
    Invoke-Native git @('-C', $headers, 'sparse-checkout', 'set', 'include')
}

$cmake = Find-CMake
$sdkPath = $sdk.Replace('\', '/')
$headersPath = $headers.Replace('\', '/')
Invoke-Native $cmake @('-S', $PSScriptRoot, '-B', $build, '-A', 'x64', "-DDLSS_SDK=$sdkPath", "-DVULKAN_HEADERS=$headersPath")
Invoke-Native $cmake @('--build', $build, '--config', 'Release')
New-Item -ItemType Directory -Force $output | Out-Null
Copy-Item (Join-Path $build 'Release/FlickerVoid.Dlss.dll') $output -Force
Copy-Item (Join-Path $sdk "lib/Windows_x86_64/$runtime/nvngx_dlss.dll") $output -Force
Copy-Item (Join-Path $sdk "lib/Windows_x86_64/$runtime/nvngx_dlssd.dll") $output -Force
Write-Host "Built the DLSS library and NVIDIA's $runtime runtimes into $output."
