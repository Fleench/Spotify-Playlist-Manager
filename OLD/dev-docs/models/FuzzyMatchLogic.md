# FuzzyMatchLogic.cs

## Overview
`FuzzyMatchLogic` implements heuristics for detecting duplicate or near-duplicate tracks within the local cache. It scans the catalog, computes similarity scores for every pair of tracks, and updates song identifiers or flags potential matches for manual review.

## Key Responsibilities
- **Similarity scoring:** Applies weighted comparisons (song IDs, preview URLs, artists, album, duration, etc.) to produce a normalized confidence value.
- **Automatic merging:** For high-confidence matches (score ≥ 0.75) the module copies the canonical `SongID` to the duplicate track and persists it.
- **Manual review queue:** Records borderline matches (score ≥ 0.45) as "might be similar" pairs so the UI can request user confirmation.

## Important Members
- `BasicMatch()` – Entry point that clears previous results, iterates over all tracks, and either updates duplicates or schedules manual review.
- `ScoreSong(Variables.Track a, Variables.Track b)` – Returns a `double` between 0 and 1 describing the likelihood that two tracks are the same recording.
- `SameTracks` – A working list of tracks identified as duplicates during the current run.

## Usage Notes
Run `BasicMatch()` after the synchronization pipeline finishes so the local catalog is populated. The method operates entirely on the cached data; no Spotify API calls are made.
