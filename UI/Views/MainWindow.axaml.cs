using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using UI.Services;
using UI.ViewModels;

namespace UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var vm = new MainViewModel(
            new ApiClient(AppSettings.DemoMode, AppSettings.ServerUrl),
            new DispatcherTicker(),
            AppSettings.DemoMode);
        DataContext = vm;

        vm.OnTrajectoryReady += (points, colorHex, bounds) =>
        {
            Avalonia.Media.Color.TryParse(colorHex, out var color);
            Scene.SetTrajectory(points, color, bounds);
            Chart.SetData(points);
        };
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.IsSceneVisible) or nameof(MainViewModel.IsChartVisible))
                ApplyLayers(vm);
        };
        vm.OnColorChanged += colorHex =>
        {
            if (Avalonia.Media.Color.TryParse(colorHex, out var color))
                Scene.RayColor = color;
        };
        vm.OnCleared += () =>
        {
            Scene.Clear();
            Chart.Clear();
        };

        ApplyLayers(vm);

        // Если пользователь взялся за ползунок — останавливаем автоанимацию.
        TimeSlider.AddHandler(PointerPressedEvent,
            (_, _) => vm.StopAnimation(),
            RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>Перераспределяет высоту между сценой и графиком в зависимости от включённых слоёв.</summary>
    private void ApplyLayers(MainViewModel vm)
    {
        var rows = (vm.IsSceneVisible, vm.IsChartVisible) switch
        {
            (true, true) => "*,220",
            (true, false) => "*,0",
            (false, true) => "0,*",
            _ => "0,0"
        };
        CenterGrid.RowDefinitions = new RowDefinitions(rows);
    }

    // ---------------- Экспорт ----------------

    private MainViewModel Vm => (MainViewModel)DataContext!;

    private async void OnExportCsv(object? sender, RoutedEventArgs e)
    {
        var csv = ExportService.BuildCsv(Vm.Trajectory);
        await SaveTextAsync("Сохранить траекторию", "trajectory.csv", "csv", "CSV", new[] { "*.csv" }, csv);
    }

    private async void OnExportJson(object? sender, RoutedEventArgs e)
    {
        if (Vm.LastRequest == null) return;
        var json = ExportService.BuildJson(Vm.LastRequest, Vm.Trajectory, Vm.Statistics, DateTimeOffset.UtcNow);
        await SaveTextAsync("Сохранить эксперимент", "experiment.json", "json", "JSON", new[] { "*.json" }, json);
    }

    private async void OnExportScenePng(object? sender, RoutedEventArgs e) => await SavePngAsync(SceneHost, "scene.png");
    private async void OnExportChartPng(object? sender, RoutedEventArgs e) => await SavePngAsync(ChartHost, "chart.png");

    private async Task SaveTextAsync(string title, string suggestedName, string ext, string typeName,
        string[] patterns, string content)
    {
        try
        {
            var file = await PickSaveFileAsync(title, suggestedName, ext, typeName, patterns);
            if (file == null) return;

            await using var stream = await file.OpenWriteAsync();
            stream.SetLength(0);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteAsync(content);
            Vm.StatusMessage = "Сохранено: " + file.Name;
        }
        catch (Exception ex)
        {
            Vm.StatusMessage = "Не удалось сохранить файл: " + ex.Message;
        }
    }

    private async Task SavePngAsync(Control host, string suggestedName)
    {
        try
        {
            if (host.Bounds.Width < 1 || host.Bounds.Height < 1)
            {
                Vm.StatusMessage = "Слой скрыт: включите его в разделе «Слои»";
                return;
            }

            var file = await PickSaveFileAsync("Сохранить изображение", suggestedName, "png", "PNG", new[] { "*.png" });
            if (file == null) return;

            var size = new PixelSize((int)host.Bounds.Width, (int)host.Bounds.Height);
            using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
            bitmap.Render(host);

            await using var stream = await file.OpenWriteAsync();
            stream.SetLength(0);
            bitmap.Save(stream);
            Vm.StatusMessage = "Сохранено: " + file.Name;
        }
        catch (Exception ex)
        {
            Vm.StatusMessage = "Не удалось сохранить изображение: " + ex.Message;
        }
    }

    private Task<IStorageFile?> PickSaveFileAsync(string title, string suggestedName, string ext,
        string typeName, string[] patterns)
        => StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = ext,
            FileTypeChoices = new[] { new FilePickerFileType(typeName) { Patterns = patterns } }
        });
}
