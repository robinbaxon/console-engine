using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using EternalDigger.Components;
using EternalDigger.Mining;
using EternalDigger.Rendering;
using Microsoft.Xna.Framework;

namespace EternalDigger.Systems;

/// <summary>
/// Computes the <see cref="LightMap"/> for the rows on screen from the miner's headlamp (its reach and flicker from
/// the <see cref="Headlamp"/>), then adds the coloured lights: the deep camp's lantern once it is built, a glow at every
/// burning fuse, and every <see cref="Glow"/> (blasts, gas flares). Uses the sliding <c>Position</c>, so the light
/// moves smoothly with the miner.
/// </summary>
public sealed class LightingSystem(Mine mine, LightMap light, Camera camera, Headlamp headlamp) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int VisibleRows = Layout.ScreenHeight / Layout.TileHeight + 2;

    /// <summary>The camp lantern: warm orange.</summary>
    public const uint LanternColor = 0xFFB060;
    public const float LanternStrength = .8f;

    /// <summary>A burning fuse: a small red-orange glow that flickers.</summary>
    public const uint FuseColor = 0xFF7A30;
    public const float FuseRadius = 1.2f;
    private const float FuseStrength = .6f;
    private const float FuseFlicker = .15f;
    private const float FuseFlickerSpeed = 23f;

    private Query _miners;
    private Query _sticks;
    private Query _glows;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        _miners = World.Query(Query.With<Miner, Position>());
        _sticks = World.Query(Query.With<Dynamite>());
        _glows = World.Query(Query.With<Glow, Position>());
    }

    protected override void OnUpdate(float deltaSeconds)
    {
        var firstRow = (int)MathF.Floor(camera.Position.Y / Layout.TileHeight);
        foreach (var chunk in _miners.GetChunks())
        {
            var position = chunk.GetSpan<Position>()[0].Value;
            var lamp = new Vector2(position.X / Layout.TileWidth + .5f, position.Y / Layout.TileHeight + .5f);
            var miner = chunk.GetSpan<Miner>()[0];
            light.Compute(mine, firstRow, VisibleRows, lamp, miner.LampDirection, headlamp.Reach, headlamp.IsDimmed);
            if (miner.HasDeepCamp)
                light.AddLight(mine, new PointLight(DeepCamp.LanternCenter, DeepCamp.LanternRadius, LanternColor,
                    LanternStrength));
            AddFuses();
            AddGlows();
            return;
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void AddFuses()
    {
        foreach (var chunk in _sticks.GetChunks())
        {
            var sticks = chunk.GetSpan<Dynamite>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var flicker = MathF.Sin(sticks[i].Fuse * FuseFlickerSpeed) * FuseFlicker;
                var center = new Vector2(sticks[i].Column + .5f, sticks[i].Row + .5f);
                light.AddLight(mine, new PointLight(center, FuseRadius, FuseColor, FuseStrength + flicker));
            }
        }
    }

    private void AddGlows()
    {
        foreach (var chunk in _glows.GetChunks())
        {
            var glows = chunk.GetSpan<Glow>();
            var positions = chunk.GetSpan<Position>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var center = new Vector2(positions[i].Value.X / Layout.TileWidth, positions[i].Value.Y / Layout.TileHeight);
                light.AddLight(mine, new PointLight(center, glows[i].Radius, glows[i].Color, glows[i].CurrentStrength));
            }
        }
    }
}
