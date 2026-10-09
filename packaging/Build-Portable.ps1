param(
    [switch]$IncludeTemplates
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$releaseName = 'DriverChecklist-win-x64-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
$releaseRoot = Join-Path $root 'artifacts'
$output = Join-Path $releaseRoot $releaseName
$frontend = Join-Path $root 'frontend'
$project = Join-Path $root 'backend\DriverChecklist.Api\DriverChecklist.Api.csproj'

Push-Location $frontend
try {
    & npm.cmd run build
    if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
}
finally {
    Pop-Location
}

& dotnet publish $project --configuration Release --runtime win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None `
    -p:DebugSymbols=false --output $output
if ($LASTEXITCODE -ne 0) { throw 'Backend publish failed.' }

$webroot = Join-Path $output 'wwwroot'
New-Item -ItemType Directory -Path $webroot -Force | Out-Null
Copy-Item -Path (Join-Path $frontend 'dist\frontend\browser\*') -Destination $webroot -Recurse
Copy-Item -LiteralPath (Join-Path $frontend 'dist\frontend\3rdpartylicenses.txt') -Destination $output
foreach ($name in @('portable.json', 'Start-DriverChecklist.cmd', 'READ-ME.txt')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $output
}

$templates = Join-Path $output 'Templates'
New-Item -ItemType Directory -Path $templates -Force | Out-Null
if ($IncludeTemplates) {
    $templateSource = Join-Path $root 'backend\DriverChecklist.Api\Templates'
    $files = Get-ChildItem -LiteralPath $templateSource -Filter '*.xlsx' -File
    if ($files.Count -eq 0) { throw 'No checklist templates found; package not archived.' }
    foreach ($file in $files) {
        Copy-Item -LiteralPath $file.FullName -Destination $templates
    }
}
else {
    Write-Warning 'Checklist templates are not included. Copy them into the Templates folder before generation.'
}

$archive = Join-Path $releaseRoot "$releaseName.zip"
Compress-Archive -LiteralPath $output -DestinationPath $archive -CompressionLevel Optimal
Write-Host "Portable folder: $output"
Write-Host "Transfer archive: $archive"
if ($IncludeTemplates) {
    Write-Warning 'This package contains company checklist templates. Transfer privately; do not commit or publish it.'
}
