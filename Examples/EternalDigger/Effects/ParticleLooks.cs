using System;
using ConsoleEngine.Ecs.Components;
using EternalDigger.Components;
using EternalDigger.Mining;

namespace EternalDigger.Effects;

/// <summary>
/// How each <see cref="ParticleStyle"/> looks over its life: a character ramp walked from birth to death and a bright
/// and a faded colour. Variants pick another ramp offset so a burst doesn't look uniform.
/// </summary>
public static class ParticleLooks
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const string ChunkRamp = "■▪▪·";
    private const string DustRamp = "▓▒░░·";
    private const string SparkRamp = "*+·";
    private const string GlintRamp = "·+✦✦+·";
    private const string MistRamp = "·∙··˙";
    private const string CoinRamp = "●◐|◑";
    private const string PopRamp = "✶*+·";
    private const string TrailRamp = "'·";
    private const string FlashRamp = "█";
    private const string BlastRamp = "▓▒░·";
    private const string SmokeRamp = "·˙";
    private const string LampSmokeRamp = "°°·";
    private const string PulseRamp = "·∙•●•∙·";
    private const string GasRamp = "~∙~·";
    private const string FlareRamp = "✶**+·";

    /// <summary>Mist as a 24-bit tint (<see cref="TintColor"/>): a pale cool grey.</summary>
    public const uint MistTint = 0xB4C0C8;

    /// <summary>Gas shimmer as a 24-bit tint: a sickly green.</summary>
    public const uint GasTint = 0x5CD040;

    /// <summary>The opacity of a mist tint at the middle of its life.</summary>
    public const float MistPeakAlpha = .3f;

    /// <summary>The opacity of a gas tint at the middle of its life.</summary>
    public const float GasPeakAlpha = .4f;

    //**********************************************************
    //** public methods
    //**********************************************************

    public static char Character(ParticleStyle style, byte variant, float progress) => style switch
    {
        ParticleStyle.Dust => Ramp(DustRamp, progress),
        ParticleStyle.Spark => Ramp(SparkRamp, progress),
        ParticleStyle.Glint => Ramp(GlintRamp, progress),
        ParticleStyle.Mist => MistRamp[variant % MistRamp.Length],
        // Coins spin: the ramp cycles with age instead of being walked once.
        ParticleStyle.Coin => CoinRamp[(int)(progress * 24f + variant) % CoinRamp.Length],
        ParticleStyle.Pop => Ramp(PopRamp, progress),
        ParticleStyle.Trail => Ramp(TrailRamp, progress),
        ParticleStyle.Flash => Ramp(FlashRamp, progress),
        ParticleStyle.Blast => Ramp(BlastRamp, progress),
        ParticleStyle.Smoke => Ramp(SmokeRamp, progress),
        ParticleStyle.LampSmoke => Ramp(LampSmokeRamp, progress),
        ParticleStyle.Pulse => Ramp(PulseRamp, progress),
        ParticleStyle.GasShimmer => GasRamp[(variant + (int)(progress * 6f)) % GasRamp.Length],
        ParticleStyle.GasFlare => Ramp(FlareRamp, progress),
        _ => ChunkRamp[Math.Min(ChunkRamp.Length - 1, (int)(progress * ChunkRamp.Length) + (variant & 1))]
    };

    public static ConsoleColor Color(ParticleStyle style, byte variant, float progress)
    {
        var faded = progress > .65f;
        return style switch
        {
            ParticleStyle.Dirt => (variant & 1) == 0 ? ConsoleColor.DarkYellow : ConsoleColor.DarkRed,
            ParticleStyle.Stone => faded || (variant & 1) == 0 ? ConsoleColor.DarkGray : ConsoleColor.Gray,
            ParticleStyle.Granite => (variant & 1) == 0 ? ConsoleColor.DarkGray : ConsoleColor.DarkMagenta,
            ParticleStyle.Copper => faded ? ConsoleColor.DarkRed : ConsoleColor.DarkYellow,
            ParticleStyle.Silver => faded ? ConsoleColor.Gray : ConsoleColor.White,
            ParticleStyle.Gold => faded ? ConsoleColor.DarkYellow : ConsoleColor.Yellow,
            ParticleStyle.Dust => progress < .3f ? ConsoleColor.Gray : ConsoleColor.DarkGray,
            ParticleStyle.Spark => faded ? ConsoleColor.DarkYellow : ConsoleColor.Yellow,
            ParticleStyle.Glint => GlintColor(variant, progress),
            ParticleStyle.Mist => ConsoleColor.DarkGray,
            ParticleStyle.Coin => (int)(progress * 24f + variant) % 4 == 2 ? ConsoleColor.DarkYellow : ConsoleColor.Yellow,
            ParticleStyle.Pop => faded ? ConsoleColor.DarkYellow : ConsoleColor.White,
            ParticleStyle.Trail => faded ? ConsoleColor.DarkGray : ConsoleColor.Gray,
            ParticleStyle.Obsidian => (variant & 1) == 0 ? ConsoleColor.DarkMagenta : ConsoleColor.DarkGray,
            ParticleStyle.Slate => faded || (variant & 1) == 0 ? ConsoleColor.DarkBlue : ConsoleColor.Blue,
            ParticleStyle.Quartzite => faded || (variant & 1) == 0 ? ConsoleColor.DarkCyan : ConsoleColor.Cyan,
            ParticleStyle.Gem => faded ? ConsoleColor.DarkMagenta : ConsoleColor.Magenta,
            ParticleStyle.Flash => (variant & 1) == 0 ? ConsoleColor.White : ConsoleColor.Yellow,
            ParticleStyle.Blast => progress < .3f ? ConsoleColor.Yellow
                : faded ? ConsoleColor.DarkGray : ConsoleColor.DarkYellow,
            ParticleStyle.Pulse => PulseColor(variant, progress),
            ParticleStyle.GasShimmer => ConsoleColor.DarkGreen,
            ParticleStyle.GasFlare => progress < .3f ? ConsoleColor.Yellow
                : faded ? ConsoleColor.DarkRed : ConsoleColor.Red,
            ParticleStyle.Smoke or ParticleStyle.LampSmoke => faded ? ConsoleColor.DarkGray : ConsoleColor.Gray,
            _ => ConsoleColor.Gray
        };
    }

    /// <summary>
    /// The glyph for a point in a particle's life. Spawners use it at progress 0, so a particle looks right in the
    /// frame it is created, before <c>ParticleLifetimeSystem</c> has updated it.
    /// </summary>
    public static Glyph GlyphAt(ParticleStyle style, byte variant, float progress) =>
        new(Character(style, variant, progress), Color(style, variant, progress));

    /// <summary>
    /// Whether a style is drawn as a see-through tint over the cells (<c>Canvas.Tint</c>) instead of a glyph in 24-bit
    /// colour: mist and gas shimmer.
    /// </summary>
    public static bool IsTint(ParticleStyle style) => style is ParticleStyle.Mist or ParticleStyle.GasShimmer;

    /// <summary>The colour of a tint style (<see cref="IsTint"/>); black for any other style.</summary>
    public static uint TintColor(ParticleStyle style) => style switch
    {
        ParticleStyle.Mist => MistTint,
        ParticleStyle.GasShimmer => GasTint,
        _ => 0
    };

    /// <summary>
    /// The opacity of a tint style at a point in its life: it fades in, peaks halfway and fades out
    /// (<c>peak × 4p(1 − p)</c>), so a puff never pops in or out. 0 for any other style.
    /// </summary>
    public static float TintAlpha(ParticleStyle style, float progress)
    {
        var peak = style switch
        {
            ParticleStyle.Mist => MistPeakAlpha,
            ParticleStyle.GasShimmer => GasPeakAlpha,
            _ => 0f
        };
        var p = Math.Clamp(progress, 0f, 1f);
        return peak * 4f * p * (1f - p);
    }

    /// <summary>The debris style for a tile type: the colour of the chunks that fly off it.</summary>
    public static ParticleStyle DebrisStyle(TileType type) => type switch
    {
        TileType.Stone => ParticleStyle.Stone,
        TileType.Granite => ParticleStyle.Granite,
        TileType.Copper => ParticleStyle.Copper,
        TileType.Silver => ParticleStyle.Silver,
        TileType.Gold => ParticleStyle.Gold,
        TileType.Obsidian => ParticleStyle.Obsidian,
        TileType.Slate => ParticleStyle.Slate,
        TileType.Quartzite => ParticleStyle.Quartzite,
        TileType.Gem => ParticleStyle.Gem,
        TileType.Fossil => ParticleStyle.Stone,
        TileType.Relic => ParticleStyle.Slate,
        _ => ParticleStyle.Dirt
    };

    /// <summary>Glint variants encode the ore: 0 copper, 1 silver, 2 gold, 3 gem.</summary>
    public static byte GlintVariant(TileType ore) => ore switch
    {
        TileType.Gem => 3,
        TileType.Gold => 2,
        TileType.Silver => 1,
        _ => 0
    };

    //**********************************************************
    //** internals
    //**********************************************************

    private static char Ramp(string ramp, float progress) =>
        ramp[Math.Clamp((int)(progress * ramp.Length), 0, ramp.Length - 1)];

    /// <summary>Pulse variants encode the find: 0 fossil (white), 1 relic (yellow); dimmer at the ends.</summary>
    public static byte PulseVariant(TileType find) => find == TileType.Relic ? (byte)1 : (byte)0;

    private static ConsoleColor PulseColor(byte variant, float progress)
    {
        var bright = progress is > .25f and < .75f;
        return variant == 1
            ? bright ? ConsoleColor.Yellow : ConsoleColor.DarkYellow
            : bright ? ConsoleColor.White : ConsoleColor.Gray;
    }

    private static ConsoleColor GlintColor(byte variant, float progress)
    {
        // The middle of the twinkle flashes white, the ends show the metal's colour.
        if (progress is > .35f and < .65f)
            return ConsoleColor.White;

        return variant switch
        {
            3 => ConsoleColor.Magenta,
            2 => ConsoleColor.Yellow,
            1 => ConsoleColor.Cyan,
            _ => ConsoleColor.DarkYellow
        };
    }
}
