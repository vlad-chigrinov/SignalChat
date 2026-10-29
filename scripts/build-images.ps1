# Builds the production images on this workstation. Never run it on the production host (too little RAM).
# Tag = short git sha of HEAD. The "latest" tag is deliberately not produced.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$tag = (git -C $root rev-parse --short HEAD).Trim()

$dirty = git -C $root status --porcelain
if ($dirty) {
    Write-Warning 'The working tree has uncommitted changes: the images do not match the tag exactly.'
}

docker build -t "signalchat-api:$tag" -f (Join-Path $root 'backend/Dockerfile') (Join-Path $root 'backend')
if ($LASTEXITCODE -ne 0) { throw 'API image build failed.' }

$webDockerfile = Join-Path $root 'frontend/Dockerfile'
if (Test-Path $webDockerfile) {
    docker build -t "signalchat-web:$tag" -f $webDockerfile (Join-Path $root 'frontend')
    if ($LASTEXITCODE -ne 0) { throw 'Web image build failed.' }
} else {
    Write-Warning 'frontend/Dockerfile does not exist yet: the web image was not built.'
}

Write-Output "API_IMAGE=signalchat-api:$tag"
Write-Output "WEB_IMAGE=signalchat-web:$tag"
