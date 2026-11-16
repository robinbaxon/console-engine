using System;
using EternalDigger.Mining;
using EternalDigger.Rendering;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace EternalDigger.Tests;

public class LightMapTests
{
    private const int Seed = 99;
    private const int MaxRows = 30;

    /// <summary>A row far below daylight.</summary>
    private const int Row = Mine.SurfaceRow + 50;

    private const int Column = 5;

    [Test]
    public void Compute_SurfaceRows_AreBright()
    {
        var (mine, light) = Create();

        light.Compute(mine, 0, MaxRows, new Vector2(Column + .5f, Row + .5f), Vector2.UnitX);

        Assert.That(light.Get(20, 3), Is.EqualTo(LightMap.Bright));
        Assert.That(light.Get(20, Mine.SurfaceRow), Is.EqualTo(LightMap.Bright));
    }

    [Test]
    public void Get_OutsideTheWindow_IsDark()
    {
        var (mine, light) = Create();

        light.Compute(mine, 0, 5, new Vector2(Column + .5f, 2.5f), Vector2.UnitX);

        Assert.That(light.Get(Column, 6), Is.EqualTo(LightMap.Dark));
        Assert.That(light.Get(-1, 2), Is.EqualTo(LightMap.Dark));
    }

    [Test]
    public void Compute_SolidNeighbourOfTheLamp_IsBright()
    {
        var (mine, light) = Create();
        RockAround(mine);

        ComputeAt(mine, light, Vector2.UnitX);

        Assert.That(light.Get(Column - 1, Row), Is.EqualTo(LightMap.Bright));
        Assert.That(light.Get(Column, Row + 1), Is.EqualTo(LightMap.Bright));
    }

    [Test]
    public void Compute_TunnelAheadInTheCone_IsBrightAndSeen()
    {
        var (mine, light) = Create();
        RockAround(mine);
        DigTunnel(mine, Column + 1, Column + 4);

        ComputeAt(mine, light, Vector2.UnitX);

        Assert.That(light.Get(Column + 3, Row), Is.EqualTo(LightMap.Bright));
        Assert.That(mine.At(Column + 3, Row).Seen, Is.True);
    }

    [Test]
    public void Compute_TileBehindSolidRock_IsDark()
    {
        var (mine, light) = Create();
        RockAround(mine);

        ComputeAt(mine, light, Vector2.UnitX);

        Assert.That(light.Get(Column + 3, Row), Is.EqualTo(LightMap.Dark));
        Assert.That(mine.At(Column + 3, Row).Seen, Is.False);
    }

    [Test]
    public void Compute_TunnelBehindTheMiner_IsDark()
    {
        var (mine, light) = Create();
        RockAround(mine);
        DigTunnel(mine, Column - 4, Column - 1);

        ComputeAt(mine, light, Vector2.UnitX);

        Assert.That(light.Get(Column - 3, Row), Is.EqualTo(LightMap.Dark));
    }

    [Test]
    public void Compute_FarEndOfALongTunnel_GetsDimmer()
    {
        var (mine, light) = Create();
        RockAround(mine);
        DigTunnel(mine, Column + 1, Column + 9);

        ComputeAt(mine, light, Vector2.UnitX);

        Assert.That(light.Get(Column + 5, Row), Is.EqualTo(LightMap.Lit));
        Assert.That(light.Get(Column + 7, Row), Is.EqualTo(LightMap.Dim));
        Assert.That(light.Get(Column + 9, Row), Is.EqualTo(LightMap.Dark));
    }

    [Test]
    public void Intensity_AlongALongTunnel_FallsSmoothlyAndMatchesTheLevels()
    {
        var (mine, light) = Create();
        RockAround(mine);
        DigTunnel(mine, Column + 1, Column + 9);

        ComputeAt(mine, light, Vector2.UnitX);

        for (var column = Column + 2; column <= Column + 8; column++)
            Assert.That(light.Intensity(column, Row), Is.LessThanOrEqualTo(light.Intensity(column - 1, Row)),
                $"column {column}");
        Assert.That(light.Intensity(Column, Row), Is.EqualTo(1f));
        Assert.That(light.Intensity(Column + 3, Row), Is.InRange(.75f, 1f), "bright range");
        Assert.That(light.Intensity(Column + 5, Row), Is.InRange(.5f, .75f), "lit range");
        Assert.That(light.Intensity(Column + 7, Row), Is.InRange(.25f, .5f), "dim range");
        Assert.That(light.Intensity(Column + 9, Row), Is.Zero, "beyond the reach");
    }

    [Test]
    public void Intensity_AnyTile_IsAWholeStep()
    {
        var (mine, light) = Create();
        RockAround(mine);
        DigTunnel(mine, Column + 1, Column + 9);

        ComputeAt(mine, light, Vector2.UnitX);

        for (var column = Column; column <= Column + 9; column++)
        {
            var steps = light.Intensity(column, Row) * LightMap.IntensitySteps;
            Assert.That(steps, Is.EqualTo(MathF.Round(steps)).Within(1e-4f), $"column {column}");
        }
    }

    [Test]
    public void Intensity_FlickerFrame_IsLowerButNotDark()
    {
        var (mine, light) = Create();
        RockAround(mine);
        DigTunnel(mine, Column + 1, Column + 9);
        var origin = new Vector2(Column + .5f, Row + .5f);

        light.Compute(mine, Row - MaxRows / 2, MaxRows, origin, Vector2.UnitX, LightMap.LampRange, dimmed: false);
        var steady = light.Intensity(Column + 4, Row);
        light.Compute(mine, Row - MaxRows / 2, MaxRows, origin, Vector2.UnitX, LightMap.LampRange, dimmed: true);

        Assert.That(light.Intensity(Column + 4, Row), Is.LessThan(steady).And.GreaterThan(0f));
    }

    [Test]
    public void Intensity_AtTheConeEdge_IsLowerThanStraightAhead()
    {
        var (mine, light) = Create();
        mine.At(Column, Row) = new TileCell { Type = TileType.Air };
        for (var row = Row - 4; row <= Row + 4; row++)
        for (var column = Column; column <= Column + 6; column++)
            mine.At(column, row) = new TileCell { Type = TileType.Air };

        ComputeAt(mine, light, Vector2.UnitX);

        // (4, 3) is about 37° off the lamp's axis, inside the 40° cone; (5, 0) is about as far, straight ahead.
        Assert.That(light.Get(Column + 4, Row + 3), Is.GreaterThan(LightMap.Dark));
        Assert.That(light.Intensity(Column + 4, Row + 3), Is.LessThan(light.Intensity(Column + 5, Row)));
    }

    [Test]
    public void IsWarm_LampLight_IsWarmAndDaylightIsNot()
    {
        var (mine, light) = Create();
        RockAround(mine);
        DigTunnel(mine, Column + 1, Column + 4);

        ComputeAt(mine, light, Vector2.UnitX);
        Assert.That(light.IsWarm(Column + 2, Row), Is.True, "lamp");

        light.Compute(mine, 0, MaxRows, new Vector2(Column + .5f, Row + .5f), Vector2.UnitX);
        Assert.That(light.IsWarm(20, 3), Is.False, "the sky");
        Assert.That(light.Intensity(20, 3), Is.EqualTo(1f));
    }

    [Test]
    public void AddLantern_AroundTheCentre_IsWarmAndFadesOutward()
    {
        var (mine, light) = Create();
        RockAround(mine);
        ComputeAt(mine, light, -Vector2.UnitX);
        var center = new Vector2(Column + 8.5f, Row + .5f);

        light.AddLantern(mine, center, 2f);

        Assert.That(light.IsWarm(Column + 8, Row), Is.True);
        Assert.That(light.Intensity(Column + 8, Row), Is.GreaterThan(light.Intensity(Column + 10, Row)));
        Assert.That(light.Intensity(Column + 10, Row), Is.GreaterThan(0f), "inside 1.5 times the radius");
        Assert.That(light.Intensity(Column + 13, Row), Is.Zero, "beyond it");
    }

    //**********************************************************
    //** helpers
    //**********************************************************

    private static (Mine Mine, LightMap Light) Create() => (new Mine(Seed), new LightMap(Mine.Columns, MaxRows));

    private static void ComputeAt(Mine mine, LightMap light, Vector2 direction) =>
        light.Compute(mine, Row - MaxRows / 2, MaxRows, new Vector2(Column + .5f, Row + .5f), direction);

    /// <summary>Solid stone around the miner's tile (which is air), so caves in the seed can't change the result.</summary>
    private static void RockAround(Mine mine)
    {
        for (var row = Row - 10; row <= Row + 10; row++)
        for (var column = 1; column < Mine.Columns - 1; column++)
            mine.At(column, row) = new TileCell { Type = TileType.Stone };

        mine.At(Column, Row) = new TileCell { Type = TileType.Air };
    }

    private static void DigTunnel(Mine mine, int fromColumn, int toColumn)
    {
        for (var column = fromColumn; column <= toColumn; column++)
            mine.At(column, Row) = new TileCell { Type = TileType.Air };
    }
}
