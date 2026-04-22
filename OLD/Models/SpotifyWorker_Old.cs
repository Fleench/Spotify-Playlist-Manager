/* File: SpotifyWorker_Old.cs
 * Author: Glenn Sutherland, ChatGPT Codex
 * Description: A wrapper for the SpotifyAPI.Web module. This abstraction allows
 * this module to be used instead ensuring that if the API used by the module is
 * switched the programs using this module do not break.
 */
using System.Security.Cryptography;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Spotify_Playlist_Manager.Models
{
    static class SpotifyWorker_Old
    {
        /// <summary>
        /// The Spotify application client ID. This is a unique identifier for your application.
        /// It is provided by Spotify when you register your application in the Spotify Developer Dashboard.
        /// </summary>
        private static string ClientID;

        /// <summary>
        /// The Spotify application client secret. This is a secret key that is used to authenticate your application.
        /// It is provided by Spotify when you register your application in the Spotify Developer Dashboard.
        /// Keep this secret confidential.
        /// </summary>
        private static string ClientSecret;

        /// <summary>
        /// The access token obtained from Spotify after a successful authentication.
        /// This token is used to make authorized requests to the Spotify API.
        /// It has a limited lifetime and needs to be refreshed periodically.
        /// </summary>
        private static string AccessToken;

        /// <summary>
        /// The refresh token obtained from Spotify after a successful authentication.
        /// This token is used to obtain a new access token when the current one expires,
        /// without requiring the user to go through the authentication flow again.
        /// </summary>
        private static string RefreshToken;

        /// <summary>
        /// The redirect URI that Spotify will use to send the authorization code back to the application
        /// after the user has granted permission. This must match one of the redirect URIs
        /// configured in your Spotify application settings.
        /// </summary>
        private static string Uri = "http://127.0.0.1:5543/callback";

        /// <summary>
        /// The port number on which the local server will listen for the OAuth callback.
        /// This port must be available and should match the port specified in the redirect URI.
        /// </summary>
        private static int port = 5543;

        /// <summary>
        /// The date and time when the current access token expires.
        /// This is used to determine if the access token needs to be refreshed.
        /// </summary>
        private static DateTime Expires = DateTime.MinValue;

        /// <summary>
        /// The embedded web server instance that listens for the OAuth callback from Spotify.
        /// This server is used to capture the authorization code during the authentication flow.
        /// </summary>
        private static EmbedIOAuthServer _server;

        private static SpotifyClient spotify;

        /// <summary>
        /// Initializes the SpotifyWorker_Old with the necessary credentials. This method must be called
        /// before any other methods in this class to ensure that the client ID and client secret are set up correctly.
        /// It also allows for providing existing access and refresh tokens to avoid re-authentication.
        /// </summary>
        /// <param name="ck">The client key (ID) for your Spotify application. This is a public identifier.</param>
        /// <param name="cs">The client secret for your Spotify application. This is a confidential key.</param>
        /// <param name="at">An optional existing access token. If provided, this token will be used for API requests.</param>
        /// <param name="rt">An optional existing refresh token. If provided, this token will be used to refresh the access token when it expires.</param>
        /// <param name="e">An optional expiration date for the existing access token. This helps in determining if the token needs to be refreshed.</param>
        public static void Init(string ck, string cs, string at = "", string rt = "", DateTime e = new DateTime())
        {
            ClientID = ck;
            ClientSecret = cs;
            if (at != "")
            {
                AccessToken = at;
            }

            if (rt != "")
            {
                RefreshToken = rt;
            }

            if (e != DateTime.MinValue)
            {
                Expires = e;
            }
        }

        /// <summary>
        /// Authenticates the user with Spotify. This is the main entry point for starting the authentication process.
        /// If a valid refresh token is available, it will attempt to refresh the access token silently.
        /// Otherwise, it will initiate the full OAuth 2.0 authentication flow, which may require user interaction
        /// (i.e., opening a browser window for the user to log in and grant permissions).
        /// </summary>
        /// <returns>A tuple containing the new access token and refresh token upon successful authentication.</returns>
        public static async Task<(string AccessToken, string RefreshToken)> AuthenticateAsync()
        {
            if (string.IsNullOrEmpty(RefreshToken) || string.IsNullOrEmpty(AccessToken))
            {
                var (accessToken, refreshToken, expiresAt) = await AuthenticateFlowAsync(ClientID, ClientSecret);

                AccessToken = accessToken;
                RefreshToken = refreshToken;
                Expires = expiresAt;
            }
            else
            {
                var (accessToken, refreshToken, expiresAt) = await RefreshTokensIfNeededAsync(
                    ClientID,
                    ClientSecret,
                    AccessToken,
                    RefreshToken,
                    Expires
                );

                AccessToken = accessToken;
                RefreshToken = refreshToken;
                Expires = expiresAt;
            }
            spotify = new SpotifyClient(AccessToken);
            return (AccessToken, RefreshToken);
        }

        /// <summary>
        /// Refreshes the access token if it is expired or about to expire.
        /// This method is called internally by `AuthenticateAsync` but can also be used directly
        /// if you need to manually manage token refreshing. It checks the expiration time of the
        /// current token and, if it's close to expiring, requests a new one using the refresh token.
        /// </summary>
        /// <param name="clientId">The Spotify application client ID.</param>
        /// <param name="clientSecret">The Spotify application client secret.</param>
        /// <param name="accessToken">The current access token.</param>
        /// <param name="refreshToken">The refresh token.</param>
        /// <param name="expiresAtUtc">The UTC expiration time of the current access token.</param>
        /// <param name="refreshThreshold">The time before expiration to refresh the token. If the token is within this timespan of expiring, it will be refreshed. Defaults to 5 minutes.</param>
        /// <returns>A tuple containing the new access token, the (possibly updated) refresh token, and the new expiration time.</returns>
        public static async Task<(string accessToken, string refreshToken, DateTime expiresAt)> RefreshTokensIfNeededAsync(
            string clientId,
            string clientSecret,
            string accessToken,
            string refreshToken,
            DateTime expiresAtUtc,
            TimeSpan? refreshThreshold = null)
        {
            // Default to refreshing if token expires within the next 5 minutes
            var threshold = refreshThreshold ?? TimeSpan.FromMinutes(5);

            if (expiresAtUtc == DateTime.MinValue)
            {
                // Token expiration is unknown, so we must force a refresh to be safe.
            }
            else if (DateTime.UtcNow < expiresAtUtc - threshold)
            {
                // Token is still valid and not close to expiring, so no need to refresh.
                return (accessToken, refreshToken, expiresAtUtc);
            }

            // Token is expired or about to expire, so we need to refresh it.
            var config = SpotifyClientConfig.CreateDefault();
            var tokenResponse = await new OAuthClient(config).RequestToken(
                new AuthorizationCodeRefreshRequest(clientId, clientSecret, refreshToken)
            );

            var newAccessToken = tokenResponse.AccessToken;
            // A new refresh token is not always returned. If it's null, we keep the old one.
            var newRefreshToken = tokenResponse.RefreshToken ?? refreshToken;
            var newExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

            return (newAccessToken, newRefreshToken, newExpiresAt);
        }

        /// <summary>
        /// Performs the full OAuth 2.0 authorization code flow. This method should be used when
        /// no valid refresh token is available. It starts a local server to listen for the callback from Spotify,
        /// opens a browser window for the user to authorize the application, and then exchanges the
        /// authorization code received from Spotify for an access token and a refresh token.
        /// </summary>
        /// <param name="clientId">The Spotify application client ID.</param>
        /// <param name="clientSecret">The Spotify application client secret.</param>
        /// <returns>A tuple containing the newly obtained access token, refresh token, and the expiration time of the access token.</returns>
        public static async Task<(string accessToken, string refreshToken, DateTime expiresAt)> AuthenticateFlowAsync(string clientId, string clientSecret)
        {
            // Start a local server to listen for the callback.
            _server = new EmbedIOAuthServer(new Uri(Uri), port);
            await _server.Start();

            var tcs = new TaskCompletionSource<(string, string, DateTime)>();

            // Event handler for when the authorization code is received.
            _server.AuthorizationCodeReceived += async (sender, response) =>
            {
                // Stop the server as it's no longer needed.
                await _server.Stop();

                // Exchange the authorization code for an access token and refresh token.
                var config = SpotifyClientConfig.CreateDefault();
                var tokenResponse = await new OAuthClient(config).RequestToken(
                    new AuthorizationCodeTokenRequest(clientId, clientSecret, response.Code, new Uri(Uri))
                );

                // Calculate the expiration time of the new access token.
                var expiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
                // Set the result of the TaskCompletionSource to signal that the flow is complete.
                tcs.SetResult((tokenResponse.AccessToken, tokenResponse.RefreshToken, expiresAt));
            };



            // Event handler for any errors that occur during the authentication flow.
            _server.ErrorReceived += async (sender, error, state) =>
            {
                // Stop the server and set an exception on the TaskCompletionSource.
                await _server.Stop();
                tcs.SetException(new Exception($"Spotify auth error: {error}"));
            };

            // Create the login request and open the user's browser to the Spotify authorization page.
            var request = new LoginRequest(_server.BaseUri, clientId, LoginRequest.ResponseType.Code)
            {
                // Define the scopes (permissions) that the application is requesting.
                Scope = new List<string> { Scopes.UserReadEmail, Scopes.PlaylistModifyPrivate, Scopes.PlaylistModifyPublic, Scopes.PlaylistReadCollaborative, Scopes.PlaylistReadPrivate, Scopes.UserLibraryRead, Scopes.UserLibraryModify, Scopes.UserReadPrivate }
            };
            BrowserUtil.Open(request.ToUri());

            // Wait for the TaskCompletionSource to be completed (either with a result or an exception).
            return await tcs.Task;
        }
        //saved tracks
        
        public static async IAsyncEnumerable<(string Id, string Name, string Artists)> GetLikedSongsAsync()
        {
            await AuthenticateAsync();
            // Request the first page (50 is Spotify’s max page size)
            var page = await spotify.Library.GetTracks(new LibraryTracksRequest { Limit = 50 });


            while (page != null && page.Items.Count > 0)
            {
                foreach (var item in page.Items)
                {
                    var track = item.Track;

                    if (track == null)
                        continue;

                    yield return (
                        track.Id,
                        track.Name,
                        string.Join(";;", track.Artists.Select(a => a.Id))
                    );
                }

                // Move to next page
                try
                {
                    page = await spotify.NextPage(page);
                }
                catch
                {
                    break;
                }
                
            }

            //Console.WriteLine($"Total liked tracks: {totalCount}");
        }
        //user playlists
        public static async IAsyncEnumerable<(string Id, string Name, int TrackCount)> GetUserPlaylistsAsync()
        {
            await AuthenticateAsync();
            var page = await spotify.Playlists.CurrentUsers(new PlaylistCurrentUsersRequest() { Limit = 50 });
            //Console.WriteLine($"Got first page: {firstPage.Items.Count} playlists, next: {firstPage.Next}");

            while (page != null && page.Items.Count > 0)
            {
                foreach (var playlist in page.Items)
                {
                    //Console.WriteLine($"Yielding from page {page}: {playlist.Name}");
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
                    break;
                }
                
            }
            
        }
        //user albums
        public static async IAsyncEnumerable<(string Id, string Name, int TrackCount, string Artists)> GetUserAlbumsAsync()
        {
            await AuthenticateAsync();
            var page = await spotify.Library.GetAlbums(new LibraryAlbumsRequest() { Limit = 50 });
            //Console.WriteLine($"Got first page: {firstPage.Items.Count} playlists, next: {firstPage.Next}");

            while (page != null && page.Items.Count > 0)
            {
                foreach (var Album in page.Items)
                {
                    //Console.WriteLine($"Yielding from page {page}: {playlist.Name}");
                    yield return (
                        Album.Album.Id,
                        Album.Album.Name,
                        Album.Album.Tracks?.Total ?? 0,
                        string.Join(";;", Album.Album.Artists.Select(a => a.Id))
                    );

                }
                try
                {
                    page = await spotify.NextPage(page);
                }
                catch
                {
                    break;
                }
                
            }
        }
        //more data
        public static async Task<(string? name, string? imageURL, string? Id, string? Description, string? SnapshotID, string? TrackIDs)> GetPlaylistDataAsync(string id)
        {
            await AuthenticateAsync();
            FullPlaylist playlist = await spotify.Playlists.Get(id);
            string TrackIDs = "";
            string? imageURL = "";
            try
            {
                 imageURL = playlist.Images[0].Url;
            }
            catch
            {
                 imageURL = "";
            }
            
            foreach (PlaylistTrack<IPlayableItem> item in playlist.Tracks.Items)
            {
                if (item.Track is FullTrack track)
                {
                    // All FullTrack properties are available
                    //Console.WriteLine(track.Id);
                    TrackIDs += track.Id + ";;";
                }
            }

            return (playlist.Name, imageURL, playlist.Id, playlist.Description, playlist.SnapshotId, TrackIDs);
        }
        public static async Task<(string? name, string? imageURL, string? Id, string TrackIDs,string artistIDs)> GetAlbumDataAsync(string id)
        {
            await AuthenticateAsync();
            FullAlbum album = await spotify.Albums.Get(id);
            string TrackIDs = "";
            string imageURL = "";
            string artistIDs = "";
            try
            {
                imageURL = album.Images[0].Url;
            }
            catch
            {
                imageURL = "";
            }
            foreach (SimpleTrack track in album.Tracks.Items)
            {
                    // All FullTrack properties are available
                    //Console.WriteLine(track.Id);
                    TrackIDs += track.Id + ";;";
            }
            foreach (SimpleArtist artist in album.Artists)
            {
                artistIDs += artist.Id + ";;";
            }
            return (album.Name, imageURL, album.Id,TrackIDs,artistIDs);
        }
        //song data
        public static async Task<(string name, string id, string albumID, string artistIDs, int discnumber, int durrationms, bool Explicit, string previewURL, int tracknumber)> GetSongDataAsync(string id)
        {
            await AuthenticateAsync();
            FullTrack track = await spotify.Tracks.Get(id);
            string artistIDs = string.Join(Variables.Seperator, track.Artists.Select(a => a.Id));;
            return (track.Name,track.Id,track.Album.Id,artistIDs,track.DiscNumber,track.DurationMs,track.Explicit,track.PreviewUrl,track.TrackNumber);
        }
        //artist data
        public static async Task<(string Id, string Name, string? ImageUrl, string Genres)> GetArtistDataAsync(string id)
        {
            var artist = await spotify.Artists.Get(id);
            string? imageUrl = artist.Images?.FirstOrDefault()?.Url;
            string genres = artist.Genres != null && artist.Genres.Any()
                ? string.Join(";;", artist.Genres)
                : string.Empty;

            return (artist.Id, artist.Name, imageUrl, genres);
        }

        //send data
        public static async Task AddTracksToPlaylistAsync(string playlistId, List<string> trackIds)
        {
            await AuthenticateAsync();
            var uris = trackIds.ConvertAll(id => $"spotify:track:{id}");
            var request = new PlaylistAddItemsRequest(uris);
            await spotify.Playlists.AddItems(playlistId, request);
        }

        /// <summary>
        /// Provides the currently cached Spotify authentication tokens and their expiration timestamp.
        /// Returns empty strings for tokens when the authentication flow has not yet completed.
        /// </summary>
        /// <returns>A tuple containing the access token, refresh token, and token expiration time.</returns>
        public static (string AccessToken, string RefreshToken, DateTime ExpiresAt) GetCurrentTokens()
        {
            return (AccessToken ?? string.Empty, RefreshToken ?? string.Empty, Expires);
        }
    }
}