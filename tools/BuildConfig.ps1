# Shared config loader for the Frostline Paintball build/tooling scripts.
# Dot-source this file and call Get-FrostlineBuildConfig to read
# tools\BuildConfig.jsonc (or a custom path), with built-in fallbacks for any
# field that's missing or blank so the scripts keep working even if the config
# file is deleted.

function Get-FrostlineBuildConfig {
    param(
        [string]$ConfigPath = (Join-Path $PSScriptRoot 'BuildConfig.jsonc')
    )

    # Values used if the config file is missing entirely, or a field in it is
    # missing/blank. These match what the scripts hardcoded before this file existed.
    $defaults = [ordered]@{
        Cs2Root                   = ''
        AddonName                 = 'frostline_paintball_assets'
        PluginFolderName          = 'FrostlinePaintball'
        ReleaseDir                = 'release'
        ReleasePluginParentSubdir = 'server\addons\counterstrikesharp\plugins'
        ReleaseWorkshopSubdir     = 'workshop-addon'
    }

    $config = [ordered]@{}
    foreach ($key in $defaults.Keys) {
        $config[$key] = $defaults[$key]
    }

    if (Test-Path -LiteralPath $ConfigPath) {
        # Strip // line comments so jsonc can be read with ConvertFrom-Json.
        $raw = Get-Content -LiteralPath $ConfigPath -Raw
        $stripped = ($raw -split "`n" | ForEach-Object {
            $_ -replace '(?<!:)//.*$', ''
        }) -join "`n"

        try {
            $parsed = $stripped | ConvertFrom-Json
        }
        catch {
            throw "Failed to parse config file '$ConfigPath': $($_.Exception.Message)"
        }

        foreach ($key in @($defaults.Keys)) {
            $value = $parsed.$key
            if ($null -ne $value -and $value -ne '') {
                $config[$key] = $value
            }
        }
    }

    return [pscustomobject]$config
}
