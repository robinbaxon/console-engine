using System;

namespace EternalDigger.Rendering;

/// <summary>24-bit colours as <c>0xRRGGBB</c>: the 16 console colours, and scaling a colour by a light.</summary>
public static class Rgb
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const uint Black = 0x000000;

    /// <summary>The Windows console's default colours, in <see cref="ConsoleColor"/> order.</summary>
    private static readonly uint[] ConsoleColors =
    [
        0x000000, 0x0037DA, 0x13A10E, 0x3A96DD, 0xC50F1F, 0x881798, 0xC19C00, 0xCCCCCC,
        0x767676, 0x3B78FF, 0x16C60C, 0x61D6D6, 0xE74856, 0xB4009E, 0xF9F1A5, 0xF2F2F2
    ];

    //**********************************************************
    //** public methods
    //**********************************************************

    public static uint Of(ConsoleColor color) => ConsoleColors[(int)color];

    /// <summary>Each channel times its own factor, clamped to 0–255.</summary>
    public static uint Scale(uint rgb, float red, float green, float blue) =>
        (Channel((rgb >> 16) & 0xFF, red) << 16) | (Channel((rgb >> 8) & 0xFF, green) << 8) | Channel(rgb & 0xFF, blue);

    public static uint Scale(uint rgb, float factor) => Scale(rgb, factor, factor, factor);

    //**********************************************************
    //** internals
    //**********************************************************

    private static uint Channel(uint value, float factor) => (uint)Math.Clamp(value * factor + .5f, 0f, 255f);
}
