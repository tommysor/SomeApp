using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SomeApiMock;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHostedService<QueueHandlerService>();
builder.Services.AddMemoryCache();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.MapPost("/mock", ([FromBody]MockRequest body, [FromServices]IMemoryCache cache) =>
{
    var isAnySet = false;

    if (body.ReturnThisRequestId != null)
    {
        var key = $"{body.QueueName}-RequestId";
        cache.Set(key, body.ReturnThisRequestId);
        isAnySet = true;
    }

    if (body.ReturnThisText != null)
    {
        var key = $"{body.QueueName}-Text";
        cache.Set(key, body.ReturnThisText);
        isAnySet = true;
    }
    
    if (isAnySet)
    {
        return Results.NoContent();
    }
    else
    {
        return Results.BadRequest();
    }
});

app.Run();
