using System;
using System.Collections.Generic;
using UI.Models;

namespace UI.Helpers;

public static class TrajectoryMath
{
    /// <summary>Накопленная длина пути вдоль траектории: s[0] = 0, s[i] = s[i-1] + |P[i] - P[i-1]|.</summary>
    public static double[] PathLengths(IReadOnlyList<TrajectoryPoint> points)
    {
        var s = new double[points.Count];
        for (var i = 1; i < points.Count; i++)
        {
            var dx = points[i].X - points[i - 1].X;
            var dy = points[i].Y - points[i - 1].Y;
            s[i] = s[i - 1] + Math.Sqrt(dx * dx + dy * dy);
        }
        return s;
    }
}
