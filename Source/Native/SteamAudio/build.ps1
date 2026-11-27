# Fetches Valve's Steam Audio (Apache 2.0) release and puts its Windows library, phonon.dll, into artifacts/native/win-x64, where
# the engine's audio project finds it and copies it beside programs.
$ErrorActionPreference = 'Stop'
$version = '4.6.1'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$native = Join-Path $root 'artifacts/native'
$output = Join-Path $native 'win-x64'
$work = Join-Path $native "steamaudio-$version"
if (Test-Path (Join-Path $output 'phonon.dll')) {
    return
}

New-Item -ItemType Directory -Force -Path $work, $output | Out-Null
$zip = Join-Path $work "steamaudio_$version.zip"
if (-not (Test-Path $zip)) {
    Invoke-WebRequest -Uri "https://github.com/ValveSoftware/steam-audio/releases/download/v$version/steamaudio_$version.zip" -OutFile $zip
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
try {
    $entry = $archive.GetEntry('steamaudio/lib/windows-x64/phonon.dll')
    [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $output 'phonon.dll'), $true)
}
finally {
    $archive.Dispose()
}
