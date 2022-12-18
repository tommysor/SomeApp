using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;

namespace SomeAppWeb.Tests;

public class IndexTests
{
    private const string Faketext = "Faketext";
    private const string updateActionPath = "/index?handler=UpdateText";

    [Fact]
    public async Task Get_Succeeds()
    {
        var host = CreateHostWithFakes();
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
                    var toRemove = services.Where(x => x.ServiceType == typeof(IReceiverClient) || x.ServiceType == typeof(ISenderClient)).ToArray();
                    foreach (var rem in toRemove)
                    {
                        services.Remove(rem);
                    }
                    var options = new QueueFakeOptions();
                    optionsBuilder?.Invoke(options);
                    var receiverFake = new ReceiverFake(options);
                    services.AddSingleton<IReceiverClient>(receiverFake);
                    services.AddSingleton<ISenderClient>(new SenderFake(options, receiverFake));
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

    public class SenderFake : ISenderClient
    {
        private readonly QueueFakeOptions _options;
        private readonly ReceiverFake _receiverFake;

        public SenderFake(QueueFakeOptions options, ReceiverFake receiverFake)
        {
            _options = options;
            _receiverFake = receiverFake;
        }

        public string ClientId => "1";

        public bool IsClosedOrClosing => false;

        public string Path => "path";

        public TimeSpan OperationTimeout { get => TimeSpan.FromSeconds(2); set { return; } }

        public ServiceBusConnection ServiceBusConnection => default!;

        public bool OwnsConnection => true;

        public IList<ServiceBusPlugin> RegisteredPlugins => Array.Empty<ServiceBusPlugin>();

        public Task CancelScheduledMessageAsync(long sequenceNumber)
        {
            throw new NotImplementedException();
        }

        public Task CloseAsync()
        {
            throw new NotImplementedException();
        }

        public void RegisterPlugin(ServiceBusPlugin serviceBusPlugin)
        {
            throw new NotImplementedException();
        }

        public Task<long> ScheduleMessageAsync(Message message, DateTimeOffset scheduleEnqueueTimeUtc)
        {
            throw new NotImplementedException();
        }

        public Task SendAsync(Message message)
        {
            if (_options.SendShouldFail)
            {
                throw new InvalidOperationException("Send failed");
            }

            var id = _options.RequestIdNull
                ? null
                : _options.RequestId ?? message.UserProperties["RequestId"] as string;

            _receiverFake.StartHandler(id, _options.ResponseText);
            return Task.CompletedTask;
        }

        public Task SendAsync(IList<Message> messageList)
        {
            throw new NotImplementedException();
        }

        public void UnregisterPlugin(string serviceBusPluginName)
        {
            throw new NotImplementedException();
        }
    }

    public class ReceiverFake : IReceiverClient
    {
        private Func<Message, CancellationToken, Task>? _handler;
        private readonly QueueFakeOptions _options;

        public ReceiverFake(QueueFakeOptions options)
        {
            _options = options;
        }

        public void StartHandler(string? id, string? text)
        {
            var obj = new { Text = text };
            var json = JsonSerializer.Serialize(obj);
            var message = new Message(Encoding.UTF8.GetBytes(json));
            message.UserProperties["RequestId"] = id;
            _handler(message, CancellationToken.None);
        }

        public int PrefetchCount { get => 1; set { return; } }

        public ReceiveMode ReceiveMode => ReceiveMode.PeekLock;

        public string ClientId => "1";

        public bool IsClosedOrClosing => false;

        public string Path => "path";

        public TimeSpan OperationTimeout { get => TimeSpan.FromSeconds(2); set { return; } }

        public ServiceBusConnection ServiceBusConnection => default;

        public bool OwnsConnection => true;

        public IList<ServiceBusPlugin> RegisteredPlugins => Array.Empty<ServiceBusPlugin>();

        public Task AbandonAsync(string lockToken, IDictionary<string, object> propertiesToModify = null)
        {
            throw new NotImplementedException();
        }

        public Task CloseAsync()
        {
            throw new NotImplementedException();
        }

        public Task CompleteAsync(string lockToken)
        {
            throw new NotImplementedException();
        }

        public Task DeadLetterAsync(string lockToken, IDictionary<string, object> propertiesToModify = null)
        {
            throw new NotImplementedException();
        }

        public Task DeadLetterAsync(string lockToken, string deadLetterReason, string deadLetterErrorDescription = null)
        {
            throw new NotImplementedException();
        }

        public void RegisterMessageHandler(Func<Message, CancellationToken, Task> handler, Func<ExceptionReceivedEventArgs, Task> exceptionReceivedHandler)
        {
            _handler = handler;
        }

        public void RegisterMessageHandler(Func<Message, CancellationToken, Task> handler, MessageHandlerOptions messageHandlerOptions)
        {
            _handler = handler;
        }

        public void RegisterPlugin(ServiceBusPlugin serviceBusPlugin)
        {
            throw new NotImplementedException();
        }

        public Task UnregisterMessageHandlerAsync(TimeSpan inflightMessageHandlerTasksWaitTimeout)
        {
            throw new NotImplementedException();
        }

        public void UnregisterPlugin(string serviceBusPluginName)
        {
            throw new NotImplementedException();
        }
    }
}
