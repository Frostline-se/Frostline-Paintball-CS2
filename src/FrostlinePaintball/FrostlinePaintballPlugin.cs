using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace FrostlinePaintball;

[MinimumApiVersion(372)]
public sealed class FrostlinePaintballPlugin : BasePlugin, IPluginConfig<PaintballConfig>
{
    private const float RadiansToDegrees = 180.0f / MathF.PI;
    private const float SurfaceTraceHalfLength = 16.0f;

    private static readonly TraceOptions SurfaceTraceOptions = new()
    {
        InteractsWith = Masks.ShotBrushOnly
    };

    private readonly Queue<CEnvDecal> _activeDecals = new();
    private readonly List<PaintColor> _enabledColors = [];
    private bool _spawnErrorLogged;

    public override string ModuleName => "Frostline Paintball";
    public override string ModuleVersion => "1.1.0";
    public override string ModuleAuthor => "Frostline";
    public override string ModuleDescription => "Colored paint splats on CS2 bullet impacts.";

    public PaintballConfig Config { get; set; } = new();

    public override void Load(bool hotReload)
    {
        RefreshEnabledColors();

        RegisterListener<Listeners.OnServerPrecacheResources>(OnServerPrecacheResources);
        RegisterEventHandler<EventBulletImpact>(OnBulletImpact);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
    }

    public override void Unload(bool hotReload)
    {
        RemoveListener<Listeners.OnServerPrecacheResources>(OnServerPrecacheResources);
        DeregisterEventHandler<EventBulletImpact>(OnBulletImpact);
        DeregisterEventHandler<EventRoundStart>(OnRoundStart);
        ClearDecals();
    }

    public void OnConfigParsed(PaintballConfig config)
    {
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

    private void OnServerPrecacheResources(ResourceManifest manifest)
    {
        foreach (var material in _enabledColors
                     .Select(color => color.Material)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            manifest.AddResource(material);
        }
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        _spawnErrorLogged = false;

        if (Config.ClearOnRoundStart)
        {
            ClearDecals();
        }

        return HookResult.Continue;
    }

    private HookResult OnBulletImpact(EventBulletImpact @event, GameEventInfo info)
    {
        if (!Config.Enabled || _enabledColors.Count == 0)
        {
            return HookResult.Continue;
        }

        var player = @event.Userid;
        if (player is null || !player.IsValid || (!Config.IncludeBots && player.IsBot))
        {
            return HookResult.Continue;
        }

        var pawn = player.PlayerPawn.Value;
        var origin = pawn?.AbsOrigin;
        var camera = pawn?.CameraServices;

        if (pawn is null || !pawn.IsValid || origin is null || camera is null)
        {
            return HookResult.Continue;
        }

        var eyePosition = new Vector(origin.X, origin.Y, origin.Z + camera.OldPlayerViewOffsetZ);
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
        var dx = impactPosition.X - eyePosition.X;
        var dy = impactPosition.Y - eyePosition.Y;
        var dz = impactPosition.Z - eyePosition.Z;
        var length = MathF.Sqrt(dx * dx + dy * dy + dz * dz);

        if (length < 0.001f)
        {
            decalPosition = impactPosition;
            decalAngles = new QAngle(0.0f, 0.0f, 0.0f);
            return false;
        }

        var directionX = dx / length;
        var directionY = dy / length;
        var directionZ = dz / length;

        var traceStart = new Vector(
            impactPosition.X - directionX * SurfaceTraceHalfLength,
            impactPosition.Y - directionY * SurfaceTraceHalfLength,
            impactPosition.Z - directionZ * SurfaceTraceHalfLength);
        var traceEnd = new Vector(
            impactPosition.X + directionX * SurfaceTraceHalfLength,
            impactPosition.Y + directionY * SurfaceTraceHalfLength,
            impactPosition.Z + directionZ * SurfaceTraceHalfLength);
        var traceResult = Trace.TraceEndShape(traceStart, traceEnd, pawn, SurfaceTraceOptions);

        if (!traceResult.DidHit())
        {
            decalPosition = impactPosition;
            decalAngles = new QAngle(0.0f, 0.0f, 0.0f);
            return false;
        }

        var surfacePosition = traceResult.HasExactHitPoint
            ? traceResult.HitPoint
            : traceResult.EndPos;
        var normal = traceResult.Normal;
        var normalLength = MathF.Sqrt(normal.X * normal.X + normal.Y * normal.Y + normal.Z * normal.Z);

        if (normalLength < 0.5f)
        {
            decalPosition = impactPosition;
            decalAngles = new QAngle(0.0f, 0.0f, 0.0f);
            return false;
        }

        var normalX = normal.X / normalLength;
        var normalY = normal.Y / normalLength;
        var normalZ = normal.Z / normalLength;

        decalPosition = new Vector(
            surfacePosition.X + normalX * Config.SurfaceOffset,
            surfacePosition.Y + normalY * Config.SurfaceOffset,
            surfacePosition.Z + normalZ * Config.SurfaceOffset);

        var pitch = MathF.Acos(Math.Clamp(normalZ, -1.0f, 1.0f)) * RadiansToDegrees;
        var yaw = MathF.Atan2(normalY, normalX) * RadiansToDegrees;
        decalAngles = new QAngle(pitch, yaw, 0.0f);
        return true;
    }

    private void SpawnDecal(Vector position, QAngle angles, float size, string material)
    {
        var decal = Utilities.CreateEntityByName<CEnvDecal>("env_decal");
        if (decal is null)
        {
            LogSpawnErrorOnce("CS2 did not create env_decal.");
            return;
        }

        try
        {
            using var keyValues = new CEntityKeyValues();
            keyValues.SetString("targetname", $"frostline_paint_{Server.TickCount}");
            keyValues.SetString("material", material);

            decal.Width = size;
            decal.Height = size;
            decal.Depth = Config.ProjectionDepth;
            decal.RenderOrder = Config.RenderOrder;
            decal.RenderMode = RenderMode_t.kRenderNormal;
            decal.ProjectOnWorld = true;
            decal.ProjectOnCharacters = false;
            decal.ProjectOnWater = false;

            decal.Teleport(position, angles, null);
            decal.DispatchSpawn(keyValues);

            _activeDecals.Enqueue(decal);
            TrimDecalsToLimit();
        }
        catch (Exception exception)
        {
            if (decal.IsValid)
            {
                decal.Remove();
            }

            if (!_spawnErrorLogged)
            {
                Logger.LogError(exception, "Frostline Paintball failed to spawn an env_decal.");
                _spawnErrorLogged = true;
            }
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
            decal.Remove();
        }
    }

    private void LogSpawnErrorOnce(string message)
    {
        if (_spawnErrorLogged)
        {
            return;
        }

        Logger.LogError("{Message}", message);
        _spawnErrorLogged = true;
    }
}
