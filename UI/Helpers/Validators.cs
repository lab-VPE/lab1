using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace UI.Helpers;

public struct ValidationResult
{
    public bool Valid;
    public string Message;

    public static ValidationResult Ok() => new() { Valid = true, Message = "" };
    public static ValidationResult Fail(string message) => new() { Valid = false, Message = message };
}

public static class Validators
{
    private const string AllowedChars = "0123456789x+-*/().^ ";

    public static bool TryParseDouble(string? value, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;
        return double.TryParse(value.Trim().Replace(',', '.'),
            NumberStyles.Float, CultureInfo.InvariantCulture, out result)
            && double.IsFinite(result);
    }

    public static ValidationResult ValidateFormula(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ValidationResult.Fail("Формула не может быть пустой");

        var s = value.Trim();
        if (s.Length < 4)
            return ValidationResult.Fail("Формула слишком короткая");

        foreach (var c in s)
            if (!AllowedChars.Contains(c) && !char.IsWhiteSpace(c))
                return ValidationResult.Fail("Недопустимые символы");

        var depth = 0;
        foreach (var c in s)
        {
            if (c == '(') depth++;
            else if (c == ')' && --depth < 0)
                return ValidationResult.Fail("Лишняя скобка");
        }
        if (depth > 0)
            return ValidationResult.Fail("Не закрыта скобка");

        return ValidationResult.Ok();
    }

    public static ValidationResult ValidateAngle(string? value)
    {
        if (!TryParseDouble(value, out var a))
            return ValidationResult.Fail("Введите число");
        if (a < -360 || a > 360)
            return ValidationResult.Fail("Угол от -360 до 360");
        return ValidationResult.Ok();
    }

    public static ValidationResult ValidateStep(string? value)
    {
        if (!TryParseDouble(value, out var s) || s <= 0)
            return ValidationResult.Fail("Шаг должен быть положительным");
        if (s > 1)
            return ValidationResult.Fail("Шаг слишком большой (максимум 1)");
        return ValidationResult.Ok();
    }

    public static ValidationResult ValidateCoordinate(string? value)
        => TryParseDouble(value, out _)
            ? ValidationResult.Ok()
            : ValidationResult.Fail("Введите число");

    public static ValidationResult ValidateColor(string? value)
        => value != null && Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$")
            ? ValidationResult.Ok()
            : ValidationResult.Fail("Формат цвета: #rrggbb");
}
