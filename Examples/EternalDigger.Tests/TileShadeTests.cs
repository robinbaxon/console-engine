using System;
using EternalDigger.Mining;
using EternalDigger.Rendering;
using EternalDigger.Tests.Fakes;
using ConsoleEngine.Infrastructure.Rendering;
using NUnit.Framework;

namespace EternalDigger.Tests;

/// <summary>24-bit colours of the mine: <see cref="Rgb"/>, <see cref="TileShade"/> and the <see cref="Canvas"/>.</summary>
public class TileShadeTests
{
    [Test]
    public void Scale_Half_HalvesEveryChannel() =>
        Assert.That(Rgb.Scale(0x8040FE, .5f), Is.EqualTo(0x40207Fu));

    [Test]
    public void Scale_AboveOne_ClampsAt255() =>
        Assert.That(Rgb.Scale(0xC08010, 2f), Is.EqualTo(0xFFFF20u));

    [Test]
    public void Of_ConsoleColors_AreTheConsoleDefaults()
    {
        Assert.That(Rgb.Of(ConsoleColor.Black), Is.EqualTo(Rgb.Black));
        Assert.That(Rgb.Of(ConsoleColor.White), Is.EqualTo(0xF2F2F2u));
    }

    [Test]
    public void Lit_Daylight_KeepsTheHue() =>
        Assert.That(TileShade.Lit(0x808080, .5f, warmth: 0f), Is.EqualTo(0x404040u));

    [Test]
    public void Lit_LampLight_IsWarm()
    {
        var color = TileShade.Lit(0x808080, 1f, warmth: 1f);

        var red = (color >> 16) & 0xFF;
        var green = (color >> 8) & 0xFF;
        var blue = color & 0xFF;
        Assert.That(red, Is.GreaterThan(green));
        Assert.That(green, Is.GreaterThan(blue));
    }

    [Test]
    public void Speck_InFaintLight_IsBrighterThanItsRock()
    {
        var colors = TileShade.For(TileType.Gold);

        var speck = TileShade.Speck(colors.Speck, .25f, 0f);

        Assert.That(speck & 0xFF0000, Is.GreaterThan(TileShade.Lit(colors.Foreground, .25f, 0f) & 0xFF0000));
    }

    [Test]
    public void Crack_OnAWellLitFill_IsBlack() =>
        Assert.That(TileShade.Crack(0x555555, 1f), Is.EqualTo(Rgb.Black));

    [Test]
    public void Crack_OnDarkRock_IsLight() =>
        Assert.That(TileShade.Crack(Rgb.Black, .3f), Is.Not.EqualTo(Rgb.Black));

    [TestCase(TileType.Copper)]
    [TestCase(TileType.Silver)]
    [TestCase(TileType.Gold)]
    [TestCase(TileType.Gem)]
    [TestCase(TileType.Obsidian)]
    [TestCase(TileType.Fossil)]
    [TestCase(TileType.Relic)]
    public void For_TilesWithSpecks_HaveASpeckColour(TileType type)
    {
        Assert.That(TileLooks.For(type).HasSpecks, Is.True);
        Assert.That(TileShade.For(type).Speck, Is.Not.Zero);
    }

    [Test]
    public void DrawOver_OnA24BitCell_KeepsTheCharacterDrawn()
    {
        var console = new RenderConsole(new FakeConsoleHandler(4, 2));
        var canvas = new Canvas(console);

        canvas.DrawRgb(1, 1, '▓', 0x9A9A96, 0x55565A);
        canvas.DrawOver(1, 1, '*', ConsoleColor.Yellow);

        Assert.That(console.GetCharAt(1, 1), Is.EqualTo('*'));
        Assert.That(canvas.BackgroundAt(1, 1), Is.EqualTo(ConsoleColor.Black), "no palette background recorded");
    }
}
