using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SomeAppWeb.Tests;

public class IndexTests
{
    private const string Faketext = "Faketext";
    private const string updateActionPath = "/index?handler=UpdateText";
    private readonly TestcontainersContainer _azuriteContainer;

    public IndexTests()
    {
        _azuriteContainer = new TestcontainersBuilder<TestcontainersContainer>()
            .WithImage("mcr.microsoft.com/azure-storage/azurite")
            .WithPortBinding(10000, 10000)
            .WithPortBinding(10001, 10001)
            .WithPortBinding(10002, 10002)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(10001))
            .Build();
        _azuriteContainer.StartAsync().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Get_Succeeds()
    {
        var host = CreateHostWithFakes();
        var client = host.CreateClient();
        var actual = await client.GetAsync("/");
        Assert.Equal(System.Net.HttpStatusCode.OK, actual.StatusCode);
    }

    [Fact]
    public async Task GetSomeApiMock_Succeeds()
    {
        var host = CreateSomeApiMock();
        var client = host.CreateClient();
        var actual = await client.GetAsync("/");
        Assert.Equal(System.Net.HttpStatusCode.OK, actual.StatusCode);
    }

    [Fact]
    public async Task GetUpdateText_Succeeds()
    {
        var host = CreateHostWithFakes();
        var client = host.CreateClient();
        var actual = await client.GetAsync(updateActionPath);
        Assert.Equal(System.Net.HttpStatusCode.OK, actual.StatusCode);
        var actualText = await actual.Content.ReadAsStringAsync();
        Assert.Equal(Faketext, actualText);
    }

    [Fact]
    public async Task GetUpdateText_WhenSendFails_ShouldThrow()
    {
        var host = CreateHostWithFakes(x => x.SendShouldFail = true);
        var client = host.CreateClient();
        var actual = await client.GetAsync(updateActionPath);
        Assert.False(actual.IsSuccessStatusCode);
        var contentActual = await actual.Content.ReadAsStringAsync();
        Assert.Contains("InvalidOperationException", contentActual);
    }

    [Theory]
    [InlineData(null, true, Faketext)]
    [InlineData("", false, Faketext)]
    [InlineData(" ", false, Faketext)]
    [InlineData("\t", false, Faketext)]
    [InlineData(null, false, null)]
    [InlineData(null, false, "")]
    [InlineData(null, false, " ")]
    [InlineData(null, false, "\t")]
    public async Task GetUpdateText_WhenReceiveFails_ShouldThrow(string? id, bool idNull, string? text)
    {
        var host = CreateHostWithFakes(x => 
        {
            x.RequestId = id;
            x.RequestIdNull = idNull;
            x.ResponseText = text;
        });
        var client = host.CreateClient();
        var actual = await client.GetAsync(updateActionPath);
        Assert.False(actual.IsSuccessStatusCode);
        var contentActual = await actual.Content.ReadAsStringAsync();
        Assert.Contains("TimeoutException", contentActual);
    }

    private WebApplicationFactory<Program> CreateHostWithFakes(Action<QueueFakeOptions>? optionsBuilder = null)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // var toRemove = services.Where(x => x.ServiceType == typeof(IReceiverClient) || x.ServiceType == typeof(ISenderClient)).ToArray();
                    // foreach (var rem in toRemove)
                    // {
                    //     services.Remove(rem);
                    // }
                    // var options = new QueueFakeOptions();
                    // optionsBuilder?.Invoke(options);
                    // var receiverFake = new ReceiverFake(options);
                    // services.AddSingleton<IReceiverClient>(receiverFake);
                    // services.AddSingleton<ISenderClient>(new SenderFake(options, receiverFake));
                });
            });
    }

    private WebApplicationFactory<SomeApiMock.IAssemblyMarker> CreateSomeApiMock()
    {
        return new WebApplicationFactory<SomeApiMock.IAssemblyMarker>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                });
            });
    }

    public class QueueFakeOptions
    {
        public bool SendShouldFail { get; set; }
        public string? RequestId { get; set; } = null;
        public bool RequestIdNull { get; set; }
        public string? ResponseText { get; set; } = Faketext;
    }

   
}
