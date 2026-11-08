using System;

namespace UI.Services;

/// <summary>Ошибка обращения к серверу или к демо-данным; Message — уже понятный пользователю текст.</summary>
public class ApiException : Exception
{
    public ApiException(string message, Exception? inner = null) : base(message, inner) { }
}
