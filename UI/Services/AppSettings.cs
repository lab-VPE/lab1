using System;

namespace UI.Services;

/// <summary>
/// Настройки запуска. Источники (по приоритету): аргументы командной строки, переменные окружения.
///   --demo            или LAB1_DEMO=1            — читать Assets/*.json вместо сервера
///   --server=URL      или LAB1_SERVER=URL        — адрес Controller (по умолчанию http://localhost:5067)
/// </summary>
public static class AppSettings
{
    public const string DefaultServerUrl = "http://localhost:5067";

    public static bool DemoMode { get; private set; }
    public static string ServerUrl { get; private set; } = DefaultServerUrl;

    public static void Load(string[] args)
    {
        Parse(args, Environment.GetEnvironmentVariable("LAB1_DEMO"), Environment.GetEnvironmentVariable("LAB1_SERVER"));
    }

    /// <summary>Отдельный метод без обращения к окружению — удобно тестировать.</summary>
    public static void Parse(string[] args, string? envDemo, string? envServer)
    {
        DemoMode = IsTrue(envDemo);
        ServerUrl = string.IsNullOrWhiteSpace(envServer) ? DefaultServerUrl : envServer.Trim().TrimEnd('/');

        foreach (var arg in args)
        {
            if (arg.Equals("--demo", StringComparison.OrdinalIgnoreCase))
                DemoMode = true;
            else if (arg.StartsWith("--server=", StringComparison.OrdinalIgnoreCase))
            {
                var url = arg.Substring("--server=".Length).Trim().TrimEnd('/');
                if (url.Length > 0) ServerUrl = url;
            }
        }
    }

    private static bool IsTrue(string? v)
        => v != null && (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
}
