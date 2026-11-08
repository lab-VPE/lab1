using System.Globalization;
using System.Text.Json;
using UI.Helpers;
using UI.Models;
using UI.Services;

namespace UI.Tests;

public class ExportAndMathTests
{
    private static List<TrajectoryPoint> Sample() => new()
    {
        new() { X = 0, Y = 0, N = 1, Angle = 45 },
        new() { X = 3, Y = 4, N = 1.05, Angle = 44.5 },
    };

    [Fact]
    public void PathLengths_AreCumulative()
    {
        var s = TrajectoryMath.PathLengths(Sample());
        Assert.Equal(0, s[0]);
        Assert.Equal(5, s[1], 9);
    }

    [Fact]
    public void Csv_HasHeaderAndRows()
    {
        var lines = ExportService.BuildCsv(Sample()).TrimEnd('\n').Split('\n');
        Assert.Equal("index,path_length,x,y,n,angle_deg", lines[0]);
        Assert.Equal(3, lines.Length);
        Assert.Equal("1,5,3,4,1.05,44.5", lines[2]);
    }

    [Fact]
    public void Csv_IsCultureInvariant()
    {
        var old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
            var csv = ExportService.BuildCsv(Sample());
            Assert.Contains("1.05", csv);
            Assert.DoesNotContain("1,05", csv);
        }
        finally { CultureInfo.CurrentCulture = old; }
    }

    [Fact]
    public void Json_ContainsMetadataRequestTrajectoryAndStatistics()
    {
        var request = new RequestModel { EnvironmentFormula = "1.0 + 0.05*x", StartAngle = 45, StepSize = 0.05, Color = "#ff0000" };
        var json = ExportService.BuildJson(request, Sample(), new StatisticsResult { PathLength = 5 },
            new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.FromHours(2)));

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(ExportService.FormatVersion, root.GetProperty("version").GetString());
        Assert.Equal("2026-11-02T08:00:00Z", root.GetProperty("exportedAtUtc").GetString());
        Assert.Equal("1.0 + 0.05*x", root.GetProperty("request").GetProperty("environmentFormula").GetString());
        Assert.Equal(2, root.GetProperty("trajectory").GetArrayLength());
        Assert.Equal(5, root.GetProperty("statistics").GetProperty("pathLength").GetDouble());
    }

    [Fact]
    public void ZoomAt_KeepsPointUnderCursorFixed()
    {
        double zoom = 1, panX = 0, panY = 0;
        const double cx = 300, cy = 200;
        var bx = (cx - panX) / zoom;
        var by = (cy - panY) / zoom;

        (panX, panY, zoom) = SceneMath.ZoomAt(cx, cy, panX, panY, zoom, 2);

        Assert.Equal(2, zoom, 9);
        Assert.Equal(cx, bx * zoom + panX, 9);
        Assert.Equal(cy, by * zoom + panY, 9);
    }

    [Fact]
    public void ZoomAt_IsClamped()
    {
        var (_, _, hi) = SceneMath.ZoomAt(0, 0, 0, 0, 1, 1000);
        var (_, _, lo) = SceneMath.ZoomAt(0, 0, 0, 0, 1, 1e-9);
        Assert.Equal(SceneMath.MaxZoom, hi);
        Assert.Equal(SceneMath.MinZoom, lo);
    }

    [Theory]
    [InlineData(30, 1)]
    [InlineData(10, 5)]
    [InlineData(1, 50)]
    [InlineData(0, 1)]
    public void NiceStep(double pixelsPerUnit, double expected)
        => Assert.Equal(expected, SceneMath.NiceStep(pixelsPerUnit), 9);
}
