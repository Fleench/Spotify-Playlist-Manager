/* File: DataCoordinator.cs
 * Author: Glenn Sutherland, ChatGPT Codex
 * Description: Connects the SpotifyWorker to the DatabaseWorker
 */

using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpotifyAPI.Web;
using Spotify_Playlist_Manager.Models;
namespace Spotify_Playlist_Manager.Models
{
    public enum SyncEntryPoint
    {
        Playlists = 0,
        Albums = 1,
        LikedSongs = 2,
        TrackMetadata = 3,
        AlbumMetadata = 4,
        ArtistMetadata = 5
    }

    /// <summary>
    /// High-level orchestrator that synchronizes Spotify data with the local
    /// SQLite cache. The coordinator translates worker responses into
    /// <see cref="Variables"/> models and delegates persistence to
    /// <see cref="DatabaseWorker"/>.
    /// </summary>
    static class DataCoordinator
    {
        private static readonly TimeSpan RateLimitRetryThreshold = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan MinimumRetryDelay = TimeSpan.FromSeconds(1);
        private const string LegacyArtistSeparator = "::";
        private static readonly string[] ArtistIdSeparators = new[] { Variables.Seperator, LegacyArtistSeparator };

        private static void EnsureValidId(string? id, string paramName)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A valid identifier is required.", paramName);
            }
        }

        private static void EnsureValidSongId(Variables.Track track)
        {
            if (string.IsNullOrWhiteSpace(track.SongID))
            {
                track.SongID = Variables.MakeId();
            }
        }

        private static void EnsureValidSongIdentifier(string? songId, string paramName)
        {
            if (string.IsNullOrWhiteSpace(songId))
            {
                throw new ArgumentException("A valid song identifier is required.", paramName);
            }
        }

        /// <summary>
        /// Writes a settings key/value pair to the database.
        /// </summary>
        public static async Task SetSettingAsync(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A valid setting key is required.", nameof(key));
            }

            await DatabaseWorker.SetSetting(key, value);
        }

        /// <summary>
        /// Deletes the specified settings key.
        /// </summary>
        public static async Task RemoveSettingAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A valid setting key is required.", nameof(key));
            }

            await DatabaseWorker.RemoveSetting(key);
        }

        /// <summary>
        /// Retrieves a settings value or <c>null</c> when the key does not exist.
        /// </summary>
        public static string? GetSetting(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A valid setting key is required.", nameof(key));
            }

            return DatabaseWorker.GetSetting(key);
        }

        /// <summary>
        /// Enumerates all persisted settings.
        /// </summary>
        public static IEnumerable<(string key, string value)> GetAllSettings()
        {
            return DatabaseWorker.GetAllSettings();
        }

        /// <summary>
        /// Adds or updates a playlist, caching its artwork when necessary.
        /// </summary>
        public static async Task SetPlaylistAsync(Variables.PlayList playlist)
        {
            ArgumentNullException.ThrowIfNull(playlist);
            EnsureValidId(playlist.Id, nameof(playlist.Id));
            string? playlistImagePath = CacheWorker.GetImagePath(CacheWorker.ImageType.Playlist, playlist.Id);
            if (string.IsNullOrWhiteSpace(playlistImagePath) && !string.IsNullOrWhiteSpace(playlist.ImageURL))
            {
                playlistImagePath = await CacheWorker.DownloadImageAsync(playlist.ImageURL, CacheWorker.ImageType.Playlist, playlist.Id);
            }

            playlist.ImagePath = playlistImagePath ?? string.Empty;
            await DatabaseWorker.SetPlaylist(playlist);
        }

        /// <summary>
        /// Removes a playlist by identifier.
        /// </summary>
        public static async Task RemovePlaylistAsync(string playlistId)
        {
            EnsureValidId(playlistId, nameof(playlistId));

            await DatabaseWorker.RemovePlaylist(playlistId);
        }

        /// <summary>
        /// Retrieves a playlist from the local cache.
        /// </summary>
        public static Variables.PlayList? GetPlaylist(string playlistId)
        {
            EnsureValidId(playlistId, nameof(playlistId));

            return DatabaseWorker.GetPlaylist(playlistId);
        }

        /// <summary>
        /// Enumerates all cached playlists.
        /// </summary>
        public static IEnumerable<Variables.PlayList> GetAllPlaylists()
        {
            return DatabaseWorker.GetAllPlaylists();
        }

        /// <summary>
        /// Adds or updates an album, caching associated artwork and normalizing
        /// artist identifiers.
        /// </summary>
        public static async Task SetAlbumAsync(Variables.Album album)
        {
            ArgumentNullException.ThrowIfNull(album);
            EnsureValidId(album.Id, nameof(album.Id));
            string? albumImagePath = CacheWorker.GetImagePath(CacheWorker.ImageType.Album, album.Id);
            if (string.IsNullOrWhiteSpace(albumImagePath) && !string.IsNullOrWhiteSpace(album.ImageURL))
            {
                albumImagePath = await CacheWorker.DownloadImageAsync(album.ImageURL, CacheWorker.ImageType.Album, album.Id);
            }

            album.ImagePath = albumImagePath ?? string.Empty;
            album.ArtistIDs = NormalizeArtistIdString(album.ArtistIDs);
            await DatabaseWorker.SetAlbum(album);
        }

        /// <summary>
        /// Removes an album by identifier.
        /// </summary>
        public static async Task RemoveAlbumAsync(string albumId)
        {
            EnsureValidId(albumId, nameof(albumId));

            await DatabaseWorker.RemoveAlbum(albumId);
        }

        /// <summary>
        /// Retrieves an album from the cache.
        /// </summary>
        public static Variables.Album? GetAlbum(string albumId)
        {
            EnsureValidId(albumId, nameof(albumId));

            return DatabaseWorker.GetAlbum(albumId);
        }

        /// <summary>
        /// Enumerates every cached album.
        /// </summary>
        public static IEnumerable<Variables.Album> GetAllAlbums()
        {
            return DatabaseWorker.GetAllAlbums();
        }

        /// <summary>
        /// Adds or updates a track and normalizes artist identifiers.
        /// </summary>
        public static async Task SetTrackAsync(Variables.Track track)
        {
            ArgumentNullException.ThrowIfNull(track);
            EnsureValidId(track.Id, nameof(track.Id));
            EnsureValidSongId(track);

            track.ArtistIds = NormalizeArtistIdString(track.ArtistIds);

            await DatabaseWorker.SetTrack(track);
        }

        /// <summary>
        /// Removes a track by Spotify ID.
        /// </summary>
        public static async Task RemoveTrackAsync(string trackId)
        {
            EnsureValidId(trackId, nameof(trackId));

            await DatabaseWorker.RemoveTrack(trackId);
        }

        /// <summary>
        /// Retrieves a track from the cache.
        /// </summary>
        public static Variables.Track GetTrack(string trackId)
        {
            EnsureValidId(trackId, nameof(trackId));

            return DatabaseWorker.GetTrack(trackId);
        }

        /// <summary>
        /// Enumerates all cached tracks.
        /// </summary>
        public static IEnumerable<Variables.Track> GetAllTracks()
        {
            return DatabaseWorker.GetAllTracks();
        }

        /// <summary>
        /// Counts how many tracks share the same internal song identifier.
        /// </summary>
        public static int GetTrackCountBySongId(string songId)
        {
            EnsureValidSongIdentifier(songId, nameof(songId));

            return DatabaseWorker.GetTrackCountBySongId(songId);
        }

        /// <summary>
        /// Adds or updates an artist and caches the artist's artwork.
        /// </summary>
        public static async Task SetArtistAsync(Variables.Artist artist)
        {
            ArgumentNullException.ThrowIfNull(artist);
            EnsureValidId(artist.Id, nameof(artist.Id));
            string? artistImagePath = CacheWorker.GetImagePath(CacheWorker.ImageType.Artist, artist.Id);
            if (string.IsNullOrWhiteSpace(artistImagePath) && !string.IsNullOrWhiteSpace(artist.ImageURL))
            {
                artistImagePath = await CacheWorker.DownloadImageAsync(artist.ImageURL, CacheWorker.ImageType.Artist, artist.Id);
            }

            artist.ImagePath = artistImagePath ?? string.Empty;
            await DatabaseWorker.SetArtist(artist);
        }

        /// <summary>
        /// Removes an artist by identifier.
        /// </summary>
        public static async Task RemoveArtistAsync(string artistId)
        {
            EnsureValidId(artistId, nameof(artistId));

            await DatabaseWorker.RemoveArtist(artistId);
        }

        /// <summary>
        /// Retrieves an artist from the cache.
        /// </summary>
        public static Variables.Artist? GetArtist(string artistId)
        {
            EnsureValidId(artistId, nameof(artistId));

            return DatabaseWorker.GetArtist(artistId);
        }

        /// <summary>
        /// Enumerates all cached artists.
        /// </summary>
        public static IEnumerable<Variables.Artist> GetAllArtists()
        {
            return DatabaseWorker.GetAllArtists();
        }

        /// <summary>
        /// Persists a similarity relationship between two tracks.
        /// </summary>
        public static async Task SetSimilarAsync(string songId, string songId2, string type)
        {
            EnsureValidSongIdentifier(songId, nameof(songId));
            EnsureValidSongIdentifier(songId2, nameof(songId2));
            if (string.IsNullOrWhiteSpace(type))
            {
                throw new ArgumentException("A valid similarity type is required.", nameof(type));
            }

            await DatabaseWorker.SetSimilar(songId, songId2, type);
        }

        /// <summary>
        /// Removes a similarity relationship.
        /// </summary>
        public static async Task RemoveSimilarAsync(string songId, string songId2, string type)
        {
            EnsureValidSongIdentifier(songId, nameof(songId));
            EnsureValidSongIdentifier(songId2, nameof(songId2));
            if (string.IsNullOrWhiteSpace(type))
            {
                throw new ArgumentException("A valid similarity type is required.", nameof(type));
            }

            await DatabaseWorker.RemoveSimilar(songId, songId2, type);
        }

        /// <summary>
        /// Retrieves a similarity relationship when it exists.
        /// </summary>
        public static (string SongId, string SongId2, string Type)? GetSimilar(string songId, string songId2)
        {
            EnsureValidSongIdentifier(songId, nameof(songId));
            EnsureValidSongIdentifier(songId2, nameof(songId2));

            return DatabaseWorker.GetSimilar(songId, songId2);
        }

        /// <summary>
        /// Enumerates all similarity relationships.
        /// </summary>
        public static IEnumerable<(string SongId, string SongId2, string Type)> GetAllSimilar()
        {
            return DatabaseWorker.GetAllSimilar();
        }

        /// <summary>
        /// Records a potential similarity for later review.
        /// </summary>
        public static async Task SetMightBeSimilarAsync(string songId, string songId2)
        {
            EnsureValidSongIdentifier(songId, nameof(songId));
            EnsureValidSongIdentifier(songId2, nameof(songId2));

            await DatabaseWorker.SetMightBeSimilar(songId, songId2);
        }

        /// <summary>
        /// Removes a potential similarity entry.
        /// </summary>
        public static async Task RemoveMightBeSimilarAsync(string songId, string songId2)
        {
            EnsureValidSongIdentifier(songId, nameof(songId));
            EnsureValidSongIdentifier(songId2, nameof(songId2));

            await DatabaseWorker.RemoveMightBeSimilar(songId, songId2);
        }

        /// <summary>
        /// Retrieves a potential similarity entry.
        /// </summary>
        public static (string SongId, string SongId2)? GetMightBeSimilar(string songId, string songId2)
        {
            EnsureValidSongIdentifier(songId, nameof(songId));
            EnsureValidSongIdentifier(songId2, nameof(songId2));

            return DatabaseWorker.GetMightBeSimilar(songId, songId2);
        }

        /// <summary>
        /// Enumerates all potential similarity entries.
        /// </summary>
        public static IEnumerable<(string SongId, string SongId2)> GetAllMightBeSimilar()
        {
            return DatabaseWorker.GetAllMightBeSimilar();
        }

        /// <summary>
        /// Legacy synchronization pipeline kept for reference. It performs the
        /// full synchronization flow without retry logic or batching optimizations.
        /// </summary>
        public static async Task SlowSync()
        {
            /* Playlists → Playlist Tracks

            Fetch all user playlists.

            For each playlist, pull its track list and save locally.

            Albums → Album Tracks

            Fetch all saved albums.
    
            For each album, pull its track list and save locally.
    
            Liked Songs

            Fetch all saved (liked) tracks.

            Save them and include extra Spotify API fields that liked tracks provide (like added_at).

            Missing Song Data

            Scan for any tracks without full metadata and request those track details.

            Unique Albums

            From all gathered tracks, create a unique album list that hasn’t already been stored.

            Fetch missing album metadata.

            Unique Artists

            From all gathered tracks and albums, create a unique artist list.

            Fetch and save their metadata.
            */
             //1. Set playlists
            await foreach(var item in SpotifyWorker.GetUserPlaylistsAsync())
            {
                //Console.WriteLine($"{item.Id} - {item.Name}");
                var data = await SpotifyWorker.GetPlaylistDataAsync(item.Id);
                Variables.PlayList playlist = new()
                {
                    Id = data.Id,
        Name = data.name,
        ImageURL = data.imageURL,
        Description = data.Description,
        SnapshotID=data.SnapshotID,
        TrackIDs = data.TrackIDs,
                };
                Variables.PlayList? existingPlaylist = GetPlaylist(playlist.Id);
                if (existingPlaylist == null || existingPlaylist.SnapshotID != playlist.SnapshotID)
                {
                    await SetPlaylistAsync(playlist);
                    foreach (string id in SplitIds(playlist.TrackIDs, Variables.Seperator))
                    {
                        await SetTrackAsync(new Variables.Track() {Id = id});
                    }
                }
            }
            Console.WriteLine("Got Playlists");
            //2. Set albums
            await foreach (var item in SpotifyWorker.GetUserAlbumsAsync())
            {
                if (GetAlbum(item.Id) is null)
                {
                    var data = await SpotifyWorker.GetAlbumDataAsync(item.Id);
                    Variables.Album album = new()
                    {
                        Id = data.Id,
                        Name = data.name,
                        ImageURL = data.imageURL,
                        ArtistIDs = data.artistIDs,
                    };
                    foreach (string id in SplitIds(data.TrackIDs, Variables.Seperator))
                    {
                        await SetTrackAsync(new Variables.Track() {Id = id});
                    }
                    await SetAlbumAsync(album);
                }

            }
            Console.WriteLine("Got Albums");
            //3. set liked songs
            await foreach (var item in SpotifyWorker.GetLikedSongsAsync())
            {
                await SetTrackAsync(new Variables.Track() {Id = item.Id});
            }
            Console.WriteLine("Got Liked Songs");
            //update track data
            foreach (var item in GetAllTracks())
            {
                string albumID = item.AlbumId;
                var artistIDs = SplitArtistIds(item.ArtistIds);
                if (item.MissingInfo())
                {
                    var data = await SpotifyWorker.GetSongDataAsync(item.Id);
                    await SetTrackAsync(new Variables.Track()
                    {
                        Id = item.Id,
                        Name = data.name,
                        AlbumId = data.albumID,
                        ArtistIds = data.artistIDs,
                        DiscNumber = data.discnumber,
                        TrackNumber = data.tracknumber,
                        Explicit = data.Explicit,
                        DurationMs = data.durrationms,
                        PreviewUrl = data.previewURL,
                        SongID = item.SongID
                    });
                    albumID = data.albumID;
                    artistIDs = SplitArtistIds(data.artistIDs);
                }

                await SetAlbumAsync(new Variables.Album() {Id= albumID });
                foreach (string id in artistIDs)
                {
                    await SetArtistAsync(new Variables.Artist(){Id = id});
                }
            }
            Console.WriteLine("Got Track Dara");
            foreach (var item in GetAllAlbums())
            {
                var artistIDs = SplitIds(item.ArtistIDs, Variables.Seperator);
                if (item.MissingInfo())
                {
                    var data = await SpotifyWorker.GetAlbumDataAsync(item.Id);
                    await SetAlbumAsync(new Variables.Album()
                    {
                        Id = item.Id,
                        Name = data.name,
                        ImageURL = data.imageURL,
                        ArtistIDs = data.artistIDs
                    });
                    artistIDs = SplitIds(data.artistIDs, Variables.Seperator);
                }
                foreach (string id in artistIDs)
                {
                    await SetArtistAsync(new Variables.Artist(){Id = id});
                }
            }
            Console.WriteLine("Got Album Data");
            foreach (var item in GetAllArtists())
            {
                if (item.MissingInfo())
                {
                    var data = await SpotifyWorker.GetArtistDataAsync(item.Id);
                    await SetArtistAsync(new Variables.Artist()
                    {
                        Id = item.Id,
                        Genres = data.Genres,
                        Name = data.Name,
                        ImageURL = data.ImageUrl
                    });
                }
            }
            Console.WriteLine("Got Artist Data");
        }

        /// <summary>
        /// Executes the modern synchronization pipeline starting from the
        /// provided <paramref name="startFrom"/> checkpoint.
        /// </summary>
        public static async Task Sync(SyncEntryPoint startFrom = SyncEntryPoint.Playlists)
        {
            if (startFrom <= SyncEntryPoint.Playlists)
            {
                await ExecuteWithRetryAsync(SyncPlaylistsAsync, "playlist synchronization");
            }

            if (startFrom <= SyncEntryPoint.Albums)
            {
                await ExecuteWithRetryAsync(SyncAlbumsAsync, "album synchronization");
            }

            if (startFrom <= SyncEntryPoint.LikedSongs)
            {
                await ExecuteWithRetryAsync(SyncLikedSongsAsync, "liked songs synchronization");
            }

            if (startFrom <= SyncEntryPoint.TrackMetadata)
            {
                await ExecuteWithRetryAsync(SyncTrackMetadataAsync, "track metadata synchronization");
            }

            if (startFrom <= SyncEntryPoint.AlbumMetadata)
            {
                await ExecuteWithRetryAsync(SyncAlbumMetadataAsync, "album metadata synchronization");
            }

            if (startFrom <= SyncEntryPoint.ArtistMetadata)
            {
                await ExecuteWithRetryAsync(SyncArtistMetadataAsync, "artist metadata synchronization");
            }
        }

        private static List<string> SplitIds(string? ids, params string[] separators)
        {
            if (string.IsNullOrWhiteSpace(ids))
            {
                return new List<string>();
            }

            var parts = ids.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            List<string> results = new();

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    results.Add(trimmed);
                }
            }

            return results;
        }

        private static List<string> SplitArtistIds(string? ids)
        {
            return SplitIds(ids, ArtistIdSeparators);
        }

        private static string NormalizeArtistIdString(string? ids)
        {
            var normalizedIds = SplitIds(ids, ArtistIdSeparators);

            if (normalizedIds.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(Variables.Seperator, normalizedIds);
        }

        /// <summary>
        /// Normalizes legacy artist identifier separators stored in the database
        /// so that all persisted values use <see cref="Variables.Seperator"/>.
        /// </summary>
        /// <returns>The number of rows updated across tracks and albums.</returns>
        public static async Task<int> UpdateArtistIdSeparatorsAsync()
        {
            int updates = 0;

            foreach (var track in GetAllTracks())
            {
                string current = track.ArtistIds ?? string.Empty;
                string normalized = NormalizeArtistIdString(current);

                if (!string.Equals(current, normalized, StringComparison.Ordinal))
                {
                    track.ArtistIds = normalized;
                    await DatabaseWorker.SetTrack(track);
                    updates++;
                }
            }

            foreach (var album in GetAllAlbums())
            {
                string current = album.ArtistIDs ?? string.Empty;
                string normalized = NormalizeArtistIdString(current);

                if (!string.Equals(current, normalized, StringComparison.Ordinal))
                {
                    album.ArtistIDs = normalized;
                    await DatabaseWorker.SetAlbum(album);
                    updates++;
                }
            }

            return updates;
        }

        private static async Task ExecuteWithRetryAsync(Func<Task> operation, string operationName)
        {
            while (true)
            {
                try
                {
                    await operation();
                    return;
                }
                catch (APITooManyRequestsException ex) when (ShouldRetry(ex))
                {
                    var delay = ex.RetryAfter;

                    if (delay <= TimeSpan.Zero)
                    {
                        delay = MinimumRetryDelay;
                    }

                    Console.WriteLine($"Rate limit encountered during {operationName}. Waiting {delay} before retrying.");
                    await Task.Delay(delay);
                }
            }
        }

        private static bool ShouldRetry(APITooManyRequestsException ex)
        {
            var retryAfter = ex.RetryAfter;

            if (retryAfter < TimeSpan.Zero)
            {
                return false;
            }

            return retryAfter < RateLimitRetryThreshold;
        }

        private static async Task SyncPlaylistsAsync()
        {
            var playlistSummaries = new List<(string Id, string Name, int TrackCount)>();
            await foreach (var playlist in SpotifyWorker.GetUserPlaylistsAsync())
            {
                playlistSummaries.Add(playlist);
            }

            var playlistDetails = await SpotifyWorker.GetPlaylistDataBatchAsync(playlistSummaries.Select(p => p.Id));

            foreach (var summary in playlistSummaries)
            {
                if (!playlistDetails.TryGetValue(summary.Id, out var data))
                {
                    continue;
                }

                Variables.PlayList playlist = new()
                {
                    Id = data.Id ?? summary.Id,
                    Name = data.name ?? string.Empty,
                    ImageURL = data.imageURL ?? string.Empty,
                    Description = data.Description ?? string.Empty,
                    SnapshotID = data.SnapshotID ?? string.Empty,
                    TrackIDs = data.TrackIDs ?? string.Empty
                };

                Variables.PlayList? existing = GetPlaylist(playlist.Id);

                if (existing == null || existing.SnapshotID != playlist.SnapshotID)
                {
                    await SetPlaylistAsync(playlist);

                    foreach (string id in SplitIds(playlist.TrackIDs, Variables.Seperator))
                    {
                        await SetTrackAsync(new Variables.Track() { Id = id });
                    }
                }
            }

            Console.WriteLine("Got Playlists");
        }

        private static async Task SyncAlbumsAsync()
        {
            var albumSummaries = new List<(string Id, string Name, int TrackCount, string Artists)>();
            await foreach (var album in SpotifyWorker.GetUserAlbumsAsync())
            {
                albumSummaries.Add(album);
            }

            var newAlbumIds = albumSummaries
                .Where(summary => GetAlbum(summary.Id) is null)
                .Select(summary => summary.Id)
                .ToList();

            var newAlbumDetails = await SpotifyWorker.GetAlbumDataBatchAsync(newAlbumIds);

            foreach (var albumId in newAlbumIds)
            {
                if (!newAlbumDetails.TryGetValue(albumId, out var data))
                {
                    continue;
                }

                Variables.Album album = new()
                {
                    Id = data.Id ?? albumId,
                    Name = data.name ?? string.Empty,
                    ImageURL = data.imageURL ?? string.Empty,
                    ArtistIDs = data.artistIDs ?? string.Empty
                };

                foreach (var trackId in SplitIds(data.TrackIDs, Variables.Seperator))
                {
                    await SetTrackAsync(new Variables.Track() { Id = trackId });
                }

                await SetAlbumAsync(album);
            }

            Console.WriteLine("Got Albums");
        }

        private static async Task SyncLikedSongsAsync()
        {
            await foreach (var liked in SpotifyWorker.GetLikedSongsAsync())
            {
                await SetTrackAsync(new Variables.Track() { Id = liked.Id });
            }

            Console.WriteLine("Got Liked Songs");
        }

        private static async Task SyncTrackMetadataAsync()
        {
            var trackRecords = GetAllTracks().ToList();
            var missingTrackIds = trackRecords.Where(t => t.MissingInfo()).Select(t => t.Id).ToList();
            var missingTrackDetails = await SpotifyWorker.GetSongDataBatchAsync(missingTrackIds);

            var hydratedTracks = new List<Variables.Track>(trackRecords.Count);

            foreach (var track in trackRecords)
            {
                var current = track;

                if (track.MissingInfo() && missingTrackDetails.TryGetValue(track.Id, out var data))
                {
                    current = new Variables.Track()
                    {
                        Id = track.Id,
                        Name = data.name ?? string.Empty,
                        AlbumId = data.albumID ?? string.Empty,
                        ArtistIds = data.artistIDs ?? string.Empty,
                        DiscNumber = data.discnumber,
                        TrackNumber = data.tracknumber,
                        Explicit = data.Explicit,
                        DurationMs = data.durrationms,
                        PreviewUrl = data.previewURL ?? string.Empty,
                        SongID = string.IsNullOrEmpty(track.SongID) ? Variables.MakeId() : track.SongID
                    };

                    await SetTrackAsync(current);
                }

                hydratedTracks.Add(current);
            }

            foreach (var track in hydratedTracks)
            {
                if (!string.IsNullOrEmpty(track.AlbumId))
                {
                    await SetAlbumAsync(new Variables.Album() { Id = track.AlbumId });
                }

                foreach (var artistId in SplitArtistIds(track.ArtistIds))
                {
                    await SetArtistAsync(new Variables.Artist() { Id = artistId });
                }
            }

            Console.WriteLine("Got Track Data");
        }

        private static async Task SyncAlbumMetadataAsync()
        {
            var albumRecords = GetAllAlbums().ToList();
            var albumsNeedingDetails = albumRecords.Where(a => a.MissingInfo()).Select(a => a.Id).ToList();
            var albumDetails = await SpotifyWorker.GetAlbumDataBatchAsync(albumsNeedingDetails);

            foreach (var album in albumRecords)
            {
                if (albumDetails.TryGetValue(album.Id, out var data))
                {
                    if (album.MissingInfo())
                    {
                        var updatedAlbum = new Variables.Album()
                        {
                            Id = album.Id,
                            Name = data.name ?? string.Empty,
                            ImageURL = data.imageURL ?? string.Empty,
                            ArtistIDs = data.artistIDs ?? string.Empty
                        };

                        await SetAlbumAsync(updatedAlbum);

                        album.Name = updatedAlbum.Name;
                        album.ImageURL = updatedAlbum.ImageURL;
                        album.ImagePath = updatedAlbum.ImagePath;
                        album.ArtistIDs = updatedAlbum.ArtistIDs;
                    }

                    foreach (var artistId in SplitIds(data.artistIDs, Variables.Seperator))
                    {
                        await SetArtistAsync(new Variables.Artist() { Id = artistId });
                    }
                }
                else
                {
                    foreach (var artistId in SplitIds(album.ArtistIDs, Variables.Seperator))
                    {
                        await SetArtistAsync(new Variables.Artist() { Id = artistId });
                    }
                }
            }

            Console.WriteLine("Got Album Data");
        }

        private static async Task SyncArtistMetadataAsync()
        {
            var artistRecords = GetAllArtists().ToList();
            var artistsNeedingDetails = artistRecords.Where(a => a.MissingInfo()).Select(a => a.Id).ToList();
            var artistDetails = await SpotifyWorker.GetArtistDataBatchAsync(artistsNeedingDetails);

            foreach (var artist in artistRecords)
            {
                if (artist.MissingInfo() && artistDetails.TryGetValue(artist.Id, out var data))
                {
                    var updatedArtist = new Variables.Artist()
                    {
                        Id = data.Id ?? artist.Id,
                        Genres = data.Genres ?? string.Empty,
                        Name = data.Name ?? string.Empty,
                        ImageURL = data.ImageUrl ?? string.Empty
                    };

                    await SetArtistAsync(updatedArtist);

                    artist.Name = updatedArtist.Name;
                    artist.ImageURL = updatedArtist.ImageURL;
                    artist.ImagePath = updatedArtist.ImagePath;
                    artist.Genres = updatedArtist.Genres;
                }
            }

            Console.WriteLine("Got Artist Data");
        }
    }
}

