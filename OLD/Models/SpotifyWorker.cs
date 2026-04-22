/* File: SpotifyWorker.cs
 * Author: Glenn Sutherland, ChatGPT Codex
 * Description: A guarded wrapper for the SpotifyAPI.Web module that mirrors
 *              SpotifyWorker_Old functionality while coordinating access through
 *              a singleton Spotify session.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;

namespace Spotify_Playlist_Manager.Models
{
    /// <summary>
    /// Centralized entry point for every Spotify Web API interaction in the app.
    /// The class exposes the same surface area as the previous generation worker
    /// but funnels calls through <see cref="SpotifySession"/> so that token
    /// refreshes, client reuse, and configuration stay consistent across the
    /// application.
    /// </summary>
    static class SpotifyWorker
    {
        private static string ClientID;
        private static string ClientSecret;
        private static readonly string Uri = "http://127.0.0.1:5543/callback";
        private static readonly int Port = 5543;

        /// <summary>
        /// Configures the worker with user-provided credentials and optional
        /// cached tokens. This mirrors the old API surface but now forwards the
        /// data into <see cref="SpotifySession"/> which owns the Spotify client
        /// lifecycle.
        /// </summary>
        /// <param name="ck">The Spotify client ID.</param>
        /// <param name="cs">The Spotify client secret.</param>
        /// <param name="at">An optional cached access token.</param>
        /// <param name="rt">An optional cached refresh token.</param>
        /// <param name="e">The token expiration timestamp in UTC.</param>
        public static void Init(string ck, string cs, string at = "", string rt = "", DateTime e = new DateTime())
        {
            if (string.IsNullOrWhiteSpace(ck))
            {
                throw new ArgumentException("A non-empty Spotify client ID must be provided.", nameof(ck));
            }

            if (string.IsNullOrWhiteSpace(cs))
            {
                throw new ArgumentException("A non-empty Spotify client secret must be provided.", nameof(cs));
            }

            ClientID = ck.Trim();
            ClientSecret = cs.Trim();

            DateTime? expires = e == DateTime.MinValue ? null : e;

            SpotifySession.Instance.Initialize(
                ClientID,
                ClientSecret,
                string.IsNullOrEmpty(at) ? null : at,
                string.IsNullOrEmpty(rt) ? null : rt,
                expires
            );
        }

        /// <summary>
        /// Performs the full OAuth flow if the current session is missing
        /// tokens. When tokens are already present it simply ensures that the
        /// underlying client is ready to use. The method returns a tuple with
        /// the active access and refresh tokens so callers can persist them.
        /// </summary>
        public static async Task<(string AccessToken, string RefreshToken)> AuthenticateAsync()
        {
            if (string.IsNullOrWhiteSpace(ClientID) || string.IsNullOrWhiteSpace(ClientSecret))
            {
                var (clientId, clientSecret) = SpotifySession.Instance.GetCredentialSnapshot();

                if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
                {
                    ClientID = clientId!.Trim();
                    ClientSecret = clientSecret!.Trim();
                }
            }

            if (string.IsNullOrWhiteSpace(ClientID) || string.IsNullOrWhiteSpace(ClientSecret))
            {
                throw new InvalidOperationException("SpotifyWorker.Init must be called with a valid client ID and client secret before AuthenticateAsync.");
            }

            var session = SpotifySession.Instance;

            if (!session.HasTokens)
            {
                var (accessToken, refreshToken, expiresAt) = await AuthenticateFlowAsync(ClientID, ClientSecret);
                session.Initialize(ClientID, ClientSecret, accessToken, refreshToken, expiresAt);
            }
            else
            {
                await session.GetClientAsync();
            }

            var snapshot = session.GetTokenSnapshot();
            return (snapshot.AccessToken, snapshot.RefreshToken);
        }

        /// <summary>
        /// Provides read-only access to the currently cached tokens without
        /// forcing a refresh cycle. Primarily used for persisting credentials
        /// between runs of the desktop application.
        /// </summary>
        public static (string AccessToken, string RefreshToken, DateTime ExpiresAt) GetCurrentTokens()
        {
            var snapshot = SpotifySession.Instance.GetTokenSnapshot();
            return (snapshot.AccessToken, snapshot.RefreshToken, snapshot.ExpiresAt);
        }

        /// <summary>
        /// Given a token set and expiration timestamp, refreshes the tokens if
        /// they are close to expiry. This helper exists separately so that the
        /// session can reuse the logic and the tests can exercise it in
        /// isolation.
        /// </summary>
        public static async Task<(string accessToken, string refreshToken, DateTime expiresAt)> RefreshTokensIfNeededAsync(
            string clientId,
            string clientSecret,
            string accessToken,
            string refreshToken,
            DateTime expiresAtUtc,
            TimeSpan? refreshThreshold = null)
        {
            // Default to five minutes of buffer time so we do not send users
            // into an API call with an access token that will expire mid-flight.
            var threshold = refreshThreshold ?? TimeSpan.FromMinutes(5);

            if (expiresAtUtc != DateTime.MinValue && DateTime.UtcNow < expiresAtUtc - threshold)
            {
                // Tokens are still valid, so we return the inputs untouched.
                return (accessToken, refreshToken, expiresAtUtc);
            }

            var config = SpotifyClientConfig.CreateDefault();
            var tokenResponse = await new OAuthClient(config).RequestToken(
                new AuthorizationCodeRefreshRequest(clientId, clientSecret, refreshToken)
            );

            var newAccessToken = tokenResponse.AccessToken;
            var newRefreshToken = tokenResponse.RefreshToken ?? refreshToken;
            var newExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

            return (newAccessToken, newRefreshToken, newExpiresAt);
        }

        /// <summary>
        /// Kicks off the full embedded authorization flow and resolves once the
        /// user has completed the login in their browser. The EmbedIO server
        /// mirrors the original worker behaviour but with clearer error
        /// reporting.
        /// </summary>
        public static async Task<(string accessToken, string refreshToken, DateTime expiresAt)> AuthenticateFlowAsync(string clientId, string clientSecret)
        {
            using var server = new EmbedIOAuthServer(new Uri(Uri), Port);
            await server.Start();

            var tcs = new TaskCompletionSource<(string, string, DateTime)>();

            server.AuthorizationCodeReceived += async (sender, response) =>
            {
                await server.Stop();

                var config = SpotifyClientConfig.CreateDefault();
                var tokenResponse = await new OAuthClient(config).RequestToken(
                    new AuthorizationCodeTokenRequest(clientId, clientSecret, response.Code, new Uri(Uri))
                );

                var expiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                // Capture all token data for the caller to persist.
                tcs.TrySetResult((tokenResponse.AccessToken, tokenResponse.RefreshToken, expiresAt));
            };

            server.ErrorReceived += async (sender, error, state) =>
            {
                await server.Stop();
                // Propagate the failure to the awaiting caller so they can show
                // an appropriate error message.
                tcs.TrySetException(new Exception($"Spotify auth error: {error}"));
            };

            var request = new LoginRequest(server.BaseUri, clientId, LoginRequest.ResponseType.Code)
            {
                Scope = new List<string>
                {
                    Scopes.UserReadEmail,
                    Scopes.PlaylistModifyPrivate,
                    Scopes.PlaylistModifyPublic,
                    Scopes.PlaylistReadCollaborative,
                    Scopes.PlaylistReadPrivate,
                    Scopes.UserLibraryRead,
                    Scopes.UserLibraryModify,
                    Scopes.UserReadPrivate
                }
            };

            BrowserUtil.Open(request.ToUri());

            return await tcs.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// Enumerates every liked track in chunks of fifty. The method yields a
        /// lightweight tuple that mirrors the old worker's string payload while
        /// avoiding additional allocations.
        /// </summary>
        public static async IAsyncEnumerable<(string Id, string Name, string Artists)> GetLikedSongsAsync()
        {
            var spotify = await SpotifySession.Instance.GetClientAsync();
            var page = await spotify.Library.GetTracks(new LibraryTracksRequest { Limit = 50 });

            while (page != null && page.Items.Count > 0)
            {
                foreach (var item in page.Items)
                {
                    var track = item.Track;

                    if (track == null)
                    {
                        // Some saved items can be local files or missing tracks.
                        continue;
                    }

                    yield return (
                        track.Id,
                        track.Name,
                        string.Join(";;", track.Artists.Select(a => a.Id))
                    );
                }

                try
                {
                    page = await spotify.NextPage(page);
                }
                catch
                {
                    // If Spotify rejects pagination we stop rather than
                    // spinning forever. This matches the behaviour of the old
                    // worker which swallowed such errors.
                    break;
                }
            }
        }

        /// <summary>
        /// Streams all of the user's playlists. Each result contains the ID,
        /// the display name, and the count of tracks so the caller can decide
        /// whether to fetch additional details.
        /// </summary>
        public static async IAsyncEnumerable<(string Id, string Name, int TrackCount)> GetUserPlaylistsAsync()
        {
            var spotify = await SpotifySession.Instance.GetClientAsync();
            var page = await spotify.Playlists.CurrentUsers(new PlaylistCurrentUsersRequest { Limit = 50 });

            while (page != null && page.Items.Count > 0)
            {
                foreach (var playlist in page.Items)
                {
                    yield return (
                        playlist.Id,
                        playlist.Name,
                        playlist.Tracks?.Total ?? 0
                    );
                }

                try
                {
                    page = await spotify.NextPage(page);
                }
                catch
                {
                    // Gracefully abandon pagination on errors; the UI already
                    // shows what we managed to load, which is the legacy
                    // behaviour the user expects.
                    break;
                }
            }
        }

        /// <summary>
        /// Walks through the user's saved albums, yielding a tuple that lines up
        /// with the consumer logic in the view model. Artist IDs are packed with
        /// the same separators used elsewhere in the code base.
        /// </summary>
        public static async IAsyncEnumerable<(string Id, string Name, int TrackCount, string Artists)> GetUserAlbumsAsync()
        {
            var spotify = await SpotifySession.Instance.GetClientAsync();
            var page = await spotify.Library.GetAlbums(new LibraryAlbumsRequest { Limit = 50 });

            while (page != null && page.Items.Count > 0)
            {
                foreach (var album in page.Items)
                {
                    yield return (
                        album.Album.Id,
                        album.Album.Name,
                        album.Album.Tracks?.Total ?? 0,
                        string.Join(";;", album.Album.Artists.Select(a => a.Id))
                    );
                }

                try
                {
                    page = await spotify.NextPage(page);
                }
                catch
                {
                    // Stop iterating if Spotify signals an error while paging.
                    break;
                }
            }
        }

        /// <summary>
        /// Retrieves detailed playlist information including the snapshot ID and
        /// a flattened list of track IDs. Consumers rely on the string payloads
        /// so we preserve that structure while documenting the intent in code.
        /// </summary>
        public static async Task<(string? name, string? imageURL, string? Id, string? Description, string? SnapshotID, string? TrackIDs)> GetPlaylistDataAsync(string id)
        {
            var spotify = await SpotifySession.Instance.GetClientAsync();
            FullPlaylist playlist = await spotify.Playlists.Get(id);
            string trackIDs = string.Empty;
            string? imageURL = string.Empty;

            try
            {
                imageURL = playlist.Images[0].Url;
            }
            catch
            {
                // Older playlists occasionally lack artwork. Matching the
                // original implementation we fall back to an empty string.
                imageURL = string.Empty;
            }

            foreach (PlaylistTrack<IPlayableItem> item in playlist.Tracks.Items)
            {
                if (item.Track is FullTrack track)
                {
                    trackIDs += track.Id + ";;";
                }
            }

            return (playlist.Name, imageURL, playlist.Id, playlist.Description, playlist.SnapshotId, trackIDs);
        }

        public static async Task<Dictionary<string, (string? name, string? imageURL, string? Id, string? Description, string? SnapshotID, string? TrackIDs)>> GetPlaylistDataBatchAsync(IEnumerable<string> playlistIds)
        {
            var results = new Dictionary<string, (string? name, string? imageURL, string? Id, string? Description, string? SnapshotID, string? TrackIDs)>();
            var ids = NormalizeIds(playlistIds);

            if (ids.Count == 0)
            {
                return results;
            }

            var spotify = await SpotifySession.Instance.GetClientAsync();

            foreach (var batch in Chunk(ids, 50))
            {
                var tasks = batch.Select(async id =>
                {
                    try
                    {
                        return await spotify.Playlists.Get(id).ConfigureAwait(false);
                    }
                    catch
                    {
                        return null;
                    }
                }).ToArray();

                var playlists = await Task.WhenAll(tasks).ConfigureAwait(false);

                foreach (var playlist in playlists)
                {
                    if (playlist == null || string.IsNullOrEmpty(playlist.Id))
                    {
                        continue;
                    }

                    string imageURL = string.Empty;

                    if (playlist.Images != null && playlist.Images.Count > 0)
                    {
                        imageURL = playlist.Images[0].Url;
                    }

                    string trackIds = string.Empty;

                    if (playlist.Tracks?.Items != null)
                    {
                        foreach (var item in playlist.Tracks.Items)
                        {
                            if (item.Track is FullTrack track && !string.IsNullOrEmpty(track.Id))
                            {
                                trackIds += track.Id + ";;";
                            }
                        }
                    }

                    results[playlist.Id] = (
                        playlist.Name,
                        imageURL,
                        playlist.Id,
                        playlist.Description,
                        playlist.SnapshotId,
                        trackIds
                    );
                }
            }

            return results;
        }

        /// <summary>
        /// Pulls full album metadata plus lists of track and artist IDs. The
        /// data is formatted exactly like the legacy worker so downstream
        /// consumers do not need to change.
        /// </summary>
        public static async Task<(string? name, string? imageURL, string? Id, string TrackIDs, string artistIDs)> GetAlbumDataAsync(string id)
        {
            var spotify = await SpotifySession.Instance.GetClientAsync();
            FullAlbum album = await spotify.Albums.Get(id);
            string trackIDs = string.Empty;
            string imageURL = string.Empty;
            string artistIDs = string.Empty;

            try
            {
                imageURL = album.Images[0].Url;
            }
            catch
            {
                imageURL = string.Empty;
            }

            foreach (SimpleTrack track in album.Tracks.Items)
            {
                trackIDs += track.Id + ";;";
            }

            foreach (SimpleArtist artist in album.Artists)
            {
                artistIDs += artist.Id + ";;";
            }

            return (album.Name, imageURL, album.Id, trackIDs, artistIDs);
        }

        public static async Task<Dictionary<string, (string? name, string? imageURL, string? Id, string TrackIDs, string artistIDs)>> GetAlbumDataBatchAsync(IEnumerable<string> albumIds)
        {
            var results = new Dictionary<string, (string? name, string? imageURL, string? Id, string TrackIDs, string artistIDs)>();
            var ids = NormalizeIds(albumIds);

            if (ids.Count == 0)
            {
                return results;
            }

            var spotify = await SpotifySession.Instance.GetClientAsync();

            foreach (var batch in Chunk(ids, 20))
            {
                AlbumsResponse response;

                try
                {
                    response = await spotify.Albums.GetSeveral(new AlbumsRequest(batch)).ConfigureAwait(false);
                }
                catch
                {
                    continue;
                }

                if (response?.Albums == null)
                {
                    continue;
                }

                foreach (var album in response.Albums)
                {
                    if (album == null || string.IsNullOrEmpty(album.Id))
                    {
                        continue;
                    }

                    string imageURL = album.Images?.FirstOrDefault()?.Url ?? string.Empty;
                    string trackIds = string.Empty;

                    if (album.Tracks?.Items != null)
                    {
                        foreach (var track in album.Tracks.Items)
                        {
                            if (!string.IsNullOrEmpty(track.Id))
                            {
                                trackIds += track.Id + Variables.Seperator;
                            }
                        }
                    }

                    string artistIds = album.Artists != null && album.Artists.Any()
                        ? string.Join(Variables.Seperator, album.Artists.Where(a => !string.IsNullOrEmpty(a.Id)).Select(a => a.Id))
                        : string.Empty;

                    results[album.Id] = (
                        album.Name,
                        imageURL,
                        album.Id,
                        trackIds,
                        artistIds
                    );
                }
            }

            return results;
        }

        /// <summary>
        /// Returns a tuple of the most important song metadata for a given
        /// track. Tuples keep the public signature identical to the v1 worker so
        /// callers can continue unpacking values without refactoring.
        /// </summary>
        public static async Task<(string name, string id, string albumID, string artistIDs, int discnumber, int durrationms, bool Explicit, string previewURL, int tracknumber)> GetSongDataAsync(string id)
        {
            var spotify = await SpotifySession.Instance.GetClientAsync();
            FullTrack track = await spotify.Tracks.Get(id);
            string artistIDs = string.Join(Variables.Seperator, track.Artists.Select(a => a.Id));
            return (
                track.Name,
                track.Id,
                track.Album.Id,
                artistIDs,
                track.DiscNumber,
                track.DurationMs,
                track.Explicit,
                track.PreviewUrl,
                track.TrackNumber
            );
        }

        public static async Task<Dictionary<string, (string name, string id, string albumID, string artistIDs, int discnumber, int durrationms, bool Explicit, string previewURL, int tracknumber)>> GetSongDataBatchAsync(IEnumerable<string> trackIds)
        {
            var results = new Dictionary<string, (string name, string id, string albumID, string artistIDs, int discnumber, int durrationms, bool Explicit, string previewURL, int tracknumber)>();
            var ids = NormalizeIds(trackIds);

            if (ids.Count == 0)
            {
                return results;
            }

            var spotify = await SpotifySession.Instance.GetClientAsync();

            foreach (var batch in Chunk(ids, 50))
            {
                TracksResponse response;

                try
                {
                    response = await spotify.Tracks.GetSeveral(new TracksRequest(batch)).ConfigureAwait(false);
                }
                catch
                {
                    continue;
                }

                if (response?.Tracks == null)
                {
                    continue;
                }

                foreach (var track in response.Tracks)
                {
                    if (track == null || string.IsNullOrEmpty(track.Id))
                    {
                        continue;
                    }

                    string artistIDs = track.Artists != null && track.Artists.Any()
                        ? string.Join(Variables.Seperator, track.Artists.Where(a => !string.IsNullOrEmpty(a.Id)).Select(a => a.Id))
                        : string.Empty;

                    results[track.Id] = (
                        track.Name,
                        track.Id,
                        track.Album?.Id ?? string.Empty,
                        artistIDs,
                        track.DiscNumber,
                        track.DurationMs,
                        track.Explicit,
                        track.PreviewUrl,
                        track.TrackNumber
                    );
                }
            }

            return results;
        }

        /// <summary>
        /// Collects the basic artist info used throughout the UI. The method
        /// includes a defensive check for missing artwork because many artists
        /// do not expose images via the API.
        /// </summary>
        public static async Task<(string Id, string Name, string? ImageUrl, string Genres)> GetArtistDataAsync(string id)
        {
            var spotify = await SpotifySession.Instance.GetClientAsync();
            var artist = await spotify.Artists.Get(id);
            string? imageUrl = artist.Images?.FirstOrDefault()?.Url;
            string genres = artist.Genres != null && artist.Genres.Any()
                ? string.Join(";;", artist.Genres)
                : string.Empty;

            return (artist.Id, artist.Name, imageUrl, genres);
        }

        public static async Task<Dictionary<string, (string Id, string Name, string? ImageUrl, string Genres)>> GetArtistDataBatchAsync(IEnumerable<string> artistIds)
        {
            var results = new Dictionary<string, (string Id, string Name, string? ImageUrl, string Genres)>();
            var ids = NormalizeIds(artistIds);

            if (ids.Count == 0)
            {
                return results;
            }

            var spotify = await SpotifySession.Instance.GetClientAsync();

            foreach (var batch in Chunk(ids, 50))
            {
                ArtistsResponse response;

                try
                {
                    response = await spotify.Artists.GetSeveral(new ArtistsRequest(batch)).ConfigureAwait(false);
                }
                catch
                {
                    continue;
                }

                if (response?.Artists == null)
                {
                    continue;
                }

                foreach (var artist in response.Artists)
                {
                    if (artist == null || string.IsNullOrEmpty(artist.Id))
                    {
                        continue;
                    }

                    string? imageUrl = artist.Images?.FirstOrDefault()?.Url;
                    string genres = artist.Genres != null && artist.Genres.Any()
                        ? string.Join(";;", artist.Genres)
                        : string.Empty;

                    results[artist.Id] = (
                        artist.Id,
                        artist.Name,
                        imageUrl,
                        genres
                    );
                }
            }

            return results;
        }

        /// <summary>
        /// Adds the provided track IDs to a playlist. The worker handles the
        /// conversion from raw Spotify IDs to Spotify URIs so callers can remain
        /// agnostic about URI formatting rules.
        /// </summary>
        public static async Task AddTracksToPlaylistAsync(string playlistId, List<string> trackIds)
        {
            var spotify = await SpotifySession.Instance.GetClientAsync();
            var uris = trackIds.ConvertAll(id => $"spotify:track:{id}");
            var request = new PlaylistAddItemsRequest(uris);
            await spotify.Playlists.AddItems(playlistId, request);
        }

        private static List<string> NormalizeIds(IEnumerable<string> ids)
        {
            var results = new List<string>();

            if (ids == null)
            {
                return results;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var normalized = id.Trim();

                if (seen.Add(normalized))
                {
                    results.Add(normalized);
                }
            }

            return results;
        }

        private static IEnumerable<List<T>> Chunk<T>(IReadOnlyList<T> source, int size)
        {
            if (source.Count == 0 || size <= 0)
            {
                yield break;
            }

            for (int i = 0; i < source.Count; i += size)
            {
                int count = Math.Min(size, source.Count - i);
                var chunk = new List<T>(count);

                for (int j = 0; j < count; j++)
                {
                    chunk.Add(source[i + j]);
                }

                yield return chunk;
            }
        }
    }

    /// <summary>
    /// Thread-safe singleton responsible for storing credentials, refreshing
    /// tokens, and providing a configured <see cref="SpotifyClient"/> instance.
    /// The worker above delegates all state management to this type so that it
    /// can focus on the public API surface.
    /// </summary>
    sealed class SpotifySession
    {
        private static readonly Lazy<SpotifySession> _instance = new(() => new SpotifySession());

        private SpotifyClient? _client;
        private string? _accessToken;
        private string? _refreshToken;
        private DateTime _expiresAt = DateTime.MinValue;
        private string? _clientId;
        private string? _clientSecret;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _configured;

        public static SpotifySession Instance => _instance.Value;

        private SpotifySession()
        {
        }

        public bool HasTokens => !string.IsNullOrEmpty(_accessToken) && !string.IsNullOrEmpty(_refreshToken);

        /// <summary>
        /// Configures the session with the credentials required to establish a
        /// Spotify connection. Optional token values allow the app to resume a
        /// previous session without forcing the user back through the OAuth
        /// flow.
        /// </summary>
        public void Initialize(string clientId, string clientSecret, string? accessToken = null, string? refreshToken = null, DateTime? expiresAt = null)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new ArgumentException("A Spotify client ID is required when configuring the session.", nameof(clientId));
            }

            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new ArgumentException("A Spotify client secret is required when configuring the session.", nameof(clientSecret));
            }

            _clientId = clientId.Trim();
            _clientSecret = clientSecret.Trim();
            _configured = true;

            if (!string.IsNullOrEmpty(accessToken))
            {
                _accessToken = accessToken;
                _client = new SpotifyClient(accessToken);
            }

            if (!string.IsNullOrEmpty(refreshToken))
            {
                _refreshToken = refreshToken;
            }

            if (expiresAt.HasValue && expiresAt.Value != DateTime.MinValue)
            {
                _expiresAt = expiresAt.Value;
            }
        }

        /// <summary>
        /// Returns a simple tuple representing the cached token state. The
        /// method avoids exposing the underlying fields so callers cannot
        /// mutate them directly.
        /// </summary>
        public (string AccessToken, string RefreshToken, DateTime ExpiresAt) GetTokenSnapshot()
        {
            return (
                _accessToken ?? string.Empty,
                _refreshToken ?? string.Empty,
                _expiresAt
            );
        }

        /// <summary>
        /// Returns a tuple of the stored client credentials so that
        /// <see cref="SpotifyWorker"/> can fall back to them if the caller did
        /// not provide explicit values.
        /// </summary>
        public (string? ClientId, string? ClientSecret) GetCredentialSnapshot()
        {
            return (_clientId, _clientSecret);
        }

        /// <summary>
        /// Produces a ready-to-use <see cref="SpotifyClient"/>. The method takes
        /// care of refreshing tokens when necessary and lazily creates the
        /// client to avoid unnecessary network calls.
        /// </summary>
        public async Task<SpotifyClient> GetClientAsync()
        {
            if (!_configured)
            {
                throw new InvalidOperationException("SpotifySession has not been configured. Call SpotifyWorker.Init first.");
            }

            await _lock.WaitAsync().ConfigureAwait(false);

            try
            {
                if (string.IsNullOrEmpty(_accessToken) || string.IsNullOrEmpty(_refreshToken))
                {
                    throw new InvalidOperationException("SpotifySession is missing tokens. Call SpotifyWorker.AuthenticateAsync to establish a session.");
                }

                var threshold = TimeSpan.FromMinutes(5);
                bool needsRefresh = _expiresAt == DateTime.MinValue || DateTime.UtcNow >= _expiresAt - threshold;

                if (needsRefresh)
                {
                    if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
                    {
                        throw new InvalidOperationException("SpotifySession is missing client credentials required for token refresh.");
                    }

                    // Delegate the refresh logic to the static helper so that
                    // both the worker and the session share the same behaviour.
                    var (newAccessToken, newRefreshToken, newExpiresAt) = await SpotifyWorker.RefreshTokensIfNeededAsync(
                        _clientId,
                        _clientSecret,
                        _accessToken,
                        _refreshToken,
                        _expiresAt,
                        threshold
                    ).ConfigureAwait(false);

                    _accessToken = newAccessToken;
                    _refreshToken = newRefreshToken;
                    _expiresAt = newExpiresAt;
                    _client = new SpotifyClient(_accessToken);
                }
                else if (_client == null)
                {
                    // Lazily create the Spotify client if we have tokens but
                    // have not yet needed to execute an API call in this run.
                    _client = new SpotifyClient(_accessToken);
                }

                return _client;
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
