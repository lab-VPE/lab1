using Core.Formula;
using Core.Models;

namespace Core.Physics;

public class RayCalculator
{
    private const double DegToRad = Math.PI / 180.0;
    private const double RadToDeg = 180.0 / Math.PI;

    public RunResponse Run(RequestModel request)
    {
        var startTime = DateTime.Now;
        var result = new RunResponse();

        var parser = new FormulaParser(request.EnvironmentFormula);
        if (!parser.IsValid(out var parseError))
        {
            result.Success = false;
            result.Errors.Add(new ErrorModel("INVALID_FORMULA", "environmentFormula", parseError));
            return result;
        }

        try
        {
            double startN = parser.Evaluate(request.StartPoint.X);
            if (double.IsNaN(startN) || double.IsInfinity(startN) || startN <= 0)
                throw new Exception($"Invalid initial refraction index at start point: {startN}");

            var trajectory = CalculateTrajectory(request, parser);
            result.Trajectory = trajectory;
            result.Success = true;

            var stats = StatisticsCalculator.Calculate(trajectory, request.StartAngle);
            stats.ComputationTimeMs = (long)(DateTime.Now - startTime).TotalMilliseconds;
            result.Statistics = stats;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add(new ErrorModel("CALCULATION_FAILED", "environmentFormula", ex.Message));
        }

        return result;
    }

    private List<TrajectoryPoint> CalculateTrajectory(RequestModel request, FormulaParser parser)
    {
        var points = new List<TrajectoryPoint>();
        double x = request.StartPoint.X;
        double y = request.StartPoint.Y;
        double angle = request.StartAngle;
        double step = request.StepSize;

        points.Add(new TrajectoryPoint { X = x, Y = y, N = SafeEvaluate(parser, x), Angle = angle });

        int maxSteps = 100000;
        int steps = 0;

        while (steps < maxSteps)
        {
            double n1 = SafeEvaluate(parser, x);
            double dx = step * Math.Cos(angle * DegToRad);
            double dy = step * Math.Sin(angle * DegToRad);
            double newX = x + dx;
            double newY = y + dy;

            if (newX < request.Bounds.XMin || newX > request.Bounds.XMax ||
                newY < request.Bounds.YMin || newY > request.Bounds.YMax)
                break;

            double n2 = SafeEvaluate(parser, newX);

            if (double.IsNaN(n2) || double.IsInfinity(n2) || n2 <= 0)
                throw new Exception($"Refraction index invalid (≤ 0 or NaN) at x = {newX:F3}");

            double sinA1 = Math.Sin(angle * DegToRad);
            double sinA2 = n1 * sinA1 / n2;

            if (Math.Abs(sinA2) > 1)
                throw new Exception($"Total internal reflection at x = {newX:F3}");

            double newAngle = Math.Asin(sinA2) * RadToDeg;
            
            // Нормализация угла при переходе через 180 градусов, чтобы не было скачков
            if (dx < 0) 
                newAngle = 180.0 - newAngle;
            else if (dx >= 0 && angle > 90) 
                newAngle = 180.0 - newAngle;

            x = newX;
            y = newY;
            angle = newAngle;
            steps++;

            points.Add(new TrajectoryPoint
            {
                X = Math.Round(x, 4), Y = Math.Round(y, 4),
                N = Math.Round(n2, 4), Angle = Math.Round(angle, 4)
            });
        }

        return points;
    }

    private double SafeEvaluate(FormulaParser parser, double x)
    {
        try 
        { 
            var val = parser.Evaluate(x);
            if (double.IsNaN(val) || double.IsInfinity(val)) return -1; // Спровоцирует ошибку на следующем шаге
            return val; 
        }
        catch (Exception ex) 
        { 
            throw new Exception($"Error at x = {x:F3}: {ex.Message}"); 
        }
    }
}