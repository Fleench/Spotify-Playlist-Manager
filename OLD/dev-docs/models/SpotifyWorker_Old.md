# SpotifyWorker_Old.cs

## Overview
`SpotifyWorker_Old` preserves the original Spotify API abstraction used by early versions of the application. It remains available primarily for regression testing and for code paths that have not yet been migrated to the new `SpotifyWorker` backed by `SpotifySession`.

## Key Responsibilities
- **Authentication:** Provides helper methods to initialize credentials and perform OAuth via `SpotifyAPI.Web`.
- **Data retrieval:** Mirrors the operations exposed by `SpotifyWorker` (playlists, albums, liked tracks, etc.) using the legacy client implementation.
- **Backward compatibility:** Ensures existing tools that depend on the old surface area continue to function while the new worker evolves.

## Usage Notes
Prefer `SpotifyWorker` for new development. Use `SpotifyWorker_Old` only when validating legacy flows or when the new session-backed worker lacks a required feature.
