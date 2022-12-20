using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SomeAppWeb.Tests;

public class SystemDependenciesFixture : IDisposable
{
    private const string AzuriteImage = "mcr.microsoft.com/azure-storage/azurite";
    private readonly TestcontainersContainer _azuriteContainer;

    public SystemDependenciesFixture()
    {
        _azuriteContainer = BuildAzuriteContainer();
        _azuriteContainer.StartAsync().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _azuriteContainer.StopAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    private TestcontainersContainer BuildAzuriteContainer()
    {
        return new TestcontainersBuilder<TestcontainersContainer>()
            .WithImage("mcr.microsoft.com/azure-storage/azurite")
            // .WithPortBinding(10000, 10000)
            .WithPortBinding(10001, 10001)
            // .WithPortBinding(10002, 10002)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(10001))
            .Build();
    }
}
