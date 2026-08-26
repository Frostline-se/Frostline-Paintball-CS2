param(
    [string]$Configuration = 'Release',

    # Optional: path to an alternate BuildConfig.jsonc. Defaults to the one next to this script.
    [string]$ConfigPath
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'BuildConfig.ps1')
$config = if ($ConfigPath) { Get-FrostlineBuildConfig -ConfigPath $ConfigPath } else { Get-FrostlineBuildConfig }

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot "src\$($config.PluginFolderName)\$($config.PluginFolderName).csproj"
$buildOutput = Join-Path $projectRoot "src\$($config.PluginFolderName)\bin\$Configuration\net10.0"
# CounterStrikeSharp loads plugins from addons\counterstrikesharp\plugins\<FolderName>\<FolderName>.dll,
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

$assemblyName = $config.PluginFolderName
foreach ($name in @("$assemblyName.dll", "$assemblyName.deps.json", "$assemblyName.pdb")) {
    $source = Join-Path $buildOutput $name
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Expected build artifact was not found: '$source'."
    }

    Copy-Item -LiteralPath $source -Destination $releasePlugin -Force
}

Write-Host "Deployable plugin copied to: $releasePlugin"
