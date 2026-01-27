---
name: git-workflow
description: >
  Git operations for Dawning.Agents with conventional commit messages.
  Use when asked to "commit", "push", "check status", "view history",
  "stage changes", or "create branch".
---

# Git Workflow Skill

## What This Skill Does

Manages Git operations for the Dawning.Agents project with conventional commit messages.

## When to Use

- "Commit my changes"
- "Push to remote"
- "Check git status"
- "View history"
- "What changed?"
- "Create a branch"

## Common Commands

### Quick Reference

| Command | Purpose |
|---------|---------|
| `git status` | View changes |
| `git diff` | View unstaged changes |
| `git add -A` | Stage all changes |
| `git commit -m "..."` | Commit |
| `git push` | Push to remote |
| `git log --oneline -10` | Recent history |

### Full Workflow

```powershell
cd C:\github\dawning-agents

# 1. Check status
git status

# 2. Review changes
git diff

# 3. Stage changes
git add -A

# 4. Commit
git commit -m "type(scope): description"

# 5. Push
git push
```

## Commit Message Convention

### Format

```
type(scope): description

[optional body]
```

### Types

| Type | When to Use |
|------|-------------|
| `feat` | New feature |
| `fix` | Bug fix |
| `docs` | Documentation only |
| `style` | Formatting, no code change |
| `refactor` | Code change without feature/fix |
| `test` | Adding/updating tests |
| `chore` | Maintenance tasks |
| `perf` | Performance improvement |

### Scopes for This Project

| Scope | Area |
|-------|------|
| `llm` | LLM Provider |
| `agent` | Agent core |
| `tools` | Tools system |
| `memory` | Memory system |
| `humanloop` | Human-in-the-loop |
| `handoff` | Multi-agent handoff |
| `safety` | Safety guardrails |
| `demo` | Demo application |
| `test` | Test related |

### Examples

```bash
# New feature
feat(tools): add CodeReviewTool for automated code review

# Bug fix
fix(agent): handle null response from LLM provider

# Documentation
docs(readme): update installation instructions

# Refactoring
refactor(humanloop): move ConsoleInteractionHandler to Demo

# Tests
test(memory): add WindowMemory edge case tests

# Maintenance
chore: add .vs/ to gitignore
```

## Pre-Commit Checklist

Before committing, ensure:

```powershell
# 1. Build passes
dotnet build --nologo -v q

# 2. Tests pass
dotnet test --nologo

# 3. Code formatted
dotnet csharpier .
```

## Branch Naming (if needed)

```
feature/add-code-review-skill
fix/null-pointer-in-agent
refactor/simplify-memory-api
docs/update-readme
```

## Useful Git Commands

### View Changes

```powershell
# Unstaged changes
git diff

# Staged changes
git diff --staged

# Changes in specific file
git diff path/to/file.cs
```

### History

```powershell
# Recent commits
git log --oneline -20

# Commits affecting specific file
git log --oneline -- path/to/file.cs

# Show specific commit
git show <commit-hash>
```
