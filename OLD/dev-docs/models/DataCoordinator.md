# DataCoordinator.cs

## Overview
`DataCoordinator` is the brain of the synchronization pipeline. It mediates every interaction between the Spotify REST workers (`SpotifyWorker`/`SpotifyWorker_Old`), the SQLite persistence layer (`DatabaseWorker`), and the in-memory models in `Variables`. The coordinator owns the high-level sync flow as well as convenience helpers for CRUD-style operations exposed to the UI.

## Key Responsibilities
- **Settings management:** Wraps `DatabaseWorker` calls that store OAuth tokens and other configuration values.
- **Entity hydration:** Normalizes playlists, albums, tracks, and artists coming from the API, ensuring artwork is cached and identifiers are cleanly delimited.
- **Similarity tracking:** Forwards fuzzy-match results into the database so duplicates can be merged or reviewed later.
- **Synchronization workflows:** Provides both the legacy `SlowSync` implementation and the modern checkpointed `Sync` pipeline with retry handling.

## Important Members
- `SetPlaylistAsync`, `SetAlbumAsync`, `SetTrackAsync`, `SetArtistAsync` – Persist Spotify entities after normalizing identifiers and caching artwork.
- `GetAllPlaylists()`, `GetAllAlbums()`, `GetAllTracks()`, `GetAllArtists()` – Enumerate the cached entities for consumption by the UI or logic layers.
- `Sync(SyncEntryPoint startFrom)` – Runs the modern staged synchronization routine, allowing callers to resume work from a given checkpoint.
- `SetSimilarAsync` / `SetMightBeSimilarAsync` – Record high-confidence duplicates or possible matches produced by the fuzzy matching engine.

## Usage Notes
All `Set*Async` members assume that `Variables.Init()` and `DatabaseWorker.Init()` have already executed so that paths and tables are available. The coordinator defensively validates identifiers; expect `ArgumentException` if you pass blank IDs.
