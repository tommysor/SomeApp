using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SomeAppWeb.Services;

namespace SomeAppWeb.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IGetSomeTextQueueHandler _getSomeTextQueueHandler;

    public IndexModel(ILogger<IndexModel> logger, IGetSomeTextQueueHandler getSomeTextQueueHandler)
    {
        _logger = logger;
        _getSomeTextQueueHandler = getSomeTextQueueHandler;
    }

    public void OnGet()
    {

    }

    public async Task<IActionResult> OnGetUpdateText()
    {
        var id = await _getSomeTextQueueHandler.SendGetTextRequestAsync();
        var text = await _getSomeTextQueueHandler.GetTextAsync(id);
        return new OkObjectResult(text);
    }
}
