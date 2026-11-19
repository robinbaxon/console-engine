using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Physics;
using EternalDigger.Components;
using EternalDigger.Mining;

namespace EternalDigger.Systems;

/// <summary>
/// Keeps the physics tiles in step with the mine, every fixed step before <see cref="PhysicsSystem"/>: moves the
/// <see cref="RubbleGrid"/> window with the camera, copies tiles that were dug, blasted or built
/// (<see cref="RubbleGrid.Sync"/>) and hands the window to <see cref="PhysicsSystem.TileGrid"/>. Rubble beside a
/// changed tile is woken, so a pile falls when the floor under it is dug away; rubble in a tile that turned solid or
/// got a ladder is destroyed.
/// </summary>
public sealed class RubbleGridSystem(RubbleGrid grid, PhysicsSystem physics, Camera camera) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _rubble;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _rubble = World.Query(Query.With<Rubble, Position, RigidBody>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var top = camera.Position.Y;
        grid.Follow(FloorDiv(top), FloorDiv(top + Layout.ScreenHeight - 1));
        grid.Sync();
        physics.TileGrid = grid.Tiles;
        if (!grid.HasMarks)
            return;

        foreach (var chunk in _rubble.GetChunks())
        {
            var entities = chunk.Entities;
            var positions = chunk.GetSpan<Position>();
            var bodies = chunk.GetSpan<RigidBody>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var tile = RubbleGrid.TileOf(positions[i].Value);
                if (grid.ShouldClear(tile.X, tile.Y))
                    Commands.Destroy(entities[i]);
                else if (grid.ShouldWake(tile.X, tile.Y))
                    bodies[i].IsSleeping = false;
            }
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static int FloorDiv(float y) => (int)MathF.Floor(y / Layout.TileHeight);
}
