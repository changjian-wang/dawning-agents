---
description: "Markdown formatting rules and conventions for Dawning.Agents documentation. Trigger: markdown, 写文档, README, API docs, documentation, XML docs, 文档格式, 注释规范"
---

# Markdown Formatting Skill

## 目标

定义项目中所有 Markdown 文档和 XML 注释的格式化规范。

## 触发条件

- **关键词**：markdown, 写文档, README, API docs, documentation, XML docs, 文档格式, 注释规范
- **文件模式**：`*.md`, `docs/**`
- **用户意图**：编写文档、格式化 Markdown、编写 XML 注释

## 编排

- **前置**：无
- **后续**：`changelog`（文档需要记录到变更日志时）

---

## Core Rules

1. **Headings** — blank lines before and after
2. **Code Blocks** — always specify language (```csharp, ```bash, ```json)
3. **Lists** — consistent markers (- or 1.), blank line for nested content
4. **Tables** — aligned columns
5. **Links** — descriptive text, not "click here"
6. **Emphasis** — use sparingly, `code` for inline code references
7. **Line Length** — wrap at ~100 characters
8. **Inline Code** — backticks for class/method/variable names
9. **Blockquotes** — for notes and warnings
10. **GitHub Alerts** — `> [!NOTE]`, `> [!WARNING]`

## XML Documentation

```csharp
/// <summary>
/// Gets user by ID.
/// </summary>
/// <param name="id">The user's unique identifier.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>The user DTO or null if not found.</returns>
/// <exception cref="ArgumentException">Thrown when id is empty.</exception>
public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
```

## Key Documentation Files

| File | Purpose |
|------|---------|
| `README.md` | Project overview, installation, quick start |
| `CHANGELOG.md` | Detailed change history |
| `docs/QUICKSTART.md` | 5-minute setup guide |
| `docs/API_REFERENCE.md` | Full API reference |

## 验收场景

- **输入**："帮我写一段 XML 注释"
- **预期**：agent 生成包含 summary、param、returns、exception 的完整 XML 注释
- **上次验证**：2026-02-27
