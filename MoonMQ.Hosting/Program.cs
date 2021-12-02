using Microsoft.Extensions.Options;
using MoonMQ.Hosting.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

builder.Services.Configure<MoonMQ.Core.Cluster>(
    builder.Configuration.GetSection(nameof(MoonMQ.Core.Cluster))
);
builder.Services.AddSingleton<MoonMQ.Core.MoonMQ>(sp =>
{
    IOptions<MoonMQ.Core.Cluster> options = sp.GetService<IOptions<MoonMQ.Core.Cluster>>();
    ILogger<MoonMQ.Core.MoonMQ> logger = sp.GetService<ILogger<MoonMQ.Core.MoonMQ>>();

    MoonMQ.Core.MoonMQ moonmq = new MoonMQ.Core.MoonMQ(options.Value, logger);
    moonmq.Start();

    return moonmq;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<MoonMQService>();
app.MapGrpcReflectionService();

app.MapGet("/", ctx =>
    {
        return Task.FromResult("MoonMQ");
    });

app.Run();