---
name: markdown
description: >
  Markdown formatting rules and conventions for Dawning.Agents documentation.
  Use when writing README, CHANGELOG, API docs, or any .md files.
---

# Markdown Formatting Skill

## What This Skill Does

Defines the Markdown formatting rules for all documentation in this project.

## When to Use

- Writing README.md or CHANGELOG.md
- Creating API documentation
- Writing code comments with Markdown
- Reviewing documentation for formatting

## Core Rules

### 1. Headings - Blank Lines Before and After

```markdown
<!-- ✅ Correct -->
Some text here.

## Heading

More text here.

<!-- ❌ Wrong - no blank lines -->
Some text here.
## Heading
More text here.
```

### 2. Code Blocks - Specify Language

````markdown
<!-- ✅ Correct - with language -->
```csharp
var x = 1;
```

```bash
dotnet build
```

```json
{ "key": "value" }
```

<!-- ❌ Wrong - no language -->
```
var x = 1;
```
````

### 3. Lists - Consistent Markers

```markdown
<!-- ✅ Correct - consistent markers -->
- Item 1
- Item 2
- Item 3

1. First
2. Second
3. Third

<!-- ❌ Wrong - mixed markers -->
- Item 1
* Item 2
+ Item 3
```

### 4. Lists - Blank Line for Nested Content

```markdown
<!-- ✅ Correct - blank line before nested code -->
- Item with code:

  ```csharp
  var x = 1;
  ```

- Next item

<!-- ❌ Wrong - no blank line -->
- Item with code:
  ```csharp
  var x = 1;
  ```
```

### 5. Tables - Aligned Columns

```markdown
<!-- ✅ Correct - aligned -->
| Name   | Type   | Description       |
|--------|--------|-------------------|
| id     | Guid   | Unique identifier |
| name   | string | Display name      |

<!-- ❌ Wrong - not aligned -->
| Name | Type | Description |
|---|---|---|
| id | Guid | Unique identifier |
```

### 6. Links - Descriptive Text

```markdown
<!-- ✅ Correct - descriptive -->
See the [configuration guide](docs/config.md) for details.
Check out [Microsoft Docs](https://docs.microsoft.com).

<!-- ❌ Wrong - generic text -->
Click [here](docs/config.md) for details.
See [https://docs.microsoft.com](https://docs.microsoft.com).
```

### 7. Emphasis - Use Sparingly

```markdown
<!-- ✅ Correct - meaningful emphasis -->
This is **important** information.
Use `code` for inline code references.

<!-- ❌ Wrong - overused -->
This is **very** **important** **information**.
```

### 8. Line Length - Wrap at ~100 Characters

```markdown
<!-- ✅ Correct - wrapped -->
This is a long paragraph that should be wrapped at a reasonable
length to maintain readability in plain text editors.

<!-- ❌ Wrong - very long line -->
This is a long paragraph that goes on and on without any line breaks which makes it very hard to read in plain text editors and causes horizontal scrolling.
```

### 9. Inline Code - For Code References

```markdown
<!-- ✅ Correct -->
Use `ILLMProvider` interface.
Call `RunAsync()` method.
Set `MaxIterations` property.

<!-- ❌ Wrong - no backticks for code -->
Use ILLMProvider interface.
Call RunAsync() method.
```

### 10. Blockquotes - For Notes and Warnings

```markdown
<!-- ✅ Correct -->
> **Note**: This is important information.

> **Warning**: This operation cannot be undone.

<!-- GitHub style alerts -->
> [!NOTE]
> Useful information.

> [!WARNING]
> Critical warning.
```

## XML Documentation Comments

```csharp
// ✅ Correct - complete documentation
/// <summary>
/// Gets user by ID.
/// </summary>
/// <param name="id">The user's unique identifier.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>The user DTO or null if not found.</returns>
/// <exception cref="ArgumentException">Thrown when id is empty.</exception>
public async Task<UserDto?> GetByIdAsync(
    Guid id,
    CancellationToken cancellationToken = default
);

// ❌ Wrong - incomplete or missing
public async Task<UserDto?> GetByIdAsync(Guid id);
```

## README Structure Template

```markdown
# Project Name

Brief description of the project.

## Features

- Feature 1
- Feature 2

## Installation

```bash
dotnet add package ProjectName
```

## Quick Start

```csharp
// Example code
```

## Configuration

Configuration details...

## API Reference

API documentation...

## Contributing

How to contribute...

## License

MIT License
```

## CHANGELOG Structure

```markdown
# Changelog

## [Version] - YYYY-MM-DD

### Added
- New feature

### Changed
- Modified behavior

### Fixed
- Bug fix

### Removed
- Deprecated feature
```

## Quick Reference

| Element | Rule |
|---------|------|
| Headings | Blank lines before/after |
| Code blocks | Always specify language |
| Lists | Consistent markers (- or 1.) |
| Nested content | Blank line + 2-space indent |
| Tables | Align columns |
| Links | Descriptive text, not "click here" |
| Emphasis | Use sparingly |
| Line length | Wrap at ~100 characters |
| Inline code | Backticks for code references |
| XML docs | Complete with summary, params, returns |
