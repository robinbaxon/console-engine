using System;

namespace EternalDigger.Rendering;

/// <summary>
/// 24-bit colours as <c>0xRRGGBB</c>: the 16 console colours, scaling a colour by a light, mixing two colours and
/// greying a colour out.
/// </summary>
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

    /// <summary>
    /// <paramref name="from"/> blended towards <paramref name="to"/> by <paramref name="alpha"/> (0: from, 1: to),
    /// channel by channel: <paramref name="to"/> laid over <paramref name="from"/> with that opacity.
    /// </summary>
    public static uint Mix(uint from, uint to, float alpha)
    {
        alpha = Math.Clamp(alpha, 0f, 1f);
        return (Blend((from >> 16) & 0xFF, (to >> 16) & 0xFF, alpha) << 16) |
               (Blend((from >> 8) & 0xFF, (to >> 8) & 0xFF, alpha) << 8) |
               Blend(from & 0xFF, to & 0xFF, alpha);
    }

    /// <summary>The brightness of a colour, 0–255, weighted as the eye sees it (Rec. 601).</summary>
    public static float Luma(uint rgb) =>
        .299f * ((rgb >> 16) & 0xFF) + .587f * ((rgb >> 8) & 0xFF) + .114f * (rgb & 0xFF);

    /// <summary>
    /// A colour greyed out by <paramref name="amount"/> (0: unchanged, 1: the grey of the same <see cref="Luma"/>).
    /// </summary>
    public static uint Grey(uint rgb, float amount)
    {
        var grey = (uint)Math.Clamp(Luma(rgb) + .5f, 0f, 255f);
        return Mix(rgb, (grey << 16) | (grey << 8) | grey, amount);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static uint Channel(uint value, float factor) => (uint)Math.Clamp(value * factor + .5f, 0f, 255f);

    private static uint Blend(uint from, uint to, float alpha) =>
        (uint)Math.Clamp(from + ((float)to - from) * alpha + .5f, 0f, 255f);
}
