using NexusBackend.Hubs;
using NexusBackend.Services;

var builder = WebApplication.CreateBuilder(args);

LoadDotEnv();

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddHttpClient();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "http://localhost:5173";
        policy.WithOrigins(frontendUrl).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    });
});

builder.Services.AddSingleton<ObsidianService>();
builder.Services.AddSingleton<LogService>();
builder.Services.AddSingleton<MemoryService>();
builder.Services.AddSingleton<TaskService>();
builder.Services.AddSingleton<OpenAIService>();
builder.Services.AddSingleton<OllamaService>();
builder.Services.AddSingleton<ElevenLabsService>();
builder.Services.AddSingleton<ComputerControlService>();
builder.Services.AddSingleton<DocumentIndexService>();
builder.Services.AddSingleton<OperationService>();

var app = builder.Build();

app.UseCors("Frontend");
app.MapControllers();
app.MapHub<NexusHub>("/hubs/nexus");

app.MapGet("/", () => new
{
    name = "N.E.X.U.S Backend",
    status = "online",
    endpoints = new[]
    {
        "/api/nexus/chat",
        "/api/nexus/status",
        "/api/memory",
        "/api/tasks",
        "/api/logs",
        "/hubs/nexus"
    }
});

app.Run();

static void LoadDotEnv()
{
    var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
    if (!File.Exists(envPath)) return;

    foreach (var line in File.ReadAllLines(envPath))
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#")) continue;

        var parts = trimmed.Split('=', 2);
        if (parts.Length != 2) continue;

        Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
    }
}
