using InsuranceBrokerage.AI.Models;
using InsuranceBrokerage.AI.Services;
using System.Diagnostics;

// ── Auto-start companion services ──────────────────────────────────────────────
var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

static void StartCompanionService(string projectFolder, string label)
{
    var projectPath = Path.Combine(projectFolder);
    if (!Directory.Exists(projectPath))
    {
        Console.WriteLine($"[Launcher] ⚠️  {label} folder not found: {projectPath}");
        return;
    }

    var psi = new ProcessStartInfo("dotnet", "run")
    {
        WorkingDirectory = projectPath,
        UseShellExecute  = false,
        CreateNoWindow   = false,
    };

    var process = Process.Start(psi);
    if (process != null)
        Console.WriteLine($"[Launcher] ✅ {label} started (PID {process.Id}) → {projectPath}");
    else
        Console.WriteLine($"[Launcher] ❌ Failed to start {label}");
}

// Kill any leftover processes on these ports before starting
static void KillPort(int port)
{
    try
    {
        var result = Process.Start(new ProcessStartInfo("cmd", $"/c for /f \"tokens=5\" %a in ('netstat -ano ^| findstr :{port}') do taskkill /PID %a /F")
        {
            UseShellExecute = false, CreateNoWindow = true
        });
        result?.WaitForExit(2000);
    }
    catch { /* ignore */ }
}

Console.WriteLine("[Launcher] Stopping any existing services...");
KillPort(3001);
KillPort(5153);
Thread.Sleep(1500);

Console.WriteLine("[Launcher] Starting MCP Server (port 3001)...");
StartCompanionService(Path.Combine(solutionRoot, "InsuranceBrokerage.McpServer"), "MCP Server");

Thread.Sleep(4000); // Wait for MCP to be ready

Console.WriteLine("[Launcher] Starting REST API (port 5153)...");
StartCompanionService(Path.Combine(solutionRoot, "InsuranceBrokerage.Api"), "REST API");

Thread.Sleep(2000);
// ───────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

// Register CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register MCP Client & Orchestrator
builder.Services.AddHttpClient<IMcpClient, McpClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddScoped<IAiOrchestrator, InsuranceAiOrchestrator>();

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseCors("AllowAll");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();

// AI Orchestrator Chat Endpoint
app.MapPost("/api/chat", async (ChatRequest request, IAiOrchestrator orchestrator) =>
{
    if (string.IsNullOrWhiteSpace(request?.Message))
    {
        return Results.BadRequest(new { error = "Message is required." });
    }

    var response = await orchestrator.ProcessMessageAsync(request);
    return Results.Ok(response);
});

// List Discovered MCP Tools Endpoint
app.MapGet("/api/mcp/tools", async (IMcpClient mcpClient) =>
{
    var tools = await mcpClient.GetToolsAsync();
    return Results.Ok(new
    {
        count = tools.Count,
        tools = tools
    });
});

// Status Endpoint
app.MapGet("/api/status", () => Results.Ok(new
{
    service = "Emirates Insurance AI Orchestrator",
    status = "Online",
    mcpServerUrl = "http://localhost:3001/mcp",
    timestampUtc = DateTime.UtcNow
}));

app.Run();
