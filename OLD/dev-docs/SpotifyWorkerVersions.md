# Spotify Worker Versions Overview

This document explains how the original `SpotifyWorker` ("V1") differs from the newer `SpotifyWorker2` ("V2") helper. Both abstractions target the Spotify Web API, but they take different approaches to configuration, state management, and concurrency.

## High-Level Summary

| Capability | V1: `SpotifyWorker` | V2: `SpotifyWorker2` |
| --- | --- | --- |
| Initialization | Stores credentials and optional tokens in static fields when `Init` is called. | Forwards credentials and optional tokens to a singleton `SpotifySession` helper that centralizes state. |
| Authentication entry point | `AuthenticateAsync` inspects cached fields, then either runs the full OAuth flow or refreshes tokens inline. | `AuthenticateAsync` delegates to `SpotifySession`: it launches the OAuth flow only when the session has no tokens, otherwise it just ensures the singleton client is ready. |
| Spotify client access | The static `SpotifyClient` instance is updated in place. | Clients are dispensed by `SpotifySession.GetClientAsync`, which lazily refreshes the token and rebuilds the client under a semaphore lock. |
| Token refresh logic | Public `RefreshTokensIfNeededAsync` refreshes tokens and returns them to the caller; the method is also used internally during auth. | Reuses the same refresh helper but is invoked from inside `SpotifySession.GetClientAsync` so any API call automatically refreshes on demand. |
| Concurrency guarantees | Not thread-safe; simultaneous calls may race because static fields are updated without coordination. | Uses `SemaphoreSlim` in `SpotifySession` to serialize access and guarantee a single client/token refresh at a time. |
| Error surface | Throws only when the OAuth callbacks report an error. | Adds guard clauses for missing initialization, missing tokens, or missing credentials when attempting API calls. |
| Usage style | Callers are expected to invoke `AuthenticateAsync` before every API call to ensure the static client is initialized. | Callers may call `AuthenticateAsync` once; subsequent API calls ask the session for a client, which will refresh tokens if necessary. |

## Detailed Differences

### State & Configuration

- **V1** keeps client credentials, tokens, expiration, and the `SpotifyClient` as private static fields inside the worker. The embedded OAuth server instance also lives on the class.
- **V2** splits responsibilities: `SpotifyWorker2` stores only the client ID/secret, while a nested `SpotifySession` singleton owns tokens, expiration, and the reusable `SpotifyClient` instance. This enables centralized lifecycle management.

### Authentication Flow

- **V1** decides whether to refresh or run a full authorization flow by checking whether cached tokens exist. The refresh path returns the new tokens directly, and `AuthenticateAsync` updates the static fields before constructing a new `SpotifyClient`.
- **V2** still exposes `AuthenticateFlowAsync` and `RefreshTokensIfNeededAsync`, but the top-level `AuthenticateAsync` only initializes the `SpotifySession`. Future API calls go through `SpotifySession.GetClientAsync`, which self-refreshes tokens when they are close to expiring.

### Thread Safety & Reentrancy

- **V1** has no locking. If multiple components call data retrieval helpers simultaneously, refreshes and client creation can overlap, potentially overwriting shared state.
- **V2** uses a `SemaphoreSlim` inside `SpotifySession` to coordinate access. Concurrent calls to `GetClientAsync` queue behind the lock, ensuring only one refresh or client instantiation runs at a time.

### Error Handling Improvements

- **V1** assumes proper sequencing: calling `AuthenticateAsync` before any other member is a convention, not a guarded invariant. Using any helper before initialization can lead to `NullReferenceException` or authorization failures.
- **V2** adds explicit guard clauses. `AuthenticateAsync` throws if `Init` was not called, and `SpotifySession.GetClientAsync` throws descriptive `InvalidOperationException`s when credentials or tokens are missing.

### API Surface Compatibility

- Both versions expose identical public helper methods for fetching liked songs, playlists, albums, individual playlist/album/track details, artist data, and for adding tracks to a playlist. Return types and delimiters remain the same, so consumers can switch versions without changing call sites.

### When to Use Each Version

- Choose **V1** if you prefer the original, minimal wrapper and can guarantee single-threaded access (for example, from a UI thread that sequences all Spotify calls).
- Choose **V2** when you need safer concurrency, better encapsulation of tokens, or stricter validation of initialization order. The session-based design also makes it easier to plug in additional lifecycle features (logging, diagnostics, token persistence) in one place.

## Migration Notes

1. Replace `SpotifyWorker` usages with `SpotifyWorker2` in dependency registrations or view models. Method signatures are the same.
2. Ensure `SpotifyWorker2.Init` is invoked during application startup with the same credential values used previously.
3. Update any token persistence code to read from `SpotifyWorker2.AuthenticateAsync`, which still returns the latest access and refresh tokens.
4. Remove any ad-hoc synchronization around Spotify calls if it only existed to protect the old static fields; the session lock now provides mutual exclusion.

By understanding these differences, you can decide which worker fits your architecture and migrate confidently if you need the guarded session model offered by V2.
