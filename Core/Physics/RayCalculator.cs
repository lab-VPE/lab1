Максим Жук:
Вот полный, финальный и правильный код для файла Core/Physics/RayCalculator.cs. В нём учтены все правки: и защита от NaN (из main), и правильная физика градиентной оптики с делением на n.
вот весь файл RayCalculator.cs целиком. Скопируй его и полностью замени им содержимое твоего файла (сотри всё, что там есть, и вставь это):

`csharp
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
            // Защита начального n (сохранено из main)
            double startN = parser.Evaluate(request.StartPoint.X);
            if (double.IsNaN(startN) double.IsInfinity(startN) startN <= 0)
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
        double angleRad = request.StartAngle * DegToRad; // Угол в радианах
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
            // 1. Берем текущий показатель преломления в точке x
            double n = SafeEvaluate(parser, x);

            // 2. Проверяем его на адекватность (защита из main)
            if (double.IsNaN(n) double.IsInfinity(n) n <= 0)
throw new Exception($"Refraction index invalid (≤ 0 or NaN) at x = {x:F3}");

            // 3. Считаем производную среды по X
            double dndx = NumericalDerivative(parser, x);

            // 4. Считаем изменение угла (уравнение градиентной оптики с делением на n)
            double dTheta = (dndx / n) * Math.Sin(angleRad);

            // 5. Обновляем угол и координаты (метод Эйлера)
            angleRad -= dTheta * ds;

            double newX = x + Math.Cos(angleRad) * ds;
            double newY = y + Math.Sin(angleRad) * ds;

            // 6. Проверка выхода за границы
            if (newX < request.Bounds.XMin newX > request.Bounds.XMax
        newY < request.Bounds.YMin || newY > request.Bounds.YMax)
break;

            x = newX;
            y = newY;
            steps++;

            points.Add(new TrajectoryPoint
            {
                X = Math.Round(x, 4),
                Y = Math.Round(y, 4),
                N = Math.Round(n, 4),
                Angle = Math.Round(angleRad * RadToDeg, 4) // Обратно в градусы
            });
        }

        return points;
    }

    Максим Жук:
// Вспомогательный метод для численного дифференцирования
private double NumericalDerivative(FormulaParser parser, double x)
    {
        double h = 1e-4;
        double nPlus = SafeEvaluate(parser, x + h);
        double nMinus = SafeEvaluate(parser, x - h);
        return (nPlus - nMinus) / (2 * h);
    }

    private double SafeEvaluate(FormulaParser parser, double x)
    {
        try
        {
            var val = parser.Evaluate(x);
            if (double.IsNaN(val) || double.IsInfinity(val))
                throw new Exception("Значение не является числом");
            return val;
        }
        catch (Exception ex)
        {
            throw new Exception($"Error at x = {x:F3}: {ex.Message}");
        }
    }
}
