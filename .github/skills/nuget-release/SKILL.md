---
description: "NuGet release workflow for Dawning.Agents: versioning, packing, publishing, CI/CD. Trigger: nuget, 发布, release, publish, pack, version, 版本, 打包, tag"
---

> **Skill 使用日志**：使用本 skill 后，在 `/memories/session/skill-log.md` 追加一行：`- {时间} nuget-release — {触发原因}`

# NuGet Release Workflow

## Version Management

Version is centralized in `Directory.Build.props`:

```xml
<Version>0.1.0-preview.1</Version>
```

All packages inherit this version. Never set `<Version>` in individual `.csproj` files.

### Versioning Scheme

- **Pre-release**: `0.1.0-preview.1`, `0.2.0-alpha.1`, `0.3.0-beta.1`
- **Stable**: `1.0.0`, `1.1.0`, `1.0.1`
- Bump **major** for breaking API changes
- Bump **minor** for new features (backward-compatible)
- Bump **patch** for bug fixes only

## Published Packages (7)

| Package | Path |
|---------|------|
| Dawning.Agents.Abstractions | `src/Dawning.Agents.Abstractions/` |
| Dawning.Agents.Core | `src/Dawning.Agents.Core/` |
| Dawning.Agents.OpenAI | `src/Dawning.Agents.OpenAI/` |
| Dawning.Agents.Azure | `src/Dawning.Agents.Azure/` |
| Dawning.Agents.Redis | `src/Dawning.Agents.Redis/` |
| Dawning.Agents.Qdrant | `src/Dawning.Agents.Qdrant/` |
| Dawning.Agents.Pinecone | `src/Dawning.Agents.Pinecone/` |

**Not published** (no `.csproj` pack config): MCP, OpenTelemetry, Serilog, Chroma, Weaviate.

## Local Pack

```powershell
./scripts/pack.ps1 -Version 0.2.0-preview.1
```

Steps: update `Directory.Build.props` → restore → build → test → pack 7 projects → output to `./nupkgs/`.

To publish manually:

```bash
dotnet nuget push "./nupkgs/*.nupkg" --api-key $NUGET_API_KEY --source https://api.nuget.org/v3/index.json
```

## CI/CD Release (`.github/workflows/publish-nuget.yml`)

### Tag-triggered

```bash
git tag v0.2.0 && git push origin v0.2.0
```

Workflow extracts version from tag (`v0.2.0` → `0.2.0`), updates `Directory.Build.props`, builds, tests, packs 7 projects, pushes `.nupkg` + `.snupkg` to NuGet.org, creates GitHub Release with auto-generated notes.

### Manual dispatch

Go to Actions → "Publish NuGet Packages" → Run workflow → enter version (e.g. `0.2.0`).

## Pre-release Checklist

Before tagging a release:

1. **CHANGELOG.md** — move `[Unreleased]` entries under `[x.y.z] - YYYY-MM-DD`
2. **Build** — `dotnet build --nologo -v q` → 0 errors, 0 warnings
3. **Test** — `dotnet test --nologo -v q` → all pass
4. **Format** — `~/.dotnet/tools/csharpier format .`
5. **Version** — update `Directory.Build.props` to target version
6. **README.md** — verify installation instructions match new version
7. **Commit** — `chore(release): bump version to x.y.z`
8. **Tag** — `git tag vx.y.z && git push origin vx.y.z`

## Required Secrets

| Secret | Where | Purpose |
|--------|-------|---------|
| `NUGET_API_KEY` | GitHub repo → Settings → Secrets | NuGet.org push |
| `GITHUB_TOKEN` | Auto-provided by GitHub Actions | GitHub Release creation |

## Troubleshooting

- **409 Conflict on push**: package version already exists on NuGet.org; `--skip-duplicate` handles this
- **Symbol push fails**: `continue-on-error: true` in workflow; snupkg failures are non-blocking
- **Version mismatch**: ensure `Directory.Build.props` matches the tag; CI auto-updates it
- **Pack fails**: check that all 7 projects have `<IsPackable>true</IsPackable>` (inherited from `Directory.Build.props`)
