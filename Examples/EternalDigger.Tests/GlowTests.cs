using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using EternalDigger.Components;
using EternalDigger.Effects;
using EternalDigger.Mining;
using EternalDigger.Rendering;
using EternalDigger.Systems;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace EternalDigger.Tests;

/// <summary>
/// Coloured lights in the mine: <see cref="Glow"/> entities from blasts and gas flares, <see cref="GlowSystem"/>, and
/// the fuse and glow lights <see cref="LightingSystem"/> adds to the <see cref="LightMap"/>.
/// </summary>
public class GlowTests
{
    private const float FrameSeconds = 1f / 60f;
    private const int Seed = 5;
    private const int Row = Mine.SurfaceRow + 60;

    private World _world;
    private Mine _mine;
    private LightMap _light;
    private SystemGroup _simulation;
    private EffectSpawner _effects;

    [SetUp]
    public void SetUp()
    {
        _world = new World();
        _mine = new Mine(Seed);
        for (var row = Row - 12; row <= Row + 12; row++)
        for (var column = 1; column < Mine.Columns - 1; column++)
            _mine.At(column, row) = new TileCell { Type = TileType.Stone };

        // The miner in a pocket at the left edge with the lamp pointing left, so the lamp lights nothing in the test.
        _mine.At(1, Row) = new TileCell { Type = TileType.Air };
        _world.Create(new Position(Layout.TileToWorld(1, Row)),
            new Miner { Column = 1, Row = Row, MoveFrom = new Vector2(1, Row), LampDirection = -Vector2.UnitX });

        _light = new LightMap(Mine.Columns, Layout.ScreenHeight / Layout.TileHeight + 2);
        _effects = new EffectSpawner(new Random(Seed), new ScreenShake());
        var camera = new Camera { Position = new Vector2(0f, (Row - 10) * Layout.TileHeight) };
        _simulation = new SystemGroup(_world);
        _simulation.Add(new LightingSystem(_mine, _light, camera, new Headlamp()));
        _simulation.Add(new GlowSystem());
    }

    [TearDown]
    public void TearDown() => _world.Dispose();

    [Test]
    public void CurrentStrength_OverItsLife_FadesFromFullToNothing()
    {
        var glow = new Glow(0xFFFFFF, 1f, 2f, 1f);
        Assert.That(glow.CurrentStrength, Is.EqualTo(1f));

        glow.Age = .5f;
        Assert.That(glow.CurrentStrength, Is.EqualTo(.25f), "the square of the time left");

        glow.Age = 1f;
        Assert.That(glow.CurrentStrength, Is.Zero);
    }

    [Test]
    public void Update_GlowAtTheEndOfItsLife_IsDestroyed()
    {
        _world.Create(new Position(Layout.TileCenter(15, Row)), new Glow(0xFFFFFF, 1f, 2f, .1f));

        for (var frame = 0; frame < 3; frame++)
            _simulation.Update(FrameSeconds);
        Assert.That(CountGlows(), Is.EqualTo(1), "still glowing");

        for (var frame = 0; frame < 10; frame++)
            _simulation.Update(FrameSeconds);
        Assert.That(CountGlows(), Is.Zero);
    }

    [Test]
    public void Blast_Spawned_AddsOneWhiteGlow()
    {
        _effects.Blast(_simulation.Commands, 15, Row, 1);
        _simulation.Commands.Playback(_world);

        Assert.That(CountGlows(), Is.EqualTo(1));
        Assert.That(FirstGlow().Color, Is.EqualTo(EffectSpawner.BlastGlowColor));
    }

    [Test]
    public void GasFlare_Spawned_AddsOneGreenGlowThatIsStrongerWhenBig()
    {
        _effects.GasFlare(_simulation.Commands, 15, Row, big: false);
        _simulation.Commands.Playback(_world);
        var small = FirstGlow();

        Assert.That(CountGlows(), Is.EqualTo(1));
        Assert.That(small.Color, Is.EqualTo(EffectSpawner.GasGlowColor));

        _world.Dispose();
        SetUp();
        _effects.GasFlare(_simulation.Commands, 15, Row, big: true);
        _simulation.Commands.Playback(_world);
        Assert.That(FirstGlow().Strength, Is.GreaterThan(small.Strength));
        Assert.That(FirstGlow().Radius, Is.GreaterThan(small.Radius));
    }

    [Test]
    public void Lighting_GlowInTheDark_LightsAndTintsTheTilesAroundIt()
    {
        _world.Create(new Position(Layout.TileCenter(15, Row)), new Glow(0x00FF00, 1f, 2f, 1f));

        _simulation.Update(FrameSeconds);

        Assert.That(_light.Get(15, Row), Is.EqualTo(LightMap.Lit));
        var color = _light.LightColor(15, Row);
        Assert.That(color.Y, Is.GreaterThan(.5f), "green");
        Assert.That(color.X, Is.Zero, "no red");
        Assert.That(_mine.At(16, Row).Seen, Is.True);
        Assert.That(_light.Get(20, Row), Is.EqualTo(LightMap.Dark), "beyond its reach");
    }

    [Test]
    public void Lighting_BurningFuse_GlowsRedOrangeAroundTheStick()
    {
        _world.Create(new Position(Layout.TileCenter(15, Row)), new Dynamite(15, Row, 2f));

        _simulation.Update(FrameSeconds);

        var color = _light.LightColor(15, Row);
        Assert.That(_light.Get(15, Row), Is.GreaterThan(LightMap.Dark));
        Assert.That(color.X, Is.GreaterThan(color.Y), "more red than green");
        Assert.That(color.Y, Is.GreaterThan(color.Z), "more green than blue");
        Assert.That(_light.Get(18, Row), Is.EqualTo(LightMap.Dark), "a small glow");
    }

    [Test]
    public void Lighting_FuseFlicker_ChangesItsStrength()
    {
        var stick = _world.Create(new Position(Layout.TileCenter(15, Row)), new Dynamite(15, Row, 2f));
        _simulation.Update(FrameSeconds);
        var first = _light.Intensity(15, Row);

        var seen = false;
        for (var step = 1; step <= 10 && !seen; step++)
        {
            _world.Get<Dynamite>(stick).Fuse = 2f - step * .02f;
            _simulation.Update(FrameSeconds);
            seen = _light.Intensity(15, Row) != first;
        }

        Assert.That(seen, Is.True);
    }

    //**********************************************************
    //** helpers
    //**********************************************************

    private int CountGlows()
    {
        var count = 0;
        foreach (var chunk in _world.Query(Query.With<Glow>()).GetChunks())
            count += chunk.Count;

        return count;
    }

    private Glow FirstGlow()
    {
        foreach (var chunk in _world.Query(Query.With<Glow>()).GetChunks())
            return chunk.GetSpan<Glow>()[0];

        throw new InvalidOperationException("No glow.");
    }
}
