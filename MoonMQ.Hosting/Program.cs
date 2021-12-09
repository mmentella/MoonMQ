using MoonMQ.Hosting.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();
builder.Services.AddHealthChecks();

//Add MoonMQ Services
builder.Services.AddMoonMQ(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.MapHealthChecks("/health");
});

app.MapGrpcService<MoonMQService>();
app.MapGrpcReflectionService();

app.MapGet("/", () => "MoonMQ");

// Start MoonMQ
app.StartMoonMQ();

app.Run();