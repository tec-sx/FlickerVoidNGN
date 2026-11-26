# Builds Manifold (github.com/elalish/manifold, Apache-2.0), the mesh Boolean library the editor's mesh tools cut with, for
# Windows into artifacts/native/win-x64, where the editor and its tests find it. Fetches its source into artifacts/native first;
# needs git, CMake and Visual Studio's C++ tools (the "Desktop development with C++" workload). Its C binding (manifoldc) is the
# only part the editor calls.
$ErrorActionPreference = 'Stop'
$manifoldVersion = 'v3.5.5'

$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$native = Join-Path $root 'artifacts/native'
$source = Join-Path $native "manifold-$manifoldVersion"
$build = Join-Path $native 'build-manifold-win-x64'
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

if (-not (Test-Path $source)) {
    Invoke-Native git @('clone', '--depth', '1', '--branch', $manifoldVersion, 'https://github.com/elalish/manifold.git', $source)
}

$cmake = Find-CMake
Invoke-Native $cmake @('-S', $source, '-B', $build, '-A', 'x64', '-DBUILD_SHARED_LIBS=ON', '-DMANIFOLD_CBIND=ON', '-DMANIFOLD_TEST=OFF', '-DMANIFOLD_PAR=OFF', '-DMANIFOLD_PYBIND=OFF', '-DMANIFOLD_JSBIND=OFF')
Invoke-Native $cmake @('--build', $build, '--config', 'Release', '--target', 'manifoldc', '--parallel')
New-Item -ItemType Directory -Force $output | Out-Null
Get-ChildItem -Path $build -Filter '*.dll' -Recurse | Where-Object { $_.FullName -match 'Release' } | Copy-Item -Destination $output -Force
Write-Host "Built Manifold into $output."
