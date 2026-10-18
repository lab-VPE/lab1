using Avalonia;
using System;

namespace UI;

class Program
{
    // Главная точка входа для запуска приложения
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Этот метод используется визуальным дизайнером для отрисовки окон
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
