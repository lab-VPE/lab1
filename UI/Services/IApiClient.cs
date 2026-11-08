using System.Threading;
using System.Threading.Tasks;
using UI.Models;

namespace UI.Services;

/// <summary>Контракт клиента симуляции: реальный сервер или демо-данные.</summary>
public interface IApiClient
{
    Task<CheckResponse> CheckAsync(RequestModel request, CancellationToken ct = default);
    Task<RunResponse> RunAsync(RequestModel request, CancellationToken ct = default);
}
