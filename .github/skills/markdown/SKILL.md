---
name: markdown
description: |
  Use when: Writing or formatting Markdown documentation, XML doc comments, README files, or API docs
  Don't use when:
    - Writing C# code (use code-update)
    - Generating changelogs (use changelog)
    - Writing NuGet release notes (use nuget-release)
    - Reviewing code (use code-review)
  Inputs: Documentation content to write or format
  Outputs: Well-formatted Markdown or XML documentation following project conventions
  Success criteria: Documentation follows 10 core formatting rules, XML docs have all required tags
---

# Markdown Formatting Skill

## Core Rules

1. **Headings** — blank lines before and after
2. **Code Blocks** — always specify language (```csharp, ```bash, ```json)
3. **Lists** — consistent markers (- or 1.), blank line for nested content
4. **Tables** — aligned columns
5. **Links** — descriptive text, not "click here"
6. **Emphasis** — use sparingly, `code` for inline code references
7. **Line Length** — wrap at ~100 characters
8. **Inline Code** — backticks for class/method/variable names
9. **Blockquotes** — for notes and warnings
10. **GitHub Alerts** — `> [!NOTE]`, `> [!WARNING]`

## XML Documentation

```csharp
/// <summary>
/// Gets user by ID.
/// </summary>
/// <param name="id">The user's unique identifier.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>The user DTO or null if not found.</returns>
/// <exception cref="ArgumentException">Thrown when id is empty.</exception>
public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
```

## Key Documentation Files

| File | Purpose |
|------|---------|
| `README.md` | Project overview, installation, quick start |
| `CHANGELOG.md` | Detailed change history |
| `docs/QUICKSTART.md` | 5-minute setup guide |
| `docs/API_REFERENCE.md` | Full API reference |

