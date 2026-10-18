using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using UI.Models;
using SimBounds = UI.Models.Bounds;

namespace UI.Controls;

public class RayCanvas : Control
{
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<RayCanvas, double>(nameof(Progress), 0.0);

    static RayCanvas()
    {
        AffectsRender<RayCanvas>(ProgressProperty);
    }

    private List<TrajectoryPoint> _trajectory = new();
    private SimBounds _worldBounds = new() { XMin = -10, XMax = 10, YMin = -10, YMax = 10 };
    private Color _rayColor = Colors.Red;

    public List<TrajectoryPoint> Trajectory
    {
        get => _trajectory;
        set { _trajectory = value ?? new(); InvalidateVisual(); }
    }

    // Имя "Bounds" занято базовым Control.Bounds (Rect), поэтому границы мира — WorldBounds.
    public SimBounds WorldBounds
    {
        get => _worldBounds;
        set { _worldBounds = value; InvalidateVisual(); }
    }

    public Color RayColor
    {
        get => _rayColor;
        set { _rayColor = value; InvalidateVisual(); }
    }

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, Math.Clamp(value, 0, 1));
    }

    public void SetTrajectory(List<TrajectoryPoint> points, Color color, SimBounds bounds)
    {
        _trajectory = points ?? new();
        _rayColor = color;
        _worldBounds = bounds;
        InvalidateVisual();
    }

    public void SetProgress(double progress) => Progress = progress;

    public void Clear()
    {
        _trajectory = new();
        SetCurrentValue(ProgressProperty, 0.0);
        InvalidateVisual();
    }

    // ---- Преобразование координат (единый масштаб, чтобы углы не искажались) ----
    private (double scale, double offX, double offY) GetTransform(double w, double h)
    {
        var rangeX = Math.Max(_worldBounds.XMax - _worldBounds.XMin, 1e-9);
        var rangeY = Math.Max(_worldBounds.YMax - _worldBounds.YMin, 1e-9);
        var scale = Math.Min(w / rangeX, h / rangeY);
        var offX = (w - rangeX * scale) / 2;
        var offY = (h - rangeY * scale) / 2;
        return (scale, offX, offY);
    }

    public Point WorldToScreen(double x, double y, double w, double h)
    {
        var (s, ox, oy) = GetTransform(w, h);
        return new Point(ox + (x - _worldBounds.XMin) * s,
                         oy + (_worldBounds.YMax - y) * s);
    }

    public (double x, double y) ScreenToWorld(Point p, double w, double h)
    {
        var (s, ox, oy) = GetTransform(w, h);
        return (_worldBounds.XMin + (p.X - ox) / s,
                _worldBounds.YMax - (p.Y - oy) / s);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        // Фон
        context.DrawRectangle(new SolidColorBrush(Color.Parse("#14161c")), null, new Rect(0, 0, w, h));

        // Рамка области мира
        var tl = WorldToScreen(_worldBounds.XMin, _worldBounds.YMax, w, h);
        var br = WorldToScreen(_worldBounds.XMax, _worldBounds.YMin, w, h);
        var worldRect = new Rect(tl, br);
        context.DrawRectangle(new SolidColorBrush(Color.Parse("#1b1e26")),
            new Pen(new SolidColorBrush(Color.Parse("#3a3f4b")), 1), worldRect);

        // Сетка
        var gridPen = new Pen(new SolidColorBrush(Color.Parse("#262a33")), 1);
        for (var gx = Math.Ceiling(_worldBounds.XMin); gx <= _worldBounds.XMax; gx++)
        {
            var a = WorldToScreen(gx, _worldBounds.YMin, w, h);
            var b = WorldToScreen(gx, _worldBounds.YMax, w, h);
            context.DrawLine(gridPen, a, b);
        }
        for (var gy = Math.Ceiling(_worldBounds.YMin); gy <= _worldBounds.YMax; gy++)
        {
            var a = WorldToScreen(_worldBounds.XMin, gy, w, h);
            var b = WorldToScreen(_worldBounds.XMax, gy, w, h);
            context.DrawLine(gridPen, a, b);
        }

        // Оси
        var axisPen = new Pen(new SolidColorBrush(Color.Parse("#7a8090")), 1.5);
        var ax = Math.Clamp(0, _worldBounds.XMin, _worldBounds.XMax);
        var ay = Math.Clamp(0, _worldBounds.YMin, _worldBounds.YMax);
        context.DrawLine(axisPen,
            WorldToScreen(_worldBounds.XMin, ay, w, h),
            WorldToScreen(_worldBounds.XMax, ay, w, h));
        context.DrawLine(axisPen,
            WorldToScreen(ax, _worldBounds.YMin, w, h),
            WorldToScreen(ax, _worldBounds.YMax, w, h));

        // Траектория
        if (_trajectory.Count == 0) return;

        var rayBrush = new SolidColorBrush(_rayColor);
        var rayPen = new Pen(rayBrush, 2.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

        var maxIndex = Progress * (_trajectory.Count - 1);
        var full = (int)Math.Floor(maxIndex);
        var frac = maxIndex - full;

        var pts = new List<Point>();
        for (var i = 0; i <= full && i < _trajectory.Count; i++)
            pts.Add(WorldToScreen(_trajectory[i].X, _trajectory[i].Y, w, h));

        // Частичный сегмент между точками — для плавной анимации
        if (full + 1 < _trajectory.Count && frac > 0)
        {
            var p0 = _trajectory[full];
            var p1 = _trajectory[full + 1];
            pts.Add(WorldToScreen(p0.X + (p1.X - p0.X) * frac, p0.Y + (p1.Y - p0.Y) * frac, w, h));
        }

        if (pts.Count >= 2)
        {
            var geo = new StreamGeometry();
            using (var g = geo.Open())
            {
                g.BeginFigure(pts[0], false);
                for (var i = 1; i < pts.Count; i++) g.LineTo(pts[i]);
                g.EndFigure(false);
            }
            context.DrawGeometry(null, rayPen, geo);
        }

        // Точка на конце луча
        context.DrawEllipse(rayBrush, new Pen(Brushes.White, 1.5), pts[^1], 5, 5);
    }
}
