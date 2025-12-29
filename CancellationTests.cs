using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StrawberryShake;
using Xunit;

namespace StrawberryShakeCancellation;

public class CancellationTests
{
    [Fact]
    public async Task CancellationBeforeResponse_ThrowsOperationCanceledException()
    {
        // Arrange
        var client = CreateClient();
        var cts = new CancellationTokenSource(0); // Cancel immediately

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.GetSomething.ExecuteAsync(cts.Token));
    }

    [Fact]
    public async Task CancellationAfterResponse_NoExceptionIsThrown()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var client = CreateClient(new MockMessageHandler(cts));

        // Act
        var result = await client.GetSomething.ExecuteAsync(cts.Token);

        // Assert
        Assert.True(result.IsErrorResult());
        Assert.Equal("A task was canceled.", result.Errors.First().Message);
    }

    private static ITestClient CreateClient(HttpMessageHandler? messageHandler = null)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTestClient();
        var httpClientBuilder = serviceCollection.AddHttpClient("TestClient")
            .ConfigureHttpClient(c => c.BaseAddress = new Uri("http://localhost"));

        if (messageHandler is not null)
        {
            httpClientBuilder.ConfigurePrimaryHttpMessageHandler(() => messageHandler);
        }

        var services = serviceCollection.BuildServiceProvider();
        return services.GetRequiredService<ITestClient>();
    }
}

file class MockMessageHandler(CancellationTokenSource cts) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cts.Cancel(); // Cancel when returning the response
        return Task.FromResult(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = JsonContent.Create(new {}) // Dummy response (not important for the test)
        });
    }
}