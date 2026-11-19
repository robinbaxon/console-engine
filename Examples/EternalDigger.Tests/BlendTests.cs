using System;
using ConsoleEngine.Infrastructure.Rendering;
using EternalDigger.Components;
using EternalDigger.Effects;
using EternalDigger.Mining;
using EternalDigger.Rendering;
using EternalDigger.Tests.Fakes;
using NUnit.Framework;

namespace EternalDigger.Tests;

/// <summary>
/// Blended 24-bit overlays: <see cref="Rgb.Mix"/> and <see cref="Rgb.Grey"/>, the <see cref="Canvas"/> tint and
/// see-through panels, and the colours built on them (remembered and choked rock, mist and gas tints).
/// </summary>
public class BlendTests
{
    private static uint Red(uint rgb) => (rgb >> 16) & 0xFF;
    private static uint Green(uint rgb) => (rgb >> 8) & 0xFF;
    private static uint Blue(uint rgb) => rgb & 0xFF;

    [Test]
    public void Mix_AlphaZero_KeepsTheFirstColour() =>
        Assert.That(Rgb.Mix(0x204060, 0xFFFFFF, 0f), Is.EqualTo(0x204060u));

    [Test]
    public void Mix_AlphaOne_IsTheSecondColour() =>
        Assert.That(Rgb.Mix(0x204060, 0xA0B0C0, 1f), Is.EqualTo(0xA0B0C0u));

    [Test]
    public void Mix_Half_IsHalfwayOnEveryChannel() =>
        Assert.That(Rgb.Mix(0x000000, 0x80FF40, .5f), Is.EqualTo(0x408020u));

    [Test]
    public void Mix_AlphaOutOfRange_IsClamped()
    {
        Assert.That(Rgb.Mix(0x102030, 0xFFFFFF, -1f), Is.EqualTo(0x102030u));
        Assert.That(Rgb.Mix(0x102030, 0xFFFFFF, 2f), Is.EqualTo(0xFFFFFFu));
    }

    [Test]
    public void Grey_Full_GivesEqualChannelsOfTheSameBrightness()
    {
        var grey = Rgb.Grey(0xC05020, 1f);

        Assert.That(Red(grey), Is.EqualTo(Green(grey)));
        Assert.That(Green(grey), Is.EqualTo(Blue(grey)));
        Assert.That(Red(grey), Is.EqualTo((uint)MathF.Round(Rgb.Luma(0xC05020))));
    }

    [Test]
    public void Grey_None_KeepsTheColour() =>
        Assert.That(Rgb.Grey(0xC05020, 0f), Is.EqualTo(0xC05020u));

    [Test]
    public void Memory_OfDirt_IsDarkerAndGreyerButStillWarm()
    {
        var dirt = TileShade.For(TileType.Dirt).Foreground;

        var memory = TileShade.Memory(dirt);

        Assert.That(Rgb.Luma(memory), Is.LessThan(Rgb.Luma(dirt)));
        Assert.That(Red(memory) - Blue(memory), Is.LessThan(Red(dirt) - Blue(dirt)), "greyer");
        Assert.That(Red(memory), Is.GreaterThan(Blue(memory)), "still a warm brown");
    }

    [Test]
    public void Memory_OfSlate_StaysBlue()
    {
        var memory = TileShade.Memory(TileShade.For(TileType.Slate).Foreground);

        Assert.That(Blue(memory), Is.GreaterThan(Red(memory)));
    }

    [Test]
    public void Choked_IsDarkerAndGreyer()
    {
        const uint lit = 0x9A6030;

        var choked = TileShade.Choked(lit);

        Assert.That(Rgb.Luma(choked), Is.LessThan(Rgb.Luma(lit) * .5f));
        Assert.That(Red(choked) - Blue(choked), Is.LessThan(Red(lit) - Blue(lit)));
    }

    [Test]
    public void Tint_OverA24BitCell_BlendsBothColoursAndKeepsTheCharacter()
    {
        var (console, canvas) = NewCanvas();
        canvas.DrawRgb(1, 1, '▓', 0x808080, 0x000000);

        canvas.Tint(1, 1, 0xFFFFFF, .5f);

        Assert.That(console.GetCharAt(1, 1), Is.EqualTo('▓'));
        Assert.That(canvas.ForegroundRgbAt(1, 1), Is.EqualTo(Rgb.Mix(0x808080, 0xFFFFFF, .5f)));
        Assert.That(canvas.BackgroundRgbAt(1, 1), Is.EqualTo(0x808080u));
    }

    [Test]
    public void Tint_OverAPaletteCell_BlendsFromItsConsoleColours()
    {
        var (_, canvas) = NewCanvas();
        canvas.Draw(0, 0, '#', ConsoleColor.White, ConsoleColor.DarkBlue);

        canvas.Tint(0, 0, Rgb.Black, .5f);

        Assert.That(canvas.BackgroundRgbAt(0, 0), Is.EqualTo(Rgb.Mix(Rgb.Of(ConsoleColor.DarkBlue), Rgb.Black, .5f)));
        Assert.That(canvas.ForegroundRgbAt(0, 0), Is.EqualTo(Rgb.Mix(Rgb.Of(ConsoleColor.White), Rgb.Black, .5f)));
    }

    [Test]
    public void Tint_Twice_AddsUp()
    {
        var (_, canvas) = NewCanvas();
        canvas.DrawRgb(2, 0, ' ', Rgb.Black, Rgb.Black);

        canvas.Tint(2, 0, 0xFFFFFF, .5f);
        var once = canvas.BackgroundRgbAt(2, 0);
        canvas.Tint(2, 0, 0xFFFFFF, .5f);

        Assert.That(Rgb.Luma(canvas.BackgroundRgbAt(2, 0)), Is.GreaterThan(Rgb.Luma(once)));
    }

    [Test]
    public void Tint_AlphaZeroOrOffScreen_ChangesNothing()
    {
        var (_, canvas) = NewCanvas();
        canvas.DrawRgb(0, 0, 'a', 0x112233, 0x445566);

        canvas.Tint(0, 0, 0xFFFFFF, 0f);
        canvas.Tint(-1, 0, 0xFFFFFF, 1f);
        canvas.Tint(0, 99, 0xFFFFFF, 1f);

        Assert.That(canvas.BackgroundRgbAt(0, 0), Is.EqualTo(0x445566u));
        Assert.That(canvas.ForegroundRgbAt(0, 0), Is.EqualTo(0x112233u));
    }

    [Test]
    public void DrawGlass_LetsTheSceneShowThroughTheFill()
    {
        var (console, canvas) = NewCanvas();
        canvas.DrawRgb(3, 1, '▒', 0xFFFFFF, 0xC08040);
        canvas.DrawRgb(2, 1, '▒', 0xFFFFFF, 0x204080);

        canvas.DrawGlass(3, 1, 'A', 0xF2F2F2, PanelFill.Prompt.Rgb, PanelFill.Prompt.Alpha);
        canvas.DrawGlass(2, 1, 'B', 0xF2F2F2, PanelFill.Prompt.Rgb, PanelFill.Prompt.Alpha);

        Assert.That(console.GetCharAt(3, 1), Is.EqualTo('A'));
        Assert.That(canvas.BackgroundRgbAt(3, 1),
            Is.EqualTo(Rgb.Mix(0xC08040, PanelFill.Prompt.Rgb, PanelFill.Prompt.Alpha)));
        Assert.That(canvas.BackgroundRgbAt(3, 1), Is.Not.EqualTo(canvas.BackgroundRgbAt(2, 1)),
            "different scene colours stay different through the glass");
    }

    [Test]
    public void DrawPanel_In16Colours_DrawsTheSolidPaletteFill()
    {
        var (_, canvas) = NewCanvas();
        canvas.DrawRgb(0, 0, '▒', 0xFFFFFF, 0xC08040);

        canvas.DrawPanel(0, 0, 'A', ConsoleColor.White, PanelFill.Prompt);

        Assert.That(canvas.IsTrueColor, Is.False, "the fake console has no 24-bit colour");
        Assert.That(canvas.BackgroundAt(0, 0), Is.EqualTo(ConsoleColor.DarkBlue));
        Assert.That(canvas.BackgroundRgbAt(0, 0), Is.EqualTo(Rgb.Of(ConsoleColor.DarkBlue)));
    }

    [TestCase(ParticleStyle.Mist)]
    [TestCase(ParticleStyle.GasShimmer)]
    public void TintAlpha_FadesInAndOut(ParticleStyle style)
    {
        Assert.That(ParticleLooks.IsTint(style), Is.True);
        Assert.That(ParticleLooks.TintColor(style), Is.Not.Zero);
        Assert.That(ParticleLooks.TintAlpha(style, 0f), Is.Zero);
        Assert.That(ParticleLooks.TintAlpha(style, 1f), Is.Zero);
        Assert.That(ParticleLooks.TintAlpha(style, .5f), Is.GreaterThan(ParticleLooks.TintAlpha(style, .2f)));
        Assert.That(ParticleLooks.TintAlpha(style, .5f), Is.LessThan(.5f), "always see-through");
    }

    [TestCase(ParticleStyle.Dust)]
    [TestCase(ParticleStyle.Spark)]
    [TestCase(ParticleStyle.Glint)]
    public void IsTint_OtherStyles_StayGlyphs(ParticleStyle style)
    {
        Assert.That(ParticleLooks.IsTint(style), Is.False);
        Assert.That(ParticleLooks.TintAlpha(style, .5f), Is.Zero);
    }

    private static (RenderConsole Console, Canvas Canvas) NewCanvas()
    {
        var console = new RenderConsole(new FakeConsoleHandler(4, 2));
        return (console, new Canvas(console));
    }
}
