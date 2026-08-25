param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot 'src\FrostlinePaintball\FrostlinePaintball.csproj'
$buildOutput = Join-Path $projectRoot "src\FrostlinePaintball\bin\$Configuration\net10.0"
$releasePlugin = Join-Path $projectRoot 'release\server\addons\counterstrikesharp\plugins\FrostlinePaintball'

dotnet build $projectFile -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Plugin build failed with exit code $LASTEXITCODE."
}

New-Item -ItemType Directory -Path $releasePlugin -Force | Out-Null

$releaseRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'release'))
$resolvedPlugin = [System.IO.Path]::GetFullPath($releasePlugin)
if (-not $resolvedPlugin.StartsWith($releaseRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean an output path outside the project release directory: '$resolvedPlugin'."
}

Get-ChildItem -LiteralPath $releasePlugin -File | Remove-Item -Force

foreach ($name in @('FrostlinePaintball.dll', 'FrostlinePaintball.deps.json', 'FrostlinePaintball.pdb')) {
    $source = Join-Path $buildOutput $name
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Expected build artifact was not found: '$source'."
    }

    Copy-Item -LiteralPath $source -Destination $releasePlugin -Force
}

Write-Host "Deployable plugin copied to: $releasePlugin"
