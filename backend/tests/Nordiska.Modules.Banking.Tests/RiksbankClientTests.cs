using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Nordiska.Modules.Banking.Infrastructure.External.Riksbank;

namespace Nordiska.Modules.Banking.Tests;

public class RiksbankClientTests
{
    private class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;

        public HttpRequestMessage? LastRequest { get; private set; }

        public StubHttpMessageHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private static RiksbankClient CreateClient(StubHttpMessageHandler handler)
    {
        var options = new RiksbankOptions();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(options.BaseUrl) };
        return new RiksbankClient(httpClient, Options.Create(options));
    }

    [Fact]
    public async Task GetLatestPolicyRate_ValidResponse_ReturnsDateAndPercent()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, """{"date":"2026-09-18","value":1.75}""");
        var client = CreateClient(handler);

        var observation = await client.GetLatestPolicyRateAsync();

        Assert.Equal(new DateOnly(2026, 9, 18), observation.Date);
        Assert.Equal(1.75m, observation.ValuePercent);
    }

    [Fact]
    public async Task GetLatestPolicyRate_CallsLatestObservationForPolicyRateSeries()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, """{"date":"2026-09-18","value":1.75}""");
        var client = CreateClient(handler);

        await client.GetLatestPolicyRateAsync();

        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("https://api.riksbank.se/swea/v1/Observations/Latest/SECBREPOEFF", handler.LastRequest.RequestUri!.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task GetLatestPolicyRate_ErrorStatus_ThrowsHttpRequestException(HttpStatusCode statusCode)
    {
        var client = CreateClient(new StubHttpMessageHandler(statusCode, "{}"));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetLatestPolicyRateAsync());

        Assert.Equal(statusCode, ex.StatusCode);
    }

    [Fact]
    public async Task GetLatestPolicyRate_MalformedJson_ThrowsJsonException()
    {
        var client = CreateClient(new StubHttpMessageHandler(HttpStatusCode.OK, "not json"));

        await Assert.ThrowsAsync<JsonException>(() => client.GetLatestPolicyRateAsync());
    }

    [Fact]
    public async Task GetLatestPolicyRate_NullBody_ThrowsHttpRequestException()
    {
        var client = CreateClient(new StubHttpMessageHandler(HttpStatusCode.OK, "null"));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetLatestPolicyRateAsync());
    }
}
