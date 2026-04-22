# CacheWorker.cs

## Overview
`CacheWorker` encapsulates all file-system interactions related to cached Spotify artwork. It exposes helpers for downloading images to the application cache and resolving already-downloaded artwork by using a deterministic naming convention (`{ImageType}_{ItemId}.{ext}`).

## Key Responsibilities
- **Download orchestration:** Streams remote artwork using `HttpClient`, chooses an extension based on the returned MIME type, and writes the file to the cache directory.
- **Cache lookups:** Searches the cache folder for existing files that match the naming pattern so callers can avoid re-downloading assets.
- **Resilience:** Converts common I/O errors into `null` results so higher-level code can gracefully continue when the cache drive is unavailable.

## Important Members
- `DownloadImageAsync(string url, ImageType type, string itemId)` – Downloads an image and returns the local path or `null` on failure.
- `GetImagePath(ImageType type, string itemId)` – Returns the cached path when the image already exists.
- `ImageType` enum – Enumerates the supported categories (`Artist`, `Album`, `Playlist`).

## Usage Notes
Callers should cache the returned file path (for example by storing it in the database) and prefer `GetImagePath` before attempting to download a fresh copy. The worker expects the cache directory defined in `Variables.CachePath` to be writable.
