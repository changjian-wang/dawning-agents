# Skills 改进计划

> 当前状态：16 个 skills + 2 个 agents，覆盖开发全流程
> 创建时间：2026-02-27
> 最后更新：2026-03-11（R38 审计后全面更新）

## 现有 Skills 清单

| Skill | 用途 | 使用频率 | 状态 |
|-------|------|---------|------|
| architecture | 项目结构、模块边界、核心接口 | 中 | ✅ 完备 |
| code-update | 编码模式、模板、禁止事项、安全编码模式 | 高 | ✅ R38 更新 |
| code-review | 代码审查（10 维度 + 禁止事项 13 条） | 中 | ✅ R38 更新 |
| deep-audit | 深度代码审计（18 维度、双模式、扫描角度库） | 高 | ✅ R38 重写 |
| build-project | 构建命令、编译错误 | 高 | ✅ 完备 |
| run-tests | 测试执行、覆盖率（2225 tests） | 高 | ✅ 完备 |
| csharpier | 格式化规则 | 高 | ✅ R38 修正命令 |
| git-workflow | 提交规范、scope 列表、pre-commit | 高 | ✅ R38 补充 scope |
| markdown | Markdown/XML 文档规范 | 低 | ✅ 完备 |
| nuget-release | 版本管理、打包发布 | 低 | ✅ 完备 |
| deployment | Docker、K8s、可观测性 | 低 | ✅ 完备 |
| changelog | CHANGELOG 格式、release notes | 低 | ✅ 完备 |
| troubleshooting | 构建/测试/部署排错、analyzer 代码 | 中 | ✅ R38 补充 |
| security-audit | OWASP Top 10、依赖漏洞、密钥泄露 | 低 | ✅ 完备 |
| performance | BenchmarkDotNet、热路径、内存分配 | 低 | ✅ 完备 |
| dependency-update | NuGet 依赖升级、CVE 修补 | 低 | ✅ 完备 |

## Agents

| Agent | 用途 | 状态 |
|-------|------|------|
| @auditor | 全量审计 → 修复 → 构建 → 测试 → 提交 | ✅ R38 更新（18 维度 + 增量模式） |
| @releaser | 变更日志 → 版本号 → 构建 → 测试 → 打包 → 标签 | ✅ 完备 |

---

## Phase 1: 高价值（直接提升效率） ✅ 已完成

### 1.1 Skill 编排链 ✅

**已实现**：`copilot-instructions.md` 定义了 3 条标准工作流链，各 skill 的 post-change workflow 段落已声明后续步骤。

### 1.2 Skill 质检回路 ✅

**已实现**：`code-review/SKILL.md` 开头声明了交叉检查引用（architecture + code-update + csharpier）。

### 1.3 Skill 触发条件精细化

**状态**：部分完成。`copilot-instructions.md` 的 Skill 索引表已有触发关键词，但各 SKILL.md 的 YAML frontmatter 中尚未增加结构化触发规则。

**优先级**：低 — 当前自然语言触发已足够可靠。

**工作量**：修改 13 个 SKILL.md + copilot-instructions.md



---

## Phase 2: 中等价值（扩展覆盖面） ✅ 已完成

### 2.1 缺失 Skill 补充 ✅

全部 3 个新 Skill 已创建：

| 新 Skill | 状态 | 创建时间 |
|----------|------|----------|
| `security-audit` | ✅ 完成 | 2026-02 |
| `performance` | ✅ 完成 | 2026-02 |
| `dependency-update` | ✅ 完成 | 2026-02 |

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

### 3.1 Custom Agent Mode ✅

**已实现**：创建了 2 个 agent 文件：
- `.github/agents/auditor.agent.md` — 组合 deep-audit + code-update + build + test + format + git
- `.github/agents/releaser.agent.md` — 组合 changelog + nuget-release + build + test + format + git

用户输入 `@auditor 全面审计` 或 `@releaser 发布 0.2.0` 即可触发。

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
