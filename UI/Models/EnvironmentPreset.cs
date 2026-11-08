namespace UI.Models;

/// <summary>Готовая среда: название для списка и формула n(x).</summary>
public sealed record EnvironmentPreset(string Name, string Formula)
{
    public override string ToString() => Name;
}
