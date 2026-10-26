using Core.Models;

namespace Core.Physics;

public static class StatisticsCalculator
{
    public static StatisticsResult Calculate(List<TrajectoryPoint> trajectory, double startAngle)
    {
        var result = new StatisticsResult();
        if (trajectory.Count == 0) return result;

        double pathLength = 0;
        for (int i = 1; i < trajectory.Count; i++)
        {
            double dx = trajectory[i].X - trajectory[i - 1].X;
            double dy = trajectory[i].Y - trajectory[i - 1].Y;
            pathLength += Math.Sqrt(dx * dx + dy * dy);
        }
        result.PathLength = Math.Round(pathLength, 4);

        double endAngle = trajectory[^1].Angle;
        result.TotalDeviation = Math.Round(Math.Abs(endAngle - startAngle), 4);

        double maxCurvature = 0;
        int maxCurvatureIndex = 0;
        for (int i = 1; i < trajectory.Count; i++)
        {

            double dAngle = Math.Abs(trajectory[i].Angle - trajectory[i - 1].Angle) * (Math.PI / 180.0);

        
            double dx = trajectory[i].X - trajectory[i - 1].X;
            double dy = trajectory[i].Y - trajectory[i - 1].Y;
            double dLen = Math.Sqrt(dx * dx + dy * dy);
            if (dLen > 1e-9)
            {
                double curvature = dAngle / dLen;
                if (curvature > maxCurvature)
                {
                    maxCurvature = curvature;
                    maxCurvatureIndex = i;
                }
            }
        }
        result.MaxCurvature = Math.Round(maxCurvature, 4);
        result.MaxCurvatureAt = new StartPoint
        {
            X = trajectory[maxCurvatureIndex].X,
            Y = trajectory[maxCurvatureIndex].Y
        };

        double nMin = double.MaxValue;
        double nMax = double.MinValue;
        foreach (var p in trajectory)
        {
            if (p.N < nMin) nMin = p.N;
            if (p.N > nMax) nMax = p.N;
        }
        result.RefractionIndexMin = Math.Round(nMin, 4);
        result.RefractionIndexMax = Math.Round(nMax, 4);
        result.Steps = trajectory.Count;

        return result;
    }
}
