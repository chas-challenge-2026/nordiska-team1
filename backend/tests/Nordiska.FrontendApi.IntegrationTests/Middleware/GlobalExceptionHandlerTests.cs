using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Nordiska.BuildingBlocks.Database.Errors;
using Nordiska.FrontendApi.Middleware;
using Xunit;

namespace Nordiska.FrontendApi.IntegrationTests.Middleware;

public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_NotFoundException_Returns404WithFixedTitle()
    {
        var ctx = CreateHttpContext(out var problemDetailsService);
        var sut = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, problemDetailsService, new FakeHostEnvironment());

        var handled = await sut.TryHandleAsync(ctx, new NotFoundException("Customer 42 not found"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, ctx.Response.StatusCode);

        var body = ReadBody(ctx);
        Assert.Equal(404, body.GetProperty("status").GetInt32());
        Assert.Equal("Resurs hittades inte", body.GetProperty("title").GetString());
        Assert.Equal("Customer 42 not found", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task TryHandleAsync_ConflictException_Returns409WithFixedTitle()
    {
        var ctx = CreateHttpContext(out var problemDetailsService);
        var sut = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, problemDetailsService, new FakeHostEnvironment());

        await sut.TryHandleAsync(ctx, new ConflictException("Account is already closed"), CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, ctx.Response.StatusCode);

        var body = ReadBody(ctx);
        Assert.Equal("Otillåten operation", body.GetProperty("title").GetString());
        Assert.Equal("Account is already closed", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task TryHandleAsync_ArgumentException_Returns400()
    {
        var ctx = CreateHttpContext(out var problemDetailsService);
        var sut = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, problemDetailsService, new FakeHostEnvironment());

        await sut.TryHandleAsync(ctx, new ArgumentException("Amount must be positive"), CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, ctx.Response.StatusCode);
        Assert.Equal("Amount must be positive", ReadBody(ctx).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task TryHandleAsync_UnauthorizedAccessException_Returns401()
    {
        var ctx = CreateHttpContext(out var problemDetailsService);
        var sut = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, problemDetailsService, new FakeHostEnvironment());

        await sut.TryHandleAsync(ctx, new UnauthorizedAccessException("Not your account"), CancellationToken.None);

        Assert.Equal(StatusCodes.Status401Unauthorized, ctx.Response.StatusCode);
        Assert.Equal("Not your account", ReadBody(ctx).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task TryHandleAsync_UnknownException_InProduction_HidesRealMessage()
    {
        var ctx = CreateHttpContext(out var problemDetailsService);
        var sut = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, problemDetailsService, new FakeHostEnvironment { EnvironmentName = Environments.Production });

        await sut.TryHandleAsync(ctx, new InvalidOperationException("Connection string is malformed"), CancellationToken.None);

        Assert.Equal(StatusCodes.Status500InternalServerError, ctx.Response.StatusCode);
        Assert.Equal("Ett oväntat fel uppstod.", ReadBody(ctx).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task TryHandleAsync_UnknownException_InDevelopment_ExposesRealMessage()
    {
        var ctx = CreateHttpContext(out var problemDetailsService);
        var sut = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, problemDetailsService, new FakeHostEnvironment { EnvironmentName = Environments.Development });

        await sut.TryHandleAsync(ctx, new InvalidOperationException("Connection string is malformed"), CancellationToken.None);

        Assert.Equal(StatusCodes.Status500InternalServerError, ctx.Response.StatusCode);
        Assert.Equal("Connection string is malformed", ReadBody(ctx).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task TryHandleAsync_TaskCanceledException_Returns504()
    {
        var ctx = CreateHttpContext(out var problemDetailsService);
        var sut = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, problemDetailsService, new FakeHostEnvironment());

        await sut.TryHandleAsync(ctx, new TaskCanceledException("The operation was canceled."), CancellationToken.None);

        Assert.Equal(StatusCodes.Status504GatewayTimeout, ctx.Response.StatusCode);
        Assert.Equal("Gateway Timeout", ReadBody(ctx).GetProperty("title").GetString());
    }

    [Fact]
    public async Task TryHandleAsync_TimeoutException_Returns504()
    {
        var ctx = CreateHttpContext(out var problemDetailsService);
        var sut = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, problemDetailsService, new FakeHostEnvironment());

        await sut.TryHandleAsync(ctx, new TimeoutException(), CancellationToken.None);

        Assert.Equal(StatusCodes.Status504GatewayTimeout, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_HttpRequestExceptionUnauthorized_PassesThroughStatus()
    {
        var ctx = CreateHttpContext(out var problemDetailsService);
        var sut = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, problemDetailsService, new FakeHostEnvironment());

        await sut.TryHandleAsync(ctx, new HttpRequestException("denied", null, HttpStatusCode.Unauthorized), CancellationToken.None);

        Assert.Equal(StatusCodes.Status401Unauthorized, ctx.Response.StatusCode);
        Assert.Equal("Upstream Authentication Failed", ReadBody(ctx).GetProperty("title").GetString());
    }

    [Fact]
    public async Task TryHandleAsync_HttpRequestExceptionForbidden_PassesThroughStatus()
    {
        var ctx = CreateHttpContext(out var problemDetailsService);
        var sut = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, problemDetailsService, new FakeHostEnvironment());

        await sut.TryHandleAsync(ctx, new HttpRequestException("denied", null, HttpStatusCode.Forbidden), CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_HttpRequestExceptionTooManyRequests_Returns429()
    {
        var ctx = CreateHttpContext(out var problemDetailsService);
        var sut = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, problemDetailsService, new FakeHostEnvironment());

        await sut.TryHandleAsync(ctx, new HttpRequestException("slow down", null, (HttpStatusCode)429), CancellationToken.None);

        Assert.Equal(StatusCodes.Status429TooManyRequests, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_HttpRequestExceptionNotFound_Returns502WithDistinctTitle()
    {
        var ctx = CreateHttpContext(out var problemDetailsService);
        var sut = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, problemDetailsService, new FakeHostEnvironment());

        await sut.TryHandleAsync(ctx, new HttpRequestException("missing", null, HttpStatusCode.NotFound), CancellationToken.None);

        // Still 502: OUR resource isn't missing, a dependency's is - that's our server's problem, not the caller's.
        Assert.Equal(StatusCodes.Status502BadGateway, ctx.Response.StatusCode);
        Assert.Equal("Upstream Resource Not Found", ReadBody(ctx).GetProperty("title").GetString());
    }

    [Fact]
    public async Task TryHandleAsync_HttpRequestExceptionWithoutStatusCode_Returns502()
    {
        var ctx = CreateHttpContext(out var problemDetailsService);
        var sut = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, problemDetailsService, new FakeHostEnvironment());

        await sut.TryHandleAsync(ctx, new HttpRequestException("Connection refused"), CancellationToken.None);

        Assert.Equal(StatusCodes.Status502BadGateway, ctx.Response.StatusCode);
        Assert.Equal("Bad Gateway", ReadBody(ctx).GetProperty("title").GetString());
    }

    [Fact]
    public async Task TryHandleAsync_AnyException_IncludesTraceIdFromCustomizeProblemDetailsHook()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddLogging();
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        });
        var provider = services.BuildServiceProvider();

        var ctx = new DefaultHttpContext { RequestServices = provider, TraceIdentifier = "trace-abc-123" };
        ctx.Response.Body = new MemoryStream();

        var sut = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance,
            provider.GetRequiredService<IProblemDetailsService>(),
            new FakeHostEnvironment());

        await sut.TryHandleAsync(ctx, new NotFoundException("Customer 42 not found"), CancellationToken.None);

        Assert.Equal("trace-abc-123", ReadBody(ctx).GetProperty("traceId").GetString());
    }

    private static DefaultHttpContext CreateHttpContext(out IProblemDetailsService problemDetailsService)
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddLogging();
        services.AddProblemDetails();
        var provider = services.BuildServiceProvider();
        problemDetailsService = provider.GetRequiredService<IProblemDetailsService>();

        var ctx = new DefaultHttpContext
        {
            RequestServices = provider
        };
        ctx.Response.Body = new MemoryStream();
        ctx.Request.Path = "/api/customers/42";

        return ctx;
    }

    private static JsonElement ReadBody(DefaultHttpContext ctx)
    {
        ctx.Response.Body.Position = 0;
        using var reader = new StreamReader(ctx.Response.Body);
        return JsonDocument.Parse(reader.ReadToEnd()).RootElement;
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Nordiska.FrontendApi";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
