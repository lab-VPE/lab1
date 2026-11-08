using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UI.Models;

namespace UI.Services;

public class ApiClient : IApiClient
{
    // camelCase + регистронезависимо — как принято в ASP.NET Core
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    /// <summary>true — читаем Assets/*.json, false — ходим на сервер.</summary>
    public bool UseLocal { get; set; }

    /// <summary>Адрес Controller без завершающего слэша.</summary>
    public string ServerUrl { get; }

    public ApiClient(bool useLocal = false, string? serverUrl = null, HttpMessageHandler? handler = null)
    {
        UseLocal = useLocal;
        ServerUrl = (serverUrl ?? AppSettings.DefaultServerUrl).TrimEnd('/');
        _http = handler == null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(60);
    }

    public Task<CheckResponse> CheckAsync(RequestModel request, CancellationToken ct = default)
        => UseLocal
            ? ReadLocalAsync<CheckResponse>("check.json", ct)
            : PostAsync<CheckResponse>("/api/simulate/check", request, ct);

    public Task<RunResponse> RunAsync(RequestModel request, CancellationToken ct = default)
        => UseLocal
            ? ReadLocalAsync<RunResponse>("run.json", ct)
            : PostAsync<RunResponse>("/api/simulate/run", request, ct);

    private async Task<T> PostAsync<T>(string path, RequestModel request, CancellationToken ct) where T : class
    {
        HttpResponseMessage resp;
        try
        {
            resp = await _http.PostAsJsonAsync(ServerUrl + path, request, Json, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException($"Сервер недоступен ({ServerUrl}). Запустите Controller и повторите.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new ApiException("Превышено время ожидания ответа сервера.", ex);
        }

        using (resp)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);

            T? parsed = null;
            try { parsed = JsonSerializer.Deserialize<T>(body, Json); }
            catch (JsonException) { /* тело не JSON контракта */ }

            // 2xx — обычный ответ; 4xx/5xx — принимаем, только если сервер вернул список ошибок по контракту
            if (parsed != null && (resp.IsSuccessStatusCode || HasErrors(parsed)))
                return parsed;

            if (!resp.IsSuccessStatusCode)
                throw new ApiException($"Сервер вернул ошибку {(int)resp.StatusCode} ({resp.ReasonPhrase}).");

            throw new ApiException("Сервер вернул ответ в неожиданном формате.");
        }
    }

    private static bool HasErrors(object response) => response switch
    {
        CheckResponse c => c.Errors.Count > 0,
        RunResponse r => r.Errors.Count > 0,
        _ => false
    };

    private static async Task<T> ReadLocalAsync<T>(string fileName, CancellationToken ct) where T : class
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, Json, ct)
                   ?? throw new ApiException($"Файл демо-данных {fileName} пуст.");
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new ApiException($"Не найден файл демо-данных: {path}", ex);
        }
        catch (JsonException ex)
        {
            throw new ApiException($"Файл демо-данных {fileName} повреждён: {ex.Message}", ex);
        }
    }
}
