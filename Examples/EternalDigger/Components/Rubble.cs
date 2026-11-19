namespace EternalDigger.Components;

/// <summary>
/// A chunk of rock that is a physics body (<c>RigidBody</c>, <c>Collider</c>, run by <c>PhysicsSystem</c> against
/// <c>RubbleGrid</c>): it tumbles, piles up in tunnels and falls down shafts. Thrown by tile breaks and blasts
/// (<c>EffectSpawner</c>). <c>RubbleSystem</c> ages it, keeps its <c>Glyph</c> and destroys it after
/// <see cref="Lifetime"/>, off screen, in the miner's way or over the cap.
/// </summary>
public struct Rubble(ParticleStyle style, byte variant, float lifetime)
{
    public float Age;
    public float Lifetime = lifetime;

    /// <summary>The debris style of the tile it broke off: its colours (<c>ParticleLooks.Color</c>).</summary>
    public ParticleStyle Style = style;

    public byte Variant = variant;
}

/// <summary>
/// The push of a blast: <c>ShockwaveSystem</c> throws every rubble body within <see cref="Radius"/> cells of its
/// <c>Position</c> outwards with a <c>Force</c>, up to <see cref="Speed"/> cells/s at the centre, then destroys it.
/// </summary>
public struct Shockwave(float radius, float speed)
{
    public float Radius = radius;
    public float Speed = speed;
}
