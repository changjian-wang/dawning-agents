# 🤖 构建智能客服机器人

> 使用 Dawning.Agents 构建多轮对话客服系统

---

## 📋 场景描述

构建一个电商客服机器人，支持：
- 查询订单状态
- 处理退换货请求
- 回答产品问题
- 收集用户反馈

---

## 🏗️ 架构设计

```
┌─────────────────────────────────────────────────────┐
│                   客户端 (Web/App)                    │
└──────────────────────┬──────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────┐
│                   API Gateway                        │
│              (认证、限流、日志)                        │
└──────────────────────┬──────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────┐
│              Dawning.Agents 服务                     │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │
│  │ Triage Agent│──│ Order Agent │──│ Product Agent│ │
│  └─────────────┘  └─────────────┘  └─────────────┘  │
│         │                │                │         │
│         ▼                ▼                ▼         │
│  ┌─────────────────────────────────────────────┐   │
│  │              Tools / Skills                  │   │
│  │  - 查询订单 - 退换货 - 产品搜索 - 知识库     │   │
│  └─────────────────────────────────────────────┘   │
└──────────────────────┬──────────────────────────────┘
                       │
           ┌───────────┼───────────┐
           ▼           ▼           ▼
     ┌─────────┐ ┌─────────┐ ┌─────────┐
     │订单系统 │ │CRM系统  │ │知识库   │
     └─────────┘ └─────────┘ └─────────┘
```

---

## 💻 实现步骤

### 1. 定义业务工具

```csharp
public class CustomerServiceTools
{
    private readonly IOrderService _orderService;
    private readonly IProductService _productService;
    
    public CustomerServiceTools(IOrderService orderService, IProductService productService)
    {
        _orderService = orderService;
        _productService = productService;
    }

    [FunctionTool("查询订单状态", Category = "Order")]
    public async Task<string> QueryOrder(string orderId)
    {
        var order = await _orderService.GetOrderAsync(orderId);
        if (order == null)
        {
            return $"未找到订单 {orderId}";
        }
        
        return $"""
            订单号: {order.Id}
            状态: {order.Status}
            下单时间: {order.CreatedAt:yyyy-MM-dd HH:mm}
            收货地址: {order.Address}
            商品: {string.Join(", ", order.Items.Select(i => i.Name))}
            总金额: ¥{order.TotalAmount:F2}
            """;
    }

    [FunctionTool("申请退换货", Category = "Order", RequiresConfirmation = true, RiskLevel = ToolRiskLevel.Medium)]
    public async Task<string> RequestReturn(string orderId, string reason)
    {
        var result = await _orderService.CreateReturnRequestAsync(orderId, reason);
        return result.Success 
            ? $"退换货申请已提交，工单号: {result.TicketId}，预计 1-3 个工作日处理" 
            : $"申请失败: {result.Error}";
    }

    [FunctionTool("搜索产品", Category = "Product")]
    public async Task<string> SearchProducts(string query, int limit = 5)
    {
        var products = await _productService.SearchAsync(query, limit);
        if (!products.Any())
        {
            return "未找到相关产品";
        }
        
        var sb = new StringBuilder("找到以下产品:\n\n");
        foreach (var p in products)
        {
            sb.AppendLine($"- {p.Name} | ¥{p.Price:F2} | {p.Description}");
        }
        return sb.ToString();
    }

    [FunctionTool("查询产品详情", Category = "Product")]
    public async Task<string> GetProductDetails(string productId)
    {
        var product = await _productService.GetAsync(productId);
        if (product == null)
        {
            return "产品不存在";
        }
        
        return $"""
            产品名称: {product.Name}
            价格: ¥{product.Price:F2}
            库存: {product.Stock}
            描述: {product.Description}
            规格: {string.Join(", ", product.Specs)}
            """;
    }

    [FunctionTool("提交用户反馈", Category = "Feedback")]
    public async Task<string> SubmitFeedback(string content, int rating)
    {
        await _feedbackService.CreateAsync(new Feedback
        {
            Content = content,
            Rating = rating,
            CreatedAt = DateTime.UtcNow,
        });
        return "感谢您的反馈！我们会认真对待每一条建议。";
    }
}
```

### 2. 配置 Agent

```csharp
var builder = Host.CreateApplicationBuilder(args);

// 配置 LLM
builder.Services.AddLLMProvider(builder.Configuration);

// 注册业务服务
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IProductService, ProductService>();

// 注册工具
builder.Services.AddToolsFromType<CustomerServiceTools>();

// 配置 Memory（保持对话上下文）
builder.Services.AddWindowMemory(windowSize: 20);

// 配置安全护栏
builder.Services.AddSafetyGuardrails(options =>
{
    options.EnableContentFilter = true;
    options.EnableSensitiveDataFilter = true;  // 过滤用户敏感信息
});

// 配置 Agent
builder.Services.AddReActAgent(options =>
{
    options.Name = "CustomerServiceBot";
    options.Instructions = """
        你是一个专业友好的电商客服助手。请遵循以下准则:
        
        1. 态度亲切，语言简洁专业
        2. 优先理解用户需求，再提供帮助
        3. 订单问题请先确认订单号
        4. 退换货需要用户确认
        5. 如果无法解决，告知会转人工处理
        6. 保护用户隐私，不要泄露敏感信息
        """;
    options.MaxSteps = 5;
});
```

### 3. 实现 API 端点

```csharp
// Controllers/ChatController.cs
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IAgent _agent;
    private readonly IConversationMemory _memory;
    
    public ChatController(IAgent agent, IConversationMemory memory)
    {
        _agent = agent;
        _memory = memory;
    }

    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        // 添加用户消息到 Memory
        await _memory.AddMessageAsync(new ConversationMessage
        {
            Role = "user",
            Content = request.Message,
            Timestamp = DateTime.UtcNow,
        });

        // 执行 Agent
        var response = await _agent.RunAsync(request.Message);
        
        // 添加助手回复到 Memory
        await _memory.AddMessageAsync(new ConversationMessage
        {
            Role = "assistant",
            Content = response.FinalAnswer,
            Timestamp = DateTime.UtcNow,
        });

        return Ok(new ChatResponse
        {
            Message = response.FinalAnswer,
            SessionId = request.SessionId,
        });
    }

    [HttpPost("stream")]
    public async Task StreamChat([FromBody] ChatRequest request)
    {
        Response.ContentType = "text/event-stream";
        
        await foreach (var chunk in _agent.RunStreamAsync(request.Message))
        {
            await Response.WriteAsync($"data: {chunk}\n\n");
            await Response.Body.FlushAsync();
        }
    }
}
```

### 4. 添加 RAG 知识库

```csharp
// 构建产品知识库
builder.Services.AddOpenAIEmbedding(builder.Configuration);
builder.Services.AddQdrantVectorStore(options =>
{
    options.Host = "localhost";
    options.Port = 6334;
    options.CollectionName = "product_knowledge";
});
builder.Services.AddRetriever();

// 初始化知识库
public class KnowledgeBaseInitializer : IHostedService
{
    private readonly IVectorStore _vectorStore;
    private readonly IEmbeddingProvider _embedding;
    
    public async Task StartAsync(CancellationToken ct)
    {
        var documents = await LoadProductDocumentsAsync();
        var chunker = new DocumentChunker(chunkSize: 500, overlap: 50);
        
        foreach (var doc in documents)
        {
            var chunks = chunker.Split(doc);
            foreach (var chunk in chunks)
            {
                var embedding = await _embedding.EmbedAsync(chunk.Text);
                await _vectorStore.AddAsync(new DocumentChunk
                {
                    Text = chunk.Text,
                    Embedding = embedding,
                    Metadata = new Dictionary<string, object>
                    {
                        ["source"] = doc.FileName,
                        ["category"] = doc.Category,
                    },
                });
            }
        }
    }
}

// 添加知识库搜索工具
[FunctionTool("搜索知识库获取产品信息", Category = "Knowledge")]
public async Task<string> SearchKnowledge(string query)
{
    var results = await _retriever.RetrieveAsync(query, topK: 3);
    if (!results.Any())
    {
        return "未找到相关信息";
    }
    
    return string.Join("\n\n", results.Select(r => r.Text));
}
```

---

## 🧪 测试对话

### 查询订单

```
用户: 我想查一下订单 ORD123456 的状态
助手: 正在为您查询订单...

订单号: ORD123456
状态: 已发货
下单时间: 2026-02-01 14:30
收货地址: 北京市朝阳区xxx
商品: iPhone 15 Pro
总金额: ¥8999.00

预计明天送达，请注意查收！
```

### 申请退货

```
用户: 我想退货，买错型号了
助手: 好的，请问您要退货的订单号是多少？

用户: ORD123456
助手: 已确认订单 ORD123456，您的退货原因是"买错型号"。
确认提交退货申请吗？[确认/取消]

用户: 确认
助手: 退换货申请已提交，工单号: TK789012，预计 1-3 个工作日处理。
如有问题可随时联系我们。
```

---

## 📈 监控和优化

### 关键指标

```csharp
// 记录会话指标
metrics.IncrementCounter("chat_sessions_total");
metrics.RecordHistogram("chat_response_time_ms", stopwatch.ElapsedMilliseconds);
metrics.IncrementCounter("tool_calls_total", tags: new { tool = toolName });
```

### A/B 测试不同 Prompt

```csharp
var runner = new ABTestRunner(evaluator);

var agentA = CreateAgent("简洁风格 Prompt");
var agentB = CreateAgent("详细风格 Prompt");

var comparison = await runner.CompareAsync(agentA, agentB, testCases);
Console.WriteLine($"用户满意度 A: {comparison.AgentAScore:F2}");
Console.WriteLine($"用户满意度 B: {comparison.AgentBScore:F2}");
```

---

## 📦 部署

### Docker Compose

```yaml
version: '3.8'
services:
  customer-service-bot:
    image: customer-service-bot:latest
    environment:
      - AZURE_OPENAI_API_KEY=${AZURE_OPENAI_API_KEY}
      - AZURE_OPENAI_ENDPOINT=${AZURE_OPENAI_ENDPOINT}
      - ConnectionStrings__Database=Host=postgres;Database=orders
      - Qdrant__Host=qdrant
    depends_on:
      - postgres
      - qdrant
    ports:
      - "8080:8080"

  postgres:
    image: postgres:15
    environment:
      POSTGRES_DB: orders
      POSTGRES_PASSWORD: secret
    volumes:
      - postgres_data:/var/lib/postgresql/data

  qdrant:
    image: qdrant/qdrant:latest
    volumes:
      - qdrant_data:/qdrant/storage

volumes:
  postgres_data:
  qdrant_data:
```

---

> 📌 **下一步**: 查看 [生产最佳实践](production-best-practices.md) 了解部署优化
