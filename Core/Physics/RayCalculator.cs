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
        double angleRad = request.StartAngle * (Math.PI / 180.0); // Угол в радианах
        double ds = request.StepSize;

        points.Add(new TrajectoryPoint
        {
            X = x,
            Y = y,
            N = SafeEvaluate(parser, x),
            Angle = request.StartAngle
        });

        int maxSteps = 100000;
        int steps = 0;

        while (steps < maxSteps)
        {
            // 1. Считаем производную показателя преломления по X
            double dndx = NumericalDerivative(parser, x);

            // 2. Считаем изменение угла (уравнение градиентной оптики)
            double dTheta = dndx * Math.Sin(angleRad);

            // 3. Обновляем угол и координаты
            angleRad -= dTheta * ds;
            x += Math.Cos(angleRad) * ds;
            y += Math.Sin(angleRad) * ds;

            // 4. Проверка выхода за границы
            if (x < request.Bounds.XMin || x > request.Bounds.XMax ||
                y < request.Bounds.YMin || y > request.Bounds.YMax)
                break;

            steps++;

            points.Add(new TrajectoryPoint
            {
                X = Math.Round(x, 4),
                Y = Math.Round(y, 4),
                N = Math.Round(SafeEvaluate(parser, x), 4),
                Angle = Math.Round(angleRad * (180.0 / Math.PI), 4) // Обратно в градусы для UI
            });
        }

        return points;
    }

    // Вспомогательный метод для численного дифференцирования (производной)
    private double NumericalDerivative(FormulaParser parser, double x)
    {
        double h = 1e-4;
        double nPlus = SafeEvaluate(parser, x + h);
        double nMinus = SafeEvaluate(parser, x - h);
        return (nPlus - nMinus) / (2 * h);
    }

    private double SafeEvaluate(FormulaParser parser, double x)
    {
        try { return parser.Evaluate(x); }
        catch (Exception ex) { throw new Exception($"Error at x = {x:F3}: {ex.Message}"); }
    }
}
