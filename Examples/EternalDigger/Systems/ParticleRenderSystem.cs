using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using EternalDigger.Components;
using EternalDigger.Effects;
using EternalDigger.Rendering;
using Microsoft.Xna.Framework;

namespace EternalDigger.Systems;

/// <summary>
/// Draws particle glyphs over the world, keeping each cell's background. Dust and mist only show where light falls;
/// debris, sparks, coins and glints show everywhere (glints are how ore is spotted in the dark). The miner stays
/// readable: nothing is drawn on his 4 x 2 cells, and within one tile around him at most <see cref="NearMinerCap"/>
/// particles are drawn, in their darker colour, so his face and lamp stay the brightest characters there. In 24-bit
/// colour (<see cref="Canvas.IsTrueColor"/>) mist and gas shimmer (<see cref="ParticleLooks.IsTint"/>) are not glyphs
/// but see-through tints over three cells in a row (<see cref="Canvas.Tint"/>), fading in and out with their life; near
/// the miner a tint is half as strong, and never on his cells. Rubble bodies (<see cref="Rubble"/>) are drawn the same way,
/// at their interpolated place (<c>RenderOffset</c>), skipped in the dark and dimmed in dim light, and they count
/// towards the cap around the miner.
/// </summary>
public sealed class ParticleRenderSystem(Canvas canvas, Camera camera, LightMap light) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Most particles drawn per frame in the one-tile ring around the miner. The rest are skipped.</summary>
    public const int NearMinerCap = 6;

    private Query _particles;
    private Query _rubble;
    private Query _miners;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        _particles = World.Query(Query.With<Particle, Position, Glyph>());
        _rubble = World.Query(Query.With<Rubble, Position, Glyph>());
        _miners = World.Query(Query.With<Miner, Position>());
    }

    protected override void OnUpdate(float deltaSeconds)
    {
        var cameraPosition = camera.Position;
        var miner = MinerScreenCell(out var hasMiner);
        var nearMiner = 0;
        var trueColor = canvas.IsTrueColor;
        foreach (var chunk in _particles.GetChunks())
        {
            var particles = chunk.GetSpan<Particle>();
            var positions = chunk.GetSpan<Position>();
            var glyphs = chunk.GetSpan<Glyph>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var world = positions[i].Value;
                if (NeedsLight(particles[i].Style) && IsDark(world.X, world.Y))
                    continue;

                var x = (int)MathF.Floor(world.X - cameraPosition.X);
                var y = (int)MathF.Floor(world.Y - cameraPosition.Y);
                var style = particles[i].Style;
                var tint = trueColor && ParticleLooks.IsTint(style);
                var color = glyphs[i].Foreground;
                var near = false;
                if (hasMiner)
                {
                    var dx = x - miner.X;
                    var dy = y - miner.Y;
                    if (!tint && IsOnMiner(dx, dy))
                        continue;

                    if (IsNearMiner(dx, dy))
                    {
                        if (++nearMiner > NearMinerCap)
                            continue;

                        color = Dim(color);
                        near = true;
                    }
                }

                if (tint)
                    DrawTint(x, y, style, particles[i].Progress, near, miner, hasMiner);
                else
                    canvas.DrawOver(x, y, glyphs[i].Character, color);
            }
        }

        foreach (var chunk in _rubble.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            var glyphs = chunk.GetSpan<Glyph>();
            var offsets = chunk.Has<RenderOffset>() ? chunk.GetSpan<RenderOffset>() : default;
            for (var i = 0; i < chunk.Count; i++)
            {
                var world = positions[i].Value + (offsets.IsEmpty ? Vector2.Zero : offsets[i].Position);
                var level = LightAt(world.X, world.Y);
                if (level == LightMap.Dark)
                    continue;

                var x = (int)MathF.Floor(world.X - cameraPosition.X);
                var y = (int)MathF.Floor(world.Y - cameraPosition.Y);
                var color = level == LightMap.Dim ? Dim(glyphs[i].Foreground) : glyphs[i].Foreground;
                if (hasMiner)
                {
                    var dx = x - miner.X;
                    var dy = y - miner.Y;
                    if (IsOnMiner(dx, dy))
                        continue;

                    if (IsNearMiner(dx, dy))
                    {
                        if (++nearMiner > NearMinerCap)
                            continue;

                        color = Dim(color);
                    }
                }

                canvas.DrawOver(x, y, glyphs[i].Character, color);
            }
        }
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>The darker twin of a bright colour (White to Gray, Yellow to DarkYellow…). Dark colours stay.</summary>
    public static ConsoleColor Dim(ConsoleColor color) => color > ConsoleColor.DarkGray ? color - 8 : color;

    /// <summary>Whether a cell offset from the miner's top-left screen cell is one of his 4 x 2 cells.</summary>
    public static bool IsOnMiner(int dx, int dy) =>
        (uint)dx < Layout.TileWidth && (uint)dy < Layout.TileHeight;

    /// <summary>Whether a cell offset from the miner's top-left screen cell is in the one-tile ring around him.</summary>
    public static bool IsNearMiner(int dx, int dy) =>
        dx >= -Layout.TileWidth && dx < 2 * Layout.TileWidth && dy >= -Layout.TileHeight && dy < 2 * Layout.TileHeight;

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>
    /// A tint particle: its colour over its cell at full strength and its left and right neighbours at half, skipping
    /// the miner's cells. Half as strong in the ring around the miner.
    /// </summary>
    private void DrawTint(int x, int y, ParticleStyle style, float progress, bool nearMiner, (int X, int Y) miner,
        bool hasMiner)
    {
        var alpha = ParticleLooks.TintAlpha(style, progress) * (nearMiner ? .5f : 1f);
        var color = ParticleLooks.TintColor(style);
        for (var dx = -1; dx <= 1; dx++)
        {
            if (hasMiner && IsOnMiner(x + dx - miner.X, y - miner.Y))
                continue;

            canvas.Tint(x + dx, y, color, dx == 0 ? alpha : alpha * .5f);
        }
    }

    private static bool NeedsLight(ParticleStyle style) => style is ParticleStyle.Dust or ParticleStyle.Mist;

    private bool IsDark(float worldX, float worldY) => LightAt(worldX, worldY) == LightMap.Dark;

    private byte LightAt(float worldX, float worldY) =>
        light.Get((int)MathF.Floor(worldX / Layout.TileWidth), (int)MathF.Floor(worldY / Layout.TileHeight));

    /// <summary>The miner's top-left screen cell, drawn the same way as in <see cref="MinerRenderSystem"/>.</summary>
    private (int X, int Y) MinerScreenCell(out bool found)
    {
        foreach (var chunk in _miners.GetChunks())
        {
            var position = chunk.GetSpan<Position>()[0].Value;
            found = true;
            return ((int)MathF.Floor(position.X - camera.Position.X), (int)MathF.Floor(position.Y - camera.Position.Y));
        }

        found = false;
        return default;
    }
}
