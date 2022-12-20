namespace SomeApiMock
{
    public sealed class MockRequest
    {
        public string? QueueName { get; set; }
        public string? ReturnThisText { get; set; }
        public string? ReturnThisRequestId { get; set; }
    }
}