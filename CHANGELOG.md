# Changelog

本文档记录 dawning-agents 项目的所有重要变更，便于在不同会话中快速恢复上下文。

---

## 🖥️ 快速恢复指南（另一台电脑）

### 环境准备

```bash
# 1. 拉取最新代码
git pull

# 2. 确保 Ollama 运行并有模型
ollama serve  # 如果未运行
ollama pull qwen2.5:0.5b

# 3. 运行测试确认环境正常
cd dawning-agents
dotnet test

# 4. 运行 Demo 验证
cd samples/Dawning.Agents.Demo
dotnet run
```

### 当前配置

```json
// samples/Dawning.Agents.Demo/appsettings.json
{
  "LLM": {
    "ProviderType": "Ollama",
    "Model": "qwen2.5:0.5b",  // 快速推理，ReAct 格式兼容好
    "Endpoint": "http://localhost:11434"
  }
}
```

### 模型选择说明

| 模型 | 大小 | 速度 | ReAct 兼容 | 用途 |
|------|------|------|-----------|------|
| qwen2.5:0.5b | 397MB | ~13秒 | ✅ 好 | Agent 推理（当前使用） |
| qwen2.5:7b | 4.7GB | ~165秒 | ✅ 好 | 复杂推理（质量更高） |
| deepseek-coder | 4GB | ~15秒 | ❌ 差 | 代码生成（Week 5 工具） |

### 当前进度

- ✅ Week 2: LLM Provider 完成
- ✅ Week 3: Agent 核心循环完成（63 测试通过）
- ✅ Week 4: Memory 系统完成（150 测试通过）
- ✅ Week 5: Tools/Skills 系统完成（74 测试通过）
- ✅ Week 5.5: Tool Sets 与 Virtual Tools 完成（106 测试通过）
- ✅ Week 7: 多 Agent 协作完成（736 测试通过）
- ✅ Week 8: Agent 通信机制完成（781 测试通过）
- ✅ Week 9: 安全护栏完成（781 测试通过）
- ✅ Week 10: 人机协作完成（781 测试通过）
- ✅ Week 11: 可观测性完成（781 测试通过）
- ✅ Week 12: 部署与扩展完成（781 测试通过）
- ✅ Demo: Week 8-12 演示更新完成
- ✅ 企业级转型: 代码优化 + 测试覆盖率提升至 72.9%（1183 测试通过）
- ✅ Week 21: Polly V8 弹性策略 + FluentValidation 验证（1385 测试通过）
- ✅ Week 23: Serilog 结构化日志（1437 测试通过）
- ✅ Week 23: 配置热重载 IOptionsMonitor（1470 测试通过）
- ✅ Week 24: 统一 Provider 工厂模式（1470 测试通过）
- ✅ Week 25: 真实 Embedding Provider（1517 测试通过）
- ✅ Week 26: Qdrant 向量存储（1547 测试通过）
- ✅ Week 27: Pinecone 向量存储（1581 测试通过）
- ✅ CSharpier Tool: 代码格式化工具（1594 测试通过）
- ✅ P3: Chroma 向量存储（1608 测试通过）
- ✅ P4: Weaviate 向量存储（1630 测试通过）

### 🎉 12 周学习计划全部完成

恭喜！您已完成完整的 Dawning.Agents 学习计划，拥有一个企业级 AI Agent 框架！

---

## 📋 后续任务规划（优先级排序）

| 优先级 | 任务 | 描述 | 状态 |
|--------|------|------|------|
| P0 | **NuGet 发布** | 准备和发布 NuGet 包到 nuget.org | ✅ |
| P1 | **文档站点** | DocFX 生成 API 文档站点 | ✅ |
| P2 | **性能基准测试** | BenchmarkDotNet 性能测试套件 | ✅ |
| P3 | **Chroma 向量存储** | 轻量级本地向量数据库，适合开发测试 | ✅ |
| P4 | **Weaviate 向量存储** | 第三个云端向量数据库 | ✅ |
| P5 | **MCP 协议** | 支持 Anthropic Model Context Protocol | ⏳ |
| P6 | **Semantic Kernel 集成** | 与微软 Semantic Kernel 互操作 | ⏳ |
| P7 | **LangChain 兼容层** | 与 LangChain 工具链集成 | ⏳ |

### 优先级说明

- **P0 NuGet 发布**：让框架可被外部项目使用，是框架价值实现的关键
- **P1 文档站点**：API 文档对用户采用至关重要
- **P2 性能基准**：企业级框架需要性能数据支撑
- **P3-P4 向量存储**：完善 RAG 生态系统
- **P5-P7 集成**：与主流 AI 框架互操作

---

## [2026-01-28] P4: Weaviate 向量存储

### 功能概述

Weaviate 是一个开源的向量搜索引擎，支持 GraphQL 和 REST API，提供多种向量索引类型和混合搜索能力。

### 核心组件

| 组件 | 描述 |
|------|------|
| `WeaviateOptions` | Weaviate 连接配置（Host, Port, ClassName 等） |
| `WeaviateVectorStore` | IVectorStore 实现，支持 GraphQL 搜索 |
| `WeaviateServiceCollectionExtensions` | DI 注册扩展 |

### 配置选项

```json
{
  "Weaviate": {
    "Host": "localhost",
    "Port": 8080,
    "GrpcPort": 50051,
    "ClassName": "Document",
    "Scheme": "http",
    "ApiKey": null,
    "TimeoutSeconds": 30,
    "VectorDimension": 1536,
    "DistanceMetric": "Cosine",
    "VectorIndexType": "Hnsw"
  }
}
```

### 使用方式

```csharp
// 通过配置注册
services.AddWeaviateVectorStore(configuration);

// 通过委托配置
services.AddWeaviateVectorStore(options =>
{
    options.Host = "localhost";
    options.Port = 8080;
    options.ClassName = "MyDocuments";
});
```

### 运行 Weaviate

```bash
# Docker 运行 Weaviate
docker run -p 8080:8080 -p 50051:50051 semitechnologies/weaviate:latest

# Docker Compose（推荐）
docker compose up -d
```

### 特性支持

- **GraphQL API** - 灵活的查询语言
- **多种索引类型** - HNSW (默认), Flat, Dynamic
- **多种距离度量** - Cosine, Dot, L2, Hamming, Manhattan
- **批量操作** - 高效的批量导入和删除

### 新增文件

- `src/Dawning.Agents.Weaviate/WeaviateOptions.cs` - 配置选项
- `src/Dawning.Agents.Weaviate/WeaviateVectorStore.cs` - IVectorStore 实现
- `src/Dawning.Agents.Weaviate/WeaviateServiceCollectionExtensions.cs` - DI 扩展
- `tests/Dawning.Agents.Tests/Weaviate/WeaviateVectorStoreTests.cs` - 单元测试（22 测试）

### 测试统计

- 新增测试：22
- 总测试数：1630
- 全部通过 ✅

---

## [2026-01-28] P3: Chroma 向量存储

### 功能概述

Chroma 是一个轻量级、开源的嵌入式向量数据库，非常适合本地开发和测试。

### 核心组件

| 组件 | 描述 |
|------|------|
| `ChromaOptions` | Chroma 连接配置（Host, Port, Collection 等） |
| `ChromaVectorStore` | IVectorStore 实现，支持 CRUD + 相似度搜索 |
| `ChromaServiceCollectionExtensions` | DI 注册扩展 |

### 配置选项

```json
{
  "Chroma": {
    "Host": "localhost",
    "Port": 8000,
    "CollectionName": "documents",
    "Tenant": "default_tenant",
    "Database": "default_database",
    "UseHttps": false,
    "ApiKey": null,
    "TimeoutSeconds": 30,
    "VectorDimension": 1536,
    "DistanceMetric": "Cosine"
  }
}
```

### 使用方式

```csharp
// 通过配置注册
services.AddChromaVectorStore(configuration);

// 通过委托配置
services.AddChromaVectorStore(options =>
{
    options.Host = "localhost";
    options.Port = 8000;
    options.CollectionName = "my-docs";
});
```

### 运行 Chroma

```bash
# Docker 运行 Chroma
docker run -p 8000:8000 chromadb/chroma

# 或使用 pip
pip install chromadb
chroma run
```

### 新增文件

- `src/Dawning.Agents.Chroma/ChromaOptions.cs` - 配置选项
- `src/Dawning.Agents.Chroma/ChromaVectorStore.cs` - IVectorStore 实现
- `src/Dawning.Agents.Chroma/ChromaServiceCollectionExtensions.cs` - DI 扩展
- `tests/Dawning.Agents.Tests/Chroma/ChromaVectorStoreTests.cs` - 单元测试（14 测试）

### 测试统计

- 新增测试：14
- 总测试数：1608
- 全部通过 ✅

---

## [2026-01-28] P2: 性能基准测试 (BenchmarkDotNet)

### 功能概述

使用 BenchmarkDotNet 创建性能基准测试套件，测量核心组件性能。

### Benchmark 列表

| Benchmark | 测试项 | 数量 |
|-----------|--------|------|
| `TokenCounterBenchmarks` | Token 计数性能 | 5 |
| `MemoryBenchmarks` | 对话记忆操作 | 4 |
| `ToolRegistryBenchmarks` | 工具注册表查找 | 5 |
| `JsonSerializationBenchmarks` | JSON 序列化 | 4 |
| **总计** | | **18** |

### 运行方式

```powershell
# 运行所有 benchmarks
./scripts/benchmark.ps1

# 运行特定 benchmark
./scripts/benchmark.ps1 -Filter "*Memory*"

# 长时间运行（更精确）
./scripts/benchmark.ps1 -Job Long
```

### 新增文件

- `benchmarks/Dawning.Agents.Benchmarks/` - Benchmark 项目
- `scripts/benchmark.ps1` - 运行脚本
- `benchmarks/README.md` - 使用说明

---

## [2026-01-28] P1: 文档站点 (DocFX)

### 功能概述

使用 DocFX 生成完整的 API 文档站点，支持 GitHub Pages 自动部署。

### 文档结构

```
docs/
├── docfx.json          # DocFX 配置
├── index.md            # 首页
├── toc.yml             # 顶级导航
├── articles/           # 教程文章
│   ├── getting-started.md
│   ├── llm-providers.md
│   ├── tools.md
│   ├── memory.md
│   ├── rag.md
│   └── multi-agent.md
├── api/                # API 文档（自动生成）
└── images/             # 图片资源
```

### 文章列表

| 文章 | 描述 |
|------|------|
| Getting Started | 安装和快速入门 |
| LLM Providers | Ollama, OpenAI, Azure 配置 |
| Tools & Skills | 64+ 内置工具，自定义工具 |
| Memory | 缓冲、滑动窗口、摘要记忆 |
| RAG | Qdrant, Pinecone 向量存储 |
| Multi-Agent | 多 Agent 协作模式 |

### 部署方式

**自动部署（推荐）**

推送到 main 分支后，GitHub Actions 自动构建并部署到 GitHub Pages。

**本地预览**

```bash
cd docs
docfx docfx.json --serve
# 访问 http://localhost:8080
```

### 访问地址

https://changjian-wang.github.io/dawning-agents/

---

## [2026-01-28] P0: NuGet 发布准备

### 功能概述

完成 NuGet 包发布的所有准备工作，包括打包配置、CI/CD 工作流和本地打包脚本。

### 发布包列表

| 包名 | 大小 | 描述 |
|------|------|------|
| `Dawning.Agents.Abstractions` | 99 KB | 核心接口和模型 |
| `Dawning.Agents.Core` | 227 KB | 核心实现（Ollama, Memory, Tools, RAG） |
| `Dawning.Agents.OpenAI` | 16 KB | OpenAI Provider |
| `Dawning.Agents.Azure` | 16 KB | Azure OpenAI Provider |
| `Dawning.Agents.Redis` | 31 KB | Redis 分布式组件 |
| `Dawning.Agents.Qdrant` | 19 KB | Qdrant 向量存储 |
| `Dawning.Agents.Pinecone` | 22 KB | Pinecone 向量存储 |

### 新增文件

- `.github/workflows/publish-nuget.yml` - GitHub Actions 自动发布工作流
- `scripts/pack.ps1` - 本地打包脚本

### 版本号

当前版本：`0.1.0-preview.1`（预发布版，因依赖预发布的 OpenAI SDK）

### 发布方式

**方式 1：Git Tag 自动发布**
```bash
git tag v0.1.0-preview.1
git push origin v0.1.0-preview.1
```

**方式 2：手动触发 GitHub Actions**
在 GitHub Actions 页面手动运行 "Publish NuGet Packages" 工作流

**方式 3：本地打包**
```powershell
./scripts/pack.ps1 -Version 0.1.0-preview.1
dotnet nuget push "nupkgs/*.nupkg" --api-key YOUR_KEY --source https://api.nuget.org/v3/index.json
```

### 前置条件

1. 在 GitHub 仓库设置中添加 Secret：`NUGET_API_KEY`
2. NuGet.org 账户和 API Key

---

## [2026-01-28] CSharpier Tool: 代码格式化工具

### 功能概述

新增 CSharpier 代码格式化工具，让 Agent 能够自动格式化 C# 代码，确保代码风格一致性。

### 核心功能

```csharp
// CSharpierTool - 6 个工具方法
public class CSharpierTool
{
    // 格式化单个文件
    [FunctionTool("格式化指定的 C# 文件")]
    Task<ToolResult> FormatFile(string filePath, bool checkOnly = false);
    
    // 格式化目录
    [FunctionTool("格式化目录下所有 C# 文件")]
    Task<ToolResult> FormatDirectory(string directoryPath, bool checkOnly = false);
    
    // 格式化代码字符串
    [FunctionTool("格式化 C# 代码字符串")]
    Task<ToolResult> FormatCode(string code);
    
    // 检查安装
    [FunctionTool("检查 CSharpier 是否已安装")]
    Task<ToolResult> CheckInstallation();
    
    // 安装工具
    [FunctionTool("安装 CSharpier 全局工具", RequiresConfirmation = true)]
    Task<ToolResult> Install();
    
    // 获取格式化规则
    [FunctionTool("获取 CSharpier 格式化规则说明")]
    ToolResult GetFormattingRules();
}
```

### 使用方式

```csharp
// 注册 CSharpier 工具
services.AddCSharpierTools();

// 自定义配置
services.AddCSharpierTools(options =>
{
    options.CSharpierCommand = "dotnet-csharpier";
    options.TimeoutSeconds = 120;
});
```

### 格式化规则

CSharpier 关键规则：
- **长参数列表**：每个参数独占一行
- **集合初始化**：元素换行，尾随逗号
- **方法链**：每个调用独占一行
- **if 语句**：始终使用大括号

### 测试统计

| 分类 | 数量 |
|------|------|
| CSharpierToolTests | 10 |
| CSharpierToolOptionsTests | 2 |
| CSharpierExtensionsTests | 2 |
| **总计新增** | **13** |

---

## [2026-01-28] Week 27: Pinecone 向量存储

### 功能概述
添加 Pinecone 云原生向量数据库支持。Pinecone 是全托管的向量数据库服务，支持 Serverless 和 Pod-based 部署模式。

### 新增包

```
src/Dawning.Agents.Pinecone/
├── Dawning.Agents.Pinecone.csproj       # 新包（依赖 Pinecone.NET）
├── PineconeOptions.cs                    # 配置选项
├── PineconeVectorStore.cs                # IVectorStore 实现
└── PineconeServiceCollectionExtensions.cs  # DI 扩展方法
```

### 配置示例

```json
{
  "Pinecone": {
    "ApiKey": "your-api-key",
    "IndexName": "documents",
    "Namespace": "default",
    "VectorSize": 1536,
    "Metric": "cosine",
    "AutoCreateIndex": false,
    "Cloud": "aws",
    "Region": "us-east-1"
  }
}
```

### 核心 API

```csharp
// 使用配置文件
services.AddPineconeVectorStore(configuration);

// 使用配置委托
services.AddPineconeVectorStore(options => {
    options.ApiKey = "your-api-key";
    options.IndexName = "my-docs";
    options.VectorSize = 1536;
});

// 快速配置
services.AddPineconeVectorStore(
    apiKey: "your-api-key",
    indexName: "documents",
    @namespace: "my-namespace"
);

// Serverless 模式（自动创建索引）
services.AddPineconeServerless(
    apiKey: "your-api-key",
    indexName: "my-index",
    vectorSize: 1536,
    cloud: "aws",
    region: "us-east-1"
);
```

### 支持的度量方式

| Metric | 说明 |
|--------|------|
| cosine | 余弦相似度（默认） |
| dotproduct | 点积 |
| euclidean | 欧氏距离 |

### 环境变量

- `PINECONE_API_KEY` - 自动覆盖配置中的 ApiKey

### 测试统计

- 新增测试: 34 个
- 总测试数: 1581

---

## [2026-01-28] Week 26: Qdrant 向量存储

### 功能概述
添加 Qdrant 向量数据库支持，提供生产级向量存储能力。Qdrant 是高性能开源向量数据库，支持本地部署和云服务。

### 新增包

```
src/Dawning.Agents.Qdrant/
├── Dawning.Agents.Qdrant.csproj     # 新包（依赖 Qdrant.Client）
├── QdrantOptions.cs                  # 配置选项
├── QdrantVectorStore.cs              # IVectorStore 实现
└── QdrantServiceCollectionExtensions.cs  # DI 扩展方法
```

### 配置示例

```json
{
  "Qdrant": {
    "Host": "localhost",
    "Port": 6334,
    "CollectionName": "documents",
    "VectorSize": 1536,
    "ApiKey": null,
    "UseTls": false
  }
}
```

### 核心 API

```csharp
// 使用配置文件
services.AddQdrantVectorStore(configuration);

// 使用配置委托
services.AddQdrantVectorStore(options => {
    options.Host = "localhost";
    options.Port = 6334;
    options.CollectionName = "my-docs";
    options.VectorSize = 1536;
});

// 快速配置（本地）
services.AddQdrantVectorStore(host: "localhost", port: 6334);

// Qdrant Cloud
services.AddQdrantCloud(
    cloudUrl: "xxx.aws.cloud.qdrant.io",
    apiKey: "your-api-key",
    collectionName: "documents"
);
```

### IVectorStore 实现

```csharp
// QdrantVectorStore 实现 IVectorStore 接口
await vectorStore.AddAsync(chunk);                    // 添加单个文档块
await vectorStore.AddBatchAsync(chunks);              // 批量添加
var results = await vectorStore.SearchAsync(embedding, topK: 5);  // 向量搜索
await vectorStore.DeleteAsync(id);                    // 删除单个
await vectorStore.DeleteByDocumentIdAsync(docId);     // 按文档删除
await vectorStore.ClearAsync();                       // 清空集合
var chunk = await vectorStore.GetAsync(id);           // 获取单个
```

### 安装 Qdrant（Docker）

```bash
docker run -p 6333:6333 -p 6334:6334 qdrant/qdrant
```

### 测试统计

- 新增测试: 30 个
- 总测试数: 1547

---

## [2026-01-28] Week 25: 真实 Embedding Provider

### 功能概述

实现真实的 Embedding Provider，支持 OpenAI、Azure OpenAI 和 Ollama 三种嵌入模型服务。

### 新增文件

```
src/Dawning.Agents.OpenAI/
└── OpenAIEmbeddingProvider.cs    # OpenAI Embedding Provider

src/Dawning.Agents.Azure/
└── AzureOpenAIEmbeddingProvider.cs  # Azure OpenAI Embedding Provider

src/Dawning.Agents.Core/RAG/
├── OllamaEmbeddingProvider.cs    # Ollama Embedding Provider
└── RAGServiceCollectionExtensions.cs  # 更新 DI 扩展

tests/Dawning.Agents.Tests/RAG/
├── OpenAIEmbeddingProviderTests.cs       # 11 测试
├── AzureOpenAIEmbeddingProviderTests.cs  # 11 测试
├── OllamaEmbeddingProviderTests.cs       # 14 测试
└── EmbeddingProviderDITests.cs           # 11 测试
```

### 核心 API

```csharp
// 统一入口（根据 LLM 配置自动选择）
services.AddEmbeddingProvider(configuration);

// 独立注册方式
services.AddOpenAIEmbedding("sk-xxx", "text-embedding-3-small");
services.AddAzureOpenAIEmbedding(endpoint, apiKey, "embedding-deployment");
services.AddOllamaEmbedding("nomic-embed-text");
```

### 支持的模型

| Provider | 模型 | 维度 |
|----------|------|------|
| OpenAI | text-embedding-3-small | 1536 |
| OpenAI | text-embedding-3-large | 3072 |
| OpenAI | text-embedding-ada-002 | 1536 |
| Azure | 自定义部署 | 可配置 |
| Ollama | nomic-embed-text | 768 |
| Ollama | mxbai-embed-large | 1024 |
| Ollama | all-minilm | 384 |

### 测试统计

- 新增测试: 47 个
- 总测试数: 1470 → 1517

---

## [2026-01-28] Week 24: 统一 Provider 工厂模式

### 功能概述

统一 LLM Provider 注册方式，`AddLLMProvider()` 现在根据配置自动选择 Ollama、OpenAI 或 Azure OpenAI。

### 核心改进

**统一配置驱动**:
```csharp
// 一个方法支持所有 Provider 类型
services.AddLLMProvider(configuration);

// 根据配置自动选择:
// - ProviderType: Ollama → OllamaProvider
// - ProviderType: OpenAI → OpenAIProvider  
// - ProviderType: AzureOpenAI → AzureOpenAIProvider
```

**环境变量自动检测**:
```bash
# 设置环境变量后自动使用 OpenAI
export OPENAI_API_KEY=sk-xxx

# 或自动使用 Azure OpenAI
export AZURE_OPENAI_ENDPOINT=https://xxx.openai.azure.com
export AZURE_OPENAI_API_KEY=xxx
export AZURE_OPENAI_DEPLOYMENT=gpt-4o
```

### 文件变更

- **LLMServiceCollectionExtensions.cs**: 添加统一 `CreateProvider()` 工厂方法
- **Dawning.Agents.Core.csproj**: 添加对 OpenAI/Azure 包的项目引用
- **ProviderTests.cs**: 更新测试以验证统一工厂行为

### 架构说明

```
Dawning.Agents.Core (统一入口)
├── AddLLMProvider(configuration)  ← 根据配置自动选择
│
├── Dawning.Agents.OpenAI (独立包)
│   └── OpenAIProvider
│
└── Dawning.Agents.Azure (独立包)
    └── AzureOpenAIProvider
```

---

## [2026-01-27] Week 23: 配置热重载 IOptionsMonitor

### 功能概述

实现配置热重载机制，允许在运行时动态更新 LLM 配置而无需重启应用。

### 新增文件

```
src/Dawning.Agents.Abstractions/Configuration/
└── IConfigurationChangeNotifier.cs  # 配置变更通知接口（更新：添加 Timestamp）

src/Dawning.Agents.Core/Configuration/
├── ConfigurationChangeNotifier.cs       # 配置变更通知实现（修复：disposed 状态处理）
└── HotReloadServiceCollectionExtensions.cs  # 热重载 DI 扩展（新增）

src/Dawning.Agents.Core/LLM/
├── HotReloadableLLMProvider.cs          # 可热重载的 LLM Provider（新增）
└── LLMServiceCollectionExtensions.cs    # 更新：添加 AddHotReloadableLLMProvider()

tests/Dawning.Agents.Tests/
├── Configuration/
│   ├── ConfigurationChangeNotifierTests.cs          # 13 测试用例
│   └── HotReloadServiceCollectionExtensionsTests.cs # 10 测试用例
└── LLM/
    └── HotReloadableLLMProviderTests.cs             # 10 测试用例
```

### 核心接口

```csharp
// 配置变更事件
public class ConfigurationChangedEventArgs<T> : EventArgs
{
    public T? OldValue { get; }
    public T NewValue { get; }
    public string? Name { get; }
    public DateTime Timestamp { get; }  // 新增
}

// 配置变更通知器
public interface IConfigurationChangeNotifier<T> : IDisposable
{
    T CurrentValue { get; }
    event EventHandler<ConfigurationChangedEventArgs<T>>? ConfigurationChanged;
}

// 可热重载的 LLM Provider
public class HotReloadableLLMProvider : ILLMProvider, IDisposable
{
    public event EventHandler<ConfigurationChangedEventArgs<LLMOptions>>? ConfigurationChanged;
    // 配置变更时自动重建底层 Provider
}
```

### DI 扩展

```csharp
// 热重载配置
services.AddHotReloadOptions<LLMOptions>(configuration, "LLM");
services.AddHotReloadOptions<LLMOptions>(configuration, "LLM", opts => opts.Validate());

// 可热重载的 LLM Provider
services.AddHotReloadableLLMProvider(configuration);
```

### 测试统计

- 新增测试: 33 个
- 总测试数: 1437 → 1470

---

## [2026-01-27] Week 23: Serilog 结构化日志

### 功能概述

集成 Serilog 日志框架，提供企业级结构化日志能力。

### 新增文件

```
src/Dawning.Agents.Core/Logging/
├── SerilogAgentLogger.cs                    # Serilog 实现
└── SerilogServiceCollectionExtensions.cs    # DI 扩展

tests/Dawning.Agents.Tests/Logging/
├── SerilogAgentLoggerTests.cs               # 27 测试用例
└── SerilogServiceCollectionExtensionsTests.cs # 25 测试用例
```

### 核心功能

- **SerilogAgentLogger**: 集成 Serilog 的结构化日志记录器
- **日志级别映射**: 自动映射 AgentLogLevel → Serilog LogEventLevel
- **上下文丰富**: 支持结构化属性和作用域
- **DI 集成**: 无缝集成到服务容器

### DI 扩展

```csharp
// 使用默认 Serilog 配置
services.AddSerilogAgentLogger();

// 使用自定义 Logger
services.AddSerilogAgentLogger(customLogger);
```

### 测试统计

- 新增测试: 52 个
- 总测试数: 1385 → 1437

---

## [2026-01-26] Week 21: Polly V8 弹性策略 + FluentValidation

### 功能概述

集成 Polly V8 弹性策略和 FluentValidation 验证框架。

### 已完成内容

- Polly V8 弹性管道（重试、熔断、超时、回退）
- FluentValidation 配置验证
- 企业级错误处理

### 测试统计

- 总测试数: 1183 → 1385 (+202)

---

## [2026-01-26] 企业级转型: 代码优化与测试覆盖率提升

### 目标

将 dawning-agents 从学习项目转型为企业级 AI Agent 框架，提升代码质量和测试覆盖率。

### 代码优化（已完成）

#### 性能优化

- **SIMD 向量计算**: `InMemoryVectorStore.CosineSimilarity` 使用 `TensorPrimitives` 优化
- **内存优化**: `WindowMemory` 改用 `LinkedList<T>` 实现 O(1) 移除
- **缓存优化**: `ToolRegistry` 添加 `_cachedAllTools/ToolSets/Categories` 缓存

#### 线程安全

- `ToolRegistry`: 改用 `ConcurrentDictionary` + `InvalidateCache()` 模式
- `GuardrailPipeline`: 使用 `ImmutableList` + `ImmutableInterlocked.Update()`
- `CircuitBreaker`: 修复 `State` getter 副作用 + `TimeProvider` 注入

#### 内存泄漏修复

- `MethodTool.ExecuteAsync`: 添加 `using` 确保 `JsonDocument` 释放

#### 代码规范

- 50+ Core 类添加 `sealed` 关键字

### 测试覆盖率提升

| 指标 | 起始 | 最终 | 变化 |
|------|------|------|------|
| 行覆盖率 | 64.1% | **72.9%** | +8.8% |
| 分支覆盖率 | - | 62.6% | - |
| 方法覆盖率 | - | 86.3% | - |
| 测试数量 | 781 | **1183** | +402 |

### 新增测试文件

```
tests/Dawning.Agents.Tests/
├── RAG/RAGOptionsTests.cs                           # 22 用例 - RAG 配置验证
├── Scaling/AgentWorkerPoolTests.cs                  # 18 用例 - 工作池功能
├── Tools/
│   ├── VirtualToolTests.cs                          # 21 用例 - 虚拟工具展开/折叠
│   ├── MethodToolTests.cs                           # 23 用例 - 方法工具执行和参数解析
│   ├── DefaultToolApprovalHandlerTests.cs           # 43 用例 - 工具审批策略
│   ├── DefaultToolSelectorTests.cs                  # 21 用例 - 工具智能选择
│   └── BuiltIn/BuiltInToolExtensionsTests.cs        # 13 用例 - 内置工具 DI 扩展
├── Memory/MemoryServiceCollectionExtensionsTests.cs # Memory DI 扩展
├── Agent/AgentServiceCollectionExtensionsTests.cs   # Agent DI 扩展
├── Prompts/AgentPromptsTests.cs                     # Agent 提示词模板
├── Safety/ContentModeratorTests.cs                  # 内容审核
├── HumanLoop/AutoApprovalHandlerTests.cs            # 自动审批处理器
└── Tools/ToolScannerTests.cs                        # 工具扫描器
```

### 后续可继续的工作

#### 可提升覆盖率的区域

- `BuiltInToolExtensions` 58.8%
- `LLMServiceCollectionExtensions` 50.5%
- `AgentLogger` 44.2%
- `ObservabilityServiceCollectionExtensions` 23.8%

#### 需要外部服务的区域（难以单元测试）

- `AzureOpenAIProvider` 11.9%
- `OpenAIProvider` 12.1%
- `OllamaProvider` 12%
- `HttpTool`, `GitTool`, `ProcessTool` (需要实际 IO)

### 常用命令

```bash
# 运行测试
dotnet test

# 生成覆盖率报告
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults
reportgenerator -reports:"./TestResults/**/coverage.cobertura.xml" \
  -targetdir:"./TestResults/CoverageReport" -reporttypes:"TextSummary"
cat ./TestResults/CoverageReport/Summary.txt
```

---

## [2026-01-24] Phase 6: Week 12 部署与扩展完成

### 已实现的文件结构

```
src/Dawning.Agents.Abstractions/
├── Configuration/
│   ├── ConfigurationModels.cs    # 配置模型（AgentOptions, LLMOptions, ScalingOptions）
│   └── ISecretsManager.cs        # 密钥管理接口
└── Scaling/
    ├── IScalingComponents.cs     # 扩展组件接口
    └── ScalingModels.cs          # 扩展模型（ScalingMetrics, ScalingDecision）

src/Dawning.Agents.Core/
├── Configuration/
│   └── SecretsManager.cs         # 密钥管理实现
└── Scaling/
    ├── AgentRequestQueue.cs      # 请求队列
    ├── AgentWorkerPool.cs        # 工作池
    ├── AgentLoadBalancer.cs      # 负载均衡
    ├── CircuitBreaker.cs         # 熔断器
    ├── AgentAutoScaler.cs        # 自动扩展
    └── ScalingServiceCollectionExtensions.cs
```

### 核心功能

- **AgentRequestQueue** - 带优先级的请求队列
- **AgentWorkerPool** - 多工作线程处理池
- **AgentLoadBalancer** - 轮询/最小负载均衡
- **CircuitBreaker** - 熔断器（Closed/Open/HalfOpen）
- **AgentAutoScaler** - 基于 CPU/内存/队列长度的自动扩展
- **SecretsManager** - 环境变量密钥管理

---

## [2026-01-24] Phase 6: Week 11 可观测性完成

### 已实现的文件结构

```
src/Dawning.Agents.Abstractions/Observability/
├── HealthModels.cs               # 健康检查模型
├── MetricsModels.cs              # 指标模型
├── TelemetryConfig.cs            # 遥测配置
└── TracingModels.cs              # 追踪模型

src/Dawning.Agents.Core/Observability/
├── AgentHealthCheck.cs           # Agent 健康检查
├── AgentLogger.cs                # 结构化日志
├── AgentTelemetry.cs             # 遥测收集
├── DistributedTracer.cs          # 分布式追踪
├── LogContext.cs                 # 日志上下文
├── MetricsCollector.cs           # 指标收集
├── ObservableAgent.cs            # 可观测 Agent 包装
└── ObservabilityServiceCollectionExtensions.cs
```

### 核心功能

- **ObservableAgent** - 带遥测的 Agent 包装器
- **AgentTelemetry** - 请求/延迟/错误指标
- **MetricsCollector** - Prometheus 风格指标
- **DistributedTracer** - 分布式追踪
- **AgentHealthCheck** - 健康检查端点

---

## [2026-01-24] Phase 6: Week 10 人机协作完成

### 已实现的文件结构

```
src/Dawning.Agents.Abstractions/HumanLoop/
├── ApprovalResult.cs             # 审批结果
├── ConfirmationRequest.cs        # 确认请求
├── ConfirmationResponse.cs       # 确认响应
├── EscalationRequest.cs          # 升级请求
├── HumanLoopOptions.cs           # 配置选项
└── IHumanInteractionHandler.cs   # 人机交互接口

src/Dawning.Agents.Core/HumanLoop/
├── AgentEscalationException.cs   # 升级异常
├── ApprovalWorkflow.cs           # 审批工作流
├── AsyncCallbackHandler.cs       # 异步回调处理
├── ConsoleInteractionHandler.cs  # 控制台交互
├── HumanInLoopAgent.cs           # 人机协作 Agent
└── HumanLoopServiceCollectionExtensions.cs
```

### 核心功能

- **HumanInLoopAgent** - 带人工审批的 Agent
- **ApprovalWorkflow** - 多级审批工作流
- **AsyncCallbackHandler** - 异步回调处理
- **ConsoleInteractionHandler** - 控制台交互
- **ConfirmationRequest/Response** - 确认对话

---

## [2026-01-22] Week 8-12 Demo 更新

### 新增的演示文件

```text
samples/Dawning.Agents.Demo/
├── SafetyDemos.cs          ← 安全护栏演示（敏感数据检测、最大长度限制）
├── HumanLoopDemos.cs       ← 人机协作演示（确认请求、风险等级策略）
├── ObservabilityDemos.cs   ← 可观测性演示（指标收集、健康检查、追踪）
└── ScalingDemos.cs         ← 扩缩容演示（请求队列、负载均衡、熔断器）
```

### 修改的文件

- **RunMode.cs**: 添加 `Safety`, `HumanLoop`, `Observability`, `Scaling` 枚举值
- **Program.cs**: 添加菜单选项 `[S] Safety`, `[H] Human-in-Loop`, `[O] Observability`, `[C] Scaling`

---

## [2026-01-22] Phase 6: Week 12 Deployment & Scaling 完成

### 新增的文件

**Abstractions:**

```text
src/Dawning.Agents.Abstractions/Scaling/
├── ILoadBalancer.cs        ← 负载均衡接口
├── IAutoScaler.cs          ← 自动扩缩容接口
├── ICircuitBreaker.cs      ← 熔断器接口
├── CircuitState.cs         ← 熔断器状态枚举（Closed/Open/HalfOpen）
└── ScalingOptions.cs       ← 扩缩容配置选项
```

**Core:**

```text
src/Dawning.Agents.Core/Scaling/
├── RoundRobinLoadBalancer.cs      ← 轮询负载均衡
├── LeastLoadedLoadBalancer.cs     ← 最小负载均衡
├── SimpleAutoScaler.cs            ← 简单自动扩缩容
├── DefaultCircuitBreaker.cs       ← 默认熔断器实现
└── ScalingServiceCollectionExtensions.cs ← DI 扩展方法
```

### 核心组件

| 组件 | 职责 | 实现 |
|------|------|------|
| `ILoadBalancer` | 请求分发 | `RoundRobinLoadBalancer`, `LeastLoadedLoadBalancer` |
| `IAutoScaler` | 自动扩缩容 | `SimpleAutoScaler` |
| `ICircuitBreaker` | 故障隔离 | `DefaultCircuitBreaker` |

---

## [2026-01-22] Phase 6: Week 11 Observability & Monitoring 完成

### 新增的文件

**Abstractions:**

```text
src/Dawning.Agents.Abstractions/Observability/
├── IMetricsCollector.cs    ← 指标收集接口
├── IHealthCheck.cs         ← 健康检查接口
├── HealthStatus.cs         ← 健康状态枚举（Healthy/Degraded/Unhealthy）
└── MetricsSnapshot.cs      ← 指标快照数据模型
```

**Core:**

```text
src/Dawning.Agents.Core/Observability/
├── MetricsCollector.cs                    ← 指标收集器实现
├── CompositeHealthCheck.cs                ← 复合健康检查
└── ObservabilityServiceCollectionExtensions.cs ← DI 扩展方法
```

### 核心功能

| 功能 | 方法 | 说明 |
|------|------|------|
| Counter | `IncrementCounter()` | 递增计数器 |
| Histogram | `RecordHistogram()` | 记录直方图 |
| Gauge | `SetGauge()` | 设置仪表值 |
| Snapshot | `GetSnapshot()` | 获取指标快照 |

---

## [2026-01-22] Phase 5: Week 10 Human-in-the-Loop 完成

### 新增的文件

**Abstractions:**

```text
src/Dawning.Agents.Abstractions/HumanLoop/
├── IApprovalHandler.cs       ← 审批处理接口
├── ConfirmationRequest.cs    ← 确认请求数据模型
├── ConfirmationType.cs       ← 确认类型枚举（Binary/MultiChoice/FreeformInput/Review）
├── ApprovalStrategy.cs       ← 审批策略枚举（AlwaysApprove/AlwaysDeny/RiskBased/Interactive）
└── HumanLoopOptions.cs       ← 人机协作配置选项
```

**Core:**

```text
src/Dawning.Agents.Core/HumanLoop/
├── RiskBasedApprovalHandler.cs           ← 基于风险等级的审批处理
├── InteractiveApprovalHandler.cs         ← 交互式审批处理
└── HumanLoopServiceCollectionExtensions.cs ← DI 扩展方法
```

### 风险等级策略

| 风险等级 | 行为 |
|---------|------|
| Low | 自动批准 |
| Medium | 记录日志后批准 |
| High | 需要确认 |
| Critical | 需要多重确认 |

---

## [2026-01-22] Phase 5: Week 9 Safety & Guardrails 完成

### 新增的文件

**Abstractions:**

```text
src/Dawning.Agents.Abstractions/Safety/
├── IGuardrail.cs           ← 安全护栏接口
├── IInputGuardrail.cs      ← 输入验证接口
├── IOutputGuardrail.cs     ← 输出过滤接口
├── GuardrailResult.cs      ← 护栏结果数据模型
└── SafetyOptions.cs        ← 安全配置选项
```

**Core:**

```text
src/Dawning.Agents.Core/Safety/
├── SensitiveDataGuardrail.cs            ← 敏感数据检测（信用卡、邮箱、电话、身份证）
├── MaxLengthGuardrail.cs                ← 最大长度限制
├── CompositeGuardrail.cs                ← 复合护栏
└── SafetyServiceCollectionExtensions.cs ← DI 扩展方法
```

### 敏感数据检测模式

```csharp
// 支持的敏感数据类型
- 信用卡号: \b\d{4}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}\b
- 邮箱地址: \b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b
- 电话号码: \b1[3-9]\d{9}\b
- 身份证号: \b\d{17}[\dXx]\b
```

---

## [2026-01-22] Phase 4: Week 8 Token Usage Tracking 完成

### 新增的文件

**Abstractions:**

```text
src/Dawning.Agents.Abstractions/Telemetry/
├── ITokenTracker.cs        ← Token 追踪接口
├── ITelemetryCollector.cs  ← 遥测收集接口
├── TokenUsage.cs           ← Token 使用量数据模型
├── TelemetryData.cs        ← 遥测数据模型
└── TelemetryOptions.cs     ← 遥测配置选项
```

**Core:**

```text
src/Dawning.Agents.Core/Telemetry/
├── DefaultTokenTracker.cs                  ← 默认 Token 追踪器
├── InMemoryTelemetryCollector.cs           ← 内存遥测收集器
└── TelemetryServiceCollectionExtensions.cs ← DI 扩展方法
```

### DI 注册方式

```csharp
services.AddTelemetry(configuration);       // 根据配置自动选择
services.AddTokenTracker();                 // Token 追踪
services.AddTelemetryCollector();           // 遥测收集
```

---

## [2026-01-21] Phase 4: Week 7 Handoff 多 Agent 协作完成

### 新增的文件

**Abstractions:**

```text
src/Dawning.Agents.Abstractions/Handoff/
├── IHandoff.cs             ← Handoff 接口
├── Handoff.cs              ← 泛型 Handoff 实现
├── HandoffFilter.cs        ← Handoff 过滤器
└── HandoffOptions.cs       ← Handoff 配置选项
```

**Core:**

```text
src/Dawning.Agents.Core/Handoff/
├── HandoffExecutor.cs                    ← Handoff 执行器
├── ConditionalHandoff.cs                 ← 条件 Handoff
└── HandoffServiceCollectionExtensions.cs ← DI 扩展方法
```

### 核心概念

| 概念 | 说明 |
|------|------|
| `IHandoff` | Agent 切换接口 |
| `Handoff<TAgent>` | 泛型 Handoff，指定目标 Agent 类型 |
| `HandoffFilter` | 切换条件过滤器 |
| `HandoffExecutor` | 执行 Agent 切换 |

### DI 注册方式

```csharp
services.AddHandoff<ResearchAgent>();       // 注册 Handoff
services.AddHandoffExecutor();              // 注册执行器
```

---

## [2026-01-21] Phase 3: Week 6 RAG 系统完成

### 新增的文件

**Abstractions:**

```text
src/Dawning.Agents.Abstractions/RAG/
├── IEmbeddingProvider.cs      ← 嵌入向量提供者接口
├── IVectorStore.cs            ← 向量存储接口 + DocumentChunk/SearchResult
├── IRetriever.cs              ← 检索器接口
└── RAGOptions.cs              ← RAG 配置选项
```

**Core:**

```text
src/Dawning.Agents.Core/RAG/
├── SimpleEmbeddingProvider.cs       ← 基于哈希的本地嵌入（开发测试用）
├── InMemoryVectorStore.cs           ← 内存向量存储（余弦相似度）
├── DocumentChunker.cs               ← 文档分块器（段落/句子分割）
├── VectorRetriever.cs               ← 向量检索器
├── KnowledgeBase.cs                 ← 知识库（端到端 RAG）
└── RAGServiceCollectionExtensions.cs ← DI 扩展方法
```

**Tests:**

```text
tests/Dawning.Agents.Tests/RAG/
├── DocumentChunkerTests.cs              ← 9 个测试
├── InMemoryVectorStoreTests.cs          ← 10 个测试
├── SimpleEmbeddingProviderTests.cs      ← 14 个测试
├── VectorRetrieverTests.cs              ← 4 个测试
├── KnowledgeBaseTests.cs                ← 6 个测试
└── RAGServiceCollectionExtensionsTests.cs ← 7 个测试
```

### RAG 核心组件

| 组件 | 职责 | 实现 |
|------|------|------|
| `IEmbeddingProvider` | 文本 → 向量 | `SimpleEmbeddingProvider` (本地哈希) |
| `IVectorStore` | 向量存储 + 相似度搜索 | `InMemoryVectorStore` (余弦相似度) |
| `DocumentChunker` | 文档分块 | 段落 → 句子 → 固定大小 |
| `IRetriever` | 语义检索 | `VectorRetriever` |
| `KnowledgeBase` | 端到端知识库 | 分块 + 嵌入 + 存储 + 检索 |

### DI 注册方式

```csharp
// 完整 RAG（开发测试）
services.AddRAG();

// 带配置
services.AddRAG(configuration);
services.AddRAG(options => {
    options.ChunkSize = 500;
    options.ChunkOverlap = 50;
    options.TopK = 5;
    options.MinScore = 0.5f;
});

// 单独组件
services.AddInMemoryVectorStore();
services.AddSimpleEmbedding(dimensions: 384);
services.AddKnowledgeBase();
```

### Bug 修复

1. **DocumentChunker 无限循环** - `SplitLargeParagraph` 方法当 `overlap >= length` 时导致无限循环，消耗 17GB 内存
   - 修复：限制 `safeOverlap = Math.Min(overlap, chunkSize / 2)`
   - 修复：确保每次至少前进 1 个字符 `Math.Max(1, length - safeOverlap)`

2. **ProcessTool 内存泄漏** - `Process` 对象未正确释放
   - 修复：添加 `process?.Dispose()` 和 `finally` 块

### 测试基础设施改进

1. 添加 `xunit.runner.json` 禁用并行测试
2. 5 个集成测试标记为 `[Trait("Category", "Integration")]`
3. 228 个单元测试通过，耗时约 2 秒

### 测试统计

| 类别 | 测试数 |
|------|--------|
| RAG 核心 | 50 |
| Memory | 44 |
| Tools | 89 |
| Agent | 25 |
| LLM | 11 |
| Prompts | 14 |
| **总计** | **233** |

---

## [2026-01-20] Phase 3: Week 6 PackageManagerTool 完成

### 新增的文件

**Abstractions:**

```text
src/Dawning.Agents.Abstractions/Tools/
└── PackageManagerOptions.cs     ← 包管理工具配置
```

**Core:**

```text
src/Dawning.Agents.Core/Tools/BuiltIn/
└── PackageManagerTool.cs        ← 19 个包管理工具方法
```

**Tests:**

```text
tests/Dawning.Agents.Tests/Tools/
└── PackageManagerToolTests.cs   ← 23 个单元测试
```

### 实现的工具方法 (19 个)

| 包管理器 | 方法 | 风险等级 |
|----------|------|----------|
| **Winget** | WingetSearch, WingetShow, WingetList | Low |
| **Winget** | WingetInstall, WingetUninstall | High |
| **Pip** | PipList, PipShow | Low |
| **Pip** | PipInstall, PipUninstall | High |
| **Npm** | NpmSearch, NpmView, NpmList | Low |
| **Npm** | NpmInstall, NpmUninstall | High |
| **Dotnet** | DotnetToolSearch, DotnetToolList | Low |
| **Dotnet** | DotnetToolInstall, DotnetToolUninstall, DotnetToolUpdate | High |

### 安全特性

- **白名单机制**: 只允许安装白名单中的包
- **黑名单机制**: 禁止安装黑名单中的包
- **高风险标记**: 所有安装/卸载操作标记为 `RequiresConfirmation = true`
- **超时控制**: 默认 300 秒超时

### 使用示例

```csharp
// 注册工具
services.AddPackageManagerTools(options =>
{
    options.WhitelistedPackages = ["Git.*", "Microsoft.*"];
    options.BlacklistedPackages = ["*hack*", "*malware*"];
    options.DefaultTimeoutSeconds = 300;
});

// 使用工具
var tool = new PackageManagerTool(options);
await tool.DotnetToolList(global: true);
await tool.WingetSearch("git");
```

### Demo 命令

```bash
dotnet run -- -pm    # 演示 PackageManagerTool
```

---

## [2026-01-20] Phase 2.5: Week 4 Memory 系统完成

### 新增的接口（Abstractions）

```csharp
// 对话消息记录
public record ConversationMessage
{
    public string Id { get; init; }
    public required string Role { get; init; }
    public required string Content { get; init; }
    public DateTime Timestamp { get; init; }
    public int? TokenCount { get; init; }
}

// 对话记忆管理接口
public interface IConversationMemory
{
    Task AddMessageAsync(ConversationMessage message, CancellationToken ct = default);
    Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ChatMessage>> GetContextAsync(int? maxTokens = null, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
    Task<int> GetTokenCountAsync(CancellationToken ct = default);
    int MessageCount { get; }
}

// Token 计数器接口
public interface ITokenCounter
{
    int CountTokens(string text);
    int CountTokens(IEnumerable<ChatMessage> messages);
    string ModelName { get; }
    int MaxContextTokens { get; }
}
```

### 新增的实现类（Core）

| 类 | 描述 |
|---|---|
| `SimpleTokenCounter` | 基于字符估算的 Token 计数器（英文 4 字符/token，中文 1.5 字符/token） |
| `BufferMemory` | 存储所有消息的简单缓冲记忆 |
| `WindowMemory` | 只保留最后 N 条消息的滑动窗口记忆 |
| `SummaryMemory` | 自动摘要旧消息的智能记忆（需要 LLM） |

### DI 扩展方法

```csharp
// 根据配置自动选择 Memory 类型
services.AddMemory(configuration);

// 或直接指定类型
services.AddBufferMemory();
services.AddWindowMemory(windowSize: 10);
services.AddSummaryMemory(maxRecentMessages: 6, summaryThreshold: 10);
services.AddTokenCounter();
```

### 配置选项

```json
{
  "Memory": {
    "Type": "Window",
    "WindowSize": 10,
    "MaxRecentMessages": 6,
    "SummaryThreshold": 10,
    "ModelName": "gpt-4",
    "MaxContextTokens": 8192
  }
}
```

### 测试覆盖

- `SimpleTokenCounterTests` - 10 个测试
- `BufferMemoryTests` - 11 个测试
- `WindowMemoryTests` - 10 个测试
- `SummaryMemoryTests` - 13 个测试

**总计：150 个测试通过**（包括之前的 106 个）

---

## [2026-01-19] Phase 3.5: Week 5.5 Tool Sets 与 Virtual Tools 完成

### 新增的接口（Abstractions）

```csharp
// 工具集 - 将相关工具分组管理
public interface IToolSet
{
    string Name { get; }
    string Description { get; }
    string? Icon { get; }
    IReadOnlyList<ITool> Tools { get; }
    int Count { get; }
    ITool? GetTool(string toolName);
    bool Contains(string toolName);
}

// 虚拟工具 - 延迟加载工具组（参考 GitHub Copilot）
public interface IVirtualTool : ITool
{
    IReadOnlyList<ITool> ExpandedTools { get; }
    bool IsExpanded { get; }
    IToolSet ToolSet { get; }
    void Expand();
    void Collapse();
}

// 智能工具选择器
public interface IToolSelector
{
    Task<IReadOnlyList<ITool>> SelectToolsAsync(
        string query, IReadOnlyList<ITool> availableTools,
        int maxTools = 20, CancellationToken ct = default);
    Task<IReadOnlyList<IToolSet>> SelectToolSetsAsync(...);
}

// 工具审批处理器
public interface IToolApprovalHandler
{
    Task<bool> RequestApprovalAsync(ITool tool, string input, CancellationToken ct = default);
    Task<bool> RequestUrlApprovalAsync(ITool tool, string url, CancellationToken ct = default);
    Task<bool> RequestCommandApprovalAsync(ITool tool, string command, CancellationToken ct = default);
}

// 审批策略枚举
public enum ApprovalStrategy
{
    AlwaysApprove,   // 开发/测试环境
    AlwaysDeny,      // 安全敏感环境
    RiskBased,       // 基于风险等级（推荐）
    Interactive      // 交互式确认
}
```

### 新增的实现（Core）

```
src/Dawning.Agents.Core/
└── Tools/
    ├── ToolSet.cs                  # 工具集实现 ✨ 新
    ├── VirtualTool.cs              # 虚拟工具实现 ✨ 新
    ├── DefaultToolSelector.cs      # 默认工具选择器 ✨ 新
    ├── DefaultToolApprovalHandler.cs # 默认审批处理器 ✨ 新
    └── ToolServiceCollectionExtensions.cs # 扩展 DI 注册方法
```

### IToolRegistry 扩展

```csharp
public interface IToolRegistry
{
    // 原有方法...
    
    // 新增方法
    IReadOnlyList<ITool> GetToolsByCategory(string category);
    IReadOnlyList<string> GetCategories();
    void RegisterToolSet(IToolSet toolSet);
    IToolSet? GetToolSet(string name);
    IReadOnlyList<IToolSet> GetAllToolSets();
    void RegisterVirtualTool(IVirtualTool virtualTool);
    IReadOnlyList<IVirtualTool> GetVirtualTools();
}
```

### DI 注册方式

```csharp
// 注册工具选择器和审批处理器
services.AddToolSelector();  // 默认 keyword-based
services.AddToolApprovalHandler(ApprovalStrategy.RiskBased);

// 注册工具集
services.AddToolSet(new ToolSet("math", "数学工具", mathTools));
services.AddToolSetFrom<MathTool>("math", "数学计算工具集");

// 注册虚拟工具
services.AddVirtualTool(new VirtualTool(toolSet));
services.AddVirtualToolFrom<GitTool>("git", "Git 版本控制工具集", "🔧");
```

### DefaultToolApprovalHandler 特性

- **信任的 URL**: localhost, github.com, microsoft.com, azure.com, nuget.org
- **安全的命令**: ls, dir, pwd, git status, dotnet --version 等
- **危险的命令**: rm -rf /, format, shutdown, del /s /q 等（自动拒绝）
- **自动批准列表**: 可添加自定义 URL 和命令

### 测试统计

| 测试文件 | 测试数量 | 说明 |
|----------|----------|------|
| ToolSetTests.cs | 15 | ToolSet 和 VirtualTool |
| ToolSelectorTests.cs | 7 | DefaultToolSelector |
| ToolApprovalHandlerTests.cs | 12 | DefaultToolApprovalHandler |
| 原有测试 | 72 | LLM, Agent, Tools |
| **总计** | **106** | |

---

## [2026-01-19] Phase 3: Week 5 Tools/Skills 系统完成

### 新增的文件结构

```
src/Dawning.Agents.Abstractions/
└── Tools/
    ├── ITool.cs                    # 工具核心接口（扩展安全属性）
    ├── IToolRegistry.cs            # 工具注册表接口
    ├── ToolResult.cs               # 执行结果（新增 NeedConfirmation）
    ├── FunctionToolAttribute.cs    # 工具特性（新增安全属性）
    └── ToolRiskLevel.cs            # 风险等级枚举 ✨ 新

src/Dawning.Agents.Core/
└── Tools/
    ├── MethodTool.cs               # 方法工具实现
    ├── ToolRegistry.cs             # 工具注册表实现
    ├── ToolServiceCollectionExtensions.cs
    └── BuiltIn/
        ├── DateTimeTool.cs         # 日期时间工具 (4 methods)
        ├── MathTool.cs             # 数学工具 (8 methods)
        ├── JsonTool.cs             # JSON 工具 (4 methods)
        ├── UtilityTool.cs          # 实用工具 (5 methods)
        ├── FileSystemTool.cs       # 文件系统工具 (13 methods) ✨ 新
        ├── HttpTool.cs             # HTTP 工具 (6 methods) ✨ 新
        ├── ProcessTool.cs          # 进程工具 (6 methods) ✨ 新
        ├── GitTool.cs              # Git 工具 (18 methods) ✨ 新
        └── BuiltInToolExtensions.cs # DI 注册扩展（更新）

tests/Dawning.Agents.Tests/
└── Tools/
    ├── FunctionToolAttributeTests.cs
    ├── MethodToolTests.cs
    ├── ToolRegistryTests.cs
    └── BuiltInToolTests.cs         # 内置工具测试 ✨ 新
```

### 安全机制设计（参考 GitHub Copilot）

#### 风险等级（ToolRiskLevel）

```csharp
public enum ToolRiskLevel
{
    Low = 0,     // 读取操作：GetTime, Calculate, ReadFile
    Medium = 1,  // 网络操作：HttpGet, SearchWeb
    High = 2     // 危险操作：DeleteFile, RunCommand, GitPush
}
```

#### 工具属性扩展

```csharp
[FunctionTool(
    "删除文件",
    RequiresConfirmation = true,  // 需要用户确认
    RiskLevel = ToolRiskLevel.High,
    Category = "FileSystem"
)]
public string DeleteFile(string path) { ... }
```

#### ITool 接口扩展

```csharp
public interface ITool
{
    string Name { get; }
    string Description { get; }
    string ParametersSchema { get; }
    bool RequiresConfirmation { get; }      // 是否需要确认
    ToolRiskLevel RiskLevel { get; }        // 风险等级
    string? Category { get; }               // 工具分类
    Task<ToolResult> ExecuteAsync(...);
}
```

### 内置工具统计

| 类别 | 工具类 | 方法数 | 风险等级 |
|------|--------|--------|----------|
| DateTime | DateTimeTool | 4 | Low |
| Math | MathTool | 8 | Low |
| Json | JsonTool | 4 | Low |
| Utility | UtilityTool | 5 | Low |
| FileSystem | FileSystemTool | 13 | Low/Medium/High |
| Http | HttpTool | 6 | Medium |
| Process | ProcessTool | 6 | High |
| Git | GitTool | 18 | Low/Medium/High |
| **总计** | **8 类** | **64 方法** | |

### DI 注册方式

```csharp
// 注册所有内置工具（包括高风险）
services.AddAllBuiltInTools();

// 按类别注册
services.AddFileSystemTools();  // 文件系统
services.AddHttpTools();        // HTTP
services.AddProcessTools();     // 进程
services.AddGitTools();         // Git

// 只注册安全工具（不包括 Process/Git 高风险方法）
services.AddBuiltInTools();
```

### 测试统计

- 新增测试: 11 个（BuiltInToolTests）
- 总测试数: 74 个（全部通过）

---

## [2026-01-19] 下一步规划：Tool Sets 与 Virtual Tools

### 背景

参考 GitHub Copilot 的工具管理策略：

- 默认 40 个工具精简为 13 个核心工具
- 非核心工具分为 4 个 Virtual Tool 组
- 使用 Embedding-Guided Tool Routing 智能选择

### 计划实现的功能

#### 1. Tool Sets（工具集）

将相关工具分组，便于管理和引用。

```csharp
public interface IToolSet
{
    string Name { get; }
    string Description { get; }
    IReadOnlyList<ITool> Tools { get; }
}

// 使用方式
var searchTools = new ToolSet("search", "搜索相关工具", 
    [grepTool, searchFilesTool, semanticSearchTool]);
```

#### 2. Virtual Tools（虚拟工具）

延迟加载的工具组，减少 LLM 的工具选择压力。

```csharp
public interface IVirtualTool : ITool
{
    IReadOnlyList<ITool> ExpandedTools { get; }
    bool IsExpanded { get; }
    void Expand();
}

// LLM 先看到虚拟工具摘要，需要时再展开
// "FileSystemTools" → 展开为 13 个具体文件操作工具
```

#### 3. Tool Selector（工具选择器）

基于语义匹配的智能工具路由。

```csharp
public interface IToolSelector
{
    Task<IReadOnlyList<ITool>> SelectToolsAsync(
        string query,
        IReadOnlyList<ITool> availableTools,
        int maxTools = 20,
        CancellationToken ct = default);
}

// 实现策略
// - EmbeddingToolSelector: 基于 Embedding 相似度
// - LLMToolSelector: 使用 LLM 选择
// - HybridToolSelector: 混合策略
```

#### 4. Tool Approval Workflow（审批流程）

增强的工具执行确认机制。

```csharp
public interface IToolApprovalHandler
{
    Task<bool> RequestApprovalAsync(
        ITool tool,
        string input,
        CancellationToken ct = default);
}

// 支持的审批策略
// - AlwaysApprove: 自动批准所有
// - NeverApprove: 总是拒绝（只读模式）
// - RiskBasedApproval: 基于风险等级
// - InteractiveApproval: 交互式确认
```

### 预期架构

```
┌─────────────────────────────────────────────────────┐
│                    Agent                            │
├─────────────────────────────────────────────────────┤
│  ToolSelector (选择工具)                            │
│       ↓                                             │
│  ToolRegistry (管理所有工具)                        │
│       │                                             │
│       ├── Core Tools (13个核心工具，直接可见)       │
│       │   ├── read_file                            │
│       │   ├── edit_file                            │
│       │   ├── search                               │
│       │   └── terminal                             │
│       │                                             │
│       └── Virtual Tools (按需展开)                  │
│           ├── NotebookTools → [run_cell, ...]      │
│           ├── WebTools → [fetch, http_get, ...]    │
│           ├── TestingTools → [run_tests, ...]      │
│           └── GitTools → [commit, push, ...]       │
│                                                     │
│  ToolApprovalHandler (审批确认)                     │
│       ↓                                             │
│  Tool.ExecuteAsync()                                │
└─────────────────────────────────────────────────────┘
```

---

## [2026-01-18] Phase 2: Week 3 Agent 核心循环实现

### 新增的文件结构

```
src/Dawning.Agents.Abstractions/
├── Agent/
│   ├── IAgent.cs              # Agent 核心接口
│   ├── AgentContext.cs        # 执行上下文
│   ├── AgentStep.cs           # 单步执行记录
│   ├── AgentResponse.cs       # 执行响应
│   └── AgentOptions.cs        # 配置选项
└── Prompts/
    └── IPromptTemplate.cs     # 提示词模板接口

src/Dawning.Agents.Core/
├── Agent/
│   ├── AgentBase.cs                        # Agent 基类（核心循环）
│   ├── ReActAgent.cs                       # ReAct 模式实现
│   └── AgentServiceCollectionExtensions.cs # DI 注册扩展
└── Prompts/
    ├── PromptTemplate.cs      # 模板实现
    └── AgentPrompts.cs        # 预定义模板

tests/Dawning.Agents.Tests/
├── Agent/
│   ├── AgentModelsTests.cs    # 数据模型测试 (9 tests)
│   └── ReActAgentTests.cs     # ReActAgent 测试 (6 tests)
└── Prompts/
    └── PromptTemplateTests.cs # 模板测试 (7 tests)
```

### 核心接口设计

```csharp
public interface IAgent
{
    string Name { get; }
    string Instructions { get; }
    Task<AgentResponse> RunAsync(string input, CancellationToken ct = default);
    Task<AgentResponse> RunAsync(AgentContext context, CancellationToken ct = default);
}
```

### ReAct 模式实现

- **Thought**: Agent 的思考过程
- **Action**: 要执行的动作
- **Action Input**: 动作输入参数
- **Observation**: 动作执行结果
- **Final Answer**: 最终答案

### 测试统计

- 新增测试: 21 个
- 总测试数: 63 个（全部通过）

### 其他变更

- 项目重命名: `DawningAgents` → `Dawning.Agents`
- 更新 copilot-instructions.md 添加 CSharpier 格式规范

---

## [2026-01-17] Phase 1: Week 2 项目初始化完成

### 创建的解决方案结构

```
dawning-agents/
├── .editorconfig                    # 代码规范
├── .github/workflows/build.yml      # GitHub Actions CI/CD
├── Directory.Build.props            # 统一项目配置 (net10.0)
├── Dawning.Agents.sln                # 解决方案
├── src/
│   ├── Dawning.Agents.Core/          # 核心类库
│   │   └── LLM/
│   │       ├── ILLMProvider.cs      # LLM 抽象接口
│   │       └── OllamaProvider.cs    # Ollama 本地模型实现
│   └── Dawning.Agents.Demo/          # 演示控制台
│       └── Program.cs
└── tests/
    └── Dawning.Agents.Tests/         # 单元测试 (8 tests)
        └── LLM/
            └── OllamaProviderTests.cs
```

### 核心接口设计

```csharp
public interface ILLMProvider
{
    string Name { get; }
    Task<ChatCompletionResponse> ChatAsync(...);
    IAsyncEnumerable<string> ChatStreamAsync(...);
}
```

### 技术栈

- **.NET**: 10.0 (最新 LTS)
- **本地 LLM**: Ollama + deepseek-coder (1.3b/6.7B)
- **测试框架**: xUnit + FluentAssertions + Moq
- **CI/CD**: GitHub Actions

### NuGet 包

| 包 | 版本 | 用途 |
|---|---|---|
| Microsoft.Extensions.Http | 10.0.2 | HTTP 客户端 |
| Microsoft.Extensions.Logging.Abstractions | 10.0.2 | 日志抽象 |
| xUnit | 2.9.2 | 单元测试 |
| FluentAssertions | 8.8.0 | 断言库 |
| Moq | 4.20.72 | Mock 框架 |

---

## [2026-01-16] Phase 0: 框架分析文档全面更新

### 背景

微软在 2025年11月宣布将 **Semantic Kernel** 和 **AutoGen** 整合为统一的 **Microsoft Agent Framework**。同时 **OpenAI Agents SDK**（Swarm 的生产版本）成为主流框架。因此需要更新所有框架分析文档。

### 删除的文档

- `docs/readings/03-semantic-kernel-analysis/` - Semantic Kernel 分析（已过时）
- `docs/readings/04-autogen-analysis/` - AutoGen 分析（已过时）

### 新增的文档

| 文件 | 描述 |
|------|------|
| `docs/readings/03-ms-agent-framework-analysis/ms-agent-framework-analysis-zh.md` | MS Agent Framework 架构分析（中文） |
| `docs/readings/03-ms-agent-framework-analysis/ms-agent-framework-analysis-en.md` | MS Agent Framework 架构分析（英文） |
| `docs/readings/04-openai-agents-sdk-analysis/openai-agents-sdk-analysis-zh.md` | OpenAI Agents SDK 架构分析（中文） |
| `docs/readings/04-openai-agents-sdk-analysis/openai-agents-sdk-analysis-en.md` | OpenAI Agents SDK 架构分析（英文） |

### 更新的文档

#### `LEARNING_PLAN.md`

- **Week 1 Day 5-7**: Semantic Kernel/AutoGen → MS Agent Framework/OpenAI Agents SDK
- **Week 5**: SK Plugins → OpenAI Agents SDK `@function_tool` + MS Agent Framework `ai_function`
- **Week 7**: AutoGen 源码 → MS Agent Framework HandoffBuilder + OpenAI Agents SDK Handoff
- **资源列表**: 更新必读源码（新增 LangGraph、MS Agent Framework、OpenAI Agents SDK）

#### `docs/readings/05-framework-comparison/`

- **三框架对比**: LangChain/LangGraph, MS Agent Framework, OpenAI Agents SDK
- **新增双编排模式**:
  - `IWorkflow` - Workflow 编排（LLM 动态决策交接）
  - `IStateGraph` - 状态机编排（开发者预定义流程）
- **更新设计原则**: 从"四个核心原语 + 工作流"改为"四个核心原语 + 双编排模式"
- **新增接口**: `IStateGraph<TState>`, `StateGraphBuilder<TState>`

#### `docs/readings/06-week2-setup-guide/`

- **Python 包更新**:
  - 移除: `autogen-agentchat`
  - 新增: `openai-agents`, `langgraph`, `agent-framework`
- **.NET 包更新**:
  - 移除: `Microsoft.SemanticKernel`
  - 新增: `Microsoft.Agents.AI --prerelease`

### 安装的 VS Code 扩展

- `shd101wyy.markdown-preview-enhanced` - 增强的 Markdown 预览（支持 Mermaid）

---

## [2026-01-XX] Phase 0: 初始框架分析（历史记录）

### 创建的文档

- `docs/readings/00-agent-core-concepts/` - Agent 核心概念
- `docs/readings/01-building-effective-agents/` - 构建有效 Agent
- `docs/readings/02-langchain-analysis/` - LangChain 分析
- `docs/readings/02-openai-function-calling/` - OpenAI Function Calling
- `docs/readings/03-react-paper/` - ReAct 论文分析
- `docs/readings/04-chain-of-thought/` - 思维链分析
- `docs/readings/05-framework-comparison/` - 框架对比（初版，比较 LangChain/SK/AutoGen）
- `docs/readings/06-week2-setup-guide/` 至 `16-week12-deployment/` - 12周学习计划

---

## dawning-agents 设计决策摘要

### 核心原语（来自 OpenAI Agents SDK）

```csharp
public interface IAgent { }      // Agent - LLM + 指令 + 工具
public interface ITool { }       // Tool - 可调用的功能
public interface IHandoff { }    // Handoff - Agent 间委托
public interface IGuardrail { }  // Guardrail - 输入/输出验证
```

### 双编排模式

```csharp
// Workflow 编排 - LLM 动态决策（来自 MS Agent Framework）
public interface IWorkflow<TContext> { }
public class HandoffBuilder<TContext> { }

// 状态机编排 - 开发者预定义流程（来自 LangGraph）
public interface IStateGraph<TState> { }
public class StateGraphBuilder<TState> { }
```

### 场景选择指南

| 场景 | 推荐模式 | 原因 |
|------|----------|------|
| 多 Agent 协作、客服分流 | Workflow (HandoffBuilder) | LLM 智能决策交接目标 |
| 审批流、数据管道、多轮迭代 | StateGraph | 需要确定性的流程控制 |
| 简单对话 | 直接用 Agent | 无需编排 |

### 关键设计来源

| 特性 | 来源 |
|------|------|
| 四个核心原语 | OpenAI Agents SDK |
| Guardrails | OpenAI Agents SDK |
| Tracing | OpenAI Agents SDK |
| HandoffBuilder | MS Agent Framework |
| 两层架构 | MS Agent Framework |
| StateGraph | LangGraph |
| `[Tool]` 属性 | .NET 最佳实践 |

---

## 当前文档结构

```text
docs/readings/
├── 00-agent-core-concepts/           # Agent 核心概念
├── 01-building-effective-agents/     # 构建有效 Agent
├── 02-langchain-analysis/            # LangChain 分析
├── 02-openai-function-calling/       # OpenAI Function Calling
├── 03-ms-agent-framework-analysis/   # MS Agent Framework 分析 ✨ 新
├── 03-react-paper/                   # ReAct 论文
├── 04-chain-of-thought/              # 思维链
├── 04-openai-agents-sdk-analysis/    # OpenAI Agents SDK 分析 ✨ 新
├── 05-framework-comparison/          # 框架对比 ✅ 已更新
├── 06-week2-setup-guide/             # Week 2 环境搭建 ✅ 已更新
├── 07-week3-agent-loop/              # Week 3 Agent 循环
├── 08-week4-memory/                  # Week 4 记忆系统
├── 09-week5-tools/                   # Week 5 工具系统
├── 10-week6-rag/                     # Week 6 RAG
├── 11-week7-multi-agent/             # Week 7 多 Agent
├── 12-week8-communication/           # Week 8 通信
├── 13-week9-safety/                  # Week 9 安全
├── 14-week10-human-loop/             # Week 10 人机协作
├── 15-week11-observability/          # Week 11 可观测性
└── 16-week12-deployment/             # Week 12 部署
```

---

## 下一步计划

### Phase 1: 核心原语实现（Week 1-2）

- [ ] 创建解决方案结构
- [ ] 实现 `IAgent` 和 `Agent`
- [ ] 实现 `ITool` 和 `FunctionTool`
- [ ] 实现 `[Tool]` 属性发现
- [ ] OpenAI 集成
- [ ] 基础 `Runner`

### Phase 2: Handoff 与 Guardrails（Week 3-4）

- [ ] 实现 `IHandoff`
- [ ] 实现 `HandoffBuilder`
- [ ] 实现 `IGuardrail`
- [ ] 输入/输出护栏

### Phase 3: 双编排模式（Week 5-6）

- [ ] 实现 `HandoffWorkflow`
- [ ] 实现 `StateGraph` 和 `StateGraphBuilder`
- [ ] 条件边和循环
- [ ] 人机协作

### Phase 4: 可观测性（Week 7-8）

- [ ] Tracing 系统
- [ ] OpenTelemetry 集成

### Phase 5: 完善（Week 9-10）

- [ ] 更多 LLM 提供商
- [ ] Session 管理
- [ ] 文档和示例
