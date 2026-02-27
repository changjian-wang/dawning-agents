---
description: "Security-focused audit for Dawning.Agents: OWASP Top 10, dependency vulnerabilities, secrets leakage, input validation, injection attacks. Trigger: 安全审计, security audit, security review, OWASP, 漏洞, vulnerability, 注入, injection, 密钥泄露, secrets, CVE"
---

# Security Audit Skill

## 目标

对 Dawning.Agents 进行安全专项审计，聚焦 OWASP Top 10、依赖漏洞和安全最佳实践。

## 触发条件

- **关键词**：安全审计, security audit, security review, OWASP, 漏洞, vulnerability, 注入, injection, 密钥泄露, secrets, CVE
- **文件模式**：`*.cs`, `*.csproj`, `appsettings*.json`, `docker-compose*.yml`
- **用户意图**：安全审查、漏洞检测、密钥泄露检查、依赖安全评估

## 编排

- **前置**：无
- **后续**：`code-update` → `build-project` → `run-tests` → `git-workflow`

## Skill 使用日志

使用本 skill 后，在 `/memories/repo/skill-usage.md` 追加一行：`- {日期} security-audit — {触发原因}`

---

## 审计维度（8 个）

### 1. 注入攻击

| 检查项 | 说明 |
|--------|------|
| SQL 注入 | 检查是否有字符串拼接 SQL（应使用参数化查询） |
| GraphQL 注入 | Weaviate 等使用 GraphQL 的组件，检查变量拼接 |
| Command 注入 | bash/process 工具是否过滤用户输入 |
| LDAP/XPath 注入 | 如有目录服务集成 |
| Log 注入 | 用户输入直接写入日志可导致日志伪造 |

### 2. 敏感信息泄露

| 检查项 | 说明 |
|--------|------|
| API Key 硬编码 | 搜索 `apikey`, `api_key`, `secret`, `password`, `token` 等模式 |
| 配置文件泄露 | `appsettings.json` 中是否有真实密钥 |
| 日志泄露 | 日志中是否打印了 API Key、Bearer Token、用户密码 |
| 异常信息泄露 | catch 块中是否将内部异常细节返回给调用方 |
| Git 历史泄露 | `.gitignore` 是否遗漏了敏感文件 |

### 3. 认证与授权

| 检查项 | 说明 |
|--------|------|
| API 密钥验证 | LLM Provider 的 API Key 是否在 Options.Validate() 中校验 |
| 权限边界 | MCP 工具是否有权限控制（文件系统、bash 执行） |
| Tool Approval | 危险工具是否启用了 `IToolApprovalHandler` |

### 4. 输入验证

| 检查项 | 说明 |
|--------|------|
| Options 校验 | 所有 public Options 类是否实现 `IValidatableOptions` |
| 参数守卫 | 公开方法是否有 `ArgumentNullException.ThrowIfNull()` |
| 路径穿越 | 文件工具是否校验路径不超出允许范围 |
| 大小限制 | 上传/输入是否有大小限制防止 DoS |

### 5. 依赖安全

| 检查项 | 说明 |
|--------|------|
| 已知漏洞 | `dotnet list package --vulnerable` |
| 过期依赖 | `dotnet list package --outdated` |
| 不安全包源 | `nuget.config` 是否只使用 HTTPS 源 |

### 6. 加密与传输

| 检查项 | 说明 |
|--------|------|
| TLS/HTTPS | HTTP 连接是否强制 HTTPS（特别是 LLM API 调用） |
| 弱哈希 | 是否使用 MD5/SHA1 做安全相关操作（非安全场景可接受） |
| 随机数 | 是否使用 `Random` 而非 `RandomNumberGenerator` 做安全相关随机 |

### 7. 资源安全

| 检查项 | 说明 |
|--------|------|
| DoS 防护 | 是否有速率限制、超时保护、最大迭代次数限制 |
| 成本控制 | `ICostTracker` + `BudgetExceededException` 是否覆盖所有 LLM 调用路径 |
| 资源释放 | `IAsyncDisposable` 实现是否完整（SemaphoreSlim、HttpClient 等） |

### 8. 安全配置

| 检查项 | 说明 |
|--------|------|
| 默认配置 | 默认配置是否安全（如 `EnableDangerousTools = false`） |
| 环境隔离 | Development/Production 配置是否正确隔离 |
| Docker 安全 | 容器是否以非 root 用户运行 |

## 执行流程

### Step 1: 依赖扫描

```bash
dotnet list package --vulnerable
dotnet list package --outdated
```

### Step 2: 密钥搜索

```bash
grep -rn "apikey\|api_key\|secret\|password\|token\|bearer" src/ --include="*.cs" -i
grep -rn "apikey\|api_key\|secret\|password" appsettings*.json samples/ -i
```

### Step 3: 代码审查

按 8 个维度逐文件检查（优先检查以下高风险文件）：

- `**/Tools/**` — 所有工具实现（bash、文件系统）
- `**/MCP/**` — MCP 协议（外部输入入口）
- `**/Providers/**` — LLM Provider（API Key、网络通信）
- `**/Options/**` — 配置类（Validate 覆盖率）
- `appsettings*.json` — 配置文件

### Step 4: 输出报告

```markdown
# 安全审计报告 - {日期}

## 概要
- 扫描范围：N 个文件
- 发现总数：N（CRITICAL: N, HIGH: N, MEDIUM: N, LOW: N）

## CRITICAL（必须立即修复）
| # | 维度 | 文件 | 行 | 描述 | OWASP 分类 | 修复建议 |

## HIGH / MEDIUM / LOW
（同上格式）

## 依赖漏洞
| 包名 | 当前版本 | 漏洞 | 修复版本 |

## 安全最佳实践合规
| 检查项 | 状态 | 备注 |
```

## 验收场景

- **输入**："对项目做一次安全审计"
- **预期**：agent 执行依赖扫描 + 密钥搜索 + 代码审查，输出结构化安全报告
- **上次验证**：2026-02-27
