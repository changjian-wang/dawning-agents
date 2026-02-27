---
description: "CHANGELOG management and documentation generation for Dawning.Agents: format, DocFX, release notes, API docs. Trigger: changelog, 变更日志, release notes, DocFX, 文档生成, documentation, 写文档, API docs"
---

# Changelog & Documentation Skill

## 目标

管理 CHANGELOG.md 格式、DocFX 文档生成和 release notes 编写。

## 触发条件

- **关键词**：changelog, 变更日志, release notes, DocFX, 文档生成, API docs
- **文件模式**：`CHANGELOG.md`, `docs/**`, `docfx.json`
- **用户意图**：更新变更日志、生成文档、编写发布说明

## 编排

- **前置**：`git-workflow`（提交后整理变更日志）
- **后续**：`nuget-release`（发布前更新版本号）

---

## CHANGELOG.md Format

All entries use Chinese with emoji section headers:

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
4. Code references in backticks
5. Section emojis: 🏗️ features, 📖 docs, 🧪 tests, 🐛 fixes, 📋 samples
6. Phase labels end with ✅ when complete

## DocFX Documentation

### Build Docs

```bash
dotnet tool restore
dotnet docfx docs/docfx.json
```

### XML Doc Requirements

All public APIs must have XML documentation (`<summary>`, `<param>`, `<returns>`, `<exception>`).

## Release Notes

When tagging a release:

1. Move all `[Unreleased]` content to `[x.y.z] - YYYY-MM-DD`
2. GitHub Actions auto-generates release notes from commits
3. Optionally paste curated CHANGELOG excerpt into GitHub Release description

## 验收场景

- **输入**："帮我更新 CHANGELOG，这次修了 3 个线程安全 bug"
- **预期**：agent 在 `[Unreleased]` 下追加 🐛 修复条目，包含具体描述
- **上次验证**：2026-02-27
