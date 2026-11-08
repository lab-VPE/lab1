using UI.Models;
using UI.Services;
using UI.ViewModels;

namespace UI.Tests;

public class MainViewModelTests
{
    private sealed class FakeTicker : ITicker
    {
        public bool Running { get; private set; }
        public event Action<double>? Tick;
        public void Start() => Running = true;
        public void Stop() => Running = false;
        public void Fire(double dt) => Tick?.Invoke(dt);
    }

    private sealed class FakeApi : IApiClient
    {
        public RunResponse? RunResult { get; set; }
        public CheckResponse CheckResult { get; set; } = new() { Valid = true };
        public Exception? Throw { get; set; }
        public bool Hang { get; set; }
        public RequestModel? LastRequest { get; private set; }

        public async Task<CheckResponse> CheckAsync(RequestModel request, CancellationToken ct = default)
        {
            LastRequest = request;
            if (Throw != null) throw Throw;
            await Task.Yield();
            return CheckResult;
        }

        public async Task<RunResponse> RunAsync(RequestModel request, CancellationToken ct = default)
        {
            LastRequest = request;
            if (Throw != null) throw Throw;
            if (Hang) await Task.Delay(Timeout.Infinite, ct);
            await Task.Yield();
            return RunResult ?? SampleRun();
        }
    }

    private static RunResponse SampleRun() => new()
    {
        Success = true,
        Trajectory = new List<TrajectoryPoint>
        {
            new() { X = 0, Y = 0, N = 1.0, Angle = 45 },
            new() { X = 1, Y = 1, N = 1.05, Angle = 44 },
            new() { X = 2, Y = 2, N = 1.10, Angle = 43 },
        },
        Statistics = new StatisticsResult { PathLength = 2.8, Steps = 3, MaxCurvatureAt = new StartPoint { X = 1, Y = 1 } }
    };

    private static (MainViewModel vm, FakeApi api, FakeTicker ticker) Create()
    {
        var api = new FakeApi();
        var ticker = new FakeTicker();
        return (new MainViewModel(api, ticker), api, ticker);
    }

    [Fact]
    public void Defaults_AreValid()
    {
        var (vm, _, _) = Create();
        Assert.True(vm.IsCheckEnabled);
        Assert.False(vm.HasResult);
        Assert.False(vm.IsDemoMode);
    }

    [Fact]
    public void InvalidFormula_DisablesCommandsAndShowsError()
    {
        var (vm, _, _) = Create();
        vm.EnvironmentFormula = "(1 + x";
        Assert.False(vm.IsCheckEnabled);
        Assert.NotEqual("", vm.FormulaError);
        Assert.False(vm.RunCommand.CanExecute(null));
        Assert.False(vm.CheckCommand.CanExecute(null));
    }

    [Fact]
    public void StartPoint_OutsideBounds_IsRejected_AndWideningBoundsFixesIt()
    {
        var (vm, _, _) = Create();
        vm.StartXText = "12";
        Assert.NotEqual("", vm.XError);
        Assert.False(vm.IsCheckEnabled);

        vm.XMaxText = "20";
        Assert.Equal("", vm.XError);
        Assert.True(vm.IsCheckEnabled);
    }

    [Fact]
    public void Bounds_MinNotLessThanMax_IsRejected()
    {
        var (vm, _, _) = Create();
        vm.XMinText = "15";
        Assert.Contains("XMin", vm.BoundsError);
        Assert.False(vm.IsCheckEnabled);
    }

    [Fact]
    public void Preset_SetsFormula_AndManualEditClearsSelection()
    {
        var (vm, _, _) = Create();
        vm.SelectedPreset = MainViewModel.Presets[2];
        Assert.Equal(MainViewModel.Presets[2].Formula, vm.EnvironmentFormula);

        vm.EnvironmentFormula = "3*x + 1";
        Assert.Null(vm.SelectedPreset);
    }

    [Fact]
    public async Task Run_Success_PublishesResultAndStartsAnimation()
    {
        var (vm, api, ticker) = Create();
        string? color = null;
        Models.Bounds? bounds = null;
        vm.OnTrajectoryReady += (_, c, b) => { color = c; bounds = b; };

        await vm.OnRun();

        Assert.Equal("Готово", vm.StatusMessage);
        Assert.Equal(3, vm.Trajectory.Count);
        Assert.NotNull(vm.Statistics);
        Assert.Equal("#ff0000", color);
        Assert.Equal(-10, bounds!.XMin);
        Assert.True(vm.IsPlaying);
        Assert.True(ticker.Running);
        Assert.Equal("1.0 + 0.05*x", api.LastRequest!.EnvironmentFormula);
        Assert.Equal(45, api.LastRequest.StartAngle);
        Assert.Equal(vm.LastRequest, api.LastRequest);
        Assert.False(vm.IsRunning);
    }

    [Fact]
    public async Task Run_ServerErrors_AreMappedToFields()
    {
        var (vm, api, _) = Create();
        api.RunResult = new RunResponse
        {
            Success = false,
            Errors = new List<ErrorModel>
            {
                new() { Code = "X", Field = "environmentFormula", Message = "Bad formula" },
                new() { Code = "X", Field = "startPoint", Message = "Outside" },
                new() { Code = "X", Field = "bounds", Message = "Bad bounds" },
            }
        };

        await vm.OnRun();

        Assert.Equal("Bad formula", vm.FormulaError);
        Assert.Equal("Outside", vm.XError);
        Assert.Equal("Outside", vm.YError);
        Assert.Equal("Bad bounds", vm.BoundsError);
        Assert.StartsWith("Ошибка", vm.StatusMessage);
        Assert.False(vm.HasResult);
    }

    [Fact]
    public async Task Run_ConnectionFailure_ShowsFriendlyMessage()
    {
        var (vm, api, _) = Create();
        api.Throw = new ApiException("Сервер недоступен (http://x). Запустите Controller и повторите.");

        await vm.OnRun();

        Assert.Contains("недоступен", vm.StatusMessage);
        Assert.False(vm.IsRunning);
    }

    [Fact]
    public async Task Run_CanBeCancelled()
    {
        var (vm, api, _) = Create();
        api.Hang = true;

        var run = vm.OnRun();
        Assert.True(vm.IsRunning);
        Assert.True(vm.CancelCommand.CanExecute(null));

        vm.CancelCommand.Execute(null);
        await run;

        Assert.Equal("Операция отменена", vm.StatusMessage);
        Assert.False(vm.IsRunning);
        Assert.False(vm.HasResult);
    }

    [Fact]
    public async Task Check_Valid_And_Invalid()
    {
        var (vm, api, _) = Create();
        await vm.OnCheck();
        Assert.Equal("Параметры корректны", vm.StatusMessage);

        api.CheckResult = new CheckResponse
        {
            Valid = false,
            Errors = new List<ErrorModel> { new() { Field = "stepSize", Message = "Step too big" } }
        };
        await vm.OnCheck();
        Assert.Equal("Step too big", vm.StepError);
    }

    [Fact]
    public async Task Playback_PauseResumeReplay()
    {
        var (vm, _, ticker) = Create();
        Assert.False(vm.PlayPauseCommand.CanExecute(null));

        await vm.OnRun();
        ticker.Fire(1.5);
        Assert.Equal(0.5, vm.Progress, 6);

        vm.PlayPauseCommand.Execute(null);
        Assert.False(vm.IsPlaying);
        Assert.Equal("Старт", vm.PlayButtonText);

        vm.PlayPauseCommand.Execute(null);
        ticker.Fire(1.5);
        Assert.Equal(1.0, vm.Progress, 6);
        Assert.False(vm.IsPlaying);

        vm.ReplayCommand.Execute(null);
        Assert.True(vm.IsPlaying);
        Assert.Equal(0.0, vm.Progress, 6);
    }

    [Fact]
    public async Task ColorChange_OnExistingResult_RaisesRecolorOnly()
    {
        var (vm, api, _) = Create();
        string? recolored = null;
        vm.OnColorChanged += c => recolored = c;

        vm.Color = "#00ff00";            // результата ещё нет — перекрашивать нечего
        Assert.Null(recolored);

        await vm.OnRun();
        vm.Color = "#0000ff";
        Assert.Equal("#0000ff", recolored);

        recolored = null;
        vm.Color = "nope";               // некорректный цвет — событие не нужно
        Assert.Null(recolored);
        Assert.NotEqual("", vm.ColorError);
    }

    [Fact]
    public async Task Reset_RestoresDefaultsAndClearsResult()
    {
        var (vm, _, _) = Create();
        await vm.OnRun();
        vm.XMaxText = "20";
        var cleared = false;
        vm.OnCleared += () => cleared = true;

        vm.OnReset();

        Assert.True(cleared);
        Assert.False(vm.HasResult);
        Assert.False(vm.IsPlaying);
        Assert.Null(vm.Statistics);
        Assert.Equal("10", vm.XMaxText);
        Assert.Equal("1.0 + 0.05*x", vm.EnvironmentFormula);
        Assert.True(vm.IsCheckEnabled);
    }

    [Fact]
    public void DescribeFailure_Variants()
    {
        Assert.Equal("Операция отменена", MainViewModel.DescribeFailure(new OperationCanceledException()));
        Assert.Equal("Ошибка: x", MainViewModel.DescribeFailure(new ApiException("x")));
        Assert.StartsWith("Непредвиденная ошибка", MainViewModel.DescribeFailure(new InvalidOperationException("y")));
    }
}
