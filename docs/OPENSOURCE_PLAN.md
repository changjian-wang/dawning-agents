# Dawning.Agents 开源最后一英里实施计划

> 创建：2026-03-01 | 状态：待执行 | 分支：feat/opensource-readiness

## 背景

经过深度审读 12 个源码包 / 95 个抽象接口 / ~2225 测试，框架在架构、安全、Memory、Tool 等维度已达企业级水平。
距离开源发布差"最后一英里"：社区治理文件、可运行 API 示例、测试并行化、OTEL 合规、README 更新。

## 执行顺序

```
WP1 社区治理文件 → WP2 API Sample 重建 → WP3 测试并行化 → WP4 OTEL gen_ai.* → WP5 README 更新 → 构建验证 → CSharpier → git commit
```

---

## WP1：社区治理文件（3 个新文件）

### CONTRIBUTING.md
- 开发环境：.NET 10 SDK + Ollama（可选）
- Fork & PR 流程
- Commit 规范：Conventional Commits（feat/fix/docs/refactor/test/chore）
- Scope 列表：core, abstractions, openai, azure, mcp, redis, qdrant, pinecone, chroma, weaviate, otel, serilog, samples, docs
- 代码风格：CSharpier (`dotnet tool run csharpier .`)、Meziantou Analyzer、TreatWarningsAsErrors
- 测试要求：新功能必须附带测试，运行 `dotnet test`
- Issue/PR 模板说明

### CODE_OF_CONDUCT.md
- Contributor Covenant v2.1（行业标准）
- 联系方式：changjian-wang (GitHub)

### SECURITY.md
- 安全漏洞报告：GitHub Security Advisories（私密报告）
- 支持版本：0.1.x（当前）
- 响应 SLA：确认 48h / 修复 7-30 天（按严重度）
- 不接受的报告类型：已公开的 CVE、非安全相关 bug

---

## WP2：API Sample 重建 + SSE Streaming

### 背景
- `samples/Dawning.Agents.Api/` 当前只有 bin/ obj/，无源码
- 不在 Dawning.Agents.sln 中
- 需要新建完整 ASP.NET Core Minimal API 项目

### 新建文件清单

**samples/Dawning.Agents.Api/Dawning.Agents.Api.csproj**
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <RootNamespace>Dawning.Agents.Api</RootNamespace>
    </PropertyGroup>
    <ItemGroup>
        <ProjectReference Include="..\..\src\Dawning.Agents.Core\Dawning.Agents.Core.csproj" />
        <ProjectReference Include="..\..\src\Dawning.Agents.OpenTelemetry\Dawning.Agents.OpenTelemetry.csproj" />
    </ItemGroup>
</Project>
```

**samples/Dawning.Agents.Api/Program.cs**
- Minimal API 风格
- 注册：AddLLMProvider + AddCoreTools + AddFunctionCallingAgent + AddMemory + AddSafetyGuardrails
- Map endpoints from ChatEndpoints + AgentEndpoints
- Swagger/OpenAPI

**samples/Dawning.Agents.Api/Endpoints/ChatEndpoints.cs**
- `POST /api/chat` — 同步对话（ChatMessage → ChatCompletionResponse）
- `POST /api/chat/stream` — SSE 流式响应
  - ContentType: text/event-stream
  - 使用 provider.ChatStreamAsync()
  - data: {json}\n\n 格式

**samples/Dawning.Agents.Api/Endpoints/AgentEndpoints.cs**
- `POST /api/agent/run` — Agent 执行（input → AgentResponse）
- `GET /api/agent/health` — 健康检查

**samples/Dawning.Agents.Api/appsettings.json**
```json
{
  "LLM": { "ProviderType": "Ollama", "Model": "qwen2.5:0.5b", "Endpoint": "http://localhost:11434" },
  "Agent": { "Name": "DawningAgent", "Instructions": "You are a helpful assistant.", "MaxSteps": 10 },
  "Memory": { "Type": "Adaptive", "DowngradeThreshold": 4000 }
}
```

### 操作
1. 删除 samples/Dawning.Agents.Api/bin/ 和 obj/
2. 创建上述 4 个文件
3. 使用 `dotnet sln add` 注册到解决方案的 samples 文件夹

---

## WP3：测试并行化

### 文件：tests/Dawning.Agents.Tests/xunit.runner.json

当前：
```json
{ "parallelizeTestCollections": false, "maxParallelThreads": 1, "diagnosticMessages": true }
```

改为：
```json
{ "parallelizeTestCollections": true, "maxParallelThreads": 0, "diagnosticMessages": true }
```

- `maxParallelThreads: 0` = CPU 核心自动检测
- 所有测试都是 Mock-based 纯单元测试，无共享状态
- 无需 Trait 标注（现阶段没有真正的外部集成测试）

---

## WP4：OpenTelemetry gen_ai.* 语义约定迁移

### 文件：src/Dawning.Agents.Core/Observability/AgentInstrumentation.cs

#### Span 属性名映射

| 当前 | → | gen_ai.* |
|------|---|----------|
| `agent.name` | → | `gen_ai.agent.name` |
| `agent.input.length` | → | `gen_ai.request.input.length` |
| `tool.name` | → | `gen_ai.tool.name` |
| `llm.provider` | → | `gen_ai.system` |
| `llm.model` | → | `gen_ai.request.model` |

#### Span 操作名映射

| 当前 | → | gen_ai.* |
|------|---|----------|
| `agent.request` | → | `gen_ai.agent.run` |
| `agent.tool.execute` | → | `gen_ai.tool.execute` |
| `llm.call` | → | `gen_ai.chat` |

#### Metric 名映射

| 当前 | → | gen_ai.* |
|------|---|----------|
| `agent_requests_total` | → | `gen_ai.agent.invocations` |
| `agent_requests_success_total` | → | `gen_ai.agent.invocations` (tag: status=success) |
| `agent_requests_failed_total` | → | `gen_ai.agent.invocations` (tag: status=error) |
| `agent_tool_executions_total` | → | `gen_ai.tool.invocations` |
| `llm_calls_total` | → | `gen_ai.client.operation.duration` (Histogram) |
| `llm_tokens_used_total` | → | `gen_ai.client.token.usage` |
| `agent_request_duration_seconds` | → | `gen_ai.agent.duration` |
| `agent_tool_execution_duration_seconds` | → | `gen_ai.tool.duration` |
| `llm_call_duration_seconds` | → | `gen_ai.client.operation.duration` |

#### 注意
- OTEL GenAI Semantic Conventions 仍为 experimental (v0.29.0+)，代码里加注释标注版本
- 在 StartLLMCall 中增加 `gen_ai.request.max_tokens`, `gen_ai.request.temperature` 参数
- 保持类/方法签名不变，只改内部 tag 名和 metric 名

#### 同步更新测试：tests/Dawning.Agents.Tests/Observability/AgentInstrumentationTests.cs
- 更新所有 Should().Be("xxx") 断言

---

## WP5：README.md 全面更新

### 过时内容清单（README.md 727 行）

| 区域 | 行号范围 | 改什么 |
|------|---------|--------|
| 特性-工具 | ~63 | `64 个内置工具` → `6 核心工具 + 动态工具创建` |
| 项目结构-Tools | ~156-175 | 移除 BuiltIn/ ToolSet.cs VirtualTool.cs，改为 Core/ 6 工具 |
| 依赖关系图 | ~268-285 | 移除"⚠️ 已知问题"注释（Core→OpenAI/Azure 反向引用已修复） |
| 内置工具表 | ~443-459 | 旧 8 类 64 方法 → 6 核心工具表（read/write/edit/search/bash/create_tool） |
| 注册方式 | ~461-467 | `AddAllBuiltInTools()` → `AddCoreTools()` |
| Demo 运行 | ~533-535 | `samples/Dawning.Agents.Demo` → 更新路径 |
| 贡献 | ~713 | 链接到新的 CONTRIBUTING.md |
| Badge | ~4-7 | 增加 Codecov coverage badge |
| NuGet 安装 | ~90-100 | 补充 Chroma/Weaviate/OpenTelemetry/Serilog/MCP 包 |
| 项目结构-新包 | ~244-265 | 补充 OpenTelemetry、Serilog 包 |
| Roadmap v0.1.0 | ~680-690 | 标记 FunctionCalling ✅，Tools Redesign ✅ |
| Roadmap v0.2.0 | ~692-700 | 更新为真实规划（A2A、Prompt 管理等） |
| API Sample | 新增 | 在文档部分指向新的 Api sample |

---

## 明确不做（排除项）

| 不做 | 原因 |
|------|------|
| A2A 协议 | 4+ 周工作量，v0.2.0 范畴 |
| Prompt 版本管理 | 功能设计未定义，v0.2.0 |
| Distributed InMemory Fallback | Redis 实现足够 |
| Model Router 埋点 | OTel 迁移后追加，不阻塞 |
| Workflow 可视化 | 前端工程量大，v0.3.0 |
| Trait 测试分层 | 当前无真正集成测试，不需要 |

---

## 验证清单

- [ ] `dotnet build --configuration Release` 0 errors 0 warnings
- [ ] `dotnet test` 所有测试通过（应 ≥ 2225）
- [ ] `dotnet tool run csharpier .` 格式化通过
- [ ] samples/Dawning.Agents.Api 可 `dotnet run` 启动
- [ ] README 中无过时引用（64 工具、BuiltIn/、AddAllBuiltInTools）
- [ ] CONTRIBUTING.md / CODE_OF_CONDUCT.md / SECURITY.md 存在且内容完整

## Git Commit

```
feat(docs): add community governance files and API sample

- Add CONTRIBUTING.md, CODE_OF_CONDUCT.md, SECURITY.md
- Rebuild samples/Dawning.Agents.Api with SSE streaming endpoints
- Enable test parallelization (xunit.runner.json)
- Migrate OpenTelemetry to gen_ai.* semantic conventions
- Update README.md to reflect current architecture (6 core tools)
```
