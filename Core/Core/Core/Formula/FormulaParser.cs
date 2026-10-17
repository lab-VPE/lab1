using System.Globalization;

namespace Core.Formula;

public class FormulaParser
{
    private readonly string _formula;
    private int _pos;

    public FormulaParser(string formula)
    {
        _formula = formula.Replace(" ", "");
        _pos = 0;
    }

    // Проверяет формулу: можно ли её распарсить
    public bool IsValid(out string errorMessage)
    {
        try
        {
            _pos = 0;
            ParseExpression();
            if (_pos < _formula.Length)
            {
                errorMessage = $"Неожиданный символ: '{_formula[_pos]}'";
                return false;
            }
            errorMessage = "";
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    // Считает значение формулы при заданном x
    public double Evaluate(double x)
    {
        _pos = 0;
        var result = ParseExpression(x);
        if (_pos < _formula.Length)
            throw new Exception($"Неожиданный символ: '{_formula[_pos]}'");
        return result;
    }

    private double ParseExpression(double x)
    {
        var left = ParseTerm(x);
        while (_pos < _formula.Length && (_formula[_pos] == '+' || _formula[_pos] == '-'))
        {
            var op = _formula[_pos++];
            var right = ParseTerm(x);
            left = op == '+' ? left + right : left - right;
        }
        return left;
    }

    private double ParseTerm(double x)
    {
        var left = ParseFactor(x);
        while (_pos < _formula.Length && (_formula[_pos] == '*' || _formula[_pos] == '/'))
        {
            var op = _formula[_pos++];
            var right = ParseFactor(x);
            if (op == '*')
                left *= right;
            else
            {
                if (Math.Abs(right) < 1e-12)
                    throw new Exception("Деление на ноль");
                left /= right;
            }
        }
        return left;
    }

    private double ParseFactor(double x)
    {
        var value = ParsePower(x);

        // Унарный минус
        if (_pos < _formula.Length && _formula[_pos] == '-')
        {
            _pos++;
            return -ParseFactor(x);
        }

        return value;
    }

    private double ParsePower(double x)
    {
        var baseValue = ParsePrimary(x);

        if (_pos < _formula.Length && _formula[_pos] == '^')
        {
            _pos++;
            var exponent = ParsePower(x);
            return Math.Pow(baseValue, exponent);
        }

        return baseValue;
    }

    private double ParsePrimary(double x)
    {
        if (_pos >= _formula.Length)
            throw new Exception("Неожиданный конец формулы");

        var c = _formula[_pos];

        // Скобки
        if (c == '(')
        {
            _pos++;
            var value = ParseExpression(x);
            if (_pos >= _formula.Length || _formula[_pos] != ')')
                throw new Exception("Не закрыта скобка");
            _pos++;
            return value;
        }

        // Переменная x
        if (c == 'x')
        {
            _pos++;
            return x;
        }

        // Число
        if (char.IsDigit(c) || c == '.')
        {
            var start = _pos;
            while (_pos < _formula.Length && (char.IsDigit(_formula[_pos]) || _formula[_pos] == '.'))
                _pos++;

            var numberStr = _formula.Substring(start, _pos - start);
            if (!double.TryParse(numberStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                throw new Exception($"Не удалось разобрать число: '{numberStr}'");

            return number;
        }

        throw new Exception($"Неожиданный символ: '{c}'");
    }
}
