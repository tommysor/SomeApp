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

    public IndexTests(SystemDependenciesFixture systemDependenciesFixture)
    {
        _systemDependenciesFixture = systemDependenciesFixture;

        var queuePrefix = Guid.NewGuid();
        _host = CreateHostWithFakes(queuePrefix);
        _sutClient = _host.CreateClient();
        _someApiMock = CreateSomeApiMock(queuePrefix);
        _someApiMockClient = _someApiMock.CreateClient();
    }

    private WebApplicationFactory<IAssemblyMarker> CreateHostWithFakes(Guid queuePrefix)
    {
        return new WebApplicationFactory<IAssemblyMarker>()
            .WithWebHostBuilder(builder =>
            {

                builder.ConfigureServices(services =>
                {
                });
            });
    }

    private WebApplicationFactory<SomeApiMock.IAssemblyMarker> CreateSomeApiMock(Guid queuePrefix)
    {
        return new WebApplicationFactory<SomeApiMock.IAssemblyMarker>()
            .WithWebHostBuilder(builder =>
            {
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

    // [Fact]
    // public async Task GetUpdateText_WhenSendFails_ShouldThrow()
    // {
    //     var host = CreateHostWithFakes(x => x.SendShouldFail = true);
    //     var client = host.CreateClient();
    //     var actual = await client.GetAsync(updateActionPath);
    //     Assert.False(actual.IsSuccessStatusCode);
    //     var contentActual = await actual.Content.ReadAsStringAsync();
    //     Assert.Contains("InvalidOperationException", contentActual);
    // }

    // [Theory]
    // [InlineData(null, true, Faketext)]
    // [InlineData("", false, Faketext)]
    // [InlineData(" ", false, Faketext)]
    // [InlineData("\t", false, Faketext)]
    // [InlineData(null, false, null)]
    // [InlineData(null, false, "")]
    // [InlineData(null, false, " ")]
    // [InlineData(null, false, "\t")]
    // public async Task GetUpdateText_WhenReceiveFails_ShouldThrow(string? id, bool idNull, string? text)
    // {
    //     var host = CreateHostWithFakes(x => 
    //     {
    //         x.RequestId = id;
    //         x.RequestIdNull = idNull;
    //         x.ResponseText = text;
    //     });
    //     var client = host.CreateClient();
    //     var actual = await client.GetAsync(updateActionPath);
    //     Assert.False(actual.IsSuccessStatusCode);
    //     var contentActual = await actual.Content.ReadAsStringAsync();
    //     Assert.Contains("TimeoutException", contentActual);
    // }
}
