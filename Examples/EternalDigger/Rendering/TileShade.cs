using System;
using EternalDigger.Mining;
using Microsoft.Xna.Framework;

namespace EternalDigger.Rendering;

/// <summary>A tile's 24-bit colours at full light: texture, fill and ore speck (<c>0xRRGGBB</c>).</summary>
public readonly record struct TileRgb(uint Foreground, uint Background, uint Speck = 0);

/// <summary>
/// The colours of the mine in 24-bit colour: a tile's colour times the colour of the light on it
/// (<see cref="LightMap.LightColor"/>: white daylight, the warm lamp, coloured point lights).
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

    /// <summary>How much of its colour remembered rock loses (<see cref="Memory"/>): 0 keeps it, 1 is grey.</summary>
    public const float MemoryGrey = .6f;

    /// <summary>How bright remembered rock is, as a share of its texture colour at full light.</summary>
    public const float MemoryBrightness = .5f;

    /// <summary>How much of its colour a tile in or beside black damp loses (<see cref="Choked"/>).</summary>
    public const float ChokedGrey = .75f;

    /// <summary>How bright a tile in or beside black damp is, as a share of its lit colour.</summary>
    public const float ChokedBrightness = .45f;

    /// <summary>A ladder that was seen before and is dark now.</summary>
    public const uint RememberedLadder = 0x4A4436;

    /// <summary>Ore keeps this much of its colour in the faintest light, so a lit vein always stands out.</summary>
    public const float SpeckFloor = .35f;

    /// <summary>Below this light a crack is drawn light on the dark rock instead of black on the fill.</summary>
    public const float LightCrackBelow = .45f;

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
    /// <paramref name="rgb"/> under a <paramref name="light"/> colour (red, green and blue from 0 to 1): each channel
    /// times the light's.
    /// </summary>
    public static uint Lit(uint rgb, Vector3 light) => Rgb.Scale(rgb, light.X, light.Y, light.Z);

    /// <summary>An ore speck: brighter than its rock in faint light, never below <see cref="SpeckFloor"/>.</summary>
    public static uint Speck(uint rgb, Vector3 light) =>
        Lit(rgb, new Vector3(SpeckFloor) + light * (1f - SpeckFloor));

    /// <summary>
    /// Rock that was seen before and is dark now: a faint, greyed memory of its texture colour
    /// (<see cref="TileRgb.Foreground"/>), so dirt is remembered brown and slate blue. Never an ore's speck colour.
    /// </summary>
    public static uint Memory(uint rgb) => Rgb.Scale(Rgb.Grey(rgb, MemoryGrey), MemoryBrightness);

    /// <summary>A lit colour in or beside black damp: darker and greyer, as if the bad air soaks up the light.</summary>
    public static uint Choked(uint rgb) => Rgb.Scale(Rgb.Grey(rgb, ChokedGrey), ChokedBrightness);

    /// <summary>A crack over <paramref name="background"/>: black on a well-lit fill, grey on dark rock.</summary>
    public static uint Crack(uint background, float intensity) =>
        background == Rgb.Black || intensity < LightCrackBelow
            ? Rgb.Scale(Rgb.Of(ConsoleColor.Gray), Math.Min(1f, intensity + .3f))
            : Rgb.Black;
}
