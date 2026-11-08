using Avalonia;
using System;
using UI.Services;

namespace UI;

class Program
{
    // Главная точка входа для запуска приложения
    [STAThread]
    public static void Main(string[] args)
    {
        // --demo / --server=URL или LAB1_DEMO / LAB1_SERVER (см. Services/AppSettings.cs)
        AppSettings.Load(args);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Этот метод используется визуальным дизайнером для отрисовки окон
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
