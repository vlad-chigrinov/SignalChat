# Saves the images built by build-images.ps1 into dist-images/ as gzip tarballs, each with a SHA-256 file.
# Copy the tarballs and their .sha256 files to the server, check them with `sha256sum -c`, then `docker load`.
param(
    [string]$Tag
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Tag) {
    $Tag = (git -C $root rev-parse --short HEAD).Trim()
}

$outDir = Join-Path $root 'dist-images'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

foreach ($name in 'signalchat-api', 'signalchat-web') {
    $image = "${name}:$Tag"
    $tar = Join-Path $outDir "$name-$Tag.tar"
    $gz = "$tar.gz"

    # docker save writes a file: piping binary output through PowerShell would corrupt it.
    docker save -o $tar $image
    if ($LASTEXITCODE -ne 0) { throw "docker save failed for $image (is the image built?)." }

    $inStream = [System.IO.File]::OpenRead($tar)
    $outStream = [System.IO.File]::Create($gz)
    $gzipStream = New-Object System.IO.Compression.GzipStream($outStream, [System.IO.Compression.CompressionMode]::Compress)
    try {
        $inStream.CopyTo($gzipStream)
    } finally {
        $gzipStream.Dispose()
        $outStream.Dispose()
        $inStream.Dispose()
    }
    Remove-Item $tar

    # Same layout as sha256sum prints, so `sha256sum -c` on the server verifies it.
    $hash = (Get-FileHash -Algorithm SHA256 $gz).Hash.ToLowerInvariant()
    Set-Content -Path "$gz.sha256" -Value "$hash  $(Split-Path -Leaf $gz)" -Encoding ascii
    Write-Output "$gz"
}
