namespace EternalDigger.Components;

/// <summary>
/// A short-lived coloured light (a blast's flash, a gas flare), with the engine's <c>Position</c> in world cells.
/// <c>LightingSystem</c> adds it to the light map as a <c>PointLight</c>, fading from <see cref="Strength"/> to 0 over
/// its <see cref="Lifetime"/>; <c>GlowSystem</c> ages it and destroys it at the end.
/// </summary>
public struct Glow(uint color, float strength, float radius, float lifetime)
{
    /// <summary>The light's colour, <c>0xRRGGBB</c>.</summary>
    public uint Color = color;

    /// <summary>Intensity at the centre when the glow starts (0–1).</summary>
    public float Strength = strength;

    /// <summary>In tiles: lit within it, dim out to 1.5 times it.</summary>
    public float Radius = radius;

    public float Age;
    public float Lifetime = lifetime;

    /// <summary>The strength now: falls off with the square of the time left, so a flash dies quickly.</summary>
    public readonly float CurrentStrength
    {
        get
        {
            var left = Lifetime <= 0f ? 0f : 1f - Age / Lifetime;
            return left <= 0f ? 0f : Strength * left * left;
        }
    }
}
