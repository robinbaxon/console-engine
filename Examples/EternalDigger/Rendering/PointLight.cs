using Microsoft.Xna.Framework;

namespace EternalDigger.Rendering;

/// <summary>
/// A coloured light added to the <see cref="LightMap"/> after the lamp (<see cref="LightMap.AddLight"/>): the camp
/// lantern, a burning fuse, a blast, a gas flare. <see cref="Center"/> is in tile coordinates. Its intensity is
/// <see cref="Strength"/> at the centre, 0.69 × that at <see cref="Radius"/> tiles and 0.31 × that at 1.5 ×
/// <see cref="Radius"/>, then nothing. <see cref="Color"/> (<c>0xRRGGBB</c>) is the light's colour at full strength.
/// Walls don't block it.
/// </summary>
public readonly record struct PointLight(Vector2 Center, float Radius, uint Color, float Strength);
