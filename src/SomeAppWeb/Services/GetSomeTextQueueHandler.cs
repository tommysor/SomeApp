using System.Text;
using System.Text.Json;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;

namespace SomeAppWeb.Services
{
    public class GetSomeTextQueueHandler : IGetSomeTextQueueHandler
    {
        private readonly IReceiverClient _receiverClient;
        private readonly ISenderClient _senderClient;
        private readonly ILogger<GetSomeTextQueueHandler> _logger;
        private readonly Dictionary<string, string> _Responses = new();

        public GetSomeTextQueueHandler(IReceiverClient receiverClient, ISenderClient senderClient, ILogger<GetSomeTextQueueHandler> logger)
        {
            _logger = logger;
            _senderClient = senderClient;
            _receiverClient = receiverClient;
            _receiverClient.RegisterMessageHandler(
                MessageHandler,
                new MessageHandlerOptions(ExceptionReceivedHandler)
                {
                    MaxConcurrentCalls = 1,
                    AutoComplete = false,
                }
            );
        }

        private async Task MessageHandler(Message message, CancellationToken cancellationToken)
        {
            var requestId = message.UserProperties["RequestId"] as string;
            var text = Encoding.UTF8.GetString(message.Body);
            _logger.LogInformation("Received message from queue: {text}", text);
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Received empty message from queue");
                return;
            }

            var messageObject = JsonSerializer.Deserialize<ResponseObject>(text);
            if (messageObject == null)
            {
                _logger.LogWarning("Received invalid message from queue");
                return;
            }

            if (string.IsNullOrWhiteSpace(requestId))
            {
                _logger.LogWarning("Received invalid message from queue. RequestId is null or empty");
                return;
            }

            if (string.IsNullOrWhiteSpace(messageObject.Text))
            {
                _logger.LogWarning("Received invalid message from queue. Text is null or empty");
                return;
            }

            if (_Responses.ContainsKey(requestId))
            {
                _logger.LogInformation("Received duplicate message from queue. Id is {RequestId}", requestId);
                return;
            }

            _Responses.Add(requestId, messageObject.Text);
            await _receiverClient.CompleteAsync(message.SystemProperties.LockToken);
        }

        private Task ExceptionReceivedHandler(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
        {
            _logger.LogError(exceptionReceivedEventArgs.Exception, "Error receiving message from queue");
            return Task.CompletedTask;
        }

        public async Task<string> GetTextAsync(string requestId)
        {
            if (_Responses.TryGetValue(requestId, out var text))
            {
                _Responses.Remove(requestId);
                return text;
            }

            for(var i = 0; i < 10; i++)
            {
                await Task.Delay(100);

                if (_Responses.TryGetValue(requestId, out text))
                {
                    _Responses.Remove(requestId);
                    return text;
                }
            }

            throw new TimeoutException($"Timeout waiting for response from queue for {requestId}");
        }

        public async Task<string> SendGetTextRequestAsync()
        {
            var requestId = Guid.NewGuid().ToString();
            var message = new Message();
            message.UserProperties.Add("RequestId", requestId);
            await _senderClient.SendAsync(message);
            _logger.LogInformation("Sent message to queue: {requestId}", requestId);
            return requestId;
        }

        private class ResponseObject
        {
            public string? Text { get; set; }
        }
    }
}
