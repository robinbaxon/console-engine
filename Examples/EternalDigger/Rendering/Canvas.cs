using System;
using ConsoleEngine.Infrastructure.Rendering;

namespace EternalDigger.Rendering;

/// <summary>
/// Draws into the <see cref="RenderConsole"/> and remembers the background colour of every screen cell, so effects
/// can be drawn over the sky, the house or a tile without painting a black box behind them
/// (<see cref="DrawOver"/>). The sky and terrain cover every cell each frame, so the backgrounds never go stale.
/// A background is one of the 16 colours (<see cref="Draw(int, int, char, ConsoleColor, ConsoleColor)"/>) or 24-bit
/// (<see cref="DrawRgb"/>).
/// </summary>
public sealed class Canvas(RenderConsole console)
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Set in <see cref="_backgroundRgb"/> for a cell with a 24-bit background; 0 is a palette cell.</summary>
    private const uint RgbFlag = 0x1000000;

    private readonly ConsoleColor[] _backgrounds = new ConsoleColor[console.Width * console.Height];
    private readonly uint[] _backgroundRgb = new uint[console.Width * console.Height];

    //**********************************************************
    //** props
    //**********************************************************

    public int Width { get; } = console.Width;
    public int Height { get; } = console.Height;

    /// <summary>Whether the console shows 24-bit colour right now. Without it, draw in the 16 colours.</summary>
    public bool IsTrueColor => console.TrueColorActive;

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Draw(int x, int y, char character, ConsoleColor foreground, ConsoleColor background)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            return;

        _backgrounds[y * Width + x] = background;
        _backgroundRgb[y * Width + x] = 0;
        console.Draw(x, y, character, foreground, background);
    }

    /// <summary>Draws in 24-bit colour (<c>0xRRGGBB</c>) and records the background for <see cref="DrawOver"/>.</summary>
    public void DrawRgb(int x, int y, char character, uint foreground, uint background)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            return;

        _backgrounds[y * Width + x] = ConsoleColor.Black;
        _backgroundRgb[y * Width + x] = (background & 0xFFFFFF) | RgbFlag;
        console.DrawRgb(x, y, character, foreground, background);
    }

    /// <summary>Draws a character and keeps the background that is already there, palette or 24-bit.</summary>
    public void DrawOver(int x, int y, char character, ConsoleColor foreground)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            return;

        var rgb = _backgroundRgb[y * Width + x];
        if (rgb == 0)
            console.Draw(x, y, character, foreground, _backgrounds[y * Width + x]);
        else
            console.DrawRgb(x, y, character, Rgb.Of(foreground), rgb & 0xFFFFFF);
    }

    public void Draw(int x, int y, ReadOnlySpan<char> text, ConsoleColor foreground, ConsoleColor background)
    {
        for (var i = 0; i < text.Length; i++)
            Draw(x + i, y, text[i], foreground, background);
    }

    /// <summary>
    /// The last palette background drawn at a cell: black outside the screen and for a 24-bit cell.
    /// </summary>
    public ConsoleColor BackgroundAt(int x, int y) =>
        (uint)x < (uint)Width && (uint)y < (uint)Height ? _backgrounds[y * Width + x] : ConsoleColor.Black;
}
