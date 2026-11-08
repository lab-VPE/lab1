using System;

namespace UI.Helpers;

/// <summary>Чистая математика сцены (без типов Avalonia): масштабирование и шаг сетки.</summary>
public static class SceneMath
{
    public const double MinZoom = 0.2;
    public const double MaxZoom = 50;

    /// <summary>
    /// Масштабирование вокруг точки (px, py) экрана. Преобразование: screen = base * zoom + pan.
    /// Возвращает новые pan и zoom; точка под курсором остаётся на месте.
    /// </summary>
    public static (double panX, double panY, double zoom) ZoomAt(
        double px, double py, double panX, double panY, double zoom, double factor)
    {
        var newZoom = Math.Clamp(zoom * factor, MinZoom, MaxZoom);
        var ratio = newZoom / zoom;
        return (px - (px - panX) * ratio, py - (py - panY) * ratio, newZoom);
    }

    /// <summary>Шаг линий сетки в единицах мира: 1, 2, 5 × 10^k, чтобы линии были не чаще minPx пикселей.</summary>
    public static double NiceStep(double pixelsPerUnit, double minPx = 28)
    {
        if (pixelsPerUnit <= 0 || double.IsNaN(pixelsPerUnit) || double.IsInfinity(pixelsPerUnit)) return 1;
        var target = minPx / pixelsPerUnit;
        var pow = Math.Pow(10, Math.Floor(Math.Log10(target)));
        foreach (var m in new[] { 1.0, 2.0, 5.0, 10.0 })
            if (m * pow >= target) return m * pow;
        return 10 * pow;
    }
}
