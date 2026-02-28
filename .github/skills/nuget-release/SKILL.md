---
description: "Use when: Managing package versions, packing NuGet packages, publishing releases, creating git tags, or running release CI/CD\nDon't use when: Writing changelog (use changelog), updating dependencies (use dependency-update)\nInputs: Target version number or release request\nOutputs: Version bumped in Directory.Build.props, packages built and packed, git tag created\nSuccess criteria: Pre-release checklist complete, packages published, tag pushed"
---

# NuGet Release Skill

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

