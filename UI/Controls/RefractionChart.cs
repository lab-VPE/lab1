using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Avalonia;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using UI.Models;

namespace UI.Controls;

public class RefractionChart : UserControl
{
    private readonly CartesianChart _chart;
    private static readonly SKColor Teal = SKColor.Parse("#2ec4b6");
    private static readonly SKColor Yellow = SKColor.Parse("#ffd166");
    private static readonly SKColor Text = SKColor.Parse("#c8ccd6");

    public RefractionChart()
    {
        _chart = new CartesianChart
        {
            LegendPosition = LegendPosition.Bottom,
            LegendTextPaint = new SolidColorPaint(Text),
            XAxes = new[]
            {

                new Axis { Name = "Координата x", LabelsPaint = new SolidColorPaint(Text), NamePaint = new SolidColorPaint(Text) }

                new Axis { Name = "Точка", LabelsPaint = new SolidColorPaint(Text), NamePaint = new SolidColorPaint(Text) }

            },
            YAxes = new[]
            {
                new Axis { Name = "N", LabelsPaint = new SolidColorPaint(Teal), NamePaint = new SolidColorPaint(Teal) },
                new Axis { Name = "Угол, °", LabelsPaint = new SolidColorPaint(Yellow), NamePaint = new SolidColorPaint(Yellow),
                           Position = AxisPosition.End, ShowSeparatorLines = false }
            },
            Series = Array.Empty<ISeries>()
        };
        Content = _chart;
    }

    public void SetData(List<TrajectoryPoint> points)
    {
        _chart.Series = new ISeries[]
        {
            new LineSeries<double>
            {
                Name = "N",
                Values = points.Select(p => p.N).ToArray(),
                Stroke = new SolidColorPaint(Teal) { StrokeThickness = 2 },
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0,
                ScalesYAt = 0
            },
            new LineSeries<double>
            {
                Name = "Angle",
                Values = points.Select(p => p.Angle).ToArray(),
                Stroke = new SolidColorPaint(Yellow) { StrokeThickness = 2 },
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0,
                ScalesYAt = 1
            }
        };
    }

    public void Clear() => _chart.Series = Array.Empty<ISeries>();
}
