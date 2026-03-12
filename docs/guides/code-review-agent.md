# 🔍 构建代码审查 Agent

> 使用 Dawning.Agents 实现自动化代码审查助手

---

## 📋 场景描述

构建一个代码审查 Agent，支持：
- 自动审查 Pull Request
- 检测代码问题和安全漏洞
- 提供改进建议
- 生成审查报告

---

## 🏗️ 架构设计

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

---

## 💻 实现步骤

### 1. 定义代码审查工具

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

    [FunctionTool("获取文件内容", Category = "Git")]
    public async Task<string> GetFileContent(string owner, string repo, string path, string @ref)
    {
        var content = await _github.Repository.Content.GetRawContent(owner, repo, path, @ref);
        return content;
    }

    [FunctionTool("检查代码风格问题", Category = "Analysis")]
    public async Task<string> CheckCodeStyle(string code, string language)
    {
        var issues = new List<string>();
        
        // 检查常见问题
        if (language == "csharp")
        {
            if (code.Contains("var ") && code.Contains(" = new "))
            {
                issues.Add("建议：使用目标类型 new 表达式 (target-typed new)");
            }
            
            if (Regex.IsMatch(code, @"catch\s*\(\s*Exception\s+\w+\s*\)\s*\{"))
            {
                issues.Add("警告：避免捕获通用 Exception，应该捕获具体异常类型");
            }
            
            if (code.Contains("TODO") || code.Contains("FIXME"))
            {
                issues.Add("提示：代码中存在 TODO/FIXME 注释，请确认是否需要处理");
            }
        }
        
        return issues.Any() 
            ? string.Join("\n", issues) 
            : "未发现明显的代码风格问题";
    }

    [FunctionTool("检查安全漏洞", Category = "Security")]
    public async Task<string> CheckSecurity(string code, string language)
    {
        var vulnerabilities = new List<string>();
        
        // SQL 注入检测
        if (Regex.IsMatch(code, @"\".*\+.*\".*SELECT|INSERT|UPDATE|DELETE", RegexOptions.IgnoreCase))
        {
            vulnerabilities.Add("🔴 高危：可能存在 SQL 注入漏洞，建议使用参数化查询");
        }
        
        // 硬编码密钥检测
        if (Regex.IsMatch(code, @"(password|secret|key|token)\s*=\s*[\"'][^""']+[\"']", RegexOptions.IgnoreCase))
        {
            vulnerabilities.Add("🔴 高危：检测到硬编码的敏感信息，应使用配置或环境变量");
        }
        
        // XSS 检测
        if (code.Contains("innerHTML") || code.Contains("dangerouslySetInnerHTML"))
        {
            vulnerabilities.Add("🟡 中危：使用 innerHTML 可能导致 XSS 漏洞");
        }
        
        return vulnerabilities.Any() 
            ? string.Join("\n", vulnerabilities) 
            : "未发现明显的安全漏洞";
    }

    [FunctionTool("分析代码复杂度", Category = "Analysis")]
    public string AnalyzeComplexity(string code)
    {
        // 简单的复杂度分析
        var lines = code.Split('\n').Length;
        var ifCount = Regex.Matches(code, @"\bif\b").Count;
        var loopCount = Regex.Matches(code, @"\b(for|while|foreach)\b").Count;
        var methodCount = Regex.Matches(code, @"\b(public|private|protected|internal)\s+(static\s+)?(async\s+)?[\w<>]+\s+\w+\s*\(").Count;
        
        var complexity = ifCount + loopCount * 2;
        
        return $"""
            代码统计:
            - 总行数: {lines}
            - 方法数: {methodCount}
            - 条件分支: {ifCount}
            - 循环: {loopCount}
            - 圈复杂度估算: {complexity}
            
            {(complexity > 10 ? "⚠️ 复杂度较高，建议拆分方法" : "✅ 复杂度正常")}
            """;
    }

    [FunctionTool("添加 PR 评论", Category = "GitHub", RequiresConfirmation = true)]
    public async Task<string> AddPRComment(
        string owner, 
        string repo, 
        int prNumber, 
        string comment,
        string? path = null,
        int? line = null)
    {
        if (path != null && line != null)
        {
            await _github.PullRequest.ReviewComment.Create(owner, repo, prNumber, new PullRequestReviewCommentCreate
            {
                Body = comment,
                Path = path,
                Position = line.Value,
            });
        }
        else
        {
            await _github.Issue.Comment.Create(owner, repo, prNumber, comment);
        }
        
        return "评论已添加";
    }
}
```

### 2. 配置代码审查 Agent

```csharp
builder.Services.AddReActAgent(options =>
{
    options.Name = "CodeReviewAgent";
    options.Instructions = """
        你是一个专业的代码审查助手。请按照以下步骤审查代码:
        
        1. 首先获取 PR 的 diff 了解变更内容
        2. 对每个修改的文件进行分析:
           - 检查代码风格是否符合规范
           - 检查是否存在安全漏洞
           - 分析代码复杂度
           - 检查是否有潜在 bug
        3. 生成审查报告，包含:
           - 问题总结
           - 具体建议
           - 代码评分 (1-10)
        4. 在 PR 上添加评论
        
        审查标准:
        - 代码可读性
        - 安全性
        - 性能
        - 测试覆盖
        - 架构设计
        
        语气要求: 建设性、专业、友善
        """;
    options.MaxSteps = 10;
});
```

### 3. 定义工作流

```csharp
var reviewWorkflow = new WorkflowBuilder("CodeReviewWorkflow")
    // 获取 PR 信息
    .StartWith<ToolNode>("get_diff", tool: "GetPRDiff")
    
    // 并行分析
    .Then<ParallelNode>("analyze")
        .Branch<ToolNode>("style_check", tool: "CheckCodeStyle")
        .Branch<ToolNode>("security_check", tool: "CheckSecurity")
        .Branch<ToolNode>("complexity_check", tool: "AnalyzeComplexity")
    .EndParallel()
    
    // 生成报告
    .Then<AgentNode>("generate_report", agent: reportAgent)
    
    // 人工确认后发布
    .Then<HumanApprovalNode>("approve", prompt: "确认发布审查报告？")
    
    // 添加评论
    .Then<ToolNode>("post_comment", tool: "AddPRComment")
    
    .Build();
```

### 4. 处理 GitHub Webhook

```csharp
[ApiController]
[Route("api/webhook")]
public class WebhookController : ControllerBase
{
    private readonly IWorkflow _reviewWorkflow;
    
    [HttpPost("github")]
    public async Task<IActionResult> HandleGitHubWebhook(
        [FromBody] JsonDocument payload,
        [FromHeader(Name = "X-GitHub-Event")] string eventType)
    {
        if (eventType != "pull_request")
        {
            return Ok();
        }
        
        var action = payload.RootElement.GetProperty("action").GetString();
        if (action is not ("opened" or "synchronize"))
        {
            return Ok();
        }
        
        var pr = payload.RootElement.GetProperty("pull_request");
        var prNumber = pr.GetProperty("number").GetInt32();
        var repo = payload.RootElement.GetProperty("repository");
        var owner = repo.GetProperty("owner").GetProperty("login").GetString();
        var repoName = repo.GetProperty("name").GetString();
        
        // 异步执行审查
        _ = Task.Run(async () =>
        {
            var context = new WorkflowContext
            {
                ["owner"] = owner,
                ["repo"] = repoName,
                ["prNumber"] = prNumber,
            };
            
            await _reviewWorkflow.ExecuteAsync(context);
        });
        
        return Ok();
    }
}
```

---

## 🧪 审查报告示例

```markdown
## 🔍 代码审查报告

**PR #123**: feat: 添加用户认证功能  
**审查员**: CodeReviewAgent  
**审查时间**: 2026-02-04 10:30

---

### 📊 总体评分: 7/10

### ✅ 优点
- 代码结构清晰，职责分明
- 使用了依赖注入，便于测试
- 有基本的错误处理

### ⚠️ 需要改进

#### 安全问题 (2)
1. **[高危]** `AuthService.cs:45` - 密码比较使用 `==` 而非 `ConstantTimeEquals`，存在时序攻击风险
2. **[中危]** `UserController.cs:78` - 未对用户输入进行验证

#### 代码质量 (3)
1. `AuthService.cs:23` - 方法过长 (85行)，建议拆分
2. `TokenGenerator.cs:12` - 魔术数字 `3600`，建议提取为常量
3. 缺少单元测试

### 💡 改进建议

```csharp
// 建议使用安全的密码比较
- if (hash == storedHash)
+ if (CryptographicOperations.FixedTimeEquals(
+     Encoding.UTF8.GetBytes(hash),
+     Encoding.UTF8.GetBytes(storedHash)))
```

---

**状态**: 请求修改 (Request Changes)
```

---

## 📈 集成 CI/CD

### GitHub Actions

```yaml
name: Code Review

on:
  pull_request:
    types: [opened, synchronize]

jobs:
  review:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
          
      - name: Run Code Review Agent
        run: |
          curl -X POST ${{ secrets.REVIEW_AGENT_WEBHOOK }} \
            -H "Content-Type: application/json" \
            -d '{
              "owner": "${{ github.repository_owner }}",
              "repo": "${{ github.event.repository.name }}",
              "prNumber": ${{ github.event.pull_request.number }}
            }'
```

---

## 🔧 配置选项

### appsettings.json

```json
{
  "CodeReview": {
    "EnableSecurityCheck": true,
    "EnableStyleCheck": true,
    "MaxFilesToReview": 20,
    "IgnorePatterns": [
      "*.min.js",
      "*.generated.cs",
      "**/node_modules/**"
    ],
    "SeverityThreshold": "Medium",
    "AutoApprove": false
  }
}
```

---

> 📌 **扩展阅读**: [生产最佳实践](production-best-practices.md)
