namespace SomeAppWeb.Services
{
    public interface IGetSomeTextQueueHandler
    {
        Task<string> GetTextAsync(string id);
        Task<string> SendGetTextRequestAsync();
    }
}
