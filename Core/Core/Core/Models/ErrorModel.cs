namespace Core.Models;

public class ErrorModel
{
    public string Code { get; set; } = "";
    public string Field { get; set; } = "";
    public string Message { get; set; } = "";

    public ErrorModel() { }

    public ErrorModel(string code, string field, string message)
    {
        Code = code;
        Field = field;
        Message = message;
    }
}
