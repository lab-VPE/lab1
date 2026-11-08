using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Globalization;
using System.Threading;
using System.Windows.Input;
using UI.Helpers;
using UI.Models;
using UI.Services;

namespace UI.ViewModels;

public class MainViewModel : ObservableObject
{
    private const string DefaultFormula = "1.0 + 0.05*x";
    private static readonly Models.Bounds DefaultBounds = new() { XMin = -10, XMax = 10, YMin = -10, YMax = 10 };
    private Models.Bounds _bounds = new() { XMin = DefaultBounds.XMin, XMax = DefaultBounds.XMax, YMin = DefaultBounds.YMin, YMax = DefaultBounds.YMax };
    private const double AnimationSeconds = 3.0;

    private readonly IApiClient _api;
    private readonly ITicker _ticker;
    private double _animElapsed;

    private readonly RelayCommand _check;
    private readonly RelayCommand _run;
    private readonly RelayCommand _reset;
    private readonly RelayCommand _cancel;
    private readonly RelayCommand _playPause;
    private readonly RelayCommand _replay;
    private CancellationTokenSource? _cts;

    // ---------- Форма ----------
    private string _formula = DefaultFormula;
    private string _startXText = "0", _startYText = "0", _angleText = "45", _stepText = "0.05";
    private double _startX, _startY, _startAngle = 45, _stepSize = 0.05;
    private string _color = "#ff0000";

    public string EnvironmentFormula
    {
        get => _formula;
        set
        {
            if (!Set(ref _formula, value)) return;
            if (_selectedPreset != null && _selectedPreset.Formula != value)
            {
                _selectedPreset = null;
                OnPropertyChanged(nameof(SelectedPreset));
            }
            ValidateAll();
        }
    }

    // Привязка идёт к *Text, чтобы можно было показать "Введите число" на мусорном вводе.
    public string StartXText { get => _startXText; set { if (Set(ref _startXText, value)) ValidateAll(); } }
    public string StartYText { get => _startYText; set { if (Set(ref _startYText, value)) ValidateAll(); } }
    public string StartAngleText { get => _angleText; set { if (Set(ref _angleText, value)) ValidateAll(); } }
    public string StepSizeText { get => _stepText; set { if (Set(ref _stepText, value)) ValidateAll(); } }

    public double StartX { get => _startX; private set => Set(ref _startX, value); }
    public double StartY { get => _startY; private set => Set(ref _startY, value); }
    public double StartAngle { get => _startAngle; private set => Set(ref _startAngle, value); }
    public double StepSize { get => _stepSize; private set => Set(ref _stepSize, value); }

    // ---------- Границы области расчёта ----------
    private string _xMinText = "-10", _xMaxText = "10", _yMinText = "-10", _yMaxText = "10";
    private string _boundsError = "";
    public string XMinText { get => _xMinText; set { if (Set(ref _xMinText, value)) ValidateAll(); } }
    public string XMaxText { get => _xMaxText; set { if (Set(ref _xMaxText, value)) ValidateAll(); } }
    public string YMinText { get => _yMinText; set { if (Set(ref _yMinText, value)) ValidateAll(); } }
    public string YMaxText { get => _yMaxText; set { if (Set(ref _yMaxText, value)) ValidateAll(); } }
    public string BoundsError { get => _boundsError; set => Set(ref _boundsError, value); }

    // ---------- Готовые среды ----------
    /// <summary>Формулы поддерживаются парсером Core (переменная x, + - * / ^ и скобки).</summary>
    public static IReadOnlyList<EnvironmentPreset> Presets { get; } = new[]
    {
        new EnvironmentPreset("Постоянная: n = 1.5", "1.5"),
        new EnvironmentPreset("Линейная возрастающая: 1 + 0.05x", "1.0 + 0.05*x"),
        new EnvironmentPreset("Линейная убывающая: 2 − 0.05x", "2.0 - 0.05*x"),
        new EnvironmentPreset("Квадратичная: 1 + 0.01x²", "1.0 + 0.01*x^2"),
    };

    private EnvironmentPreset? _selectedPreset;
    public EnvironmentPreset? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (!Set(ref _selectedPreset, value) || value == null) return;
            _formula = value.Formula;
            OnPropertyChanged(nameof(EnvironmentFormula));
            ValidateAll();
        }
    }

    public string Color
    {
        get => _color;
        set
        {
            if (!Set(ref _color, value)) return;
            ValidateAll();
            // Цвет — чисто визуальный параметр: перекрашиваем готовый луч без нового расчёта
            if (HasResult && ColorError.Length == 0) OnColorChanged?.Invoke(_color);
        }
    }

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

    public List<TrajectoryPoint> Trajectory
    {
        get => _trajectory;
        private set
        {
            Set(ref _trajectory, value);
            UpdatePointsInfo();
            OnPropertyChanged(nameof(HasResult));
            RaiseCanExecute();
        }
    }

    /// <summary>Параметры последнего успешного расчёта (для экспорта).</summary>
    public RequestModel? LastRequest { get; private set; }

    /// <summary>Есть ли готовая траектория (доступны воспроизведение и экспорт).</summary>
    public bool HasResult => _trajectory.Count > 0;

    private bool _isSceneVisible = true, _isChartVisible = true;
    /// <summary>Слои: показывать сцену (траекторию) и/или график N и угла.</summary>
    public bool IsSceneVisible { get => _isSceneVisible; set => Set(ref _isSceneVisible, value); }
    public bool IsChartVisible { get => _isChartVisible; set => Set(ref _isChartVisible, value); }

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (Set(ref _isPlaying, value)) OnPropertyChanged(nameof(PlayButtonText));
        }
    }

    public string PlayButtonText => _isPlaying ? "Пауза" : "Старт";
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
    public ICommand CancelCommand => _cancel;
    public ICommand PlayPauseCommand => _playPause;
    public ICommand ReplayCommand => _replay;

    /// <summary>true — приложение работает на демо-данных (Assets/*.json), а не на сервере.</summary>
    public bool IsDemoMode { get; }

    // ---------- События для View ----------
    /// <summary>Цвет передаётся строкой #rrggbb, чтобы ViewModel не зависела от UI-фреймворка.</summary>
    public event Action<List<TrajectoryPoint>, string, Models.Bounds>? OnTrajectoryReady;
    public event Action? OnCleared;

    /// <summary>Цвет изменён при уже готовом результате: перекрасить луч без пересчёта.</summary>
    public event Action<string>? OnColorChanged;

    public MainViewModel(IApiClient api, ITicker ticker, bool isDemoMode = false)
    {
        _api = api;
        _ticker = ticker;
        IsDemoMode = isDemoMode;
        _check = new RelayCommand(OnCheck, () => IsCheckEnabled && !IsRunning);
        _run = new RelayCommand(OnRun, () => IsCheckEnabled && !IsRunning);
        _reset = new RelayCommand(OnReset);
        _cancel = new RelayCommand(() => _cts?.Cancel(), () => IsRunning);
        _playPause = new RelayCommand(TogglePlayback, () => HasResult && !IsRunning);
        _replay = new RelayCommand(Replay, () => HasResult && !IsRunning);
        _ticker.Tick += OnTick;
        ValidateAll();
    }

    private void RaiseCanExecute()
    {
        _check.RaiseCanExecuteChanged();
        _run.RaiseCanExecuteChanged();
        _cancel.RaiseCanExecuteChanged();
        _playPause.RaiseCanExecuteChanged();
        _replay.RaiseCanExecuteChanged();
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

        var b = Validators.ValidateBounds(_xMinText, _xMaxText, _yMinText, _yMaxText);
        BoundsError = b.Message;
        if (b.Valid)
            _bounds = new Models.Bounds
            {
                XMin = Parse(_xMinText), XMax = Parse(_xMaxText),
                YMin = Parse(_yMinText), YMax = Parse(_yMaxText)
            };

        // Как на сервере (OUT_OF_BOUNDS): стартовая точка должна лежать внутри области расчёта
        if (b.Valid && x.Valid && (StartX < _bounds.XMin || StartX > _bounds.XMax))
        { x = ValidationResult.Fail($"X должен быть от {_bounds.XMin} до {_bounds.XMax}"); XError = x.Message; }
        if (b.Valid && y.Valid && (StartY < _bounds.YMin || StartY > _bounds.YMax))
        { y = ValidationResult.Fail($"Y должен быть от {_bounds.YMin} до {_bounds.YMax}"); YError = y.Message; }

        IsCheckEnabled = f.Valid && x.Valid && y.Valid && a.Valid && s.Valid && c.Valid && b.Valid;
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
            XMin = _bounds.XMin, XMax = _bounds.XMax,
            YMin = _bounds.YMin, YMax = _bounds.YMax
        },
        Color = Color
    };

    public async Task OnCheck()
    {
        ValidateAll();
        if (!IsCheckEnabled) return;

        IsRunning = true;
        StatusMessage = "Проверка...";
        _cts = new CancellationTokenSource();
        try
        {
            var resp = await _api.CheckAsync(BuildRequest(), _cts.Token);
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
            StatusMessage = DescribeFailure(ex);
        }
        finally { FinishOperation(); }
    }

    public async Task OnRun()
    {
        ValidateAll();
        if (!IsCheckEnabled) return;

        StopAnimation();
        IsRunning = true;
        StatusMessage = "Расчёт...";
        _cts = new CancellationTokenSource();
        try
        {
            var request = BuildRequest();
            var resp = await _api.RunAsync(request, _cts.Token);

            if (!resp.Success || resp.Trajectory.Count == 0)
            {
                ApplyServerErrors(resp.Errors);
                StatusMessage = resp.Errors.Count > 0
                    ? "Ошибка: " + resp.Errors[0].Message
                    : "Ошибка: пустая траектория";
                return;
            }

            LastRequest = request;
            Trajectory = resp.Trajectory;
            Statistics = resp.Statistics;
            Progress = 0;

            OnTrajectoryReady?.Invoke(resp.Trajectory, Color, request.Bounds);

            StatusMessage = "Готово";
            StartAnimation();
        }
        catch (Exception ex)
        {
            StatusMessage = DescribeFailure(ex);
        }
        finally { FinishOperation(); }
    }

    private void FinishOperation()
    {
        _cts?.Dispose();
        _cts = null;
        IsRunning = false;
    }

    /// <summary>Превращает исключение клиента в понятное пользователю сообщение.</summary>
    public static string DescribeFailure(Exception ex) => ex switch
    {
        OperationCanceledException => "Операция отменена",
        Services.ApiException api => "Ошибка: " + api.Message,
        _ => "Непредвиденная ошибка: " + ex.Message
    };

    public void OnReset()
    {
        StopAnimation();
        LastRequest = null;
        Trajectory = new List<TrajectoryPoint>();
        Statistics = null;
        Progress = 0;

        _formula = DefaultFormula; OnPropertyChanged(nameof(EnvironmentFormula));
        _startXText = "0"; OnPropertyChanged(nameof(StartXText));
        _startYText = "0"; OnPropertyChanged(nameof(StartYText));
        _angleText = "45"; OnPropertyChanged(nameof(StartAngleText));
        _stepText = "0.05"; OnPropertyChanged(nameof(StepSizeText));
        _color = "#ff0000"; OnPropertyChanged(nameof(Color));
        _xMinText = "-10"; OnPropertyChanged(nameof(XMinText));
        _xMaxText = "10"; OnPropertyChanged(nameof(XMaxText));
        _yMinText = "-10"; OnPropertyChanged(nameof(YMinText));
        _yMaxText = "10"; OnPropertyChanged(nameof(YMaxText));
        _selectedPreset = null; OnPropertyChanged(nameof(SelectedPreset));

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
                case "bounds": BoundsError = e.Message; break;
                default: break; // общая ошибка — попадёт в StatusMessage
            }
        }
    }

    // ---------- Анимация ----------
    private void StartAnimation()
    {
        _animElapsed = 0;
        _ticker.Start();
        IsPlaying = true;
    }

    public void StopAnimation()
    {
        _ticker.Stop();
        IsPlaying = false;
    }

    /// <summary>Старт / пауза. После окончания анимации старт начинает её сначала,
    /// иначе продолжает с текущего положения ползунка.</summary>
    public void TogglePlayback()
    {
        if (!HasResult) return;
        if (IsPlaying) { StopAnimation(); return; }

        if (Progress >= 1) Progress = 0;
        _animElapsed = Progress * AnimationSeconds;
        _ticker.Start();
        IsPlaying = true;
    }

    /// <summary>Проиграть траекторию заново с начала.</summary>
    public void Replay()
    {
        if (!HasResult) return;
        StopAnimation();
        Progress = 0;
        StartAnimation();
    }

    private void OnTick(double dt)
    {
        _animElapsed += dt;
        var p = _animElapsed / AnimationSeconds;
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
