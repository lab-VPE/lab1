using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Globalization;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Threading;
using UI.Helpers;
using UI.Models;
using UI.Services;

namespace UI.ViewModels;

public class MainViewModel : ObservableObject
{
    private const string DefaultFormula = "1.0 + 0.05*x";
    private static readonly Models.Bounds DefaultBounds = new() { XMin = -10, XMax = 10, YMin = -10, YMax = 10 };
    private const double AnimationSeconds = 3.0;

    private readonly ApiClient _api = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private DateTime _animStart;

    private readonly RelayCommand _check;
    private readonly RelayCommand _run;
    private readonly RelayCommand _reset;

    // ---------- Форма ----------
    private string _formula = DefaultFormula;
    private string _startXText = "0", _startYText = "0", _angleText = "45", _stepText = "0.05";
    private double _startX, _startY, _startAngle = 45, _stepSize = 0.05;
    private string _color = "#ff0000";

    public string EnvironmentFormula { get => _formula; set { if (Set(ref _formula, value)) ValidateAll(); } }

    // Привязка идёт к *Text, чтобы можно было показать "Введите число" на мусорном вводе.
    public string StartXText { get => _startXText; set { if (Set(ref _startXText, value)) ValidateAll(); } }
    public string StartYText { get => _startYText; set { if (Set(ref _startYText, value)) ValidateAll(); } }
    public string StartAngleText { get => _angleText; set { if (Set(ref _angleText, value)) ValidateAll(); } }
    public string StepSizeText { get => _stepText; set { if (Set(ref _stepText, value)) ValidateAll(); } }

    public double StartX { get => _startX; private set => Set(ref _startX, value); }
    public double StartY { get => _startY; private set => Set(ref _startY, value); }
    public double StartAngle { get => _startAngle; private set => Set(ref _startAngle, value); }
    public double StepSize { get => _stepSize; private set => Set(ref _stepSize, value); }

    public string Color { get => _color; set { if (Set(ref _color, value)) ValidateAll(); } }

    // ---------- Ошибки ----------
    private string _formulaError = "", _angleError = "", _stepError = "",
                   _xError = "", _yError = "", _colorError = "";
    public string FormulaError { get => _formulaError; set => Set(ref _formulaError, value); }
    public string AngleError { get => _angleError; set => Set(ref _angleError, value); }
    public string StepError { get => _stepError; set => Set(ref _stepError, value); }
    public string XError { get => _xError; set => Set(ref _xError, value); }
    public string YError { get => _yError; set => Set(ref _yError, value); }
    public string ColorError { get => _colorError; set => Set(ref _colorError, value); }

    // ---------- Состояние ----------
    private bool _isCheckEnabled, _isRunning;
    private string _status = "";
    public bool IsCheckEnabled { get => _isCheckEnabled; set { if (Set(ref _isCheckEnabled, value)) RaiseCanExecute(); } }
    public bool IsRunning { get => _isRunning; set { if (Set(ref _isRunning, value)) RaiseCanExecute(); } }
    public string StatusMessage { get => _status; set => Set(ref _status, value); }

    // ---------- Результат ----------
    private List<TrajectoryPoint> _trajectory = new();
    private StatisticsResult? _statistics;
    private double _progress;

    public List<TrajectoryPoint> Trajectory { get => _trajectory; private set { Set(ref _trajectory, value); UpdatePointsInfo(); } }
    public StatisticsResult? Statistics
    {
        get => _statistics;
        private set { Set(ref _statistics, value); OnPropertyChanged(nameof(MaxCurvatureAtText)); }
    }
    public string MaxCurvatureAtText => _statistics == null
        ? "—"
        : string.Format(CultureInfo.InvariantCulture, "({0:F3}; {1:F3})", _statistics.MaxCurvatureAt.X, _statistics.MaxCurvatureAt.Y);

    /// <summary>0..1 — сколько траектории показано. Двусторонняя привязка к Slider.</summary>
    public double Progress
    {
        get => _progress;
        set { if (Set(ref _progress, Math.Clamp(value, 0, 1))) UpdatePointsInfo(); }
    }

    private string _pointsInfo = "0 / 0";
    public string PointsInfo { get => _pointsInfo; private set => Set(ref _pointsInfo, value); }

    // ---------- Команды ----------
    public ICommand CheckCommand => _check;
    public ICommand RunCommand => _run;
    public ICommand ResetCommand => _reset;

    // ---------- События для View ----------
    public event Action<List<TrajectoryPoint>, Avalonia.Media.Color, Models.Bounds>? OnTrajectoryReady;
    public event Action<StatisticsResult>? OnStatisticsReady;
    public event Action? OnCleared;

    public MainViewModel()
    {
        _check = new RelayCommand(OnCheck, () => IsCheckEnabled && !IsRunning);
        _run = new RelayCommand(OnRun, () => IsCheckEnabled && !IsRunning);
        _reset = new RelayCommand(OnReset);
        _timer.Tick += OnTimerTick;
        ValidateAll();
    }

    private void RaiseCanExecute()
    {
        _check.RaiseCanExecuteChanged();
        _run.RaiseCanExecuteChanged();
    }

    // ---------- Логика ----------
    public void ValidateAll()
    {
        var f = Validators.ValidateFormula(_formula);
        var x = Validators.ValidateCoordinate(_startXText);
        var y = Validators.ValidateCoordinate(_startYText);
        var a = Validators.ValidateAngle(_angleText);
        var s = Validators.ValidateStep(_stepText);
        var c = Validators.ValidateColor(_color);

        FormulaError = f.Message;
        XError = x.Message;
        YError = y.Message;
        AngleError = a.Message;
        StepError = s.Message;
        ColorError = c.Message;

        if (x.Valid) StartX = Parse(_startXText);
        if (y.Valid) StartY = Parse(_startYText);
        if (a.Valid) StartAngle = Parse(_angleText);
        if (s.Valid) StepSize = Parse(_stepText);

        // Как на сервере (OUT_OF_BOUNDS): стартовая точка должна лежать внутри области расчёта
        if (x.Valid && StartX < DefaultBounds.XMin || x.Valid && StartX > DefaultBounds.XMax)
        { x = ValidationResult.Fail($"X от {DefaultBounds.XMin} до {DefaultBounds.XMax}"); XError = x.Message; }
        if (y.Valid && StartY < DefaultBounds.YMin || y.Valid && StartY > DefaultBounds.YMax)
        { y = ValidationResult.Fail($"Y от {DefaultBounds.YMin} до {DefaultBounds.YMax}"); YError = y.Message; }

        IsCheckEnabled = f.Valid && x.Valid && y.Valid && a.Valid && s.Valid && c.Valid;
    }

    private static double Parse(string s)
        => double.Parse(s.Trim().Replace(',', '.'), CultureInfo.InvariantCulture);

    private RequestModel BuildRequest() => new()
    {
        EnvironmentFormula = EnvironmentFormula.Trim(),
        StartPoint = new StartPoint { X = StartX, Y = StartY },
        StartAngle = StartAngle,
        StepSize = StepSize,
        Bounds = new Models.Bounds
        {
            XMin = DefaultBounds.XMin, XMax = DefaultBounds.XMax,
            YMin = DefaultBounds.YMin, YMax = DefaultBounds.YMax
        },
        Color = Color
    };

    public async Task OnCheck()
    {
        ValidateAll();
        if (!IsCheckEnabled) return;

        IsRunning = true;
        StatusMessage = "Проверка...";
        try
        {
            var resp = await _api.CheckAsync(BuildRequest());
            if (resp.Valid)
                StatusMessage = "Параметры корректны";
            else
            {
                ApplyServerErrors(resp.Errors);
                StatusMessage = "Ошибка: параметры некорректны";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Ошибка связи: " + ex.Message;
        }
        finally { IsRunning = false; }
    }

    public async Task OnRun()
    {
        ValidateAll();
        if (!IsCheckEnabled) return;

        StopAnimation();
        IsRunning = true;
        StatusMessage = "Расчёт...";
        try
        {
            var request = BuildRequest();
            var resp = await _api.RunAsync(request);

            if (!resp.Success || resp.Trajectory.Count == 0)
            {
                ApplyServerErrors(resp.Errors);
                StatusMessage = resp.Errors.Count > 0
                    ? "Ошибка: " + resp.Errors[0].Message
                    : "Ошибка: пустая траектория";
                return;
            }

            Trajectory = resp.Trajectory;
            Statistics = resp.Statistics;
            Progress = 0;

            Avalonia.Media.Color.TryParse(Color, out var color);
            OnTrajectoryReady?.Invoke(resp.Trajectory, color, request.Bounds);
            OnStatisticsReady?.Invoke(resp.Statistics);

            StatusMessage = "Готово";
            StartAnimation();
        }
        catch (Exception ex)
        {
            StatusMessage = "Ошибка связи: " + ex.Message;
        }
        finally { IsRunning = false; }
    }

    public void OnReset()
    {
        StopAnimation();
        Trajectory = new List<TrajectoryPoint>();
        Statistics = null;
        Progress = 0;

        _formula = DefaultFormula; OnPropertyChanged(nameof(EnvironmentFormula));
        _startXText = "0"; OnPropertyChanged(nameof(StartXText));
        _startYText = "0"; OnPropertyChanged(nameof(StartYText));
        _angleText = "45"; OnPropertyChanged(nameof(StartAngleText));
        _stepText = "0.05"; OnPropertyChanged(nameof(StepSizeText));
        _color = "#ff0000"; OnPropertyChanged(nameof(Color));

        ValidateAll();
        StatusMessage = "";
        OnCleared?.Invoke();
    }

    /// <summary>Раскладывает ошибки сервера по полям формы.</summary>
    private void ApplyServerErrors(IEnumerable<ErrorModel> errors)
    {
        foreach (var e in errors)
        {
            var field = (e.Field ?? "").Replace("_", "").Replace(".", "").ToLowerInvariant();
            switch (field)
            {
                case "environmentformula": FormulaError = e.Message; break;
                case "startangle": AngleError = e.Message; break;
                case "stepsize": StepError = e.Message; break;
                case "startpoint": XError = e.Message; YError = e.Message; break;
                case "startpointx": case "startx": XError = e.Message; break;
                case "startpointy": case "starty": YError = e.Message; break;
                case "color": ColorError = e.Message; break;
                default: break; // общая ошибка — попадёт в StatusMessage
            }
        }
    }

    // ---------- Анимация ----------
    private void StartAnimation()
    {
        _animStart = DateTime.UtcNow;
        _timer.Start();
    }

    public void StopAnimation() => _timer.Stop();

    private void OnTimerTick(object? sender, EventArgs e)
    {
        var p = (DateTime.UtcNow - _animStart).TotalSeconds / AnimationSeconds;
        if (p >= 1) { Progress = 1; StopAnimation(); }
        else Progress = p;
    }

    private void UpdatePointsInfo()
    {
        var total = _trajectory.Count;
        var current = total == 0 ? 0 : (int)Math.Round(_progress * (total - 1)) + 1;
        PointsInfo = $"{current} / {total}";
    }
}
