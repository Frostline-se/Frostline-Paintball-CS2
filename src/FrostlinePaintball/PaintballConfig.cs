namespace FrostlinePaintball;

// SwiftlyS2 configuration models are plain POCOs bound through the .NET Options
// pattern (Microsoft.Extensions.Configuration), so there is no CounterStrikeSharp
// `BasePluginConfig` base class to inherit from anymore.
public sealed class PaintballConfig
{
    public bool Enabled { get; set; } = true;
    public bool IncludeBots { get; set; } = true;
    public bool ClearOnRoundStart { get; set; } = true;
    public int MaxActiveDecals { get; set; } = 256;
    public float MinSize { get; set; } = 7.0f;
    public float MaxSize { get; set; } = 12.0f;
    public float ProjectionDepth { get; set; } = 5.0f;
    public float SurfaceOffset { get; set; } = 0.35f;
    public uint RenderOrder { get; set; } = 1;
    public List<PaintColor> Colors { get; set; } = PaintColor.CreateDefaults();
}

public sealed class PaintColor
{
    public string Name { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;

    public static List<PaintColor> CreateDefaults() =>
    [
        new() { Name = "Black", Material = "materials/frostline_paintball/paint_black.vmat" },
        new() { Name = "Brown", Material = "materials/frostline_paintball/paint_brown.vmat" },
        new() { Name = "Dark Green", Material = "materials/frostline_paintball/paint_dark_green.vmat" },
        new() { Name = "Golden Rod", Material = "materials/frostline_paintball/paint_golden_rod.vmat" },
        new() { Name = "Medium Slate Blue", Material = "materials/frostline_paintball/paint_medium_slate_blue.vmat" },
        new() { Name = "Olive", Material = "materials/frostline_paintball/paint_olive.vmat" },
        new() { Name = "Red Orange", Material = "materials/frostline_paintball/paint_red_orange.vmat" },
        new() { Name = "Violet", Material = "materials/frostline_paintball/paint_violet.vmat" },
        new() { Name = "Baby Blue", Material = "materials/frostline_paintball/paint_baby_blue.vmat" },
        new() { Name = "Blue", Material = "materials/frostline_paintball/paint_blue.vmat" },
        new() { Name = "Red", Material = "materials/frostline_paintball/paint_red.vmat" },
        new() { Name = "White", Material = "materials/frostline_paintball/paint_white.vmat" },
        new() { Name = "Lime Green", Material = "materials/frostline_paintball/paint_lime_green.vmat" },
        new() { Name = "Purple", Material = "materials/frostline_paintball/paint_purple.vmat" }
    ];
}
