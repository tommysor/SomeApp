using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Caching.Memory;

namespace SomeApiMock;

public sealed class QueueHandlerService : IHostedService
{
    private readonly QueueClient _replyClient;
    private readonly QueueClient _requestClient;
    private readonly ILogger<QueueHandlerService> _logger;
    private readonly IMemoryCache _cache;
    private CancellationTokenSource _cancellationTokenSource = new();
    private Task _task = Task.FromException(new Exception("Task not started yet"));

    public QueueHandlerService(ILogger<QueueHandlerService> logger, IMemoryCache cache, IConfiguration configuration)
    {
        _logger = logger;
        _cache = cache;

        var options = new QueueClientOptions();
        options.MessageEncoding = QueueMessageEncoding.Base64;
        options.Retry.Delay = TimeSpan.FromSeconds(.5);
        options.Retry.MaxRetries = 3;
        options.Retry.Mode = Azure.Core.RetryMode.Fixed;
        options.Retry.NetworkTimeout = TimeSpan.FromSeconds(2);

        _requestClient = new QueueClient(
            "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;",
            configuration["AzureStorageQueues:Request:Name"]);
        _requestClient.CreateIfNotExists();

        _replyClient = new QueueClient(
            "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;",
            configuration["AzureStorageQueues:Reply:Name"]);
        _replyClient.CreateIfNotExists();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        StartHandlingMessages();
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource.Cancel();
        await _task;
    }

    private void StartHandlingMessages()
    {
        Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            try
            {
                var message = await _requestClient.ReceiveMessageAsync();
                if (message?.Value == null)
                {
                    await Task.Delay(10, _cancellationTokenSource.Token);
                    continue;
                }

                await HandleMessageAsync(message.Value);
                await _requestClient.DeleteMessageAsync(message.Value.MessageId, message.Value.PopReceipt);
         
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QueueHandlerService failed to handle a message.");
            }
        }
    }

    private async Task HandleMessageAsync(QueueMessage message)
    {
        var text = Encoding.UTF8.GetString(message.Body.ToArray());
        var obj = JsonSerializer.Deserialize<RequestObject>(text);
        var requestId = obj!.RequestId;

        var requestIdCacheKey = $"{_requestClient.Name}-RequestId";
        if (_cache.TryGetValue(requestIdCacheKey, out string? cachedRequestId))
        {
            requestId = cachedRequestId;
        }

        var replyText = "Faketext";

        var textCacheKey = $"{_requestClient.Name}-Text";
        if (_cache.TryGetValue(textCacheKey, out string? cachedText))
        {
            replyText = cachedText;
        }

        var responseObj = new 
        {
            RequestId = requestId,
            Text = replyText
        };
        
        var responseText = JsonSerializer.Serialize(responseObj);
        await _replyClient.SendMessageAsync(responseText);
    }

    private sealed class RequestObject
    {
        public string? RequestId { get; set; }
    }
}
