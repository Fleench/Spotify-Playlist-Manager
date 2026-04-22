# DatabaseWorker.cs

## Overview
`DatabaseWorker` is a lightweight data-access layer around the SQLite cache. It owns schema initialization, enforces a single-writer policy via a semaphore, and exposes strongly-typed helpers for storing and retrieving playlists, albums, tracks, artists, settings, and similarity relationships.

## Key Responsibilities
- **Schema management:** Creates tables when the application boots and ensures new columns exist for older installations.
- **Thread-safe writes:** Guards every write operation with `SemaphoreSlim` to prevent concurrent modifications from corrupting the database.
- **Model translation:** Converts between raw SQL rows and the `Variables` POCOs used throughout the application.
- **Similarity storage:** Tracks both confirmed duplicates (`Similar`) and potential matches (`MightBeSimilar`).

## Important Members
- `Init()` – Initializes the database, sets `journal_mode=WAL`, and ensures compatibility columns exist.
- `SetPlaylist`, `SetAlbum`, `SetTrack`, `SetArtist` – Upsert helpers for the core Spotify entities.
- `GetAllPlaylists`, `GetAllAlbums`, `GetAllTracks`, `GetAllArtists` – Enumerate cached records for processing or display.
- `SetSimilar`, `SetMightBeSimilar` – Persist fuzzy-match output for duplicate resolution.

## Usage Notes
Always call `Variables.Init()` before `DatabaseWorker.Init()` so the path to the database file is guaranteed to exist. Read operations do not require the write lock and can be invoked concurrently from multiple threads.
