using System;
using EternalDigger.Mining;

namespace EternalDigger.Rendering;

/// <summary>A tile's 24-bit colours at full light: texture, fill and ore speck (<c>0xRRGGBB</c>).</summary>
public readonly record struct TileRgb(uint Foreground, uint Background, uint Speck = 0);

/// <summary>
/// The colours of the mine in 24-bit colour: a tile's colour under the <see cref="LightMap"/>'s smooth light
/// (<see cref="LightMap.Intensity"/>), warm where the lamp or a lantern lights it and neutral in daylight.
/// </summary>
public static class TileShade
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>The cave's back wall behind dug-out tiles, at full light.</summary>
    public const uint BackWall = 0x40342A;

    /// <summary>The faint dots on the back wall, at full light.</summary>
    public const uint BackWallDot = 0x8A8076;

    /// <summary>Rock that was seen before and is dark now: a cool grey stipple.</summary>
    public const uint Remembered = 0x4A4E57;

    /// <summary>A ladder that was seen before and is dark now.</summary>
    public const uint RememberedLadder = 0x4A4436;

    /// <summary>Ore keeps this much of its colour in the faintest light, so a lit vein always stands out.</summary>
    public const float SpeckFloor = .35f;

    /// <summary>Below this light a crack is drawn light on the dark rock instead of black on the fill.</summary>
    public const float LightCrackBelow = .45f;

    /// <summary>What lamp light keeps of green and blue: a warm oil flame.</summary>
    private const float WarmGreen = .86f;
    private const float WarmBlue = .62f;

    private static readonly TileRgb Dirt = new(0x8A5A2E, 0x5C3A1E);
    private static readonly TileRgb Stone = new(0x9A9A96, 0x55565A);
    private static readonly TileRgb Slate = new(0x4F72A6, 0x243650);

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>The colours of a tile type. Ores are their host rock with a bright speck.</summary>
    public static TileRgb For(TileType type) => type switch
    {
        TileType.Stone => Stone,
        TileType.Granite => new(0x8E6E86, 0x4A4450),
        TileType.Copper => Dirt with { Speck = 0xE0783A },
        TileType.Silver => Stone with { Speck = 0xE8EEF5 },
        TileType.Gold => Stone with { Speck = 0xFFD23A },
        TileType.Bedrock => new(0x3A3A3E, 0x18181A),
        TileType.Obsidian => new(0x5A2E6E, 0x120A18, 0xC070F0),
        TileType.Slate => Slate,
        TileType.Quartzite => new(0x7AC8C8, 0x2E6468),
        TileType.Gem => Slate with { Speck = 0xF040D0 },
        TileType.Fossil => Stone with { Speck = 0xF4EEDC },
        TileType.Relic => Slate with { Speck = 0xFFD23A },
        _ => Dirt
    };

    /// <summary>
    /// <paramref name="rgb"/> under a light of <paramref name="intensity"/> (0–1). <paramref name="warmth"/> is the
    /// lamp's share of that light (0 daylight, 1 lamp only).
    /// </summary>
    public static uint Lit(uint rgb, float intensity, float warmth) => Rgb.Scale(rgb, intensity,
        intensity * (1f - warmth * (1f - WarmGreen)), intensity * (1f - warmth * (1f - WarmBlue)));

    /// <summary>An ore speck: brighter than its rock in faint light.</summary>
    public static uint Speck(uint rgb, float intensity, float warmth) =>
        Lit(rgb, SpeckFloor + (1f - SpeckFloor) * intensity, warmth);

    /// <summary>A crack over <paramref name="background"/>: black on a well-lit fill, grey on dark rock.</summary>
    public static uint Crack(uint background, float intensity) =>
        background == Rgb.Black || intensity < LightCrackBelow
            ? Rgb.Scale(Rgb.Of(ConsoleColor.Gray), Math.Min(1f, intensity + .3f))
            : Rgb.Black;
}
