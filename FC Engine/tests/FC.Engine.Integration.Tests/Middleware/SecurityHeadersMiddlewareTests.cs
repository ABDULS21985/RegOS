using FC.Engine.Api.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace FC.Engine.Integration.Tests.Middleware;

public class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task Sets_All_Security_Headers_On_Response()
    {
        var context = new DefaultHttpContext();
        var onStartingCallbacks = new List<(Func<object, Task> Callback, object State)>();

        // Capture OnStarting callbacks so we can invoke them manually
        var responseFeature = new TestHttpResponseFeature(onStartingCallbacks);
        context.Features.Set<IHttpResponseFeature>(responseFeature);

        var sut = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        await sut.InvokeAsync(context);

        // Fire OnStarting callbacks (simulates what Kestrel does)
        foreach (var (callback, state) in onStartingCallbacks)
        {
            await callback(state);
        }

        context.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
        context.Response.Headers["X-Frame-Options"].ToString().Should().Be("DENY");
        context.Response.Headers["X-XSS-Protection"].ToString().Should().Be("0");
        context.Response.Headers["Referrer-Policy"].ToString().Should().Be("strict-origin-when-cross-origin");
        context.Response.Headers["Content-Security-Policy"].ToString().Should().Contain("default-src 'self'");
        context.Response.Headers["Strict-Transport-Security"].ToString().Should().Contain("max-age=31536000");
        context.Response.Headers["Permissions-Policy"].ToString().Should().Contain("camera=()");
    }

    [Fact]
    public async Task Calls_Next_Middleware()
    {
        var context = new DefaultHttpContext();
        var calledNext = false;

        var sut = new SecurityHeadersMiddleware(_ =>
        {
            calledNext = true;
            return Task.CompletedTask;
        });

        await sut.InvokeAsync(context);

        calledNext.Should().BeTrue();
    }

    /// <summary>
    /// Test double that captures OnStarting callbacks so they can be invoked in unit tests.
    /// DefaultHttpContext's OnStarting only works with a real server pipeline.
    /// </summary>
    private class TestHttpResponseFeature : IHttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _callbacks;

        public TestHttpResponseFeature(List<(Func<object, Task>, object)> callbacks)
        {
            _callbacks = callbacks;
        }

        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted => false;

        public void OnStarting(Func<object, Task> callback, object state)
        {
            _callbacks.Add((callback, state));
        }

        public void OnCompleted(Func<object, Task> callback, object state) { }
    }
}
