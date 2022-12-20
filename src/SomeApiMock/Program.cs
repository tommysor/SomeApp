using SomeApiMock;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHostedService<QueueHandlerService>();
builder.Services.AddMemoryCache();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
