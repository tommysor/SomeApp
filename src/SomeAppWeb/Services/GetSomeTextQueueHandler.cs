using System.Text;
using System.Text.Json;
using Azure.Identity;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Caching.Memory;

namespace SomeAppWeb.Services
{
    public sealed class GetSomeTextQueueHandler : IGetSomeTextQueueHandler, IDisposable
    {
        private readonly ILogger<GetSomeTextQueueHandler> _logger;
        private readonly IMemoryCache _cache;
        private readonly QueueClient _senderClient;
        private readonly QueueClient _receiverClient;
        private readonly QueueClient _receiverDeadletterClient;
        private Task _receiverTask = Task.CompletedTask;
        private CancellationTokenSource _receiverCancellationTokenSource = new();

        public GetSomeTextQueueHandler(ILogger<GetSomeTextQueueHandler> logger, IMemoryCache cache)
        {
            _logger = logger;
            _cache = cache;

            var options = new QueueClientOptions();
            options.MessageEncoding = QueueMessageEncoding.Base64;
            options.Retry.Delay = TimeSpan.FromSeconds(1);
            options.Retry.MaxDelay = TimeSpan.FromSeconds(4);
            options.Retry.MaxRetries = 3;
            options.Retry.Mode = Azure.Core.RetryMode.Exponential;
            options.Retry.NetworkTimeout = TimeSpan.FromSeconds(2);

            _senderClient = new QueueClient(
                "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;",
                "someappsend");
            _senderClient.CreateIfNotExists();

            _receiverClient = new QueueClient(
                "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;",
                "someappreceive");
            _receiverClient.CreateIfNotExists();

            _receiverDeadletterClient = new QueueClient(
                "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;",
                "someappreceivedeadletter");
            _receiverDeadletterClient.CreateIfNotExists();
        }

        public void Dispose()
        {
            _receiverCancellationTokenSource?.Cancel();
            GC.SuppressFinalize(this);
        }

        private void StartReceiveTaskIfNotRunning()
        {
            if (_receiverTask.IsCompleted)
            {
                _receiverCancellationTokenSource = new();
                _receiverTask = ReceiveMessagesAsync(_receiverCancellationTokenSource.Token);
            }
        }

        private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var message = await _receiverClient.ReceiveMessageAsync(TimeSpan.FromSeconds(2));
                    if (message == null)
                    {
                        _logger.LogInformation("No messages in queue");
                        await Task.Delay(500, cancellationToken);
                        continue;
                    }

                    if (message.Value.DequeueCount > 3)
                    {
                        // log warning and move to _receiverDeadletterClient
                        _logger.LogWarning("Message id: {MessageId}. This is the {DequeueCount} this message is seen. Moving to deadletter queue",
                            message.Value.MessageId, message.Value.DequeueCount);
                        await _receiverDeadletterClient.SendMessageAsync(message.Value.Body);
                        await _receiverClient.DeleteMessageAsync(message.Value.MessageId, message.Value.PopReceipt);
                        continue;
                    }

                    await MessageHandler(message.Value, cancellationToken);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error receiving messages from queue");
                }
            }
        }

        private async Task MessageHandler(QueueMessage message, CancellationToken cancellationToken)
        {
            var messageObject = DeserializeAndValidate(message);
            if (messageObject == null)
            {
                return;
            }

            if (_cache.TryGetValue<string>(messageObject.RequestId!, out var cacheEntry))
            {
                _logger.LogInformation("Received duplicate message from queue. {MessageId} {RequestId}", message.MessageId, messageObject.RequestId);
                return;
            }
            
            _cache.Set(messageObject.RequestId!, messageObject.Text, TimeSpan.FromSeconds(12));
            
            await _receiverClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);
        }

        private ResponseObject? DeserializeAndValidate(QueueMessage message)
        {
            var messageText = Encoding.UTF8.GetString(message.Body.ToArray());
            _logger.LogInformation("Received message from queue: {MessageText}", messageText);
            
            var messageObject = JsonSerializer.Deserialize<ResponseObject>(messageText);
            
            if (messageObject == null)
            {
                _logger.LogWarning("Received invalid message from queue. {MessageId}", message.MessageId);
                return null;
            }

            if (string.IsNullOrWhiteSpace(messageObject.RequestId))
            {
                _logger.LogWarning("Received invalid message from queue. RequestId is null or empty. {MessageId}", message.MessageId);
                return null;
            }

            if (string.IsNullOrWhiteSpace(messageObject.Text))
            {
                _logger.LogWarning("Received invalid message from queue. Text is null or empty. {MessageId} {RequestId}", message.MessageId, messageObject.RequestId);
                return null;
            }

            return messageObject;
        }        

        public async Task<string> GetTextAsync(string requestId)
        {
            string? text;
            if (_cache.TryGetValue<string>(requestId, out text))
            {
                return text!;
            }

            for (var i = 0; i < 100; i++)
            {
                await Task.Delay(100);

                if (_cache.TryGetValue<string>(requestId, out text))
                {
                    return text!;
                }
            }

            throw new TimeoutException($"Timeout waiting for response from queue for {requestId}");
        }

        public async Task<string> SendGetTextRequestAsync()
        {
            var requestId = Guid.NewGuid().ToString();

            var requestMessage = new 
            {
                RequestId = requestId
            };
            
            var requestMessageJson = JsonSerializer.Serialize(requestMessage);
            await _senderClient.SendMessageAsync(requestMessageJson);
            
            _logger.LogInformation("Sent message to queue: {requestId}", requestId);
            return requestId;
        }

        private class ResponseObject
        {
            public string? RequestId { get; set; }
            public string? Text { get; set; }
        }
    }
}
