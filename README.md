# Frostline Paintball for CS2

Frostline Paintball is a CounterStrikeSharp plugin that turns normal bullet impacts into colored paint splats in Counter-Strike 2.

Like the classic [Paintball plugin for Counter-Strike: Source and CS:GO](https://forums.alliedmods.net/showthread.php?t=107012), it listens for every `bullet_impact` and places a randomly colored decal at the hit position. This version was rebuilt for Source 2 using `env_decal` entities and CounterStrikeSharp surface tracing, allowing splats to align correctly on floors, walls, ceilings and slopes.

## Preview

![Frostline Paintball bullet impact decals in CS2](docs/images/paintball-preview.png)

## Features

- Colored paint splats on bullet impacts
- Correct alignment on floors, walls, ceilings and slopes
- Random color and configurable size for every impact
- 14 included colors
- Configurable maximum number of active decals
- Oldest decals are removed first when the limit is reached
- Optional cleanup at the beginning of every round
- Automatically generated CounterStrikeSharp configuration
- Optional bot support

## Included colors

Baby Blue, Black, Blue, Brown, Dark Green, Golden Rod, Lime Green, Medium Slate Blue, Olive, Purple, Red, Red Orange, Violet and White.

Each color can be enabled or disabled separately in the configuration file.

## Requirements

- Metamod:Source
- CounterStrikeSharp API 372 or newer with .NET 10 support
- MultiAddonManager
- The Frostline Paintball material addon mounted through the CS2 Workshop

CS2 does not download arbitrary custom materials directly from a game server. The included Source 2 materials must therefore be delivered through a Workshop addon and mounted for the server and connecting players.

## Installation

1. Download `Frostline-Paintball-CS2.zip` from the latest GitHub release.
2. Copy the contents of the `server` directory into the server's `game/csgo` directory.
3. Mount the Frostline Paintball Workshop asset addon with MultiAddonManager. If the public Workshop item is not available, build and publish your own copy from `assets/content` using the CS2 Workshop Tools.
4. Add the Workshop item ID to:

```text
game/csgo/cfg/multiaddonmanager/multiaddonmanager.cfg
```

Example:

```text
mm_extra_addons "YOUR_WORKSHOP_ID"
```

5. Restart the server or change the map after mounting the addon so the materials are precached.

The plugin files should end up in:

```text
game/csgo/addons/counterstrikesharp/plugins/FrostlinePaintball/
```

## Configuration

CounterStrikeSharp creates the configuration automatically after the plugin starts:

```text
game/csgo/addons/counterstrikesharp/configs/plugins/FrostlinePaintball/FrostlinePaintball.json
```

| Setting | Default | Description |
| --- | ---: | --- |
| `Enabled` | `true` | Enables or disables the plugin. |
| `IncludeBots` | `true` | Creates paint splats for bot shots. |
| `ClearOnRoundStart` | `true` | Removes all paint splats when a new round starts. |
| `MaxActiveDecals` | `256` | Maximum number of paint splats kept at the same time. |
| `MinSize` | `7.0` | Smallest possible random splat size. |
| `MaxSize` | `12.0` | Largest possible random splat size. |
| `ProjectionDepth` | `5.0` | Depth of the projected decal volume. |
| `SurfaceOffset` | `0.35` | Distance between the decal and the hit surface. |
| `RenderOrder` | `1` | Decal rendering order. |

Set `MinSize` and `MaxSize` to the same value if every paint splat should have an identical size.

Decals are removed at the start of each round by default. If more than `MaxActiveDecals` impacts exist during a round, the oldest decal is removed whenever a new one is created.

## Building the plugin

```powershell
.\tools\Build-PluginRelease.ps1
```

The deployable plugin is written to:

```text
release/server/addons/counterstrikesharp/plugins/FrostlinePaintball/
```

## Building the Source 2 materials

Install the Counter-Strike 2 Workshop Tools and run:

```powershell
.\tools\Compile-Assets.ps1 -Cs2Root "G:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive"
```

The script creates a local Workshop Tools addon named `frostline_paintball_assets` and places a portable compiled copy in `release/workshop-addon`.

## Troubleshooting

### Missing `.vmat_c` resources

The Workshop addon is not mounted, has not finished downloading or contains an older build. Verify the Workshop ID in MultiAddonManager, update the Workshop item and restart the server or change the map.

### Colored rectangles instead of paint splats

The client is loading an outdated material build. Update the Workshop addon and make sure old compiled texture files are not included.

### Plugin does not load

Update CounterStrikeSharp to API 372 or newer. The surface tracing used by the plugin is provided by CounterStrikeSharp.

## Credits

Inspired by the original [SourceMod Paintball plugin](https://forums.alliedmods.net/showthread.php?t=107012) for Counter-Strike: Source and CS:GO.

## License

MIT
