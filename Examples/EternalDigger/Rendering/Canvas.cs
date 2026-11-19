using System;
using ConsoleEngine.Infrastructure.Rendering;

namespace EternalDigger.Rendering;

/// <summary>
/// Draws into the <see cref="RenderConsole"/> and remembers the background colour of every screen cell, so effects
/// can be drawn over the sky, the house or a tile without painting a black box behind them
/// (<see cref="DrawOver"/>). The sky and terrain cover every cell each frame, so the backgrounds never go stale.
/// A background is one of the 16 colours (<see cref="Draw(int, int, char, ConsoleColor, ConsoleColor)"/>) or 24-bit
/// (<see cref="DrawRgb"/>). Each cell's character and colours are also kept as 24-bit colour, so in 24-bit colour an
/// effect can blend into what is already there: <see cref="Tint"/> lays a see-through colour over a cell and
/// <see cref="DrawGlass"/> draws on a see-through fill.
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
    private readonly uint[] _foregroundRgb = new uint[console.Width * console.Height];
    private readonly char[] _characters = new char[console.Width * console.Height];

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

        var index = y * Width + x;
        _backgrounds[index] = background;
        _backgroundRgb[index] = 0;
        _foregroundRgb[index] = Rgb.Of(foreground);
        _characters[index] = character;
        console.Draw(x, y, character, foreground, background);
    }

    /// <summary>Draws in 24-bit colour (<c>0xRRGGBB</c>) and records the background for <see cref="DrawOver"/>.</summary>
    public void DrawRgb(int x, int y, char character, uint foreground, uint background)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            return;

        var index = y * Width + x;
        _backgrounds[index] = ConsoleColor.Black;
        _backgroundRgb[index] = (background & 0xFFFFFF) | RgbFlag;
        _foregroundRgb[index] = foreground & 0xFFFFFF;
        _characters[index] = character;
        console.DrawRgb(x, y, character, foreground, background);
    }

    /// <summary>Draws a character and keeps the background that is already there, palette or 24-bit.</summary>
    public void DrawOver(int x, int y, char character, ConsoleColor foreground)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            return;

        var index = y * Width + x;
        var rgb = _backgroundRgb[index];
        _foregroundRgb[index] = Rgb.Of(foreground);
        _characters[index] = character;
        if (rgb == 0)
            console.Draw(x, y, character, foreground, _backgrounds[index]);
        else
            console.DrawRgb(x, y, character, Rgb.Of(foreground), rgb & 0xFFFFFF);
    }

    public void Draw(int x, int y, ReadOnlySpan<char> text, ConsoleColor foreground, ConsoleColor background)
    {
        for (var i = 0; i < text.Length; i++)
            Draw(x + i, y, text[i], foreground, background);
    }

    /// <summary>
    /// Lays <paramref name="color"/> over a cell with the opacity <paramref name="alpha"/> (0–1): its character stays,
    /// its foreground and background are both blended towards the colour (<see cref="Rgb.Mix"/>), like mist in front
    /// of it. Draws in 24-bit colour, so call it only while <see cref="IsTrueColor"/>.
    /// </summary>
    public void Tint(int x, int y, uint color, float alpha)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height || alpha <= 0f)
            return;

        var index = y * Width + x;
        DrawRgb(x, y, _characters[index], Rgb.Mix(_foregroundRgb[index], color, alpha),
            Rgb.Mix(BackgroundRgb(index), color, alpha));
    }

    /// <summary>
    /// Draws a character on a see-through <paramref name="fill"/>: the background already at the cell blended towards
    /// the fill by <paramref name="alpha"/>, so the scene shows faintly through a panel. 24-bit colour only.
    /// </summary>
    public void DrawGlass(int x, int y, char character, uint foreground, uint fill, float alpha)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            return;

        DrawRgb(x, y, character, foreground, Rgb.Mix(BackgroundRgb(y * Width + x), fill, alpha));
    }

    /// <summary>
    /// A panel cell: on the solid palette fill in the 16 colours, exactly as <see cref="Draw(int, int, char,
    /// ConsoleColor, ConsoleColor)"/>, and on a see-through fill (<see cref="DrawGlass"/>) in 24-bit colour.
    /// </summary>
    public void DrawPanel(int x, int y, char character, ConsoleColor foreground, in PanelFill fill)
    {
        if (IsTrueColor)
            DrawGlass(x, y, character, Rgb.Of(foreground), fill.Rgb, fill.Alpha);
        else
            Draw(x, y, character, foreground, fill.Color);
    }

    /// <summary>
    /// The last palette background drawn at a cell: black outside the screen and for a 24-bit cell.
    /// </summary>
    public ConsoleColor BackgroundAt(int x, int y) =>
        (uint)x < (uint)Width && (uint)y < (uint)Height ? _backgrounds[y * Width + x] : ConsoleColor.Black;

    /// <summary>The background at a cell as 24-bit colour, palette or not. Black outside the screen.</summary>
    public uint BackgroundRgbAt(int x, int y) =>
        (uint)x < (uint)Width && (uint)y < (uint)Height ? BackgroundRgb(y * Width + x) : Rgb.Black;

    /// <summary>The foreground at a cell as 24-bit colour, palette or not. Black outside the screen.</summary>
    public uint ForegroundRgbAt(int x, int y) =>
        (uint)x < (uint)Width && (uint)y < (uint)Height ? _foregroundRgb[y * Width + x] : Rgb.Black;

    //**********************************************************
    //** internals
    //**********************************************************

    private uint BackgroundRgb(int index)
    {
        var rgb = _backgroundRgb[index];
        return rgb == 0 ? Rgb.Of(_backgrounds[index]) : rgb & 0xFFFFFF;
    }
}

/// <summary>
/// A panel's fill: the solid <see cref="Color"/> in the 16 colours, and in 24-bit colour <see cref="Rgb"/> laid over the
/// scene with the opacity <see cref="Alpha"/> (<see cref="Canvas.DrawPanel"/>).
/// </summary>
public readonly record struct PanelFill(ConsoleColor Color, uint Rgb, float Alpha)
{
    /// <summary>The fill of the inventory and the prompt boxes: dark blue, or deep navy that lets the mine show.</summary>
    public static readonly PanelFill Prompt = new(ConsoleColor.DarkBlue, 0x0A1636, .8f);
}
