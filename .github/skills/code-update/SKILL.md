---
description: |
  Use when: Writing new code, implementing features, fixing bugs, refactoring, adding services/interfaces, or applying required DI/logging/cancellation patterns
  Don't use when:
    - Reviewing code without changes (use code-review)
    - Building the solution (use build-project)
    - Running tests (use run-tests)
    - Formatting code (use csharpier)
    - Committing changes (use git-workflow)
  Inputs: Feature request, bug report, or refactoring goal
  Outputs: Modified or new .cs files following all project patterns and conventions
  Success criteria: Code compiles, follows DI patterns, uses correct namespaces, no forbidden patterns
---

# Code Update Skill

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

- `templates/interface-template.cs.template`
- `templates/service-template.cs.template`
- `templates/extensions-template.cs.template`
- `templates/options-template.cs.template`

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

