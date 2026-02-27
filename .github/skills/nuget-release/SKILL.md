---
description: "NuGet release workflow for Dawning.Agents: versioning, packing, publishing, CI/CD. Trigger: nuget, 发布, release, publish, pack, version, 版本, 打包, tag"
---

# NuGet Release Skill

## 目标

管理 NuGet 包版本、打包、发布和 CI/CD 发布流程。

## 触发条件

- **关键词**：nuget, 发布, release, publish, pack, version, 版本, 打包, tag
- **文件模式**：`Directory.Build.props`, `*.nupkg`, `scripts/pack.ps1`
- **用户意图**：发布 NuGet 包、管理版本号、执行打包流程

## 编排

- **前置**：`changelog`（更新变更日志后发布）
- **后续**：`deployment`（发布后部署）

## Skill 使用日志

使用本 skill 后，在 `/memories/repo/skill-usage.md` 追加一行：`- {日期} nuget-release — {触发原因}`

---

## Version Management

Version is centralized in `Directory.Build.props`:

```xml
<Version>0.1.0-preview.1</Version>
```

### Versioning Scheme

- **Pre-release**: `0.1.0-preview.1`, `0.2.0-alpha.1`
- **Stable**: `1.0.0`, `1.1.0`, `1.0.1`
- Major = breaking API, Minor = new features, Patch = bug fixes

## Published Packages (7)

Dawning.Agents.Abstractions, Core, OpenAI, Azure, Redis, Qdrant, Pinecone

## Local Pack

```powershell
./scripts/pack.ps1 -Version 0.2.0-preview.1
```

## CI/CD Release

```bash
git tag v0.2.0 && git push origin v0.2.0
```

Workflow: extract version → update Directory.Build.props → build → test → pack → push to NuGet.org → create GitHub Release.

## Pre-release Checklist

1. CHANGELOG.md — move `[Unreleased]` to `[x.y.z]`
2. Build → 0 errors, 0 warnings
3. Test → all pass
4. Format → CSharpier
5. Version → update Directory.Build.props
6. Commit → `chore(release): bump version to x.y.z`
7. Tag → `git tag vx.y.z && git push origin vx.y.z`

## 验收场景

- **输入**："发布 0.2.0-preview.1 版本"
- **预期**：agent 按清单执行：更新版本号、构建、测试、打包、创建 tag
- **上次验证**：2026-02-27
