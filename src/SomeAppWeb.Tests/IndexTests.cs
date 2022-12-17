using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SomeAppWeb.Tests;

public class IndexTests
{
    [Fact]
    public async Task Get_Succeeds()
    {
        var host = new WebApplicationFactory<SomeAppWeb.Program>();
        var client = host.CreateClient();
        var actual = await client.GetAsync("/");
        Assert.Equal(System.Net.HttpStatusCode.OK, actual.StatusCode);
    }
}
