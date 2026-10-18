using Core.Models;
using Core.Physics;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();
app.UseCors();

app.MapGet("/", () => "Controller is running");

app.MapPost("/api/simulate/check", (RequestModel request) =>
{
    var errors = ValidateRequest(request);
    if (errors.Count > 0) return Results.Ok(new CheckResponse { Valid = false, Errors = errors });
    return Results.Ok(new CheckResponse { Valid = true });
});

app.MapPost("/api/simulate/run", (RequestModel request) =>
{
    var errors = ValidateRequest(request);
    if (errors.Count > 0) return Results.Ok(new RunResponse { Success = false, Errors = errors });
    var calculator = new RayCalculator();
    return Results.Ok(calculator.Run(request));
});

app.Run();

static List<ErrorModel> ValidateRequest(RequestModel request)
{
    var errors = new List<ErrorModel>();
    if (string.IsNullOrWhiteSpace(request.EnvironmentFormula))
        errors.Add(new ErrorModel("EMPTY_FIELD", "environmentFormula", "Formula cannot be empty"));
    if (request.StartAngle < -360 || request.StartAngle > 360)
        errors.Add(new ErrorModel("INVALID_ANGLE", "startAngle", "Angle must be between -360 and 360"));
    if (request.StepSize <= 0)
        errors.Add(new ErrorModel("INVALID_STEP", "stepSize", "Step must be positive"));
    if (request.StepSize > 1)
        errors.Add(new ErrorModel("INVALID_STEP", "stepSize", "Step too large (max 1)"));
    if (request.Bounds.XMin >= request.Bounds.XMax)
        errors.Add(new ErrorModel("OUT_OF_BOUNDS", "bounds", "XMin must be less than XMax"));
    if (request.Bounds.YMin >= request.Bounds.YMax)
        errors.Add(new ErrorModel("OUT_OF_BOUNDS", "bounds", "YMin must be less than YMax"));
    if (request.StartPoint.X < request.Bounds.XMin || request.StartPoint.X > request.Bounds.XMax ||
        request.StartPoint.Y < request.Bounds.YMin || request.StartPoint.Y > request.Bounds.YMax)
        errors.Add(new ErrorModel("OUT_OF_BOUNDS", "startPoint", "Start point is outside the bounds"));
    if (string.IsNullOrEmpty(request.Color) || !request.Color.StartsWith("#") || request.Color.Length != 7)
        errors.Add(new ErrorModel("INVALID_COLOR", "color", "Color must be in #rrggbb format"));
    return errors;
}
