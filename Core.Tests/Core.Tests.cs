using Xunit;
using Core.Formula;
using Core.Physics;
using Core.Models;

namespace Core.Tests;

public class CoreTests
{
    // 1. Тест парсера: вычитание
    [Fact]
    public void Parser_ShouldHandleSubtraction()
    {
        var parser = new FormulaParser("2 - 3");
        Assert.True(parser.IsValid(out _));
        Assert.Equal(-1.0, parser.Evaluate(0));
    }

    // 2. Тест парсера: вычитание с переменной
    [Fact]
    public void Parser_ShouldHandleVariableSubtraction()
    {
        var parser = new FormulaParser("x - 1");
        Assert.True(parser.IsValid(out _));
        Assert.Equal(4.0, parser.Evaluate(5)); // 5 - 1 = 4
    }

    // 3. Тест парсера: унарный минус
    [Fact]
    public void Parser_ShouldHandleUnaryMinus()
    {
        var parser = new FormulaParser("-x");
        Assert.True(parser.IsValid(out _));
        Assert.Equal(-5.0, parser.Evaluate(5));
    }

    // 4. Тест: среда с постоянным n=1 (луч должен идти прямо, угол не меняется)
    [Fact]
    public void RayCalculator_ConstantEnvironment_ShouldNotBend()
    {
        var request = new RequestModel
        {
            EnvironmentFormula = "1.0",
            StartPoint = new StartPoint { X = 0, Y = 0 },
            StartAngle = 45,
            StepSize = 0.1,
            Bounds = new Bounds { XMin = -10, XMax = 10, YMin = -10, YMax = 10 },
            Color = "#ff0000"
        };

        var calc = new RayCalculator();
        var result = calc.Run(request);

        Assert.True(result.Success);
        // Угол должен остаться 45 градусов
        Assert.Equal(45, result.Trajectory[0].Angle, 1);
        Assert.Equal(45, result.Trajectory[^1].Angle, 1);
        // Отклонение должно быть 0
        Assert.Equal(0, result.Statistics.TotalDeviation, 1);
    }
}