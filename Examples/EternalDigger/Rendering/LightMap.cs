using System;
using EternalDigger.Mining;
using Microsoft.Xna.Framework;

namespace EternalDigger.Rendering;

/// <summary>
/// How brightly each tile on screen is lit this frame: daylight near the surface plus the miner's headlamp, a cone
/// that solid tiles block. Covers a window of rows starting at <see cref="FirstRow"/>; everything outside is
/// <see cref="Dark"/>. Lit tiles are marked <see cref="TileCell.Seen"/>.
/// </summary>
public sealed class LightMap
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const byte Dark = 0;
    public const byte Dim = 1;
    public const byte Lit = 2;
    public const byte Bright = 3;

    /// <summary>Tiles this close to the lamp are always <see cref="Bright"/>, solid or not.</summary>
    public const float NearRadius = 1.5f;

    /// <summary>The starting lamp's reach in tiles. Tiles within half the reach are bright, within 3/4 lit.</summary>
    public const float LampRange = 8f;
    public const float BrightRange = LampRange * BrightShare;
    public const float LitRange = LampRange * LitShare;

    private const float BrightShare = .5f;
    private const float LitShare = .75f;

    /// <summary>Half the cone's opening angle, in degrees.</summary>
    public const float ConeHalfAngle = 40f;

    private static readonly float ConeCos = MathF.Cos(ConeHalfAngle * MathF.PI / 180f);

    /// <summary>Steps per tile when walking the ray from the lamp to a tile.</summary>
    private const int RaySamplesPerTile = 4;

    /// <summary>
    /// <see cref="Intensity"/> is rounded to 1/32: the light glides with the miner, but a tile's colour only changes in
    /// visible steps, so fewer console rows change per frame.
    /// </summary>
    public const float IntensitySteps = 32f;

    /// <summary>The lamp's intensity in a flicker frame, and with an empty lamp.</summary>
    private const float FlickerShare = .8f;
    private const float EmptyLampShare = .5f;

    /// <summary>The cone fades to this share of its light at its edge.</summary>
    private const float ConeEdgeShare = .7f;

    private readonly int _columns;
    private readonly int _maxRows;
    private readonly byte[] _levels;
    private readonly float[] _intensities;
    private readonly bool[] _warm;

    //**********************************************************
    //** ctor
    //**********************************************************

    public LightMap(int columns, int maxRows)
    {
        _columns = columns;
        _maxRows = maxRows;
        _levels = new byte[columns * maxRows];
        _intensities = new float[columns * maxRows];
        _warm = new bool[columns * maxRows];
    }

    //**********************************************************
    //** props
    //**********************************************************

    public int FirstRow { get; private set; }
    public int Rows { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Lights <paramref name="rows"/> rows from <paramref name="firstRow"/> (at most the ctor's maximum).
    /// <paramref name="lampOrigin"/> is in tile coordinates (the miner's tile centre is column + .5, row + .5).
    /// </summary>
    public void Compute(Mine mine, int firstRow, int rows, Vector2 lampOrigin, Vector2 lampDirection) =>
        Compute(mine, firstRow, rows, lampOrigin, lampDirection, LampRange, false);

    /// <summary>
    /// The same, with the cone reaching <paramref name="reach"/> tiles. A reach of 0 (an empty lamp) leaves only the
    /// close radius, one level darker. <paramref name="dimmed"/> (a flicker frame) makes all lamp light one level
    /// darker. Daylight is never affected.
    /// </summary>
    public void Compute(Mine mine, int firstRow, int rows, Vector2 lampOrigin, Vector2 lampDirection, float reach,
        bool dimmed)
    {
        FirstRow = Math.Max(0, firstRow);
        Rows = Math.Clamp(rows, 0, _maxRows);
        Array.Clear(_levels);
        Array.Clear(_intensities);
        Array.Clear(_warm);

        var direction = lampDirection.LengthSquared() > 0f ? Vector2.Normalize(lampDirection) : Vector2.UnitX;
        var lampShare = (reach <= 0f ? EmptyLampShare : 1f) * (dimmed ? FlickerShare : 1f);
        for (var r = 0; r < Rows; r++)
        for (var column = 0; column < _columns; column++)
        {
            var row = FirstRow + r;
            var lamp = Lamp(mine, column, row, lampOrigin, direction, reach, out var lampIntensity);
            if (lamp > Dim && (dimmed || reach <= 0f))
                lamp--;

            var daylight = Daylight(mine, column, row, out var dayIntensity);
            var level = Math.Max(daylight, lamp);
            var index = r * _columns + column;
            _levels[index] = level;
            lampIntensity *= lampShare;
            _intensities[index] = Quantize(Math.Max(dayIntensity, lampIntensity));
            _warm[index] = lampIntensity > dayIntensity;
            if (level > Dark)
                mine.At(column, row).Seen = true;
        }
    }

    /// <summary>
    /// Raises the light around a lantern at <paramref name="center"/> (tile coordinates) to at least <see cref="Lit"/>
    /// within <paramref name="radius"/> tiles, <see cref="Dim"/> within 1.5 times that, and marks those tiles seen.
    /// Call after <see cref="Compute"/>; walls don't block it.
    /// </summary>
    public void AddLantern(Mine mine, Vector2 center, float radius)
    {
        var reach = radius * 1.5f;
        var firstRow = Math.Max(FirstRow, (int)MathF.Floor(center.Y - reach));
        var lastRow = Math.Min(FirstRow + Rows - 1, (int)MathF.Ceiling(center.Y + reach));
        for (var row = firstRow; row <= lastRow; row++)
        for (var column = 0; column < _columns; column++)
        {
            var distance = Vector2.Distance(new Vector2(column + .5f, row + .5f), center);
            var index = (row - FirstRow) * _columns + column;
            var intensity = Quantize(LanternIntensity(distance, radius, reach));
            if (intensity > _intensities[index])
            {
                _intensities[index] = intensity;
                _warm[index] = true;
            }

            var level = distance <= radius ? Lit : distance <= reach ? Dim : Dark;
            ref var current = ref _levels[index];
            if (level <= current)
                continue;

            current = level;
            mine.At(column, row).Seen = true;
        }
    }

    public byte Get(int column, int row)
    {
        var r = row - FirstRow;
        if ((uint)column >= (uint)_columns || (uint)r >= (uint)Rows)
            return Dark;

        return _levels[r * _columns + column];
    }

    /// <summary>
    /// How much light falls on a tile, 0 (none) to 1 (full), in steps of 1/<see cref="IntensitySteps"/>: the smooth
    /// version of <see cref="Get"/>, for 24-bit colour. 0 outside the window.
    /// </summary>
    public float Intensity(int column, int row)
    {
        var r = row - FirstRow;
        if ((uint)column >= (uint)_columns || (uint)r >= (uint)Rows)
            return 0f;

        return _intensities[r * _columns + column];
    }

    /// <summary>Whether a tile's light comes from the lamp or a lantern (warm), not daylight.</summary>
    public bool IsWarm(int column, int row)
    {
        var r = row - FirstRow;
        return (uint)column < (uint)_columns && (uint)r < (uint)Rows && _warm[r * _columns + column];
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>The sky and grass are bright. Daylight reaches a little way into dug-out tiles right below.</summary>
    private static byte Daylight(Mine mine, int column, int row, out float intensity)
    {
        var depth = Mine.Depth(row);
        if (depth <= 0)
        {
            intensity = 1f;
            return Bright;
        }

        if (depth > 3 || !IsOpen(mine, column, row))
        {
            intensity = 0f;
            return Dark;
        }

        intensity = depth switch { 1 => .6f, 2 => .4f, _ => .3f };
        return depth == 1 ? Lit : Dim;
    }

    /// <summary>A dug-out tile, or a solid one next to a dug-out tile (the walls of the shaft catch the light).</summary>
    private static bool IsOpen(Mine mine, int column, int row) =>
        !mine.IsSolid(column, row) || !mine.IsSolid(column - 1, row) || !mine.IsSolid(column + 1, row) ||
        !mine.IsSolid(column, row - 1);

    /// <summary>
    /// The lamp's level on a tile, and its smooth <paramref name="intensity"/>: 1 near the lamp, falling linearly to
    /// .75 at the end of the bright range, .5 at the end of the lit range and .25 at the reach (so it matches the
    /// levels), and fading towards the cone's edge.
    /// </summary>
    private static byte Lamp(Mine mine, int column, int row, Vector2 origin, Vector2 direction, float reach,
        out float intensity)
    {
        var toTile = new Vector2(column + .5f, row + .5f) - origin;
        var distance = toTile.Length();
        intensity = 1f;
        if (distance <= NearRadius)
            return Bright;

        var alignment = distance > reach ? -1f : Vector2.Dot(toTile / distance, direction);
        if (alignment < ConeCos || IsBlocked(mine, origin, toTile, distance, column, row))
        {
            intensity = 0f;
            return Dark;
        }

        var edge = Math.Min(1f, (alignment - ConeCos) / (1f - ConeCos) * 3f);
        intensity = Math.Min(1f, 1.25f - distance / reach) * (ConeEdgeShare + (1f - ConeEdgeShare) * edge);
        if (distance < reach * BrightShare)
            return Bright;

        return distance < reach * LitShare ? Lit : Dim;
    }

    /// <summary>A lantern: .8 at its centre, .55 at <paramref name="radius"/>, .25 at <paramref name="reach"/>.</summary>
    private static float LanternIntensity(float distance, float radius, float reach)
    {
        if (distance <= radius)
            return .8f - .25f * distance / radius;

        return distance <= reach ? .55f - .3f * (distance - radius) / (reach - radius) : 0f;
    }

    private static float Quantize(float intensity) => MathF.Round(intensity * IntensitySteps) / IntensitySteps;

    /// <summary>Whether a solid tile lies between the lamp and the target tile (not counting those two).</summary>
    private static bool IsBlocked(Mine mine, Vector2 origin, Vector2 toTile, float distance, int column, int row)
    {
        var originColumn = (int)MathF.Floor(origin.X);
        var originRow = (int)MathF.Floor(origin.Y);
        var steps = (int)(distance * RaySamplesPerTile);
        for (var i = 1; i < steps; i++)
        {
            var point = origin + toTile * ((float)i / steps);
            var c = (int)MathF.Floor(point.X);
            var r = (int)MathF.Floor(point.Y);
            if ((c == column && r == row) || (c == originColumn && r == originRow))
                continue;

            if (mine.IsSolid(c, r))
                return true;
        }

        return false;
    }
}
