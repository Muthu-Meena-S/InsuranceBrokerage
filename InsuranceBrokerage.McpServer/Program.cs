using InsuranceBrokerage.Application.Interfaces;
using InsuranceBrokerage.Application.Services;
using InsuranceBrokerage.Infrastructure.Data;
using InsuranceBrokerage.Infrastructure.Repositories;
using InsuranceBrokerage.McpServer.Tools;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

// Database connection
builder.Services.AddDbContext<BrokerageDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Services
builder.Services.AddScoped<ILeadRepository, LeadRepository>();
builder.Services.AddScoped<ILeadService, LeadService>();
builder.Services.AddScoped<InsuranceTools>();

builder.Services
    .AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.SessionMode = HttpServerSessionMode.Stateless;
    })
    .WithTools<InsuranceTools>();

var app = builder.Build();

app.MapMcp("/mcp");

app.Run("http://localhost:3001");
