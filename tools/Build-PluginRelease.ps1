param(
    [string]$Configuration = 'Release',

    # Optional: path to an alternate BuildConfig.jsonc. Defaults to the one next to this script.
    [string]$ConfigPath
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'BuildConfig.ps1')
$config = if ($ConfigPath) { Get-FrostlineBuildConfig -ConfigPath $ConfigPath } else { Get-FrostlineBuildConfig }

$projectRoot = Split-Path -Parent $PSScriptRoot
# The .csproj lives at the repo root (src\<PluginFolderName>\ only holds the .cs files).
$projectFile = Join-Path $projectRoot "$($config.PluginFolderName).csproj"
# FrostlinePaintball.csproj overrides <OutputPath> to build\ (regardless of $Configuration)
# and net10.0 gets appended for the target framework, so output always lands in build\net10.0.
$buildOutput = Join-Path $projectRoot "build\net10.0"
# SwiftlyS2 loads plugins from game\csgo\addons\swiftlys2\plugins\<FolderName>\<FolderName>.dll,
# where FolderName must match the compiled assembly name.
$releasePlugin = Join-Path $projectRoot (Join-Path $config.ReleaseDir (Join-Path $config.ReleasePluginParentSubdir $config.PluginFolderName))

dotnet build $projectFile -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Plugin build failed with exit code $LASTEXITCODE."
}

New-Item -ItemType Directory -Path $releasePlugin -Force | Out-Null

$releaseRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $config.ReleaseDir))
$resolvedPlugin = [System.IO.Path]::GetFullPath($releasePlugin)
if (-not $resolvedPlugin.StartsWith($releaseRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean an output path outside the project release directory: '$resolvedPlugin'."
}

Get-ChildItem -LiteralPath $releasePlugin -File | Remove-Item -Force
$releasePluginResources = Join-Path $releasePlugin 'resources'
if (Test-Path -LiteralPath $releasePluginResources) {
    Remove-Item -LiteralPath $releasePluginResources -Recurse -Force
}

$assemblyName = $config.PluginFolderName
foreach ($name in @("$assemblyName.dll", "$assemblyName.deps.json", "$assemblyName.pdb")) {
    $source = Join-Path $buildOutput $name
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Expected build artifact was not found: '$source'."
    }

    Copy-Item -LiteralPath $source -Destination $releasePlugin -Force
}

# resources\ (e.g. translations\en.jsonc) is copied to the output dir by the .csproj's
# "<None Update="resources\**\*.*" CopyToOutputDirectory="PreserveNewest" />" rule, so it
# lives alongside the dll in $buildOutput and needs to ship with the plugin too.
$sourceResources = Join-Path $buildOutput 'resources'
if (-not (Test-Path -LiteralPath $sourceResources)) {
    throw "Expected build artifact was not found: '$sourceResources'."
}
Copy-Item -LiteralPath $sourceResources -Destination $releasePlugin -Recurse -Force

Write-Host "Deployable plugin copied to: $releasePlugin"