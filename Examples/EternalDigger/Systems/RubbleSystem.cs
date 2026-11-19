using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using EternalDigger.Components;
using EternalDigger.Effects;
using Microsoft.Xna.Framework;

namespace EternalDigger.Systems;

/// <summary>
/// Looks after the rubble bodies each frame: ages them and sets their <c>Glyph</c> (<c>■</c> lying square, <c>▪</c>
/// tilted, <c>·</c> in the last <see cref="FadeSeconds"/>), and destroys rubble that is past its lifetime, more than
/// <see cref="ScreenMargin"/> rows off screen, or on the miner (his 4 × 2 cells or the tile he is moving into, with a
/// puff of dust: he kicks it apart, it never blocks him). Over <see cref="MaxRubble"/> bodies the oldest go first. Runs
/// after <see cref="CameraSystem"/>.
/// </summary>
public sealed class RubbleSystem(Camera camera, EffectSpawner effects) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Most rubble bodies at a time; the oldest are destroyed above it.</summary>
    public const int MaxRubble = 80;

    /// <summary>Tile rows above and below the screen where rubble may still be; further off it is destroyed.</summary>
    public const int ScreenMargin = 4;

    /// <summary>Seconds at the end of a rubble's life drawn as a faded <c>·</c>.</summary>
    public const float FadeSeconds = 1.5f;

    private const int Tracked = 512;

    private readonly Entity[] _entities = new Entity[Tracked];
    private readonly float[] _ages = new float[Tracked];
    private Query _rubble;
    private Query _miners;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        _rubble = World.Query(Query.With<Rubble, Position, Glyph>().With<Rotation>());
        _miners = World.Query(Query.With<Miner, Position>());
    }

    protected override void OnUpdate(float deltaSeconds)
    {
        var hasMiner = TryGetMiner(out var minerCell, out var minerTile);
        var top = camera.Position.Y - ScreenMargin * Layout.TileHeight;
        var bottom = camera.Position.Y + Layout.ScreenHeight + ScreenMargin * Layout.TileHeight;
        var count = 0;
        foreach (var chunk in _rubble.GetChunks())
        {
            var entities = chunk.Entities;
            var rubble = chunk.GetSpan<Rubble>();
            var positions = chunk.GetSpan<Position>();
            var glyphs = chunk.GetSpan<Glyph>();
            var rotations = chunk.GetSpan<Rotation>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var piece = ref rubble[i];
                piece.Age += deltaSeconds;
                var position = positions[i].Value;
                if (piece.Age >= piece.Lifetime || position.Y < top || position.Y >= bottom)
                {
                    Commands.Destroy(entities[i]);
                    continue;
                }

                if (hasMiner && (IsInside(position, minerCell) || IsInside(position, minerTile)))
                {
                    Commands.Destroy(entities[i]);
                    effects.RubbleDust(Commands, position);
                    continue;
                }

                glyphs[i] = GlyphOf(piece, rotations[i].Radians);
                if (count < Tracked)
                {
                    _entities[count] = entities[i];
                    _ages[count] = piece.Age;
                }

                count++;
            }
        }

        DestroyOldest(Math.Min(count, Tracked), count - MaxRubble);
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// A rubble's character and colour: <c>■</c> when it lies within 22.5° of square, <c>▪</c> when tilted, in its
    /// tile's colour; a faded <c>·</c> in its last <see cref="FadeSeconds"/>.
    /// </summary>
    public static Glyph GlyphOf(in Rubble rubble, float radians)
    {
        if (rubble.Age >= rubble.Lifetime - FadeSeconds)
            return new Glyph('·', ParticleLooks.Color(rubble.Style, rubble.Variant, 1f));

        var quarter = MathF.PI / 2f;
        var tilt = radians - MathF.Round(radians / quarter) * quarter;
        var character = MathF.Abs(tilt) < MathF.PI / 8f ? '■' : '▪';
        return new Glyph(character, ParticleLooks.Color(rubble.Style, rubble.Variant, 0f));
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>Destroys the <paramref name="excess"/> oldest of the first <paramref name="tracked"/> rubble.</summary>
    private void DestroyOldest(int tracked, int excess)
    {
        for (var k = 0; k < excess; k++)
        {
            var oldest = -1;
            for (var i = 0; i < tracked; i++)
            {
                if (_ages[i] >= 0f && (oldest < 0 || _ages[i] > _ages[oldest]))
                    oldest = i;
            }

            if (oldest < 0)
                return;

            Commands.Destroy(_entities[oldest]);
            _ages[oldest] = -1f;
        }
    }

    /// <summary>The miner's drawn cells (top-left and size) and the tile he stands in or is moving into.</summary>
    private bool TryGetMiner(out Rectangle cells, out Rectangle tile)
    {
        foreach (var chunk in _miners.GetChunks())
        {
            var miner = chunk.GetSpan<Miner>()[0];
            var position = chunk.GetSpan<Position>()[0].Value;
            cells = new Rectangle((int)MathF.Floor(position.X), (int)MathF.Floor(position.Y), Layout.TileWidth,
                Layout.TileHeight);
            tile = new Rectangle(miner.Column * Layout.TileWidth, miner.Row * Layout.TileHeight, Layout.TileWidth,
                Layout.TileHeight);
            return true;
        }

        cells = default;
        tile = default;
        return false;
    }

    private static bool IsInside(Vector2 position, Rectangle area) =>
        position.X >= area.X && position.X < area.X + area.Width &&
        position.Y >= area.Y && position.Y < area.Y + area.Height;
}
