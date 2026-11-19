using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Physics;
using EternalDigger.Components;
using EternalDigger.Mining;
using Microsoft.Xna.Framework;

namespace EternalDigger.Effects;

/// <summary>
/// Creates the game's effect particles and floating numbers. Every method records entities in the command buffer it
/// is given (a system's <c>Commands</c>), so it is safe inside query loops. Positions are world cells. Console cells
/// are about twice as tall as wide, so vertical speeds and gravity are about half the horizontal ones.
/// </summary>
public sealed class EffectSpawner(Random random, ScreenShake shake)
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float DebrisGravity = 40f;
    private const float CoinGravity = 45f;

    /// <summary>Seconds the blast flash lasts: about one frame.</summary>
    private const float FlashLifetime = .05f;

    private const int BlastRubble = 150;
    private const int BlastChunks = 40;
    private const int BlastDust = 40;

    /// <summary>Of a blast's <see cref="BlastChunks"/>, how many are rubble bodies; the rest are debris particles.</summary>
    public const int BlastRubbleBodies = 12;

    /// <summary>Of a tile break's chunks, how many (at least, at most) are rubble bodies.</summary>
    public const int BreakRubbleMin = 4;
    public const int BreakRubbleMax = 6;

    /// <summary>Gravity of the physics world, in cells/s² down: the same pull as debris particles.</summary>
    public const float RubbleGravity = DebrisGravity;

    /// <summary>Half the width and height of a rubble body in cells: a chunk fills most of its cell.</summary>
    public const float RubbleHalfSize = .45f;

    /// <summary>How far past the blast area (in cells) its shockwave pushes rubble, and how hard at the centre.</summary>
    private const float ShockwaveReach = 8f;
    private const float ShockwaveSpeed = 30f;

    /// <summary>The light of a blast: hot white, reaching this many tiles past the blast's own radius.</summary>
    public const uint BlastGlowColor = 0xFFE8B0;
    private const float BlastGlowReach = 2.5f;
    private const float BlastGlowLifetime = .6f;

    /// <summary>The light of a burning gas pocket: a sickly yellow-green.</summary>
    public const uint GasGlowColor = 0xC8F050;

    //**********************************************************
    //** props
    //**********************************************************

    public ScreenShake Shake { get; } = shake;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// One shovel hit: chunks fly out of the struck face back towards the miner. <paramref name="digDirection"/> is
    /// the tile offset from the miner to the hit tile.
    /// </summary>
    public void DigHit(CommandBuffer commands, int column, int row, TileType type, Point digDirection)
    {
        // Just outside the struck face, on the miner's side, so the debris starts in air.
        var face = Layout.TileCenter(column, row) - new Vector2(digDirection.X * 2.5f, digDirection.Y * 1.5f);
        var style = ParticleLooks.DebrisStyle(type);
        var count = random.Next(4, 8);
        for (var i = 0; i < count; i++)
        {
            var velocity = new Vector2(
                -digDirection.X * Range(10f, 26f) + Range(-8f, 8f),
                -digDirection.Y * Range(4f, 10f) - Range(4f, 12f));
            Debris(commands, face, velocity, style, Range(.5f, 1.1f));
        }

        if (!Tiles.IsOre(type))
            return;

        var sparks = random.Next(2, 5);
        for (var i = 0; i < sparks; i++)
            Spawn(commands, face, RandomDirection(14f, 30f), ParticleStyle.Spark, Range(.2f, .45f), 0f, 3f);
    }

    /// <summary>The shovel bounces off rock it can't break: a few sparks and a tiny shake, no chunks.</summary>
    public void Clank(CommandBuffer commands, int column, int row, Point digDirection)
    {
        var face = Layout.TileCenter(column, row) - new Vector2(digDirection.X * 2.5f, digDirection.Y * 1.5f);
        var sparks = random.Next(3, 6);
        for (var i = 0; i < sparks; i++)
            Spawn(commands, face, RandomDirection(10f, 24f), ParticleStyle.Spark, Range(.15f, .35f), 0f, 3f);

        Shake.Add(.15f);
    }

    /// <summary>
    /// The tile broke: a burst of chunks, a dust cloud and a small shake. Ore adds glitter. The first 4–6 chunks are
    /// rubble bodies (<see cref="Rubble"/>) that tumble and stay; the rest are debris particles.
    /// </summary>
    public void TileBreak(CommandBuffer commands, int column, int row, TileType type)
    {
        var center = Layout.TileCenter(column, row);
        var style = ParticleLooks.DebrisStyle(type);
        var chunks = random.Next(14, 23);
        var bodies = random.Next(BreakRubbleMin, BreakRubbleMax + 1);
        for (var i = 0; i < chunks; i++)
        {
            var velocity = RandomDirection(8f, 28f) - new Vector2(0f, Range(4f, 10f));
            if (i < bodies)
                Rubble(commands, center + RandomOffset(1.4f, .5f), velocity, style);
            else
                Debris(commands, center + RandomOffset(1.5f, .8f), velocity, style, Range(.8f, 1.6f));
        }

        var dust = random.Next(6, 11);
        for (var i = 0; i < dust; i++)
            Spawn(commands, center + RandomOffset(2f, 1f), RandomDirection(2f, 7f), ParticleStyle.Dust,
                Range(.5f, 1f), -2f, 1.5f);

        var ore = Tiles.IsOre(type);
        Shake.Add(ore ? .9f : .6f);
        if (!ore)
            return;

        var glitter = random.Next(6, 11);
        for (var i = 0; i < glitter; i++)
            Spawn(commands, center, RandomDirection(16f, 36f), ParticleStyle.Spark, Range(.3f, .7f), 6f, 2.5f);
    }

    /// <summary>One or two puffs kicked back from the feet.</summary>
    public void FootDust(CommandBuffer commands, Vector2 feet, int directionX)
    {
        var count = random.Next(1, 3);
        for (var i = 0; i < count; i++)
            Spawn(commands, feet + RandomOffset(1f, 0f),
                new Vector2(directionX * Range(3f, 8f), -Range(.5f, 2f)), ParticleStyle.Dust, Range(.3f, .6f), 0f, 3f);
    }

    /// <summary>A lit stick of dynamite in a tile; <c>DynamiteSystem</c> burns its fuse and blows it up.</summary>
    public void PlantDynamite(CommandBuffer commands, int column, int row, float fuse) =>
        commands.Create(new Position(Layout.TileCenter(column, row)), new Dynamite(column, row, fuse));

    /// <summary>A puff of smoke rising from a lamp low on oil.</summary>
    public void LampSmoke(CommandBuffer commands, Vector2 lamp) =>
        Spawn(commands, lamp, new Vector2(Range(-1f, 1f), -Range(1.5f, 3f)), ParticleStyle.LampSmoke, Range(.6f, 1f),
            0f, 1f);

    /// <summary>A spark off a burning fuse, and now and then a wisp of smoke.</summary>
    public void FuseSpark(CommandBuffer commands, Vector2 stick)
    {
        var top = stick - new Vector2(.5f, 1f);
        Spawn(commands, top, RandomDirection(4f, 10f), ParticleStyle.Spark, Range(.1f, .25f), 6f, 2f);
        if (random.Next(3) == 0)
            Spawn(commands, top, new Vector2(Range(-1f, 1f), -Range(2f, 4f)), ParticleStyle.Smoke, Range(.6f, 1f), 0f, 1f);
    }

    /// <summary>
    /// A blast around a tile: one frame of full-tile flash over the area, a ring of rubble and chunks, lingering dust
    /// and a big shake, plus a white <see cref="Glow"/> that lights the area and dies away in 0.6 s.
    /// <see cref="BlastRubbleBodies"/> of the stone chunks are rubble bodies, and a <see cref="Shockwave"/> throws the
    /// rubble already lying around. <paramref name="radius"/> is in tiles (1 = 3 x 3).
    /// </summary>
    public void Blast(CommandBuffer commands, int column, int row, int radius)
    {
        var center = Layout.TileCenter(column, row);
        for (var r = row - radius; r <= row + radius; r++)
        for (var c = column - radius; c <= column + radius; c++)
        for (var y = 0; y < Layout.TileHeight; y++)
        for (var x = 0; x < Layout.TileWidth; x++)
            Spawn(commands, Layout.TileToWorld(c, r) + new Vector2(x + .5f, y + .5f), Vector2.Zero, ParticleStyle.Flash,
                FlashLifetime, 0f, 0f);

        for (var i = 0; i < BlastRubble; i++)
        {
            var angle = random.NextSingle() * MathF.Tau;
            var speed = Range(18f, 48f);
            var velocity = new Vector2(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed * .5f - Range(2f, 8f));
            Debris(commands, center + RandomOffset(2f, 1f), velocity, ParticleStyle.Blast, Range(.6f, 1.4f));
        }

        for (var i = 0; i < BlastChunks; i++)
        {
            if (i < BlastRubbleBodies)
                Rubble(commands, center + RandomOffset(3f, 1.5f), RandomDirection(10f, 30f) - new Vector2(0f, 6f),
                    ParticleStyle.Stone);
            else
                Debris(commands, center + RandomOffset(3f, 1.5f), RandomDirection(10f, 30f), ParticleStyle.Stone,
                    Range(.8f, 1.6f));
        }

        commands.Create(new Position(center),
            new Shockwave((radius + .5f) * Layout.TileWidth + ShockwaveReach, ShockwaveSpeed));

        for (var i = 0; i < BlastDust; i++)
            Spawn(commands, center + RandomOffset(5f, 2.5f), RandomDirection(1f, 5f), ParticleStyle.Dust, Range(1f, 2f),
                -1f, 1f);

        commands.Create(new Position(center),
            new Glow(BlastGlowColor, 1f, radius + BlastGlowReach, BlastGlowLifetime));
        Shake.Add(ScreenShake.MaxStrength);
    }

    /// <summary>One or two specks that drop away below the feet on a top-speed climb.</summary>
    public void ClimbTrail(CommandBuffer commands, Vector2 feet)
    {
        var count = random.Next(1, 3);
        for (var i = 0; i < count; i++)
            Spawn(commands, feet + RandomOffset(1.2f, 0f) + new Vector2(0f, .5f),
                new Vector2(Range(-1.5f, 1.5f), Range(3f, 6f)), ParticleStyle.Trail, Range(.25f, .45f), 4f, 2f);
    }

    /// <summary>Puffs to both sides of the feet, more for longer falls. Long falls shake the screen.</summary>
    public void LandingDust(CommandBuffer commands, Vector2 feet, int rowsFallen)
    {
        var count = Math.Min(12, 2 + rowsFallen * 2);
        for (var i = 0; i < count; i++)
        {
            var side = (i & 1) == 0 ? -1f : 1f;
            Spawn(commands, feet, new Vector2(side * Range(4f, 12f), -Range(.5f, 3f)), ParticleStyle.Dust,
                Range(.3f, .7f), 0f, 3f);
        }

        if (rowsFallen >= 3)
            Shake.Add(Math.Min(1.2f, rowsFallen * .15f));
    }

    /// <summary>One twinkle over an ore vein.</summary>
    public void Glint(CommandBuffer commands, Vector2 position, TileType ore)
    {
        var variant = ParticleLooks.GlintVariant(ore);
        commands.Create(
            new Position(position),
            new Velocity(Vector2.Zero),
            ParticleLooks.GlyphAt(ParticleStyle.Glint, variant, 0f),
            new Particle(ParticleStyle.Glint, Range(.5f, .8f)) { Variant = variant },
            new Physics(0f, 0f));
    }

    /// <summary>A rare find's slow pulse, seen through rock and in the dark.</summary>
    public void Pulse(CommandBuffer commands, Vector2 position, TileType find)
    {
        var variant = ParticleLooks.PulseVariant(find);
        commands.Create(
            new Position(position),
            new Velocity(Vector2.Zero),
            ParticleLooks.GlyphAt(ParticleStyle.Pulse, variant, 0f),
            new Particle(ParticleStyle.Pulse, Range(1.4f, 1.8f)) { Variant = variant },
            new Physics(0f, 0f));
    }

    /// <summary>A rare find dug up: two rings of stars, a spray of sparks and its name floating up.</summary>
    public void RareFind(CommandBuffer commands, int column, int row, TileType find)
    {
        var center = Layout.TileCenter(column, row);
        Pop(commands, center);
        Pop(commands, center);
        var sparks = random.Next(10, 16);
        for (var i = 0; i < sparks; i++)
            Spawn(commands, center, RandomDirection(16f, 36f), ParticleStyle.Spark, Range(.4f, .8f), 6f, 2.5f);

        Text(commands, center - new Vector2(0f, 3f), FloatingTextKind.RareFind, (int)find);
        Shake.Add(.6f);
    }

    /// <summary>One slow mist particle drifting through a dug-out cave.</summary>
    public void Mist(CommandBuffer commands, Vector2 position)
    {
        var variant = RandomVariant();
        commands.Create(
            new Position(position),
            new Velocity(new Vector2(Range(-1.2f, 1.2f), Range(-.4f, .4f))),
            ParticleLooks.GlyphAt(ParticleStyle.Mist, variant, 0f),
            new Particle(ParticleStyle.Mist, Range(4f, 8f)) { Variant = variant },
            new Physics(0f, 0f),
            new Components.Mist());
    }

    /// <summary>One slow green shimmer over a gas pocket (has the <see cref="Components.Gas"/> tag).</summary>
    public void GasShimmer(CommandBuffer commands, Vector2 position)
    {
        var variant = RandomVariant();
        commands.Create(
            new Position(position),
            new Velocity(new Vector2(Range(-1f, 1f), Range(-.6f, .2f))),
            ParticleLooks.GlyphAt(ParticleStyle.GasShimmer, variant, 0f),
            new Particle(ParticleStyle.GasShimmer, Range(1.5f, 3f)) { Variant = variant },
            new Physics(0f, 0f),
            new Components.Gas());
    }

    /// <summary>
    /// A gas tile burning off: fire bursting out of it and rising. A <paramref name="big"/> flare (set off by a blast)
    /// throws more and further, shakes harder and lights more. Each flare adds a yellow-green <see cref="Glow"/>.
    /// </summary>
    public void GasFlare(CommandBuffer commands, int column, int row, bool big)
    {
        var center = Layout.TileCenter(column, row);
        var count = big ? random.Next(10, 15) : random.Next(5, 8);
        var speed = big ? 30f : 18f;
        for (var i = 0; i < count; i++)
        {
            var velocity = RandomDirection(speed * .4f, speed) - new Vector2(0f, Range(2f, 6f));
            Spawn(commands, center + RandomOffset(1.5f, .8f), velocity, ParticleStyle.GasFlare, Range(.35f, .7f), -4f,
                2f);
        }

        commands.Create(new Position(center),
            big ? new Glow(GasGlowColor, .9f, 2.5f, .7f) : new Glow(GasGlowColor, .6f, 1.8f, .5f));
        Shake.Add(big ? .25f : .1f);
    }

    /// <summary>Coins burst up and cascade down onto the grass.</summary>
    public void Coins(CommandBuffer commands, Vector2 origin, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var velocity = new Vector2(Range(-16f, 16f), -Range(10f, 22f));
            var variant = RandomVariant();
            commands.Create(
                new Position(origin + RandomOffset(1.5f, .5f)),
                new Velocity(velocity),
                ParticleLooks.GlyphAt(ParticleStyle.Coin, variant, 0f),
                new Particle(ParticleStyle.Coin, Range(1.6f, 2.4f)) { Variant = variant },
                new Physics(CoinGravity, .3f),
                new TileCollider());
        }
    }

    /// <summary>A ring of stars.</summary>
    public void Pop(CommandBuffer commands, Vector2 center)
    {
        var count = random.Next(8, 13);
        for (var i = 0; i < count; i++)
        {
            var angle = MathF.Tau * i / count + Range(-.2f, .2f);
            var velocity = new Vector2(MathF.Cos(angle) * 24f, MathF.Sin(angle) * 12f);
            Spawn(commands, center, velocity, ParticleStyle.Pop, Range(.35f, .5f), 0f, 4f);
        }
    }

    /// <summary>
    /// One rubble body: a <see cref="RubbleHalfSize"/> box that tumbles (<see cref="Rotation"/>,
    /// <see cref="AngularVelocity"/>), collides with the mine's tiles and other rubble, can be pushed (<see cref="Force"/>)
    /// and is drawn between physics steps (<see cref="Interpolation"/>). Lives 20–30 s.
    /// </summary>
    public void Rubble(CommandBuffer commands, Vector2 position, Vector2 velocity, ParticleStyle style)
    {
        var variant = RandomVariant();
        var rubble = new Rubble(style, variant, Range(20f, 30f));
        var body = commands.Create(
            new Position(position),
            new Velocity(velocity),
            new Rotation(random.NextSingle() * MathF.Tau),
            new AngularVelocity(Range(-8f, 8f)),
            RigidBody.Dynamic(1f, 1f, .1f, .8f),
            Collider.Box(new Vector2(RubbleHalfSize)) with { Friction = .8f, Restitution = .15f });
        commands.Add(body, new Glyph('■', ParticleLooks.Color(style, variant, 0f)));
        commands.Add(body, rubble);
        commands.Add(body, new Force());
        commands.Add(body, new Interpolation());
        commands.Add(body, new RenderOffset());
    }

    /// <summary>One puff where the miner kicks a rubble apart.</summary>
    public void RubbleDust(CommandBuffer commands, Vector2 position) =>
        Spawn(commands, position, RandomDirection(2f, 6f), ParticleStyle.Dust, Range(.3f, .5f), 0f, 3f);

    /// <summary>Floating text of any <see cref="FloatingTextKind"/> that drifts up.</summary>
    public void Text(CommandBuffer commands, Vector2 position, FloatingTextKind kind, int amount) =>
        commands.Create(new Position(position), new Velocity(new Vector2(0f, -5f)), new FloatingText(kind, amount, 1.4f));

    /// <summary>Floating <c>+150g</c> / <c>-10g</c> that drifts up.</summary>
    public void Number(CommandBuffer commands, Vector2 position, int amount) =>
        Text(commands, position, FloatingTextKind.Gold, amount);

    //**********************************************************
    //** internals
    //**********************************************************

    private void Debris(CommandBuffer commands, Vector2 position, Vector2 velocity, ParticleStyle style, float lifetime)
    {
        var variant = RandomVariant();
        commands.Create(
            new Position(position),
            new Velocity(velocity),
            ParticleLooks.GlyphAt(style, variant, 0f),
            new Particle(style, lifetime) { Variant = variant },
            new Physics(DebrisGravity, .4f),
            new TileCollider());
    }

    private void Spawn(CommandBuffer commands, Vector2 position, Vector2 velocity, ParticleStyle style,
        float lifetime, float gravity, float drag)
    {
        var variant = RandomVariant();
        commands.Create(
            new Position(position),
            new Velocity(velocity),
            ParticleLooks.GlyphAt(style, variant, 0f),
            new Particle(style, lifetime) { Variant = variant },
            new Physics(gravity, drag));
    }

    private byte RandomVariant() => (byte)random.Next(256);

    private float Range(float min, float max) => min + random.NextSingle() * (max - min);

    /// <summary>A random direction with a speed in the range, squashed vertically to match the cell shape.</summary>
    private Vector2 RandomDirection(float minSpeed, float maxSpeed)
    {
        var angle = random.NextSingle() * MathF.Tau;
        var speed = Range(minSpeed, maxSpeed);
        return new Vector2(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed * .5f);
    }

    private Vector2 RandomOffset(float x, float y) => new(Range(-x, x), Range(-y, y));
}
