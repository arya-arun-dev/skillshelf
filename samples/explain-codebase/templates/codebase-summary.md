# Codebase orientation: [project]

## Purpose and stack
- Purpose: [What the project does]
- Language and framework: [Evidence from project files]
- Documented startup: [README section or configuration path; not a claim it was executed]

## Main components

| Component | Responsibility | Evidence |
| --- | --- | --- |
| [Entry point] | [How execution begins] | [File path and symbol] |
| [Component] | [What it owns] | [File path and symbol] |

## One workflow
1. Input enters through [file/symbol].
2. [Component] validates or transforms it.
3. [Component] reads, writes, or calls [dependency].
4. The result or error returns through [file/symbol].

## Files to read first
1. [Path] — [Why it matters]
2. [Path] — [Why it matters]
3. [Path] — [Why it matters]

## Configuration, data, and tests
- Configuration: [Paths and purpose; omit secrets]
- Persistent/generated data: [Locations and lifecycle]
- Tests: [Locations and what the inspected tests cover]

## Evidence and open questions
- Files inspected: [Paths]
- Runtime verification: [Not run, or actual commands and observed results]
- Assumptions/questions: [What the source did not establish]
