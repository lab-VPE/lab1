using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using UI.ViewModels;

namespace UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var vm = new MainViewModel();
        DataContext = vm;

        vm.OnTrajectoryReady += (points, color, bounds) =>
        {
            Scene.SetTrajectory(points, color, bounds);
            Chart.SetData(points);
        };
        vm.OnCleared += () =>
        {
            Scene.Clear();
            Chart.Clear();
        };

        // Если пользователь взялся за ползунок — останавливаем автоанимацию.
        TimeSlider.AddHandler(PointerPressedEvent,
            (_, _) => vm.StopAnimation(),
            RoutingStrategies.Tunnel, handledEventsToo: true);
    }
}
