# 实战教程

> 使用 Dawning.Agents 构建完整的 AI Agent 应用

---

## 🔍 示例 1：代码审查 Agent

自动审查 Pull Request，检测代码问题和安全漏洞，生成审查报告。

### 架构设计

```
┌─────────────────────────────────────────────────────┐
│                  GitHub Webhook                      │
│              (PR 创建/更新事件)                       │
└──────────────────────┬──────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────┐
│              Code Review Agent                       │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │
│  │ Diff Analyzer│──│ Code Checker│──│ Report Gen  │ │
│  └─────────────┘  └─────────────┘  └─────────────┘  │
│                                                      │
│  Tools: Git / FileSystem / CodeAnalysis / LLM       │
└──────────────────────┬──────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────┐
│               GitHub API                             │
│          (添加评论、请求修改)                         │
└─────────────────────────────────────────────────────┘
```

### 定义代码审查工具

```csharp
public class CodeReviewTools
{
    private readonly IGitHubClient _github;
    
    [FunctionTool("获取 PR 的文件差异", Category = "Git")]
    public async Task<string> GetPRDiff(string owner, string repo, int prNumber)
    {
        var diff = await _github.PullRequest.GetDiff(owner, repo, prNumber);
        return diff;
    }

    [FunctionTool("检查安全漏洞", Category = "Security")]
    public async Task<string> CheckSecurity(string code, string language)
    {
        var vulnerabilities = new List<string>();
        
        if (Regex.IsMatch(code, @"\".*\+.*\".*SELECT|INSERT|UPDATE|DELETE", RegexOptions.IgnoreCase))
        {
            vulnerabilities.Add("🔴 高危：可能存在 SQL 注入漏洞");
        }
        
        if (Regex.IsMatch(code, @"(password|secret|key|token)\s*=\s*[\"'][^""']+[\"']", RegexOptions.IgnoreCase))
        {
            vulnerabilities.Add("🔴 高危：检测到硬编码的敏感信息");
        }
        
        return vulnerabilities.Any() 
            ? string.Join("\n", vulnerabilities) 
            : "未发现明显的安全漏洞";
    }

    [FunctionTool("分析代码复杂度", Category = "Analysis")]
    public string AnalyzeComplexity(string code)
    {
        var lines = code.Split('\n').Length;
        var ifCount = Regex.Matches(code, @"\bif\b").Count;
        var loopCount = Regex.Matches(code, @"\b(for|while|foreach)\b").Count;
        var complexity = ifCount + loopCount * 2;
        
        return $"""
            代码统计:
            - 总行数: {lines}
            - 条件分支: {ifCount}
            - 循环: {loopCount}
            - 圈复杂度估算: {complexity}
            {(complexity > 10 ? "⚠️ 复杂度较高，建议拆分" : "✅ 复杂度正常")}
            """;
    }

    [FunctionTool("添加 PR 评论", Category = "GitHub", RequiresConfirmation = true)]
    public async Task<string> AddPRComment(string owner, string repo, int prNumber, string comment)
    {
        await _github.Issue.Comment.Create(owner, repo, prNumber, comment);
        return "评论已添加";
    }
}
```

### 配置 Agent

```csharp
builder.Services.AddReActAgent(options =>
{
    options.Name = "CodeReviewAgent";
    options.Instructions = """
        你是一个专业的代码审查助手。审查步骤:
        1. 获取 PR diff
        2. 对每个文件检查代码风格、安全漏洞、复杂度
        3. 生成审查报告（问题总结 + 评分 1-10）
        4. 在 PR 上添加评论
        
        语气要求: 建设性、专业、友善
        """;
    options.MaxSteps = 10;
});
```

### 定义工作流

```csharp
var reviewWorkflow = new WorkflowBuilder("CodeReviewWorkflow")
    .StartWith<ToolNode>("get_diff", tool: "GetPRDiff")
    .Then<ParallelNode>("analyze")
        .Branch<ToolNode>("style_check", tool: "CheckCodeStyle")
        .Branch<ToolNode>("security_check", tool: "CheckSecurity")
        .Branch<ToolNode>("complexity_check", tool: "AnalyzeComplexity")
    .EndParallel()
    .Then<AgentNode>("generate_report", agent: reportAgent)
    .Then<HumanApprovalNode>("approve", prompt: "确认发布审查报告？")
    .Then<ToolNode>("post_comment", tool: "AddPRComment")
    .Build();
```

### 审查报告示例

```markdown
## 🔍 代码审查报告

**PR #123**: feat: 添加用户认证功能
**审查员**: CodeReviewAgent

### 📊 总体评分: 7/10

### ✅ 优点
- 代码结构清晰，职责分明
- 使用了依赖注入，便于测试

### ⚠️ 需要改进
1. **[高危]** `AuthService.cs:45` - 密码比较使用 `==`，存在时序攻击风险
2. `AuthService.cs:23` - 方法过长 (85行)，建议拆分
3. 缺少单元测试
```

---

## 🤖 示例 2：智能客服机器人

多轮对话客服系统，支持查询订单、退换货、产品咨询。

### 架构设计

```
┌─────────────────────────────────────────────────────┐
│                   客户端 (Web/App)                    │
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

### 定义业务工具

```csharp
public class CustomerServiceTools
{
    private readonly IOrderService _orderService;
    private readonly IProductService _productService;
    
    [FunctionTool("查询订单状态", Category = "Order")]
    public async Task<string> QueryOrder(string orderId)
    {
        var order = await _orderService.GetOrderAsync(orderId);
        if (order == null) return $"未找到订单 {orderId}";
        
        return $"""
            订单号: {order.Id}
            状态: {order.Status}
            下单时间: {order.CreatedAt:yyyy-MM-dd HH:mm}
            商品: {string.Join(", ", order.Items.Select(i => i.Name))}
            总金额: ¥{order.TotalAmount:F2}
            """;
    }

    [FunctionTool("申请退换货", Category = "Order", RequiresConfirmation = true, RiskLevel = ToolRiskLevel.Medium)]
    public async Task<string> RequestReturn(string orderId, string reason)
    {
        var result = await _orderService.CreateReturnRequestAsync(orderId, reason);
        return result.Success 
            ? $"退换货申请已提交，工单号: {result.TicketId}" 
            : $"申请失败: {result.Error}";
    }

    [FunctionTool("搜索产品", Category = "Product")]
    public async Task<string> SearchProducts(string query, int limit = 5)
    {
        var products = await _productService.SearchAsync(query, limit);
        if (!products.Any()) return "未找到相关产品";
        
        var sb = new StringBuilder("找到以下产品:\n\n");
        foreach (var p in products)
        {
            sb.AppendLine($"- {p.Name} | ¥{p.Price:F2} | {p.Description}");
        }
        return sb.ToString();
    }
}
```

### 配置 Agent

```csharp
builder.Services.AddLLMProvider(builder.Configuration);
builder.Services.AddToolsFromType<CustomerServiceTools>();
builder.Services.AddWindowMemory(windowSize: 20);
builder.Services.AddSafetyGuardrails(options =>
{
    options.EnableContentFilter = true;
    options.EnableSensitiveDataFilter = true;
});

builder.Services.AddReActAgent(options =>
{
    options.Name = "CustomerServiceBot";
    options.Instructions = """
        你是一个专业友好的电商客服助手:
        1. 态度亲切，语言简洁专业
        2. 订单问题请先确认订单号
        3. 退换货需要用户确认
        4. 无法解决时告知转人工处理
        5. 保护用户隐私
        """;
    options.MaxSteps = 5;
});
```

### 实现 API 端点

```csharp
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IAgent _agent;
    private readonly IConversationMemory _memory;

    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        await _memory.AddMessageAsync(new ConversationMessage
        {
            Role = "user",
            Content = request.Message,
        });

        var response = await _agent.RunAsync(request.Message);
        
        await _memory.AddMessageAsync(new ConversationMessage
        {
            Role = "assistant",
            Content = response.FinalAnswer,
        });

        return Ok(new ChatResponse
        {
            Message = response.FinalAnswer,
            SessionId = request.SessionId,
        });
    }
}
```

### 添加 RAG 知识库

```csharp
builder.Services.AddOpenAIEmbedding(builder.Configuration);
builder.Services.AddQdrantVectorStore(options =>
{
    options.Host = "localhost";
    options.Port = 6334;
    options.CollectionName = "product_knowledge";
});
builder.Services.AddRetriever();

[FunctionTool("搜索知识库获取产品信息", Category = "Knowledge")]
public async Task<string> SearchKnowledge(string query)
{
    var results = await _retriever.RetrieveAsync(query, topK: 3);
    return results.Any() 
        ? string.Join("\n\n", results.Select(r => r.Text))
        : "未找到相关信息";
}
```

### 测试对话

```
用户: 我想查一下订单 ORD123456 的状态
助手: 订单号: ORD123456
      状态: 已发货
      商品: iPhone 15 Pro
      总金额: ¥8999.00
      预计明天送达，请注意查收！

用户: 我想退货，买错型号了
助手: 好的，请问您要退货的订单号是多少？

用户: ORD123456
助手: 退换货申请已提交，工单号: TK789012，预计 1-3 个工作日处理。
```
