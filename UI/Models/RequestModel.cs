namespace UI.Models;

public class RequestModel
{
    public string EnvironmentFormula { get; set; } = "";
    public StartPoint StartPoint { get; set; } = new();
    public double StartAngle { get; set; }
    public double StepSize { get; set; }
    public Bounds Bounds { get; set; } = new();
    public string Color { get; set; } = "#ff0000";
}

public class StartPoint
{
    public double X { get; set; }
    public double Y { get; set; }
}

public class Bounds
{
    public double XMin { get; set; }
    public double XMax { get; set; }
    public double YMin { get; set; }
    public double YMax { get; set; }
}
