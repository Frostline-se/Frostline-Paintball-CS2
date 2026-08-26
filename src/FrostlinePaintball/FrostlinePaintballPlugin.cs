using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.EntitySystem;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Trace;

namespace FrostlinePaintball;

[PluginMetadata(
    Id = "frostline.paintball",
    Name = "Frostline Paintball",
    Version = "S2.1.1.0",
    Author = "Frostline Port by Low",
    Description = "Colored paint splats on CS2 bullet impacts.",
    MinimumAPIVersion = "1.4.0")]
public sealed class FrostlinePaintballPlugin : BasePlugin
{
    private const string ConfigFileName = "frostline_paintball.jsonc";
    private const string ConfigSection = "FrostlinePaintball";

    private const float RadiansToDegrees = 180.0f / MathF.PI;
    private const float SurfaceTraceHalfLength = 16.0f;

    // SwiftlyS2 doesn't expose CounterStrikeSharp's `Masks.ShotBrushOnly` preset, so this
    // approximates it: solid world geometry plus the surface types a bullet can still leave
    // a mark on (windows/glass, breakable debris), while leaving Hitbox out of the mask so
    // player models are never treated as a paintable surface.
    private static readonly MaskTrace SurfaceTraceMask = MaskTrace.Solid | MaskTrace.Window | MaskTrace.Debris;

    private readonly Queue<CEnvDecal> _activeDecals = new();
    private readonly List<PaintColor> _enabledColors = [];
    private bool _spawnErrorLogged;
    private IDisposable? _configChangeSubscription;

    public PaintballConfig Config { get; private set; } = new();

    public FrostlinePaintballPlugin(ISwiftlyCore core) : base(core)
    {
    }

    public override void Load(bool hotReload)
    {
        _ = Core.Configuration
            .InitializeJsonWithModel<PaintballConfig>(ConfigFileName, ConfigSection)
            .Configure(builder => builder.AddJsonFile(
                Core.Configuration.GetConfigPath(ConfigFileName),
                optional: false,
                reloadOnChange: true));

        LoadConfigFromManager();

        // Mirrors CounterStrikeSharp's OnConfigParsed hot-reload behavior: whenever the
        // config file changes on disk, re-bind, re-clamp and refresh the enabled color list.
        _configChangeSubscription = ChangeToken.OnChange(
            () => Core.Configuration.Manager.GetReloadToken(),
            LoadConfigFromManager);
    }

    public override void Unload()
    {
        _configChangeSubscription?.Dispose();
        _configChangeSubscription = null;
        ClearDecals();
    }

    private void LoadConfigFromManager()
    {
        var config = new PaintballConfig();
        Core.Configuration.Manager.GetSection(ConfigSection).Bind(config);
        Config = config;
        NormalizeConfig();
        RefreshEnabledColors();
    }

    private void NormalizeConfig()
    {
        Config.MaxActiveDecals = Math.Clamp(Config.MaxActiveDecals, 1, 1024);
        Config.MinSize = Math.Clamp(Config.MinSize, 1.0f, 128.0f);
        Config.MaxSize = Math.Clamp(Config.MaxSize, Config.MinSize, 128.0f);
        Config.ProjectionDepth = Math.Clamp(Config.ProjectionDepth, 0.5f, 64.0f);
        Config.SurfaceOffset = Math.Clamp(Config.SurfaceOffset, 0.0f, 8.0f);
    }

    private void RefreshEnabledColors()
    {
        _enabledColors.Clear();
        _enabledColors.AddRange(Config.Colors.Where(color =>
            color.Enabled && !string.IsNullOrWhiteSpace(color.Material)));
    }

    // Attribute-driven precache hook, equivalent to CSS's Listeners.OnServerPrecacheResources.
    [EventListener<EventDelegates.OnPrecacheResource>]
    public void OnPrecacheResource(IOnPrecacheResourceEvent @event)
    {
        foreach (var material in _enabledColors
                     .Select(color => color.Material)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            @event.AddItem(material);
        }
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnRoundStart(EventRoundStart @event)
    {
        _spawnErrorLogged = false;

        if (Config.ClearOnRoundStart)
        {
            ClearDecals();
        }

        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnBulletImpact(EventBulletImpact @event)
    {
        if (!Config.Enabled || _enabledColors.Count == 0)
        {
            return HookResult.Continue;
        }

        var player = @event.UserIdPlayer;
        if (player is null || !player.IsValid || (!Config.IncludeBots && player.ServerSideClient.FakePlayer))
        {
            return HookResult.Continue;
        }

        var pawn = @event.UserIdPawn;
        var origin = pawn?.AbsOrigin;
        var camera = pawn?.CameraServices;

        if (pawn is null || !pawn.IsValid || origin is null || camera is null)
        {
            return HookResult.Continue;
        }

        var eyePosition = new Vector(origin.Value.X, origin.Value.Y, origin.Value.Z + camera.OldPlayerViewOffsetZ);
        var impactPosition = new Vector(@event.X, @event.Y, @event.Z);

        if (!TryGetProjection(pawn, eyePosition, impactPosition, out var decalPosition, out var decalAngles))
        {
            return HookResult.Continue;
        }

        var material = _enabledColors[Random.Shared.Next(_enabledColors.Count)].Material;
        var size = Config.MinSize + Random.Shared.NextSingle() * (Config.MaxSize - Config.MinSize);

        SpawnDecal(decalPosition, decalAngles, size, material);
        return HookResult.Continue;
    }

    private bool TryGetProjection(
        CCSPlayerPawn pawn,
        Vector eyePosition,
        Vector impactPosition,
        out Vector decalPosition,
        out QAngle decalAngles)
    {
        var toImpact = impactPosition - eyePosition;
        var length = toImpact.Length();

        if (length < 0.001f)
        {
            decalPosition = impactPosition;
            decalAngles = QAngle.Zero;
            return false;
        }

        var direction = toImpact / length;

        var traceStart = impactPosition - direction * SurfaceTraceHalfLength;
        var traceEnd = impactPosition + direction * SurfaceTraceHalfLength;

        var traceParams = TraceParams.Builder()
            .InteractWith(SurfaceTraceMask)
            .IgnoreEntity(pawn)
            .Build();

        var traceResult = Core.Trace.TraceShapeLine(traceStart, traceEnd, traceParams);

        if (!traceResult.DidHit)
        {
            decalPosition = impactPosition;
            decalAngles = QAngle.Zero;
            return false;
        }

        var surfacePosition = traceResult.ExactHitPoint ? traceResult.HitPoint : traceResult.EndPos;
        var normal = traceResult.HitNormal;
        var normalLength = normal.Length();

        if (normalLength < 0.5f)
        {
            decalPosition = impactPosition;
            decalAngles = QAngle.Zero;
            return false;
        }

        var unitNormal = normal / normalLength;

        decalPosition = surfacePosition + unitNormal * Config.SurfaceOffset;

        var pitch = MathF.Acos(Math.Clamp(unitNormal.Z, -1.0f, 1.0f)) * RadiansToDegrees;
        var yaw = MathF.Atan2(unitNormal.Y, unitNormal.X) * RadiansToDegrees;
        decalAngles = new QAngle(pitch, yaw, 0.0f);
        return true;
    }

    private void SpawnDecal(Vector position, QAngle angles, float size, string material)
    {
        CEnvDecal decal;
        try
        {
            // Unlike CSS's Utilities.CreateEntityByName, SwiftlyS2's CreateEntity<T>() throws
            // instead of returning null when the entity can't be created.
            decal = Core.EntitySystem.CreateEntity<CEnvDecal>();
        }
        catch (Exception exception)
        {
            LogSpawnErrorOnce(exception, "Frostline Paintball failed to create an env_decal.");
            return;
        }

        try
        {
            using var keyValues = new CEntityKeyValues();
            // SwiftlyS2 has no direct equivalent of CSS's Server.TickCount; a process-relative
            // tick counter is enough to keep these targetnames unique.
            keyValues.SetString("targetname", $"frostline_paint_{Environment.TickCount64}");
            keyValues.SetString("material", material);

            decal.Width = size;
            decal.WidthUpdated();
            decal.Height = size;
            decal.HeightUpdated();
            decal.Depth = Config.ProjectionDepth;
            decal.DepthUpdated();
            decal.RenderOrder = Config.RenderOrder;
            decal.RenderOrderUpdated();
            decal.RenderMode = RenderMode_t.kRenderNormal;
            decal.RenderModeUpdated();
            decal.ProjectOnWorld = true;
            decal.ProjectOnWorldUpdated();
            decal.ProjectOnCharacters = false;
            decal.ProjectOnCharactersUpdated();
            decal.ProjectOnWater = false;
            decal.ProjectOnWaterUpdated();

            decal.Teleport(position, angles, null);
            decal.DispatchSpawn(keyValues);

            _activeDecals.Enqueue(decal);
            TrimDecalsToLimit();
        }
        catch (Exception exception)
        {
            if (decal.IsValid)
            {
                decal.Despawn();
            }

            LogSpawnErrorOnce(exception, "Frostline Paintball failed to spawn an env_decal.");
        }
    }

    private void TrimDecalsToLimit()
    {
        while (_activeDecals.Count > Config.MaxActiveDecals)
        {
            RemoveDecal(_activeDecals.Dequeue());
        }
    }

    private void ClearDecals()
    {
        while (_activeDecals.TryDequeue(out var decal))
        {
            RemoveDecal(decal);
        }
    }

    private static void RemoveDecal(CEnvDecal decal)
    {
        if (decal.IsValid)
        {
            decal.Despawn();
        }
    }

    private void LogSpawnErrorOnce(Exception exception, string message)
    {
        if (_spawnErrorLogged)
        {
            return;
        }

        Core.Logger.LogError(exception, "{Message}", message);
        _spawnErrorLogged = true;
    }
}
