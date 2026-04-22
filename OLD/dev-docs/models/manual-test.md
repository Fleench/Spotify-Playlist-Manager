# manual-test.cs

## Overview
`manual-test.cs` hosts a console `TempProgram` entry point used exclusively for manual diagnostics. It allows developers to run workers (database initialization, Spotify authentication, synchronization) without starting the Avalonia UI.

## Key Responsibilities
- **Credential bootstrap:** Reads settings from `settings.txt` via `Models.txt.FileHelper` and prompts for missing values.
- **Authentication harness:** Invokes both `SpotifyWorker` and `SpotifyWorker_Old` to verify tokens can be acquired and persisted.
- **Data inspection:** Contains sample LINQ queries that help detect duplicate tracks or inspect synchronization results.

## Usage Notes
This file should remain excluded from production builds. Run it manually when you need to troubleshoot the pipeline or regenerate sample IDs. The harness assumes `Variables.Init()` and `DatabaseWorker.Init()` succeed and that Spotify credentials are available.
