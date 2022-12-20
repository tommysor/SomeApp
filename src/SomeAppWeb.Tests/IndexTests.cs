using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SomeAppWeb.Tests;

public class IndexTests : IClassFixture<SystemDependenciesFixture>
{
    private const string Faketext = "Faketext";
    private const string updateActionPath = "/index?handler=UpdateText";
    private readonly SystemDependenciesFixture _systemDependenciesFixture;
    private readonly WebApplicationFactory<IAssemblyMarker> _host;
    private readonly HttpClient _sutClient;
    private readonly WebApplicationFactory<SomeApiMock.IAssemblyMarker> _someApiMock;
    private readonly HttpClient _someApiMockClient;
    private readonly string _requestQueueName;
    private readonly string _replyQueueName;

    public IndexTests(SystemDependenciesFixture systemDependenciesFixture)
    {
        _systemDependenciesFixture = systemDependenciesFixture;

        var queuePostfix = Guid.NewGuid();
        _requestQueueName = $"request-{queuePostfix}";
        _replyQueueName = $"reply-{queuePostfix}";
        _host = CreateHostWithFakes();
        _sutClient = _host.CreateClient();
        _someApiMock = CreateSomeApiMock();
        _someApiMockClient = _someApiMock.CreateClient();
    }

    private void OverrideConfig(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            IEnumerable<KeyValuePair<string, string?>>? configValues = new []
            {
                new KeyValuePair<string, string?>("AzureStorageQueues:Request:Name", _requestQueueName),
                new KeyValuePair<string, string?>("AzureStorageQueues:Reply:Name", _replyQueueName),
            };
            config.AddInMemoryCollection(configValues);
        });
    }

    private WebApplicationFactory<IAssemblyMarker> CreateHostWithFakes()
    {
        return new WebApplicationFactory<IAssemblyMarker>()
            .WithWebHostBuilder(builder =>
            {
                OverrideConfig(builder);
                builder.ConfigureServices(services =>
                {
                });
            });
    }

    private WebApplicationFactory<SomeApiMock.IAssemblyMarker> CreateSomeApiMock()
    {
        return new WebApplicationFactory<SomeApiMock.IAssemblyMarker>()
            .WithWebHostBuilder(builder =>
            {
                OverrideConfig(builder);
                builder.ConfigureServices(services =>
                {
                });
            });
    }

    [Fact]
    public async Task Get_Succeeds()
    {
        var actual = await _sutClient.GetAsync("/");
        Assert.Equal(System.Net.HttpStatusCode.OK, actual.StatusCode);
    }

    [Fact]
    public async Task GetSomeApiMock_Succeeds()
    {
        var actual = await _someApiMockClient.GetAsync("/");
        Assert.Equal(System.Net.HttpStatusCode.OK, actual.StatusCode);
    }

    [Fact]
    public async Task GetUpdateText_Succeeds()
    {
        var actual = await _sutClient.GetAsync(updateActionPath);
        var actualText = await actual.Content.ReadAsStringAsync();
        Assert.Equal(System.Net.HttpStatusCode.OK, actual.StatusCode);
        Assert.Equal(Faketext, actualText);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData(" ", null)]
    [InlineData("\t", null)]
    [InlineData(null, "")]
    [InlineData(null, " ")]
    [InlineData(null, "\t")]
    public async Task GetUpdateText_WhenReceiveFails_ShouldThrow(string? id, string? text)
    {
        var mockObject = new
        {
            QueueName = _requestQueueName,
            ReturnThisRequestId = id,
            ReturnThisText = text,
        };
        await _someApiMockClient.PostAsync(
            "/mock",
            new StringContent(JsonSerializer.Serialize(mockObject), Encoding.UTF8, "application/json"));
        
        var actual = await _sutClient.GetAsync(updateActionPath);
        Assert.False(actual.IsSuccessStatusCode);
        var contentActual = await actual.Content.ReadAsStringAsync();
        Assert.Contains("TimeoutException", contentActual);
    }
}
