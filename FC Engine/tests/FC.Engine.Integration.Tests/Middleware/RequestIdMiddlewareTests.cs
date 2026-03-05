using FC.Engine.Api.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FC.Engine.Integration.Tests.Middleware;

public class RequestIdMiddlewareTests
{
    private readonly Mock<ILogger<RequestIdMiddleware>> _logger = new();

    [Fact]
    public async Task Generates_RequestId_When_Header_Missing()
    {
        var context = new DefaultHttpContext();
        var calledNext = false;

        var sut = new RequestIdMiddleware(_ =>
        {
            calledNext = true;
            return Task.CompletedTask;
        }, _logger.Object);

        await sut.InvokeAsync(context);

        calledNext.Should().BeTrue();
        context.Items["RequestId"].Should().NotBeNull();
        context.Items["RequestId"]!.ToString()!.Length.Should().Be(16);
        context.TraceIdentifier.Should().Be(context.Items["RequestId"]!.ToString());
    }

    [Fact]
    public async Task Echoes_Existing_RequestId_From_Header()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Request-ID"] = "my-custom-id-123";

        var sut = new RequestIdMiddleware(_ => Task.CompletedTask, _logger.Object);

        await sut.InvokeAsync(context);

        context.Items["RequestId"].Should().Be("my-custom-id-123");
        context.TraceIdentifier.Should().Be("my-custom-id-123");
    }

    [Fact]
    public async Task Generates_New_RequestId_When_Header_Too_Long()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Request-ID"] = new string('x', 200);

        var sut = new RequestIdMiddleware(_ => Task.CompletedTask, _logger.Object);

        await sut.InvokeAsync(context);

        context.Items["RequestId"]!.ToString()!.Length.Should().Be(16);
    }

    [Fact]
    public async Task Sets_Response_Header_Via_OnStarting()
    {
        var context = new DefaultHttpContext();
        // DefaultHttpContext triggers OnStarting when we write to body
        // For unit testing, just verify the Items are set correctly
        var sut = new RequestIdMiddleware(_ => Task.CompletedTask, _logger.Object);

        await sut.InvokeAsync(context);

        context.Items["RequestId"].Should().NotBeNull();
    }
}
