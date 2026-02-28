---
description: |
  Use when: Writing or updating CHANGELOG.md, generating release notes, or managing DocFX documentation
  Don't use when: Bumping version numbers or publishing NuGet packages (use nuget-release); writing code (use code-update); reviewing code (use code-review); writing general documentation (use markdown)
  Inputs: List of changes since last release, or request to update changelog
  Outputs: Updated CHANGELOG.md entry following Keep a Changelog format
  Success criteria: CHANGELOG.md has a properly formatted entry with categorized changes
---

# Changelog & Documentation Skill

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

