using Dawning.Agents.Api.Endpoints;
using Dawning.Agents.Core;
using Dawning.Agents.Core.LLM;
using Dawning.Agents.Core.Memory;
using Dawning.Agents.Core.Safety;
using Dawning.Agents.Core.Tools;
using Dawning.Agents.OpenTelemetry;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

// 加载 YAML 配置（优先级高于默认 JSON）
builder.Configuration.AddYamlFile("appsettings.yml", optional: true, reloadOnChange: true);
builder.Configuration.AddYamlFile(
    $"appsettings.{builder.Environment.EnvironmentName}.yml",
    optional: true,
    reloadOnChange: true
);

builder.Services.AddLLMProvider(builder.Configuration);
builder.Services.AddCoreTools();
builder.Services.AddFunctionCallingAgent(builder.Configuration);
builder.Services.AddMemory(builder.Configuration);
builder.Services.AddSafetyGuardrails(builder.Configuration);
builder.Services.AddOpenTelemetryObservability(builder.Configuration);

var app = builder.Build();

app.MapChatEndpoints();
app.MapAgentEndpoints();

app.Run();
