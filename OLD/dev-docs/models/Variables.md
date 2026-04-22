# Variables.cs

## Overview
`Variables` is a container for application-wide constants, configuration paths, and the lightweight data-transfer objects (DTOs) used throughout the Spotify Playlist Manager. These DTOs represent playlists, albums, tracks, and artists as they are stored in the local cache and passed between workers.

## Key Responsibilities
- **Global configuration:** Exposes application name/version plus the computed file-system paths for configuration, database, and cache storage.
- **Initialization:** `Init()` ensures that required directories and the database file exist before any persistence occurs.
- **Model definitions:** Declares nested classes (`PlayList`, `Album`, `Track`, `Artist`) that represent Spotify entities and provide validation helpers.

## Important Members
- `Init()` – Creates the application data folder hierarchy and database placeholder.
- `MakeId(string type = "SNG", int length = 30)` – Generates a unique internal identifier (CIID) for tracks or other entities.
- Nested classes:
  - `PlayList`, `Album`, `Track`, `Artist` – Each include `MissingInfo()` validation helpers **and the new `Copy()` method** which returns a shallow clone so callers can safely mutate data without affecting cached instances.
  - `Settings` – Defines the database keys used for Spotify credential storage.

## Usage Notes
Call `Variables.Init()` during application startup before any database or cache operations. When transforming entities (e.g., adjusting artwork paths for UI display), use the respective `Copy()` method to avoid mutating the shared reference returned by `DatabaseWorker` or `DataCoordinator`.
