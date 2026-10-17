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

        // 1. Парсим формулу
        var parser = new FormulaParser(request.EnvironmentFormula);
        if (!parser.IsValid(out var parseError))
        {
            result.Success = false;
            result.Errors.Add(new ErrorModel("INVALID_FORMULA", "environmentFormula", parseError));
            return result;
        }

        // 2. Считаем траекторию
        try
        {
            var trajectory = CalculateTrajectory(request, parser);
            result.Trajectory = trajectory;
            result.Success = true;

            // 3. Считаем статистику
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
        double angle = request.StartAngle; // в градусах, 0 = вправо
        double step = request.StepSize;

        points.Add(new TrajectoryPoint
        {
            X = x,
            Y = y,
            N = SafeEvaluate(parser, x),
            Angle = angle
        });

        int maxSteps = 100000;
        int steps = 0;

        while (steps < maxSteps)
        {
            // Текущий показатель преломления
            double n1 = SafeEvaluate(parser, x);

            // Шагаем вперёд
            double dx = step * Math.Cos(angle * DegToRad);
            double dy = step * Math.Sin(angle * DegToRad);
            double newX = x + dx;
            double newY = y + dy;

            // Выход за границы — стоп
            if (newX < request.Bounds.XMin || newX > request.Bounds.XMax ||
                newY < request.Bounds.YMin || newY > request.Bounds.YMax)
                break;

            // Новый показатель преломления
            double n2 = SafeEvaluate(parser, newX);

            // Закон Снеллиуса: n1*sin(a1) = n2*sin(a2)
            if (n2 <= 0)
                throw new Exception($"Показатель преломления не может быть ≤ 0 (при x = {newX:F3})");

            double sinA1 = Math.Sin(angle * DegToRad);
            double sinA2 = n1 * sinA1 / n2;

            // Полное внутреннее отражение — упрощённо: считаем, что луч не проходит
            if (Math.Abs(sinA2) > 1)
                throw new Exception($"Полное внутреннее отражение при x = {newX:F3}");

            double newAngle = Math.Asin(sinA2) * RadToDeg;

            // Если x убывает — угол меняет знак (движение влево)
            if (dx < 0)
                newAngle = 180.0 - newAngle;

            x = newX;
            y = newY;
            angle = newAngle;
            steps++;

            points.Add(new TrajectoryPoint
            {
                X = Math.Round(x, 4),
                Y = Math.Round(y, 4),
                N = Math.Round(n2, 4),
                Angle = Math.Round(angle, 4)
            });
        }

        return points;
    }

    private double SafeEvaluate(FormulaParser parser, double x)
    {
        try
        {
            return parser.Evaluate(x);
        }
        catch (Exception ex)
        {
            throw new Exception($"Ошибка при x = {x:F3}: {ex.Message}");
        }
    }
}
