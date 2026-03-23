---
description: |
  Use when: Reviewing code for architecture compliance, naming conventions, DI patterns, forbidden patterns, or best practices
  Don't use when:
    - Writing new code or fixing bugs directly (use code-update)
    - Running tests (use run-tests)
    - Performing deep line-by-line audit (use deep-audit)
    - Checking for security vulnerabilities specifically (use security-audit)
  Inputs: Code files or PR diff to review
  Outputs: Structured review report with findings categorized by severity
  Success criteria: All findings documented with severity, location, and recommended fix
---

# Code Review Skill

## 交叉检查

审查时**必须**对照以下 skill 的关键规则：

- **`architecture`**：模块边界（Abstractions 不引用 Core）、命名空间规则
- **`code-update`**：禁止事项清单（8 条 ❌ 规则）
- **`csharpier`**：格式化规范

---

## Review Checklist

### 1. Architecture and DI

- Service contracts in `Abstractions`, implementations in `Core`/provider modules
- No static factories for runtime services
- Constructor injection used consistently
- Async methods include `CancellationToken`

### 2. Tool Interfaces (ISP)

- Read-only consumers depend on `IToolReader`
- Registration/setup code depends on `IToolRegistrar`
- `IToolRegistry` used only when both read/write are needed
- Session-scoped dynamic tools managed via `IToolSession` (Scoped lifetime)
- Persistent tool storage via `IToolStore` (Singleton, file-system backed)
- Script execution via `IToolSandbox`; `ToolSandboxOptions.Runtime` matches `EphemeralToolDefinition.Runtime`
- `IToolSession.UpdateTool` used for in-place skill revision (not Remove+Create)
- `EphemeralToolMetadata` behavioral fields populated (WhenToUse, Limitations, FailurePatterns)

### 3. Options and Validation

- Public options implement `IValidatableOptions`
- `Validate()` covers required fields and range checks
- Tests include valid/invalid cases for `Validate()`

### 4. Cost/Budget Safety

- Cost-sensitive flows use `ICostTracker` appropriately
- Budget overflow handled via `BudgetExceededException`

### 5. Quality Gates

- Public APIs have XML docs
- Null handling uses guard clauses
- Logging is structured and meaningful
- Unit tests added for behavior changes
- CSharpier formatting passes

### 6. 禁止事项检查（Forbidden Patterns）

- ❌ `new HttpClient()` — 必须 `IHttpClientFactory`
- ❌ `new XxxService()` — 必须 DI 注入
- ❌ 静态工厂创建服务
- ❌ `[Obsolete]` 标注
- ❌ `Abstractions/` 引用 `Core/`
- ❌ `IMapper` / AutoMapper
- ❌ 同步 I/O（`.Result`、`.Wait()`）
- ❌ 遗漏 `CancellationToken` 传递
- ❌ event `?.Invoke()` 无 try/catch — 订阅者异常会崩溃调用方
- ❌ Timer/回调无 try/catch — 异常会永久停止 Timer
- ❌ public 方法缺少 `ArgumentNullException.ThrowIfNull()` — 同类中部分有部分无
- ❌ Singleton 持有 Scoped 依赖（captive dependency）
- ❌ `Options.Create()` 绕过 Options 管线
- ❌ `ConcurrentDictionary.GetOrAdd` 在 lock 外获取引用后再 lock 操作 — TOCTOU 竞态

### 7. 线程安全与原子性

- 多字段一致性读取是否在同一个 lock 内（128-bit struct/decimal 字段有 torn read 风险）
- `Interlocked` 和 `lock` 混用是否正确
- `volatile` 是否用于 `_disposed` 等跨线程标志
- `ConcurrentDictionary` 操作是否有 TOCTOU 竞态

### 8. DI 生命周期

- Singleton 是否直接解析 Scoped 服务（应创建 scope）
- `TryAddSingleton` vs `TryAddScoped` 是否匹配消费者的生命周期
- `Options.Create()` 是否绕过了 `IOptions<T>` / `IValidateOptions<T>` 管线

### 9. 数值边界

- `ulong` → `int` 是否有溢出保护
- `Convert.ToInt32()` / `Convert.ToDouble()` 是否处理了 `OverflowException`
- LINQ `.Sum()` 对大量数据是否有 `int` 溢出风险
- 模数运算 `%` 对负数是否安全

### 10. Channel/TCS 生命周期

- Channel 关闭后写入是否处理了 `ChannelClosedException`
- 挂起的 `TaskCompletionSource` 在 Dispose 时是否 `TrySetCanceled()`
- Singleton 中的 `ConcurrentDictionary<string, TCS>` 是否有清理机制

## Review Output Format

```markdown
## Findings
1. [Severity] file:line - issue

## Open Questions
1. ...

## Summary
- ...
```

Order findings by severity: correctness > security > performance > maintainability.

