# SpotifyWorker Class Documentation

## Overview

`SpotifyWorker` is a static helper that centralizes all interactions with the Spotify Web API for the Spotify Playlist Manager application. It owns the OAuth lifecycle, caches the authenticated `SpotifyClient`, and exposes high-level methods for retrieving user data. Because the worker is static, it maintains a single shared authentication context (client credentials, access token, refresh token, token expiration, and the embedded OAuth callback server) for the entire application lifetime.

## Responsibilities

- **Configuration storage** – Keeps the Spotify application Client ID, Client Secret, redirect URI, callback port, current access token, refresh token, and expiration timestamp in private static fields so subsequent calls can reuse them.
- **Authentication orchestration** – Starts the OAuth authorization code flow when required, or silently refreshes tokens if they are still valid.
- **Spotify client management** – Creates and stores the authenticated `SpotifyClient` after every successful authentication so that subsequent API calls share the same client.
- **Data retrieval** – Provides async helpers that return tuples with the minimal data the UI needs for liked tracks, playlists, saved albums, playlist metadata, album metadata, and track metadata.

## Initialization & Configuration

Before making any Spotify calls you must provide the application credentials (and optionally previously stored tokens) via `Init`:

```csharp
SpotifyWorker.Init(clientId, clientSecret, existingAccessToken, existingRefreshToken, expiresAtUtc);
```

| Parameter | Description |
| --- | --- |
| `clientId` (`ck`) | Spotify application client identifier from the Spotify Developer Dashboard. |
| `clientSecret` (`cs`) | Secret paired with the Client ID. Keep it confidential. |
| `at` | Optional access token obtained from a prior authentication run. |
| `rt` | Optional refresh token obtained from a prior authentication run. |
| `e` | Optional expiration instant for the provided access token. When omitted the worker assumes the token is expired. |

Supplying saved tokens allows the worker to refresh them without forcing the user to log in again. If you skip `Init`, any subsequent call will fail because the worker does not know your credentials.

## Authentication Workflow

### `AuthenticateAsync`

```csharp
Task<(string AccessToken, string RefreshToken)> AuthenticateAsync()
```

- Entry point used by the application before any Spotify API calls.
- Uses the credentials cached during `Init`.
- If the worker does not yet have an access token or refresh token, it launches the full OAuth authorization code flow by delegating to `AuthenticateFlowAsync`.
- If tokens are present, it delegates to `RefreshTokensIfNeededAsync` to refresh them when the expiration threshold has been reached.
- After receiving valid credentials it instantiates the shared `SpotifyClient` and returns the access and refresh tokens for persistence.

### `RefreshTokensIfNeededAsync`

```csharp
Task<(string accessToken, string refreshToken, DateTime expiresAt)> RefreshTokensIfNeededAsync(
    string clientId,
    string clientSecret,
    string accessToken,
    string refreshToken,
    DateTime expiresAtUtc,
    TimeSpan? refreshThreshold = null)
```

- Callable directly if the host application wants manual control over token refreshing.
- Defaults to a five-minute refresh window; pass a different `refreshThreshold` when you want to refresh earlier or later.
- When the current access token is not near expiry, it returns the original tokens unchanged.
- When the token is expired or about to expire, it asks Spotify for new credentials and returns the new access token, the new refresh token (if Spotify provided one; otherwise the old refresh token is retained), and the updated expiration timestamp.

### `AuthenticateFlowAsync`

```csharp
Task<(string accessToken, string refreshToken, DateTime expiresAt)> AuthenticateFlowAsync(
    string clientId,
    string clientSecret)
```

- Performs the full authorization code flow by running an `EmbedIOAuthServer` on the configured redirect URI (`http://127.0.0.1:5543/callback`).
- Opens the system browser to Spotify’s login/consent page with the following scopes: `user-read-email`, all playlist read/write scopes, `user-library-read`, `user-library-modify`, and `user-read-private`.
- Resolves when Spotify redirects back with an authorization code, exchanging it for access and refresh tokens and computing the expiration timestamp.
- Throws if an error is reported by Spotify during the flow.

> **Note:** All authentication helpers are designed to be awaited. They should be invoked from asynchronous contexts to avoid blocking the UI thread.

## Data Retrieval Methods

Every data-retrieval helper assumes `AuthenticateAsync` has already been called and the shared `SpotifyClient` is ready. They all return tuples to minimize allocations and make deconstruction convenient. Unless otherwise stated, multi-value strings use the delimiter shown below so that callers can split them into the original identifiers.

### Saved Tracks – `GetLikedSongsAsync`

```csharp
IAsyncEnumerable<(string Id, string Name, string Artists)> GetLikedSongsAsync()
```

- Streams the user’s saved tracks 50 items at a time (Spotify’s maximum page size).
- Skips tracks that are missing metadata (for example, if Spotify returns a null track reference).
- Returns a tuple for each track:
  - `Id`: the Spotify track ID.
  - `Name`: the song title.
  - `Artists`: artist IDs concatenated with `";;"` (double semicolons). Split on that delimiter to recover individual artist IDs.
- Automatically follows pagination until there are no further pages or Spotify signals an error when requesting the next page.

### User Playlists – `GetUserPlaylistsAsync`

```csharp
IAsyncEnumerable<(string Id, string Name, int TrackCount)> GetUserPlaylistsAsync()
```

- Enumerates the playlists owned or followed by the current user.
- For each playlist the returned tuple contains:
  - `Id`: the playlist ID.
  - `Name`: the playlist’s display name.
  - `TrackCount`: the total number of tracks as reported by Spotify.
- Continues requesting additional pages until all playlists are yielded or the Spotify API signals an error.

### Saved Albums – `GetUserAlbumsAsync`

```csharp
IAsyncEnumerable<(string Id, string Name, int TrackCount, string Artists)> GetUserAlbumsAsync()
```

- Retrieves the user’s saved albums from the library.
- Returns a tuple per album with:
  - `Id`: the album ID.
  - `Name`: the album title.
  - `TrackCount`: total tracks in the album.
  - `Artists`: contributing artist IDs joined with `";;"`.
- Uses the same pagination approach as the playlist and saved-track helpers.

### Playlist Metadata – `GetPlaylistDataAsync`

```csharp
Task<(string? name, string? imageURL, string? Id, string? Description, string? SnapshotID, string? TrackIDs)> GetPlaylistDataAsync(string id)
```

- Fetches the full metadata for a specific playlist.
- Returns nullable strings to account for playlists that omit certain fields (image, description, etc.).
- `TrackIDs` is a string containing every track ID in the playlist joined with `";;"`. Callers can split it to inspect or batch process tracks.
- If Spotify provides multiple images, only the first URL is returned; otherwise the string is empty.

### Album Metadata – `GetAlbumDataAsync`

```csharp
Task<(string? name, string? imageURL, string? Id, string TrackIDs, string artistIDs)> GetAlbumDataAsync(string id)
```

- Returns detailed album information:
  - `name`: album title (nullable because Spotify may omit it in rare cases).
  - `imageURL`: the first album image URL if available; otherwise an empty string.
  - `Id`: the album ID.
  - `TrackIDs`: all track IDs joined with `";;"`.
  - `artistIDs`: primary artist IDs joined with `";;"`.

### Track Metadata – `GetSongDataAsync`

```csharp
Task<(string name,
       string id,
       string albumID,
       string artistIDs,
       int discnumber,
       int durrationms,
       bool Explicit,
       string previewURL,
       int tracknumber)> GetSongDataAsync(string id)
```

- Looks up a single track by ID and returns:
  - `name`: track title.
  - `id`: Spotify track ID (echoed back).
  - `albumID`: ID of the parent album.
  - `artistIDs`: artist IDs joined with `"::"` (double colons) – note the different delimiter compared to the saved-track helpers.
  - `discnumber`: disc index for multi-disc releases.
  - `durrationms`: duration in milliseconds (as provided by Spotify).
  - `Explicit`: whether the track is marked explicit.
  - `previewURL`: preview clip URL, which may be empty if Spotify does not offer a preview.
  - `tracknumber`: the track’s index on its disc.

## Example End-to-End Usage

```csharp
using Spotify_Playlist_Manager.Models;

public async Task LoadSpotifyDataAsync()
{
    SpotifyWorker.Init(clientId, clientSecret, cachedAccessToken, cachedRefreshToken, cachedExpiryUtc);

    var (accessToken, refreshToken) = await SpotifyWorker.AuthenticateAsync();
    // Persist tokens so the next run can refresh silently.

    await foreach (var (trackId, trackName, artistIds) in SpotifyWorker.GetLikedSongsAsync())
    {
        Console.WriteLine($"Track {trackName} ({trackId}) by {artistIds}");
    }

    await foreach (var (playlistId, playlistName, trackCount) in SpotifyWorker.GetUserPlaylistsAsync())
    {
        Console.WriteLine($"Playlist {playlistName} ({playlistId}) contains {trackCount} tracks");
    }

    await foreach (var (albumId, albumName, trackCount, artistIds) in SpotifyWorker.GetUserAlbumsAsync())
    {
        Console.WriteLine($"Album {albumName} ({albumId}) has {trackCount} tracks by {artistIds}");
    }

    var playlistDetails = await SpotifyWorker.GetPlaylistDataAsync(targetPlaylistId);
    var albumDetails = await SpotifyWorker.GetAlbumDataAsync(targetAlbumId);
    var songDetails = await SpotifyWorker.GetSongDataAsync(targetTrackId);

    // Use the tuple data to update your UI or further process Spotify content.
}
```

## Implementation Notes & Best Practices

- The worker is **not thread-safe** because it uses shared static state. Ensure that only one authentication or retrieval sequence runs at a time.
- Always call `AuthenticateAsync` (or at least `RefreshTokensIfNeededAsync`) before attempting to stream user data to guarantee the `SpotifyClient` is configured with a valid token.
- The helper methods swallow pagination errors by breaking out of the loop. Consider wrapping calls with your own retry logic if you need stronger guarantees.
- When persisting identifiers returned by the helpers, remember to split the `";;"` or `"::"` delimited strings before storing them in structured formats.
