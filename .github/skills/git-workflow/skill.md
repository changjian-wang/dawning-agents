---
name: git-workflow
description: Git workflow for Dawning.Agents project
---

# Git Workflow Skill

Manage Git operations for the Dawning.Agents project.

## When to Use

- When asked to commit changes
- When asked to check status
- When asked about git history
- When asked to push changes

## Common Git Commands

### Check Status

```powershell
cd C:\github\dawning-agents
git status
```

### View Changes

```powershell
git diff
git diff --staged
```

### Stage and Commit

```powershell
git add -A
git commit -m "type(scope): description"
```

### Push Changes

```powershell
git push
```

### View History

```powershell
git log --oneline -20
```

## Commit Message Convention

### Format

```
type(scope): description

[optional body]
```

### Types

| Type | Usage |
|------|-------|
| `feat` | New feature |
| `fix` | Bug fix |
| `docs` | Documentation only |
| `style` | Formatting, no code change |
| `refactor` | Code change without feature/fix |
| `test` | Adding/updating tests |
| `chore` | Maintenance tasks |
| `perf` | Performance improvement |

### Scopes (for this project)

- `llm` - LLM Provider related
- `agent` - Agent core
- `tools` - Tools system
- `memory` - Memory system
- `humanloop` - Human-in-the-loop
- `demo` - Demo application
- `test` - Test related

### Examples

```
feat(tools): add CodeReviewTool for automated code review
fix(agent): handle null response from LLM provider
docs(readme): update installation instructions
refactor(humanloop): move ConsoleInteractionHandler to Demo
test(memory): add WindowMemory edge case tests
chore: add .vs/ to gitignore
```

## Workflow Before Commit

1. **Build**: `dotnet build --nologo -v q`
2. **Test**: `dotnet test --nologo`
3. **Check status**: `git status`
4. **Review changes**: `git diff`
5. **Stage**: `git add -A`
6. **Commit**: `git commit -m "type(scope): description"`
7. **Push**: `git push`

## Branch Naming (if needed)

```
feature/add-code-review-skill
fix/null-pointer-in-agent
refactor/simplify-memory-api
```
