using ConsoleEngine.Ecs;
using EternalDigger.Components;

namespace EternalDigger.Systems;

/// <summary>Ages every <see cref="Glow"/> and destroys it at the end of its lifetime.</summary>
public sealed class GlowSystem : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _glows;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _glows = World.Query(Query.With<Glow>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _glows.GetChunks())
        {
            var glows = chunk.GetSpan<Glow>();
            var entities = chunk.Entities;
            for (var i = 0; i < chunk.Count; i++)
            {
                glows[i].Age += deltaSeconds;
                if (glows[i].Age >= glows[i].Lifetime)
                    Commands.Destroy(entities[i]);
            }
        }
    }
}
