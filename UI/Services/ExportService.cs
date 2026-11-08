using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using UI.Helpers;
using UI.Models;

namespace UI.Services;

/// <summary>Формирование содержимого файлов экспорта (без обращения к диску и UI).</summary>
public static class ExportService
{
    public const string FormatVersion = "1.0";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>CSV траектории: index,path_length,x,y,n,angle_deg. Разделитель — запятая, десятичная точка.</summary>
    public static string BuildCsv(IReadOnlyList<TrajectoryPoint> trajectory)
    {
        var s = TrajectoryMath.PathLengths(trajectory);
        var sb = new StringBuilder();
        sb.Append("index,path_length,x,y,n,angle_deg\n");
        for (var i = 0; i < trajectory.Count; i++)
        {
            var p = trajectory[i];
            sb.Append(i.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(F(s[i])).Append(',')
              .Append(F(p.X)).Append(',')
              .Append(F(p.Y)).Append(',')
              .Append(F(p.N)).Append(',')
              .Append(F(p.Angle)).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>JSON эксперимента: параметры запроса + траектория + статистика + метаданные.</summary>
    public static string BuildJson(RequestModel request, IReadOnlyList<TrajectoryPoint> trajectory,
        StatisticsResult? statistics, DateTimeOffset exportedAt)
    {
        var doc = new
        {
            version = FormatVersion,
            application = "lab1-ui",
            exportedAtUtc = exportedAt.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture),
            request,
            trajectory,
            statistics
        };
        return JsonSerializer.Serialize(doc, JsonOptions);
    }

    private static string F(double v) => v.ToString("R", CultureInfo.InvariantCulture);
}
