using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Physics;
using EternalDigger.Components;
using EternalDigger.Effects;
using EternalDigger.Mining;
using EternalDigger.Rendering;
using EternalDigger.Systems;
using EternalDigger.Tests.Fakes;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace EternalDigger.Tests;

/// <summary>
/// Rubble bodies on the engine's physics: <see cref="RubbleGrid"/> (the tile window), <see cref="RubbleGridSystem"/>,
/// <see cref="ShockwaveSystem"/>, <see cref="PhysicsSystem"/> and <see cref="RubbleSystem"/>, run as in
/// <c>MineScene</c> on a carved-out piece of mine.
/// </summary>
public class RubbleTests
{
    private const float StepSeconds = 1f / 60f;
    private const int Seed = 11;
    private const int Row = Mine.SurfaceRow + 30;
    private const int Column = 10;

    private World _world;
    private Mine _mine;
    private Camera _camera;
    private RubbleGrid _grid;
    private PhysicsSystem _physics;
    private EffectSpawner _effects;
    private SystemGroup _fixed;
    private SystemGroup _simulation;

    [SetUp]
    public void SetUp()
    {
        _world = new World();
        _mine = new Mine(Seed);
        for (var row = Row - 14; row <= Row + 14; row++)
        for (var column = 1; column < Mine.Columns - 1; column++)
            _mine.At(column, row) = new TileCell { Type = TileType.Stone };

        // A tunnel along the row, on a stone floor.
        for (var column = 4; column <= 16; column++)
            _mine.At(column, Row) = new TileCell { Type = TileType.Air };

        _camera = new Camera { Position = new Vector2(0f, (Row - 10) * Layout.TileHeight) };
        _grid = new RubbleGrid(_mine);
        _physics = new PhysicsSystem { Gravity = new Vector2(0f, EffectSpawner.RubbleGravity) };
        _effects = new EffectSpawner(new Random(Seed), new ScreenShake());
        _fixed = new SystemGroup(_world);
        _fixed.Add(new RubbleGridSystem(_grid, _physics, _camera));
        _fixed.Add(new ShockwaveSystem());
        _fixed.Add(_physics);
        _simulation = new SystemGroup(_world);
        _simulation.Add(new ParticleLifetimeSystem());
        _simulation.Add(new RubbleSystem(_camera, _effects));
    }

    [TearDown]
    public void TearDown() => _world.Dispose();

    //**********************************************************
    //** landing, piling, sleeping
    //**********************************************************

    [Test]
    public void Rubble_DroppedInATunnel_LandsOnItsFloor()
    {
        var rubble = Spawn(Layout.TileCenter(Column, Row) - new Vector2(0f, .4f), new Vector2(6f, 0f));

        Run(2f);

        var position = _world.Get<Position>(rubble).Value;
        var floor = (Row + 1) * Layout.TileHeight;
        Assert.That(position.Y, Is.LessThan(floor).And.GreaterThan(floor - 1.2f), "resting on the floor");
        Assert.That(RubbleGrid.TileOf(position).Y, Is.EqualTo(Row));
    }

    [Test]
    public void Rubble_ThrownAgainstTheWall_StaysInTheTunnel()
    {
        var rubble = Spawn(Layout.TileCenter(16, Row), new Vector2(60f, -5f));

        Run(2f);

        var tile = RubbleGrid.TileOf(_world.Get<Position>(rubble).Value);
        Assert.That(tile, Is.EqualTo(new Point(16, Row)));
    }

    [Test]
    public void Rubble_DroppedDownAShaft_FallsToTheBottom()
    {
        for (var row = Row - 8; row < Row; row++)
            _mine.At(Column, row) = new TileCell { Type = TileType.Air };

        var rubble = Spawn(Layout.TileCenter(Column, Row - 8), Vector2.Zero);

        Run(3f);

        Assert.That(RubbleGrid.TileOf(_world.Get<Position>(rubble).Value), Is.EqualTo(new Point(Column, Row)));
    }

    [Test]
    public void Rubble_ManyInANarrowShaft_PileUp()
    {
        // A one-tile shaft above a dead end: rubble can only stack.
        for (var row = Row - 6; row <= Row; row++)
            _mine.At(2, row) = new TileCell { Type = TileType.Air };
        _mine.At(3, Row) = new TileCell { Type = TileType.Stone };

        var pieces = new Entity[12];
        for (var i = 0; i < pieces.Length; i++)
            pieces[i] = Spawn(Layout.TileCenter(2, Row - 5) + new Vector2((i % 3 - 1) * 1.1f, -(i / 3) * 1.1f),
                Vector2.Zero);

        Run(4f);

        var lowest = float.MinValue;
        var highest = float.MaxValue;
        foreach (var piece in pieces)
        {
            var position = _world.Get<Position>(piece).Value;
            lowest = Math.Max(lowest, position.Y);
            highest = Math.Min(highest, position.Y);
            Assert.That(position.Y, Is.LessThan((Row + 1) * Layout.TileHeight), "above the floor");
            Assert.That(position.X, Is.InRange(2f * Layout.TileWidth, 3f * Layout.TileWidth), "inside the shaft");
        }

        Assert.That(lowest - highest, Is.GreaterThan(2f), "stacked several pieces high");
    }

    [Test]
    public void Rubble_AtRest_FallsAsleep()
    {
        for (var i = 0; i < 4; i++)
            Spawn(Layout.TileCenter(6 + i * 2, Row), new Vector2(3f, 0f));

        Run(4f);

        Assert.That(_physics.BodyCount, Is.EqualTo(4));
        Assert.That(_physics.SleepingBodyCount, Is.EqualTo(4));
    }

    [Test]
    public void Rubble_FloorDugAway_WakesAndFalls()
    {
        var rubble = Spawn(Layout.TileCenter(Column, Row), Vector2.Zero);
        Run(3f);
        Assert.That(_world.Get<RigidBody>(rubble).IsSleeping, Is.True, "asleep before the dig");

        _mine.At(Column, Row + 1) = new TileCell { Type = TileType.Air };
        _mine.At(Column, Row + 2) = new TileCell { Type = TileType.Air };
        Run(2f);

        Assert.That(RubbleGrid.TileOf(_world.Get<Position>(rubble).Value).Y, Is.EqualTo(Row + 2));
    }

    //**********************************************************
    //** blasts
    //**********************************************************

    [Test]
    public void Shockwave_NearRestingRubble_ThrowsItAndIsDestroyed()
    {
        var rubble = Spawn(Layout.TileCenter(12, Row), Vector2.Zero);
        Run(3f);
        var before = _world.Get<Position>(rubble).Value;

        _simulation.Commands.Create(new Position(Layout.TileCenter(9, Row)), new Shockwave(16f, 30f));
        _simulation.Update(0f);
        _fixed.Update(StepSeconds);

        Assert.That(_world.Get<Velocity>(rubble).Value.X, Is.GreaterThan(5f), "thrown away from the blast");
        Assert.That(_world.Get<RigidBody>(rubble).IsSleeping, Is.False);
        Assert.That(CountOf<Shockwave>(), Is.Zero);
        Run(1f);
        Assert.That(_world.Get<Position>(rubble).Value.X, Is.GreaterThan(before.X));
    }

    [Test]
    public void Shockwave_RubbleOutOfReach_IsNotPushed()
    {
        var rubble = Spawn(Layout.TileCenter(16, Row), Vector2.Zero);
        Run(3f);

        _simulation.Commands.Create(new Position(Layout.TileCenter(4, Row)), new Shockwave(8f, 30f));
        _simulation.Update(0f);
        _fixed.Update(StepSeconds);

        Assert.That(_world.Get<RigidBody>(rubble).IsSleeping, Is.True);
    }

    [Test]
    public void Blast_ThrowsRubbleBodiesAndAShockwave()
    {
        _effects.Blast(_simulation.Commands, Column, Row, 1);
        _simulation.Commands.Playback(_world);

        Assert.That(CountOf<Rubble>(), Is.EqualTo(EffectSpawner.BlastRubbleBodies));
        Assert.That(CountOf<Shockwave>(), Is.EqualTo(1));
    }

    [Test]
    public void TileBreak_ThrowsAFewRubbleBodies()
    {
        _effects.TileBreak(_simulation.Commands, Column, Row, TileType.Stone);
        _simulation.Commands.Playback(_world);

        Assert.That(CountOf<Rubble>(),
            Is.InRange(EffectSpawner.BreakRubbleMin, EffectSpawner.BreakRubbleMax));
    }

    //**********************************************************
    //** limits
    //**********************************************************

    [Test]
    public void Rubble_OverTheCap_TheOldestAreDestroyed()
    {
        var count = RubbleSystem.MaxRubble + 10;
        var pieces = new Entity[count];
        for (var i = 0; i < count; i++)
        {
            pieces[i] = Spawn(Layout.TileCenter(4 + i % 13, Row), Vector2.Zero);
            _world.Get<Rubble>(pieces[i]).Age = count - i;   // the first ones are the oldest
            _world.Get<Rubble>(pieces[i]).Lifetime = 1000f;
        }

        _simulation.Update(StepSeconds);

        Assert.That(CountOf<Rubble>(), Is.EqualTo(RubbleSystem.MaxRubble));
        for (var i = 0; i < 10; i++)
            Assert.That(_world.IsAlive(pieces[i]), Is.False, $"old piece {i}");
        Assert.That(_world.IsAlive(pieces[count - 1]), Is.True, "the newest stays");
    }

    [Test]
    public void Rubble_PastItsLifetime_IsDestroyed()
    {
        var rubble = Spawn(Layout.TileCenter(Column, Row), Vector2.Zero);
        _world.Get<Rubble>(rubble).Age = _world.Get<Rubble>(rubble).Lifetime;

        _simulation.Update(StepSeconds);

        Assert.That(_world.IsAlive(rubble), Is.False);
    }

    [Test]
    public void Rubble_FarOffScreen_IsDestroyed()
    {
        var onScreen = Spawn(Layout.TileCenter(Column, Row), Vector2.Zero);
        var below = Spawn(Layout.TileCenter(Column, Row + 30), Vector2.Zero);
        var above = Spawn(Layout.TileCenter(Column, Row - 16), Vector2.Zero);

        _simulation.Update(StepSeconds);

        Assert.That(_world.IsAlive(onScreen), Is.True);
        Assert.That(_world.IsAlive(below), Is.False);
        Assert.That(_world.IsAlive(above), Is.False);
    }

    [Test]
    public void Rubble_InTheMinersWay_IsKickedApart()
    {
        _world.Create(new Position(Layout.TileToWorld(Column, Row)),
            new Miner { Column = Column + 1, Row = Row, MoveFrom = new Vector2(Column, Row), FacingX = 1 });
        var onHim = Spawn(Layout.TileCenter(Column, Row), Vector2.Zero);
        var inHisWay = Spawn(Layout.TileCenter(Column + 1, Row), Vector2.Zero);
        var beside = Spawn(Layout.TileCenter(Column - 1, Row), Vector2.Zero);

        _simulation.Update(StepSeconds);

        Assert.That(_world.IsAlive(onHim), Is.False);
        Assert.That(_world.IsAlive(inHisWay), Is.False, "the tile he is moving into is cleared");
        Assert.That(_world.IsAlive(beside), Is.True);
        Assert.That(CountOf<Particle>(), Is.EqualTo(2), "a puff of dust for each");
    }

    [Test]
    public void Frames_WithRubbleTumblingAndResting_DoNotAllocate()
    {
        for (var i = 0; i < 20; i++)
            Spawn(Layout.TileCenter(4 + i % 13, Row) - new Vector2(0f, i / 13), new Vector2(i - 10f, -4f));
        Run(1f);

        var before = GC.GetAllocatedBytesForCurrentThread();
        Run(2f);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.Zero);
    }

    //**********************************************************
    //** the tile window
    //**********************************************************

    [Test]
    public void Follow_SmallScroll_KeepsTheWindow()
    {
        _grid.Follow(Row - 10, Row + 13);
        var first = _grid.FirstRow;

        var moved = _grid.Follow(Row - 5, Row + 18);

        Assert.That(moved, Is.False);
        Assert.That(_grid.FirstRow, Is.EqualTo(first));
        Assert.That(_grid.Moves, Is.EqualTo(1));
    }

    [Test]
    public void Follow_CameraScrollsFarDown_MovesTheWindowAroundTheScreen()
    {
        _grid.Follow(Row - 10, Row + 13);

        var moved = _grid.Follow(Row + 20, Row + 43);

        Assert.That(moved, Is.True);
        Assert.That(_grid.Contains(Column, Row + 20 - RubbleGrid.Margin), Is.True);
        Assert.That(_grid.Contains(Column, Row + 43 + RubbleGrid.Margin), Is.True);
        Assert.That(_grid.Tiles.Origin.Y, Is.EqualTo(_grid.FirstRow * Layout.TileHeight));
        Assert.That(_grid.Tiles.IsSolid(Column, Row + 1 - _grid.FirstRow), Is.EqualTo(_mine.IsSolid(Column, Row + 1)));
    }

    [Test]
    public void Follow_AtTheTop_NeverStartsAboveRowZero()
    {
        _grid.Follow(-1, 22);

        Assert.That(_grid.FirstRow, Is.Zero);
        Assert.That(_grid.Follow(-1, 22), Is.False, "the clamped window is kept");
    }

    [Test]
    public void Sync_TileDug_IsCopiedAndWakesItsNeighbours()
    {
        _grid.Follow(Row - 10, Row + 13);
        Assert.That(_grid.Tiles.IsSolid(Column, Row + 1 - _grid.FirstRow), Is.True);

        _mine.At(Column, Row + 1) = new TileCell { Type = TileType.Air };
        _grid.Sync();

        Assert.That(_grid.Tiles.IsSolid(Column, Row + 1 - _grid.FirstRow), Is.False);
        Assert.That(_grid.ShouldWake(Column, Row), Is.True, "the tile above");
        Assert.That(_grid.ShouldWake(Column + 2, Row), Is.False);
        Assert.That(_grid.ShouldClear(Column, Row + 1), Is.False, "dug out, not filled");

        _grid.Sync();
        Assert.That(_grid.HasMarks, Is.False, "marks last one sync");
    }

    [Test]
    public void RubbleGridSystem_LadderPlaced_ClearsTheRubbleInThatTile()
    {
        var inTheTile = Spawn(Layout.TileCenter(Column, Row), Vector2.Zero);
        var beside = Spawn(Layout.TileCenter(Column + 2, Row), Vector2.Zero);
        _fixed.Update(StepSeconds);

        _mine.At(Column, Row).HasLadder = true;
        _fixed.Update(StepSeconds);

        Assert.That(_world.IsAlive(inTheTile), Is.False);
        Assert.That(_world.IsAlive(beside), Is.True);
    }

    [Test]
    public void RubbleGridSystem_HandsTheWindowToPhysics()
    {
        _fixed.Update(StepSeconds);

        Assert.That(_physics.TileGrid, Is.SameAs(_grid.Tiles));
        Assert.That(_grid.Contains(Column, Row), Is.True);
    }

    //**********************************************************
    //** looks and drawing
    //**********************************************************

    [Test]
    public void GlyphOf_LyingSquareTiltedAndOld_AreBlockSmallBlockAndDot()
    {
        var rubble = new Rubble(ParticleStyle.Gold, 0, 20f);

        Assert.That(RubbleSystem.GlyphOf(rubble, MathF.PI / 2f).Character, Is.EqualTo('■'));
        Assert.That(RubbleSystem.GlyphOf(rubble, MathF.PI / 4f).Character, Is.EqualTo('▪'));
        rubble.Age = 19.5f;
        Assert.That(RubbleSystem.GlyphOf(rubble, 0f).Character, Is.EqualTo('·'));
    }

    [Test]
    public void Render_RubbleOnTheMinersCells_IsNotDrawnButBesideHimIs()
    {
        var console = new RenderConsole(new FakeConsoleHandler(Layout.ScreenWidth, Layout.ScreenHeight));
        var canvas = new Canvas(console);
        var light = new LightMap(Mine.Columns, Layout.ScreenHeight / Layout.TileHeight + 2);
        _world.Create(new Position(Layout.TileToWorld(Column, Row)),
            new Miner { Column = Column, Row = Row, MoveFrom = new Vector2(Column, Row), FacingX = 1,
                LampDirection = Vector2.UnitX });
        var lighting = new SystemGroup(_world);
        lighting.Add(new LightingSystem(_mine, light, _camera, new Headlamp()));
        lighting.Update(StepSeconds);
        var onHim = Layout.TileToWorld(Column, Row) + new Vector2(.5f, .5f);
        var ahead = Layout.TileToWorld(Column + 2, Row) + new Vector2(1.5f, 1.5f);
        Spawn(onHim, Vector2.Zero);
        Spawn(ahead, Vector2.Zero);

        var presentation = new SystemGroup(_world);
        presentation.Add(new ParticleRenderSystem(canvas, _camera, light));
        presentation.Update(StepSeconds);

        Assert.That(console.GetCharAt(ScreenX(onHim), ScreenY(onHim)), Is.Not.EqualTo('■'));
        Assert.That(console.GetCharAt(ScreenX(ahead), ScreenY(ahead)), Is.EqualTo('■'));
    }

    //**********************************************************
    //** helpers
    //**********************************************************

    private Entity Spawn(Vector2 position, Vector2 velocity)
    {
        _effects.Rubble(_simulation.Commands, position, velocity, ParticleStyle.Stone);
        _simulation.Commands.Playback(_world);
        var spawned = Entity.Null;
        foreach (var chunk in _world.Query(Query.With<Rubble, Rotation>()).GetChunks())
        {
            var entities = chunk.Entities;
            var rotations = chunk.GetSpan<Rotation>();
            for (var i = 0; i < chunk.Count; i++)
            {
                // Lying square and still, so the tests don't depend on the random spin.
                if (_world.Get<Position>(entities[i]).Value == position)
                {
                    rotations[i].Radians = 0f;
                    _world.Get<AngularVelocity>(entities[i]).Value = 0f;
                    spawned = entities[i];
                }
            }
        }

        return spawned;
    }

    /// <summary>Runs whole frames as <c>MineScene</c> does at 60 fps: one fixed step, then the frame.</summary>
    private void Run(float seconds)
    {
        for (var elapsed = 0f; elapsed < seconds; elapsed += StepSeconds)
        {
            _fixed.Update(StepSeconds);
            _simulation.Update(StepSeconds);
        }
    }

    private int CountOf<T>() where T : unmanaged
    {
        var count = 0;
        foreach (var chunk in _world.Query(Query.With<T>()).GetChunks())
            count += chunk.Count;

        return count;
    }

    private int ScreenX(Vector2 world) => (int)MathF.Floor(world.X - _camera.Position.X);

    private int ScreenY(Vector2 world) => (int)MathF.Floor(world.Y - _camera.Position.Y);
}
