using System;

namespace UI.Services;

/// <summary>Источник "тиков" для анимации. Позволяет не зависеть от UI-фреймворка во ViewModel.</summary>
public interface ITicker
{
    /// <summary>Вызывается периодически; аргумент — секунды с предыдущего тика.</summary>
    event Action<double>? Tick;
    void Start();
    void Stop();
}
