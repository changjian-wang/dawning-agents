---
description: |
  Use when: Checking for outdated NuGet packages, upgrading dependencies, assessing breaking changes, or patching CVE vulnerabilities
  Don't use when: Publishing packages (use nuget-release); fixing compile errors from upgrades (use build-project); writing changelog entries (use changelog); general code changes unrelated to dependencies (use code-update)
  Inputs: Request to check/update dependencies, or CVE advisory
  Outputs: Updated .csproj/Directory.Build.props with new versions, risk assessment
  Success criteria: All dependencies updated, solution builds, all tests pass
---

# Dependency Update Skill

## 检查过期依赖

```bash
cd /path/to/dawning-agents

# 列出所有过期包
dotnet list package --outdated

# 仅检查有安全漏洞的包
dotnet list package --vulnerable

# 检查特定项目
dotnet list src/Dawning.Agents.Core/Dawning.Agents.Core.csproj package --outdated
```

## 升级策略

### 风险分层

| 级别 | 类型 | 策略 |
|------|------|------|
| 低 | Patch (x.y.Z) | 直接升级，跑测试确认 |
| 中 | Minor (x.Y.0) | 查阅 release notes，跑测试 |
| 高 | Major (X.0.0) | 评估 breaking changes，可能需要代码改动 |
| 紧急 | CVE 修复 | 立即升级，即使是 major |

### 核心依赖清单

| 包 | 用途 | 升级注意 |
|---|------|---------|
| `Microsoft.Extensions.*` | DI, Logging, Options | 跟随 .NET 大版本 |
| `System.Text.Json` | JSON 序列化 | API 变更较少 |
| `Moq` | 测试 Mock | v5 有 breaking changes |
| `FluentAssertions` | 测试断言 | v7 有 breaking changes |
| `BenchmarkDotNet` | 性能基准 | 通常安全升级 |
| `OpenTelemetry.*` | 可观测性 | API 频繁变动 |
| `StackExchange.Redis` | Redis 客户端 | Major 版本需注意 |
| `Meziantou.Analyzer` | 代码分析 | 新规则可能导致编译警告→错误 |

## 升级流程

### 1. 信息收集

```bash
# 过期包清单
dotnet list package --outdated --format json

# 漏洞包清单
dotnet list package --vulnerable --format json
```

### 2. 评估 Breaking Changes

对于每个要升级的包：
1. 查阅 GitHub releases / changelog
2. 识别 API 变更（删除、重命名、签名变更）
3. 评估对 Dawning.Agents 公开 API 的级联影响

### 3. 执行升级

```bash
# 升级单个包（所有引用它的项目）
dotnet add package PackageName --version X.Y.Z

# 如果包在 Directory.Build.props 中集中管理，直接编辑版本号
```

### 4. 验证

```bash
dotnet build --nologo -v q          # 编译通过
dotnet test --nologo                # 全部测试通过
~/.dotnet/tools/csharpier format .  # 格式化（新 API 可能改变代码布局）
```

### 5. 提交

```bash
git add -A
git commit -m "chore(deps): update PackageName to vX.Y.Z"
```

## Directory.Build.props 集中管理

当前版本管理在 `Directory.Build.props`：

```xml
<Version>0.1.0-preview.1</Version>
```

共享依赖版本建议在 `Directory.Build.props` 中统一声明：

```xml
<PropertyGroup>
  <MoqVersion>4.20.72</MoqVersion>
  <FluentAssertionsVersion>7.0.0</FluentAssertionsVersion>
</PropertyGroup>
```

## global.json

```bash
# 检查当前 SDK 版本
cat global.json
dotnet --version

# 升级 SDK 版本
# 编辑 global.json 中的 sdk.version
```

## 安全漏洞响应

1. 收到 CVE 通知或 `dotnet list package --vulnerable` 报告
2. 确认受影响的包和版本范围
3. 查找修复版本
4. 立即升级，不等 minor/major 评估
5. 提交信息中注明 CVE 编号：`fix(deps): patch CVE-2026-XXXX in PackageName`

