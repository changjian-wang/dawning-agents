using Dawning.Agents.Api.Endpoints;
using Dawning.Agents.Core;
using Dawning.Agents.Core.LLM;
using Dawning.Agents.Core.Memory;
using Dawning.Agents.Core.Safety;
using Dawning.Agents.Core.Tools;
using Dawning.Agents.OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);

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
