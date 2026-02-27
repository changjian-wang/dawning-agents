---
description: "Make code changes in Dawning.Agents with current patterns: DI, logging, cancellation, options, namespaces, templates. Trigger: 写代码, 改代码, implement, add service, add interface, new feature, fix bug, refactor, 实现, 重构, 新增"
---

# Code Update Skill

## 目标

按照 Dawning.Agents 编码规范实现代码变更，确保新代码能编译、符合架构、通过审查。

## 触发条件

- **关键词**：写代码, 改代码, implement, add service, add interface, new feature, fix bug, refactor, 实现, 重构, 新增
- **文件模式**：`*.cs`
- **用户意图**：实现功能、修复 bug、重构模块、新增服务/接口/Options

## 编排

- **前置**：`architecture`（确认代码放置位置）
- **后续**：`build-project` → `run-tests` → `csharpier` → `git-workflow`

## Skill 使用日志

使用本 skill 后，在 `/memories/repo/skill-usage.md` 追加一行：`- {日期} code-update — {触发原因}`

---

## Required Patterns

1. Pure DI via constructor injection
2. `ILogger<T>` with `NullLogger<T>.Instance` fallback when logger is optional
3. `CancellationToken cancellationToken = default` on async I/O methods
4. Public options implement `IValidatableOptions` with `Validate()`
5. Namespace follows subfolder path

## Namespace Convention

- `Dawning.Agents.Abstractions.{Area}`
- `Dawning.Agents.Core.{Area}`

## Templates

- `templates/interface-template.cs`
- `templates/service-template.cs`
- `templates/extensions-template.cs`
- `templates/options-template.cs`

## Post-change Workflow（必须按顺序执行）

1. `dotnet build --nologo -v q` → 0 errors, 0 warnings
2. `dotnet test --nologo -v q` → all pass
3. `~/.dotnet/tools/csharpier format .`
4. `git add + commit`（参照 git-workflow skill）

## 禁止事项（Forbidden Patterns）

- ❌ `new HttpClient()` — 必须使用 `IHttpClientFactory`
- ❌ `new XxxService()` — 所有运行时服务通过 DI 注入，禁止手动 `new`
- ❌ 静态工厂方法创建服务实例（如 `XxxService.Create()`）
- ❌ `[Obsolete]` 标注 — 开发阶段直接删除旧 API
- ❌ 在 `Abstractions/` 项目中注册实现或引用 `Core/`
- ❌ `IMapper` / AutoMapper — 使用静态 Mapper 扩展方法（`entity.ToDto()`）
- ❌ 同步 I/O（`.Result`、`.Wait()`、`.GetAwaiter().GetResult()`）
- ❌ 忘记 `CancellationToken` 参数传递（async 链中每一层都要传）

## 验收场景

- **输入**："新增一个 INotificationService 接口和实现"
- **预期**：agent 在 Abstractions 下创建接口，在 Core 下创建实现，包含 DI 扩展方法，通过构建
- **上次验证**：2026-02-27
