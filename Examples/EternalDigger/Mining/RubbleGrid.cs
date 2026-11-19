using System;
using ConsoleEngine.Physics;
using Microsoft.Xna.Framework;

namespace EternalDigger.Mining;

/// <summary>
/// The solid tiles rubble bodies collide with: a <see cref="TileGrid"/> window of <see cref="Rows"/> mine rows around
/// the screen (the engine's grid is finite, the mine is not). <see cref="Follow"/> moves the window when the screen
/// gets within <see cref="Margin"/> rows of its edge; that builds a new <see cref="TileGrid"/>, at most once every
/// 20 or so rows of scrolling, never per frame. <see cref="Sync"/> copies the tiles that changed since (dug, blasted,
/// built) and marks where rubble must wake (beside a changed tile) or be cleared (in a tile that turned solid or got a
/// ladder).
/// </summary>
public sealed class RubbleGrid(Mine mine)
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Mine rows in the window: the 24 on screen and 20 above and below.</summary>
    public const int Rows = 64;

    /// <summary>Rows that must stay inside the window above and below the screen; closer to its edge it moves.</summary>
    public const int Margin = 4;

    private const byte Wake = 1;
    private const byte Clear = 2;

    private readonly bool[] _ladders = new bool[Mine.Columns * Rows];
    private readonly byte[] _marks = new byte[Mine.Columns * Rows];
    private readonly int[] _marked = new int[Mine.Columns * Rows];
    private int _markedCount;

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>The window's tiles, for <see cref="PhysicsSystem.TileGrid"/>. Null before the first <see cref="Follow"/>.
    /// </summary>
    public TileGrid Tiles { get; private set; }

    /// <summary>The mine row of the window's top row.</summary>
    public int FirstRow { get; private set; }

    /// <summary>How often the window has moved (built a new grid).</summary>
    public int Moves { get; private set; }

    /// <summary>Whether <see cref="Sync"/> marked any tile this time.</summary>
    public bool HasMarks => _markedCount > 0;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Keeps the screen's rows (<paramref name="firstVisibleRow"/> to <paramref name="lastVisibleRow"/>) and
    /// <see cref="Margin"/> more inside the window: when they aren't, centres a new window on them and fills it from the
    /// mine. Returns whether it moved.
    /// </summary>
    public bool Follow(int firstVisibleRow, int lastVisibleRow)
    {
        var top = Math.Max(0, firstVisibleRow - Margin);
        if (Tiles != null && top >= FirstRow && lastVisibleRow + Margin < FirstRow + Rows)
            return false;

        var visible = lastVisibleRow - firstVisibleRow + 1;
        FirstRow = Math.Max(0, firstVisibleRow - Math.Max(0, Rows - visible) / 2);
        Tiles = new TileGrid(Mine.Columns, Rows, new Vector2(Layout.TileWidth, Layout.TileHeight),
            new Vector2(0f, FirstRow * Layout.TileHeight));
        for (var row = 0; row < Rows; row++)
        for (var column = 0; column < Mine.Columns; column++)
        {
            Tiles.SetSolid(column, row, mine.IsSolid(column, FirstRow + row));
            _ladders[row * Mine.Columns + column] = mine.HasLadder(column, FirstRow + row);
        }

        ClearMarks();
        Moves++;
        return true;
    }

    /// <summary>
    /// Copies every tile of the window that changed in the mine since the last call. A tile that changed marks itself
    /// and its eight neighbours to wake; one that turned solid, or got a ladder, is marked to be cleared of rubble.
    /// </summary>
    public void Sync()
    {
        ClearMarks();
        if (Tiles == null)
            return;

        for (var row = 0; row < Rows; row++)
        for (var column = 0; column < Mine.Columns; column++)
        {
            var mineRow = FirstRow + row;
            var solid = mine.IsSolid(column, mineRow);
            if (solid != Tiles.IsSolid(column, row))
            {
                Tiles.SetSolid(column, row, solid);
                MarkAround(column, row);
                if (solid)
                    Mark(column, row, Clear);
            }

            var index = row * Mine.Columns + column;
            var ladder = mine.HasLadder(column, mineRow);
            if (ladder != _ladders[index])
            {
                _ladders[index] = ladder;
                if (ladder)
                    Mark(column, row, Clear);
            }
        }
    }

    /// <summary>Whether a mine tile is inside the window.</summary>
    public bool Contains(int column, int row) =>
        (uint)column < Mine.Columns && row >= FirstRow && row < FirstRow + Rows;

    /// <summary>Whether rubble in this mine tile must go: it turned solid or got a ladder at the last <see cref="Sync"/>.
    /// </summary>
    public bool ShouldClear(int column, int row) => (MarkAt(column, row) & Clear) != 0;

    /// <summary>Whether rubble in this mine tile must wake: it or a neighbour changed at the last <see cref="Sync"/>.
    /// </summary>
    public bool ShouldWake(int column, int row) => (MarkAt(column, row) & Wake) != 0;

    /// <summary>The tile a world position (cells) is in.</summary>
    public static Point TileOf(Vector2 position) =>
        new((int)MathF.Floor(position.X / Layout.TileWidth), (int)MathF.Floor(position.Y / Layout.TileHeight));

    //**********************************************************
    //** internals
    //**********************************************************

    private byte MarkAt(int column, int row) =>
        Contains(column, row) ? _marks[(row - FirstRow) * Mine.Columns + column] : (byte)0;

    private void MarkAround(int column, int row)
    {
        for (var dy = -1; dy <= 1; dy++)
        for (var dx = -1; dx <= 1; dx++)
            Mark(column + dx, row + dy, Wake);
    }

    /// <summary>Marks a window tile (window coordinates); tiles outside the window are skipped.</summary>
    private void Mark(int column, int row, byte mark)
    {
        if ((uint)column >= Mine.Columns || (uint)row >= Rows)
            return;

        var index = row * Mine.Columns + column;
        if (_marks[index] == 0)
            _marked[_markedCount++] = index;

        _marks[index] |= mark;
    }

    private void ClearMarks()
    {
        for (var i = 0; i < _markedCount; i++)
            _marks[_marked[i]] = 0;

        _markedCount = 0;
    }
}
