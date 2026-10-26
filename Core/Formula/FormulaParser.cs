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

    public bool IsValid(out string errorMessage)
    {
        try
        {
            _pos = 0;
            ParseExpression(0);
            if (_pos < _formula.Length)
            {
                errorMessage = $"Unexpected character: '{_formula[_pos]}'";
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

    public double Evaluate(double x)
    {
        _pos = 0;
        var result = ParseExpression(x);
        if (_pos < _formula.Length)
            throw new Exception($"Unexpected character: '{_formula[_pos]}'");
        return result;
    }


    // Обработка сложения и вычитания (бинарные операторы)

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


    // Обработка умножения и деления

    private double ParseTerm(double x)
    {
        var left = ParseFactor(x);
        while (_pos < _formula.Length && (_formula[_pos] == '*' || _formula[_pos] == '/'))
        {
            var op = _formula[_pos++];
            var right = ParseFactor(x);
            if (op == '*') left *= right;
            else
            {
                if (Math.Abs(right) < 1e-12) throw new Exception("Division by zero");
                left /= right;
            }
        }
        return left;
    }


    // Обработка унарного минуса (например, -x или -5)
    private double ParseFactor(double x)
    {

    private double ParseFactor(double x)
    {
        var value = ParsePower(x);

        if (_pos < _formula.Length && _formula[_pos] == '-')
        {
            _pos++;
            return -ParseFactor(x);
        }

        return ParsePower(x);
    }

    // Обработка степеней

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
        if (_pos >= _formula.Length) throw new Exception("Unexpected end of formula");
        var c = _formula[_pos];

        if (c == '(')
        {
            _pos++;
            var value = ParseExpression(x);
            if (_pos >= _formula.Length || _formula[_pos] != ')')
                throw new Exception("Unclosed parenthesis");
            _pos++;
            return value;
        }

        if (c == 'x')
        {
            _pos++;
            return x;
        }

        if (char.IsDigit(c) || c == '.')
        {
            var start = _pos;
            while (_pos < _formula.Length && (char.IsDigit(_formula[_pos]) || _formula[_pos] == '.'))
                _pos++;
            var numberStr = _formula.Substring(start, _pos - start);
            if (!double.TryParse(numberStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                throw new Exception($"Failed to parse number: '{numberStr}'");
            return number;
        }

        throw new Exception($"Unexpected character: '{c}'");
    }

}
}

