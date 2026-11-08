using System.Net;
using System.Net.Http;
using UI.Models;
using UI.Services;

namespace UI.Tests;

public class ApiClientTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);
        public string? LastUrl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastUrl = request.RequestUri?.ToString();
            return Task.FromResult(Respond(request));
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string body)
        => new(code) { Content = new StringContent(body) };

    [Fact]
    public async Task Run_Success_ParsesTrajectoryAndStatistics()
    {
        var h = new FakeHandler
        {
            Respond = _ => Json(HttpStatusCode.OK,
                "{\"success\":true,\"trajectory\":[{\"x\":1,\"y\":2,\"n\":1.1,\"angle\":3}],\"statistics\":{\"steps\":1,\"pathLength\":5},\"errors\":[]}")
        };
        var api = new ApiClient(false, "http://srv:1/", h);

        var r = await api.RunAsync(new RequestModel());

        Assert.True(r.Success);
        Assert.Single(r.Trajectory);
        Assert.Equal(1.1, r.Trajectory[0].N);
        Assert.Equal(5, r.Statistics.PathLength);
        Assert.Equal("http://srv:1/api/simulate/run", h.LastUrl);
    }

    [Fact]
    public async Task Check_UsesCheckEndpoint()
    {
        var h = new FakeHandler { Respond = _ => Json(HttpStatusCode.OK, "{\"valid\":true,\"errors\":[]}") };
        var api = new ApiClient(false, "http://srv:1", h);

        var r = await api.CheckAsync(new RequestModel());

        Assert.True(r.Valid);
        Assert.Equal("http://srv:1/api/simulate/check", h.LastUrl);
    }

    [Fact]
    public async Task ClientError_WithContractBody_ReturnsErrors()
    {
        var h = new FakeHandler
        {
            Respond = _ => Json(HttpStatusCode.UnprocessableEntity,
                "{\"success\":false,\"errors\":[{\"code\":\"E\",\"field\":\"startAngle\",\"message\":\"bad\"}]}")
        };
        var r = await new ApiClient(false, null, h).RunAsync(new RequestModel());

        Assert.False(r.Success);
        Assert.Equal("startAngle", r.Errors[0].Field);
    }

    [Fact]
    public async Task ServerError_WithoutContractBody_ThrowsApiException()
    {
        var h = new FakeHandler { Respond = _ => Json(HttpStatusCode.InternalServerError, "boom") };
        var ex = await Assert.ThrowsAsync<ApiException>(() => new ApiClient(false, null, h).RunAsync(new RequestModel()));
        Assert.Contains("500", ex.Message);
    }

    [Fact]
    public async Task NonJsonBody_ThrowsApiException()
    {
        var h = new FakeHandler { Respond = _ => Json(HttpStatusCode.OK, "<html>") };
        await Assert.ThrowsAsync<ApiException>(() => new ApiClient(false, null, h).CheckAsync(new RequestModel()));
    }

    [Fact]
    public async Task ServerDown_ThrowsFriendlyApiException()
    {
        var h = new FakeHandler { Respond = _ => throw new HttpRequestException("refused") };
        var ex = await Assert.ThrowsAsync<ApiException>(() => new ApiClient(false, "http://x", h).CheckAsync(new RequestModel()));
        Assert.Contains("недоступен", ex.Message);
    }

    // AppSettings хранит состояние статически, поэтому эти проверки идут последовательно в одном тесте.
    [Fact]
    public void AppSettings_ParsesArgumentsAndEnvironment()
    {
        AppSettings.Parse(new[] { "--demo", "--server=http://h:1/" }, null, null);
        Assert.True(AppSettings.DemoMode);
        Assert.Equal("http://h:1", AppSettings.ServerUrl);

        AppSettings.Parse(Array.Empty<string>(), "1", "http://e:2");
        Assert.True(AppSettings.DemoMode);
        Assert.Equal("http://e:2", AppSettings.ServerUrl);

        AppSettings.Parse(Array.Empty<string>(), null, null);
        Assert.False(AppSettings.DemoMode);
        Assert.Equal(AppSettings.DefaultServerUrl, AppSettings.ServerUrl);
    }
}
