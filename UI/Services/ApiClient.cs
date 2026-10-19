using System;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using UI.Models;

namespace UI.Services;

public class ApiClient
{
    /// <summary>true — читаем Assets/*.json, false — ходим на сервер.</summary>
    public bool UseLocal { get; set; } = true;

    // Порт Controller из Controller/Properties/launchSettings.json (профиль http)
    public const string ServerBase = "http://localhost:5067";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    // camelCase + регистронезависимо — как принято в ASP.NET Core
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<CheckResponse> CheckAsync(RequestModel request)
    {
        if (UseLocal)
            return await ReadLocalAsync<CheckResponse>("check.json");

        var resp = await Http.PostAsJsonAsync($"{ServerBase}/api/simulate/check", request, Json);
        return await resp.Content.ReadFromJsonAsync<CheckResponse>(Json)
               ?? throw new InvalidOperationException("Пустой ответ сервера");
    }

    public async Task<RunResponse> RunAsync(RequestModel request)
    {
        if (UseLocal)
            return await ReadLocalAsync<RunResponse>("run.json");

        var resp = await Http.PostAsJsonAsync($"{ServerBase}/api/simulate/run", request, Json);
        return await resp.Content.ReadFromJsonAsync<RunResponse>(Json)
               ?? throw new InvalidOperationException("Пустой ответ сервера");
    }

    private static async Task<T> ReadLocalAsync<T>(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, Json)
               ?? throw new InvalidOperationException($"Не удалось прочитать {fileName}");
    }
}
