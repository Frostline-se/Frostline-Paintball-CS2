param(
    [Parameter(Mandatory = $true)]
    [string]$Cs2Root
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$contentSource = Join-Path $projectRoot 'assets\content'
$addonName = 'frostline_paintball_assets'
$contentRoot = Join-Path $Cs2Root "content\csgo_addons\$addonName"
$gameRoot = Join-Path $Cs2Root "game\csgo_addons\$addonName"
$compiler = Join-Path $Cs2Root 'game\bin\win64\resourcecompiler.exe'

function Reset-GeneratedDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$AllowedRoot
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $fullAllowedRoot = [System.IO.Path]::GetFullPath($AllowedRoot).TrimEnd('\') + '\'

    if (-not $fullPath.StartsWith($fullAllowedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean generated path outside '$fullAllowedRoot': '$fullPath'."
    }

    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }

    New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
}

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "resourcecompiler.exe was not found below '$Cs2Root'. Install CS2 Workshop Tools."
}

if (-not (Test-Path -LiteralPath $contentSource)) {
    throw "Asset source directory was not found: '$contentSource'."
}

Reset-GeneratedDirectory -Path $contentRoot -AllowedRoot (Join-Path $Cs2Root 'content\csgo_addons')
Reset-GeneratedDirectory -Path $gameRoot -AllowedRoot (Join-Path $Cs2Root 'game\csgo_addons')
Copy-Item -LiteralPath (Join-Path $contentSource 'addoninfo.txt') -Destination $contentRoot -Force
Copy-Item -LiteralPath (Join-Path $contentSource 'materials') -Destination $contentRoot -Recurse -Force

$materials = Join-Path $contentRoot 'materials\frostline_paintball\*.vmat'
& $compiler -i $materials -game (Join-Path $Cs2Root 'game\csgo') -f -nop4
if ($LASTEXITCODE -ne 0) {
    throw "Source 2 asset compilation failed with exit code $LASTEXITCODE."
}

$compiledMaterials = Join-Path $gameRoot 'materials\frostline_paintball'
if (-not (Test-Path -LiteralPath $compiledMaterials)) {
    throw "Compilation completed but no output appeared in '$compiledMaterials'."
}

$releaseAssets = Join-Path $projectRoot 'release\workshop-addon'
Reset-GeneratedDirectory -Path $releaseAssets -AllowedRoot (Join-Path $projectRoot 'release')
Copy-Item -LiteralPath (Join-Path $contentSource 'addoninfo.txt') -Destination $releaseAssets -Force
Copy-Item -LiteralPath (Join-Path $gameRoot 'materials') -Destination $releaseAssets -Recurse -Force

Write-Host "Compiled assets copied to: $releaseAssets"
