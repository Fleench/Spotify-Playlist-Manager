# SpotifyWorker2 Initialization Improvements

## Summary
- Hardened `SpotifyWorker2.Init` validation to ensure a client ID and client secret are supplied and trimmed before configuring the shared `SpotifySession`.
- Added credential persistence safeguards so `AuthenticateAsync` can recover previously supplied credentials when the static cache is empty.
- Guarded `SpotifySession.Initialize` to reject empty credential values and added a credential snapshot accessor for internal recovery.
- Added `GetCurrentTokens()` helpers to both `SpotifyWorker2` and the legacy `SpotifyWorker` to expose the cached token set and expiration timestamp.

## How it Works
1. **Input Validation & Normalization**
   - `SpotifyWorker2.Init` now throws `ArgumentException` when called without non-whitespace credentials. The method trims the incoming values before persisting them and before handing them to `SpotifySession`.
   - `SpotifySession.Initialize` enforces the same validation, preventing the session from being configured with incomplete data.

2. **Credential Recovery**
   - `SpotifySession` exposes `GetCredentialSnapshot()` so `SpotifyWorker2.AuthenticateAsync` can rehydrate credentials that were previously stored during initialization.
   - When a consumer calls `AuthenticateAsync` and the static cache is empty, the method attempts this recovery before failing, resolving the erroneous "missing ClientID" errors when valid credentials were previously supplied.

3. **Token Snapshot Access**
   - `SpotifyWorker2.GetCurrentTokens()` returns the access token, refresh token, and expiration timestamp currently cached in the session. Empty strings are returned when no tokens are cached yet.
   - `SpotifyWorker.GetCurrentTokens()` mirrors this behavior for the original worker, ensuring both implementations expose the same diagnostics surface.

## Usage Notes
- Always call `SpotifyWorker2.Init(clientId, clientSecret, ...)` during startup; the method now validates the inputs and primes the session.
- To introspect authentication state (for logging or persistence), call `SpotifyWorker2.GetCurrentTokens()` or `SpotifyWorker.GetCurrentTokens()` depending on the worker version in use.
- `AuthenticateAsync` automatically refreshes tokens when needed. If credentials are supplied once via `Init`, subsequent refreshes will succeed even if the static fields were cleared (e.g., due to domain reloads) because the credentials are retained within `SpotifySession`.
