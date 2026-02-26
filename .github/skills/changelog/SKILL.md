---
description: "CHANGELOG management and documentation generation for Dawning.Agents: format, DocFX, release notes, API docs. Trigger: changelog, 变更日志, release notes, DocFX, 文档生成, documentation, 写文档, API docs"
---

> **Skill 使用日志**：使用本 skill 后，在 `/memories/session/skill-log.md` 追加一行：`- {时间} changelog — {触发原因}`

# Changelog & Documentation

## CHANGELOG.md Format

All entries use Chinese with emoji section headers. Maintain this structure:

```markdown
## [Unreleased] - YYYY-MM-DD

### 🏗️ Feature Category Name

#### Phase Name ✅
- **新增**: description
- **删除**: description
- **DI 变更**: `AddXxx()` method changes
- **重构**: description

### 📖 文档
- description

### 🧪 测试
- 新增 N 个 XxxTests
- 总测试数: **N** (old → new)

### 🐛 修复
- description

### 📋 示例更新
- description
```

### Rules

1. Date format: `YYYY-MM-DD`
2. Use `[Unreleased]` until a version is tagged; then rename to `[x.y.z] - YYYY-MM-DD`
3. Always include total test count with delta: `**2,046** (1,896 → 2,046)`
4. Code references in backticks: `` `ClassName` ``, `` `AddMethod()` ``
5. Section emojis: 🏗️ features, 📖 docs, 🧪 tests, 🐛 fixes, 📋 samples
6. Phase labels end with ✅ when complete
7. Multiple `[Unreleased]` sections are ok during development (each dated)

### Adding Entries

When completing a task, append to the most recent `[Unreleased]` section. Include:
- What was added/removed/changed
- DI registration changes (if any)
- Test count delta
- Breaking changes (if any)

## DocFX Documentation (`docs/`)

### Build Docs

```bash
dotnet tool restore
dotnet docfx docs/docfx.json
```

Generates static site from:
- `docs/articles/` — hand-written guides
- `docs/api/` — auto-generated from XML comments (`<summary>`, `<param>`, `<returns>`)
- `docs/guides/` — production guides, tutorials

### CI/CD (`.github/workflows/docs.yml`)

Triggered on push to `main` when `docs/**` changes. Builds with DocFX → deploys to GitHub Pages.

### XML Doc Requirements

All public APIs must have XML documentation:

```csharp
/// <summary>
/// Runs the agent with the given input.
/// </summary>
/// <param name="input">User input message.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>Agent response containing the result and metadata.</returns>
/// <exception cref="BudgetExceededException">Thrown when cost exceeds MaxCostPerRun.</exception>
public Task<AgentResponse> RunAsync(string input, CancellationToken cancellationToken = default);
```

## Release Notes

When tagging a release:

1. Move all `[Unreleased]` content to `[x.y.z] - YYYY-MM-DD`
2. GitHub Actions auto-generates release notes from commits (via `softprops/action-gh-release`)
3. Optionally paste curated CHANGELOG excerpt into GitHub Release description

## Key Documentation Files

| File | Purpose |
|------|---------|
| `README.md` | Project overview, installation, quick start |
| `CHANGELOG.md` | Detailed change history |
| `LEARNING_PLAN.md` | 12-week learning curriculum |
| `docs/QUICKSTART.md` | 5-minute setup guide |
| `docs/API_REFERENCE.md` | Full API reference |
| `docs/guides/production-best-practices.md` | Production deployment guide |
| `docs/guides/security-hardening.md` | Security hardening guide |
| `docs/guides/performance-tuning.md` | Performance tuning guide |
