namespace Core.Models;

public class CheckResponse
{
    public bool Valid { get; set; }
    public List<ErrorModel> Errors { get; set; } = new();
}

public class RunResponse
{
    public bool Success { get; set; }
    public List<TrajectoryPoint> Trajectory { get; set; } = new();
    public StatisticsResult Statistics { get; set; } = new();
    public List<ErrorModel> Errors { get; set; } = new();
}

public class StatisticsResult
{
    public double PathLength { get; set; }
    public double TotalDeviation { get; set; }
    public double MaxCurvature { get; set; }
    public StartPoint MaxCurvatureAt { get; set; } = new();
    public double RefractionIndexMin { get; set; }
    public double RefractionIndexMax { get; set; }
    public int Steps { get; set; }
    public long ComputationTimeMs { get; set; }
}
