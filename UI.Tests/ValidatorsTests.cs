using UI.Helpers;

namespace UI.Tests;

public class ValidatorsTests
{
    [Theory]
    [InlineData("1.0 + 0.05*x")]
    [InlineData("x")]
    [InlineData("2")]
    [InlineData("(1 + x) * 0.5")]
    [InlineData("1.0 + 0.01*x^2")]
    public void Formula_Valid(string formula)
        => Assert.True(Validators.ValidateFormula(formula).Valid);

    [Theory]
    [InlineData("", "пустой")]
    [InlineData("   ", "пустой")]
    [InlineData("1 + sin(x)", "символы")]
    [InlineData("(1 + x", "Не закрыта")]
    [InlineData("1 + x)", "Лишняя")]
    public void Formula_Invalid(string formula, string messagePart)
    {
        var r = Validators.ValidateFormula(formula);
        Assert.False(r.Valid);
        Assert.Contains(messagePart, r.Message);
    }

    [Theory]
    [InlineData("45", true)]
    [InlineData("-360", true)]
    [InlineData("360", true)]
    [InlineData("361", false)]
    [InlineData("-361", false)]
    [InlineData("abc", false)]
    [InlineData("", false)]
    public void Angle(string value, bool valid)
        => Assert.Equal(valid, Validators.ValidateAngle(value).Valid);

    [Theory]
    [InlineData("0.05", true)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("-0.1", false)]
    [InlineData("1.5", false)]
    [InlineData("x", false)]
    public void Step(string value, bool valid)
        => Assert.Equal(valid, Validators.ValidateStep(value).Valid);

    [Theory]
    [InlineData("1.5", true)]
    [InlineData("1,5", true)]
    [InlineData("-8", true)]
    [InlineData("1e400", false)]
    [InlineData("", false)]
    [InlineData("abc", false)]
    public void Coordinate(string value, bool valid)
        => Assert.Equal(valid, Validators.ValidateCoordinate(value).Valid);

    [Theory]
    [InlineData("#ff0000", true)]
    [InlineData("#00FF7a", true)]
    [InlineData("red", false)]
    [InlineData("#ff00", false)]
    [InlineData("ff0000", false)]
    public void Color(string value, bool valid)
        => Assert.Equal(valid, Validators.ValidateColor(value).Valid);

    [Fact]
    public void Bounds_Valid()
        => Assert.True(Validators.ValidateBounds("-10", "10", "-5", "5").Valid);

    [Fact]
    public void Bounds_XMinNotLessThanXMax()
    {
        var r = Validators.ValidateBounds("10", "10", "-5", "5");
        Assert.False(r.Valid);
        Assert.Contains("XMin", r.Message);
    }

    [Fact]
    public void Bounds_YMinNotLessThanYMax()
    {
        var r = Validators.ValidateBounds("-10", "10", "5", "-5");
        Assert.False(r.Valid);
        Assert.Contains("YMin", r.Message);
    }

    [Fact]
    public void Bounds_NotNumbers()
        => Assert.False(Validators.ValidateBounds("a", "10", "-5", "5").Valid);
}
