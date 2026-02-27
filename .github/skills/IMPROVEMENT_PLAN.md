# Skills 改进计划

> 当前状态：13 个 skills，覆盖开发全流程
> 创建时间：2026-02-27

## 现有 Skills 清单

| Skill | 用途 | 使用频率 |
|-------|------|---------|
| architecture | 项目结构、模块边界、核心接口 | 中 |
| code-update | 编码模式、模板、命名空间规则 | 高 |
| code-review | 代码审查清单、质量门禁 | 中 |
| deep-audit | 逐行深度代码审计 | 高（近期） |
| build-project | 构建命令、编译错误 | 高 |
| run-tests | 测试执行、覆盖率 | 高 |
| csharpier | 格式化规则 | 高 |
| git-workflow | 提交规范、pre-commit | 高 |
| markdown | Markdown/XML 文档规范 | 低 |
| nuget-release | 版本管理、打包发布 | 低 |
| deployment | Docker、K8s、可观测性 | 低 |
| changelog | CHANGELOG 格式、release notes | 低 |
| troubleshooting | 构建/测试/部署排错 | 中 |

---

## Phase 1: 高价值（直接提升效率）

### 1.1 Skill 编排链

**目标**：让 agent 可靠地自动串联多个 skill，减少人工"继续"指令

**现状**：`copilot-instructions.md` 里有一句 `code-update → build-project → run-tests → csharpier → git-workflow`，但各 skill 内部没有声明后续步骤

**方案**：
- 在每个 SKILL.md 中增加 `## 编排` 段落，声明 `前置 skill` 和 `后续 skill`
- 定义 3 条标准工作流链：
  - **代码变更链**：`code-update → build-project → run-tests → csharpier → git-workflow`
  - **审计修复链**：`deep-audit → code-update → build-project → run-tests → csharpier → git-workflow`
  - **发布链**：`changelog → nuget-release → deployment`

**工作量**：修改 13 个 SKILL.md + copilot-instructions.md

### 1.2 Skill 质检回路

**目标**：`code-review` 自动对照 `architecture` 和 `code-update` 规则做交叉检查

**现状**：`code-review` 有自己的清单，但不会主动读取 `architecture` skill 的模块边界规则

**方案**：
- 在 `code-review/SKILL.md` 中增加引用段：列出必须交叉检查的 skill 及其关键规则
- 审查时自动读取 `architecture/SKILL.md` 中的模块边界、`code-update/SKILL.md` 中的禁止事项

**工作量**：修改 1 个 SKILL.md

### 1.3 Skill 触发条件精细化

**目标**：减少误触发和漏触发，让 agent 更精准匹配 skill

**现状**：触发全靠自然语言描述（copilot-instructions.md 的 Skill 索引表）

**方案**：
- 在每个 SKILL.md 头部增加结构化触发规则：
  ```yaml
  # 触发条件
  keywords: [构建, 编译, build, compile, dotnet build]
  file_patterns: ["*.csproj", "Directory.Build.props"]
  user_intents: [修复编译错误, 构建项目]
  ```
- 更新 copilot-instructions.md 的 Skill 索引表，增加关键词列

**工作量**：修改 13 个 SKILL.md + copilot-instructions.md

---

## Phase 2: 中等价值（扩展覆盖面）

### 2.1 缺失 Skill 补充

| 新 Skill | 用途 | 优先级 |
|----------|------|--------|
| `security-audit` | 安全专项审计：OWASP Top 10、依赖漏洞、密钥泄露 | P1 |
| `performance` | 性能分析：热路径、内存分配、Benchmark 解读 | P2 |
| `dependency-update` | 依赖升级策略：NuGet 更新、破坏性变更评估 | P3 |

**工作量**：每个新 skill 创建 1 个 SKILL.md

### 2.2 Skill 模板标准化

**目标**：统一所有 SKILL.md 的结构，方便维护和新增

**标准模板**：
```markdown
# {Skill 名称}

## 目标
一句话描述

## 触发条件
- keywords: [...]
- file_patterns: [...]
- user_intents: [...]

## 前置条件
需要先执行的 skill 或检查

## 执行步骤
1. ...
2. ...

## 编排
- 前置：{skill} 或 无
- 后续：{skill} 或 无

## 输出
执行完成后应交付什么

## 常见问题
Q&A 列表
```

**工作量**：审查 + 重构 13 个 SKILL.md

### 2.3 跨项目 Skill 复用

**目标**：dawning 和 dawning-agents 共享通用 skill，避免重复维护

**通用 Skills**（可复用）：
- csharpier、git-workflow、markdown、changelog

**专用 Skills**（不可复用）：
- architecture、code-update、deep-audit（包含项目特有知识）

**方案**：
- 通用 skill 内容存入 Copilot User Memory (`/memories/`)
- 或在两个仓库中保留独立副本，但标记来源以便同步

**工作量**：评估 + 迁移 4 个通用 skill

---

## Phase 3: 探索性（需要验证）

### 3.1 Custom Agent Mode

**目标**：创建专用 agent，一句话触发完整工作流

**方案**：
- 创建 `.github/agents/auditor.agent.md`：组合 deep-audit + code-update + build-project + run-tests + csharpier + git-workflow
- 创建 `.github/agents/releaser.agent.md`：组合 changelog + nuget-release + deployment
- 用户输入 `@auditor 全面审计` 即可触发完整流程

**前提**：VS Code Copilot 支持 `.agent.md` 自定义 agent（需验证当前版本是否支持）

**工作量**：创建 2-3 个 agent 文件 + 测试

### 3.2 Skill 自测试

**目标**：定期验证 skill 是否过时或不准确

**方案**：
- 在每个 SKILL.md 末尾增加 `## 验收场景`：
  ```markdown
  ## 验收场景
  - 输入："构建报错 CS0246 找不到类型"
  - 预期：agent 读取此 skill，按步骤排查 using/引用/包版本
  - 上次验证：2026-02-27 ✅
  ```
- 定期手动触发场景，标记通过/失败

**工作量**：每个 SKILL.md 增加 1 段

---

## 执行优先级建议

| 顺序 | 项目 | 状态 |
|------|------|------|
| 1 | 2.2 模板标准化 | ✅ 完成 — 13 个 SKILL.md 统一格式 |
| 2 | 1.3 触发条件精细化 | ✅ 完成 — 每个 skill 增加结构化触发条件 |
| 3 | 1.1 编排链 | ✅ 完成 — 每个 skill 声明前置/后续 |
| 4 | 2.1 缺失 Skill (security-audit) | ✅ 完成 — 新增 security-audit skill |
| 5 | 1.2 质检回路 | ✅ 完成 — code-review 增加交叉检查段 |
| 6 | 3.1 Custom Agent | ✅ 完成 — auditor.agent.md + releaser.agent.md |
| 7 | 3.2 Skill 自测试 | ✅ 完成 — 每个 skill 增加验收场景 |
| — | copilot-instructions.md | ✅ 更新 — 新增 skill 索引、编排链、Agent 表 |
| — | 2.3 跨项目复用 | ✅ 完成 — dawning 项目新增 4 个 skills (git-workflow, markdown, build-project, changelog) |
| — | 2.2 模板 — 更多新 skill | ✅ 完成 — performance, dependency-update |
