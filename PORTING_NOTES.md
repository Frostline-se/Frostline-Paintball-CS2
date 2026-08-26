# Porting notes: CounterStrikeSharp → SwiftlyS2

This plugin was ported from CounterStrikeSharp to [SwiftlyS2](https://swiftlys2.net) following
the official [porting guide](https://swiftlys2.net/docs/guides/porting-from-css/). Only the
`.cs` source and `.csproj` changed — the Source 2 materials, `addoninfo.txt`, docs and build
tools are untouched.

## What changed and why

- **Project file**: `net10.0` target with `AllowUnsafeBlocks`, referencing `SwiftlyS2.CS2`
  instead of `CounterStrikeSharp.API`, matching SwiftlyS2's official plugin template
  (including the publish/zip target).
- **Plugin shell**: `BasePlugin(ISwiftlyCore core)` with `[PluginMetadata(...)]` instead of
  CSS's `BasePlugin` + override properties. `Unload()` no longer takes a `hotReload` parameter.
- **Config**: SwiftlyS2 has no `IPluginConfig<T>`/`OnConfigParsed` equivalent. Config is now
  loaded through `Core.Configuration.InitializeJsonWithModel<PaintballConfig>(...)`, a plain
  POCO (no more `BasePluginConfig` base class), and hot-reloaded manually via
  `ChangeToken.OnChange(...)` on the configuration reload token — this reproduces the same
  "edit the file, it re-applies live" behavior the CSS version had through `OnConfigParsed`.
  The generated file is `frostline_paintball.jsonc` under the plugin's config directory
  (section `FrostlinePaintball`), replacing CSS's
  `configs/plugins/FrostlinePaintball/FrostlinePaintball.json`.
- **Events**: `bullet_impact`/`round_start` are now handled with the attribute-based
  `[GameEventHandler(HookMode.Post)]` recommended by SwiftlyS2, and no longer need manual
  `RegisterEventHandler`/`DeregisterEventHandler` calls in `Load`/`Unload`. Precache is now
  `[EventListener<EventDelegates.OnPrecacheResource>]` instead of
  `Listeners.OnServerPrecacheResources`.
- **Player/pawn access**: `EventBulletImpact` now exposes `UserIdPlayer`/`UserIdPawn` directly
  (typed), so there's no more manual "resolve pawn from controller" step. Bot detection moved
  from `player.IsBot` to `player.ServerSideClient.FakePlayer`, since SwiftlyS2 doesn't surface
  `IsBot` directly on `IPlayer`.
- **Entity creation**: `Core.EntitySystem.CreateEntity<CEnvDecal>()` replaces
  `Utilities.CreateEntityByName<CEnvDecal>("env_decal")`. Important difference: SwiftlyS2's
  `CreateEntity<T>()` **throws** on failure instead of returning `null`, so the "couldn't
  create decal" path is now a `try/catch` instead of a null check. Schema fields
  (`Width`, `Height`, `Depth`, `RenderOrder`, `RenderMode`, `ProjectOnWorld`, etc.) are set the
  same way as before, but each is followed by its `...Updated()` call
  (e.g. `decal.Width = size; decal.WidthUpdated();`), which is how SwiftlyS2 replaces CSS's
  `Utilities.SetStateChanged`. `Remove()` became `Despawn()`.
- **Tracing**: `Trace.TraceEndShape(...)` became `Core.Trace.TraceShapeLine(start, end,
  TraceParams)`. SwiftlyS2 has no `Masks.ShotBrushOnly` preset, so the trace mask is
  approximated as `MaskTrace.Solid | MaskTrace.Window | MaskTrace.Debris` (deliberately
  excluding `Hitbox`, and ignoring the shooter's own pawn via `TraceParamsBuilder.IgnoreEntity`)
  to keep decals landing on world geometry/glass/debris rather than on players. If your server
  needs a closer match to CS2's actual bullet-impact surface mask, tune `SurfaceTraceMask` in
  `FrostlinePaintballPlugin.cs`.
- **Vector/QAngle math**: SwiftlyS2's `Vector`/`QAngle` are plain structs with operator
  overloads and a `Length()`/`Normalized()` helper, so the projection math was simplified to use
  those instead of manual per-component arithmetic — the resulting angles/positions are
  computed with the same formulas as the original (surface-normal-based pitch/yaw), just with
  less boilerplate.

## Not yet verified

This port was written and cross-checked line-by-line against the SwiftlyS2 source
(`swiftly-solution/swiftlys2` on GitHub) and its plugin template/examples, but it has **not**
been compiled or run on a live server (NuGet isn't reachable from this environment). Before
deploying:

1. Run `dotnet publish` and fix any compiler errors from API drift since this was written.
2. Confirm `SurfaceTraceMask` produces decal placement you're happy with — it's an
   approximation of CSS's `Masks.ShotBrushOnly`, not a byte-for-byte equivalent.
3. Double check the generated `frostline_paintball.jsonc` path/section matches where you expect
   config on your server layout.
