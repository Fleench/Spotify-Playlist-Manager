# txt.cs

## Overview
The `Models.txt` namespace hosts `FileHelper`, a simple utility for manipulating line-oriented text files used by the manual testing harness. It offers convenience functions for appending, reading, and modifying individual lines in configuration files such as `settings.txt`.

## Key Responsibilities
- **Line access:** Retrieve a single line or all lines from a file with minimal boilerplate.
- **Line mutation:** Replace a specific line or append new content without manual `StreamWriter` usage.
- **File creation:** Create or overwrite files from an enumerable set of strings.

## Important Members
- `AppendLine(string path, string line)` – Adds a line and appends a newline automatically.
- `ReadSpecificLine(string path, int lineNumber)` – Fetches a single line by 1-based index, returning `null` when unavailable.
- `ModifySpecificLine(string path, int lineNumber, string newText)` – Replaces a line and persists the file.
- `CreateOrOverwriteFile(string path, IEnumerable<string> lines)` – Writes a complete file in one call.

## Usage Notes
All helpers assume UTF-8 encoded text files. Methods return `null`/`false` for missing files so callers can prompt the user for input rather than handling exceptions.
