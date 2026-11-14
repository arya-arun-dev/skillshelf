# Explain a codebase

## Purpose
Help a developer understand an unfamiliar project through its entry point, main components, and one concrete request or execution path.

## Instructions
1. Identify the project language, framework, startup configuration, and documented run instructions from files in the repository.
2. Locate the entry point and explain how it connects routes, components, or commands. Cite file paths and relevant symbols.
3. Group the main components by responsibility. Explain their dependencies using evidence from the code rather than inferring behavior from names alone.
4. Trace one representative workflow from input to output, including validation, storage, external services, and error handling where present.
5. Identify configuration, generated files, persistent data, and tests. Explain which files a newcomer should read first and why.
6. Use templates/codebase-summary.md to produce a concise orientation guide. Separate confirmed behavior from assumptions and open questions.
7. State which files were inspected and whether the application or tests were actually run. Do not claim runtime behavior from source inspection alone.

Do not execute commands or modify files unless the developer authorizes it.
