using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Physics;
using EternalDigger.Components;
using Microsoft.Xna.Framework;

namespace EternalDigger.Systems;

/// <summary>
/// The push of a blast on the rubble lying around, every fixed step before <see cref="PhysicsSystem"/>: each
/// <see cref="Shockwave"/> gives every rubble body within its radius a <see cref="Force"/> that changes its speed by up to
/// <see cref="Shockwave.Speed"/> in one step, outwards and a little up, falling off to 0 at the radius. A pushed body
/// wakes. The shockwave is then destroyed. Rubble younger than <see cref="NewbornSeconds"/> is left alone: it was
/// thrown by the same blast.
/// </summary>
public sealed class ShockwaveSystem : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Rubble younger than this was thrown by the blast itself and isn't pushed again.</summary>
    public const float NewbornSeconds = .1f;

    /// <summary>Share of the push that goes upwards on top of the outward direction.</summary>
    private const float Lift = .4f;

    private Query _shockwaves;
    private Query _rubble;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        _shockwaves = World.Query(Query.With<Shockwave, Position>());
        _rubble = World.Query(Query.With<Rubble, Position, RigidBody>().With<Force>());
    }

    protected override void OnUpdate(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
            return;

        foreach (var chunk in _shockwaves.GetChunks())
        {
            var entities = chunk.Entities;
            var waves = chunk.GetSpan<Shockwave>();
            var centres = chunk.GetSpan<Position>();
            for (var i = 0; i < chunk.Count; i++)
            {
                Push(centres[i].Value, waves[i], deltaSeconds);
                Commands.Destroy(entities[i]);
            }
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void Push(Vector2 centre, in Shockwave wave, float stepSeconds)
    {
        foreach (var chunk in _rubble.GetChunks())
        {
            var rubble = chunk.GetSpan<Rubble>();
            var positions = chunk.GetSpan<Position>();
            var bodies = chunk.GetSpan<RigidBody>();
            var forces = chunk.GetSpan<Force>();
            for (var i = 0; i < chunk.Count; i++)
            {
                if (rubble[i].Age < NewbornSeconds || bodies[i].InverseMass <= 0f)
                    continue;

                var offset = positions[i].Value - centre;
                var distance = offset.Length();
                if (distance >= wave.Radius)
                    continue;

                var outward = distance > .001f ? offset / distance : -Vector2.UnitY;
                // Cells are twice as tall as wide: half the vertical speed, as the effects throw things.
                var direction = new Vector2(outward.X, outward.Y * .5f - Lift);
                var speed = wave.Speed * (1f - distance / wave.Radius);
                forces[i].Add(direction * (speed / (bodies[i].InverseMass * stepSeconds)));
                bodies[i].IsSleeping = false;
            }
        }
    }
}
