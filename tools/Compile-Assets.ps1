param(
    [string]$Cs2Root,

    # Optional: path to an alternate BuildConfig.jsonc. Defaults to the one next to this script.
    [string]$ConfigPath
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'BuildConfig.ps1')
$config = if ($ConfigPath) { Get-FrostlineBuildConfig -ConfigPath $ConfigPath } else { Get-FrostlineBuildConfig }

# -Cs2Root on the command line always wins; otherwise fall back to BuildConfig.jsonc.
if (-not $Cs2Root) {
    $Cs2Root = $config.Cs2Root
}
if (-not $Cs2Root) {
    throw "Cs2Root was not provided. Pass -Cs2Root, or set it in $(Join-Path $PSScriptRoot 'BuildConfig.jsonc')."
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$contentSource = Join-Path $projectRoot 'assets\content'
$addonName = $config.AddonName
$contentRoot = Join-Path $Cs2Root "content\csgo_addons\$addonName"
$gameRoot = Join-Path $Cs2Root "game\csgo_addons\$addonName"
$compiler = Join-Path $Cs2Root 'game\bin\win64\resourcecompiler.exe'

function Ensure-GeneratedDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$AllowedRoot
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $fullAllowedRoot = [System.IO.Path]::GetFullPath($AllowedRoot).TrimEnd('\') + '\'

    if (-not $fullPath.StartsWith($fullAllowedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to write generated path outside '$fullAllowedRoot': '$fullPath'."
    }

    # Additive: create the folder if it doesn't exist yet, but never wipe out
    # whatever's already in there. Copy-Item -Force below will overwrite files
    # that collide by name and add anything new; it won't touch unrelated files.
    New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
}

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "resourcecompiler.exe was not found below '$Cs2Root'. Install CS2 Workshop Tools."
}

if (-not (Test-Path -LiteralPath $contentSource)) {
    throw "Asset source directory was not found: '$contentSource'."
}

Ensure-GeneratedDirectory -Path $contentRoot -AllowedRoot (Join-Path $Cs2Root 'content\csgo_addons')
Ensure-GeneratedDirectory -Path $gameRoot -AllowedRoot (Join-Path $Cs2Root 'game\csgo_addons')
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

$releaseAssets = Join-Path $projectRoot (Join-Path $config.ReleaseDir $config.ReleaseWorkshopSubdir)
Ensure-GeneratedDirectory -Path $releaseAssets -AllowedRoot (Join-Path $projectRoot $config.ReleaseDir)
Copy-Item -LiteralPath (Join-Path $contentSource 'addoninfo.txt') -Destination $releaseAssets -Force
Copy-Item -LiteralPath (Join-Path $gameRoot 'materials') -Destination $releaseAssets -Recurse -Force

Write-Host "Compiled assets copied to: $releaseAssets"