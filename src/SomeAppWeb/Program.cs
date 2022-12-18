using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;

namespace SomeAppWeb;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorPages();

        builder.Services.AddSingleton<Services.IGetSomeTextQueueHandler, Services.GetSomeTextQueueHandler>();

        builder.Services.AddSingleton<IReceiverClient, QueueClient>(services =>
        {
            var configuration = services.GetRequiredService<IConfiguration>();
            var connectionString = configuration["Queues:ReceiveReplyQueue:ConnectionString"];
            var queueName = configuration["Queues:ReceiveReplyQueue:Name"];

            var client = new QueueClient(connectionString, queueName, ReceiveMode.PeekLock, RetryPolicy.Default);
            return client;
        });

        builder.Services.AddSingleton<ISenderClient, QueueClient>(services =>
        {
            var configuration = services.GetRequiredService<IConfiguration>();
            var connectionString = configuration["Queues:SendRequestQueue:ConnectionString"];
            var queueName = configuration["Queues:SendRequestQueue:Name"];

            var client = new QueueClient(connectionString, queueName, ReceiveMode.PeekLock, RetryPolicy.Default);
            return client;
        });

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            //app.UseHsts();
        }

        //app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();

        //app.UseAuthorization();

        app.MapRazorPages();

        app.Run();

    }
}
