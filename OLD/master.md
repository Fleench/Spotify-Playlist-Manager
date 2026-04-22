# Master Code Compilation

This document aggregates all code files in the repository for reference.

## App.axaml

```
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="Spotify_Playlist_Manager.App"
             xmlns:local="using:Spotify_Playlist_Manager"
             RequestedThemeVariant="Default">
             <!-- "Default" ThemeVariant follows system theme variant. "Dark" or "Light" are other available options. -->

    <Application.DataTemplates>
        <local:ViewLocator/>
    </Application.DataTemplates>
  
    <Application.Styles>
        <FluentTheme />
    </Application.Styles>
</Application>
```

## App.axaml.cs

```
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Spotify_Playlist_Manager.ViewModels;
using Spotify_Playlist_Manager.Views;

namespace Spotify_Playlist_Manager;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
```

## Models/DataCoordinator.cs

```
/* File: DataCoordinator.cs
 * Author: Glenn Sutherland, ChatGPT Codex
 * Description: Connects the SpotifyWorker to the DatabaseWorker
 */

using SQLitePCL;
using System;
using System.Collections.Generic;
using Spotify_Playlist_Manager.Models;
namespace Spotify_Playlist_Manager.Models
{
    static class DataCoordinator
    {
        public async static void Sync()
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
                Variables.PlayList tp = DatabaseWorker.GetPlaylist(playlist.Id);
                if (tp == null || tp.SnapshotID != playlist.SnapshotID)
                {
                    await DatabaseWorker.SetPlaylist(playlist);
                    foreach (string id in SplitIds(playlist.TrackIDs, Variables.Seperator))
                    {
                        await DatabaseWorker.SetTrack(new Variables.Track() {Id = id});
                    }
                }
            }
            //2. Set albums
            await foreach (var item in SpotifyWorker.GetUserAlbumsAsync())
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
                    await DatabaseWorker.SetTrack(new Variables.Track() {Id = id});
                }
                await DatabaseWorker.SetAlbum(album);
            }
            //3. set liked songs
            await foreach (var item in SpotifyWorker.GetLikedSongsAsync())
            {
                await DatabaseWorker.SetTrack(new Variables.Track() {Id = item.Id});
            }
            //update track data
            foreach (var item in DatabaseWorker.GetAllTracks())
            {
                string albumID = item.AlbumId;
                var artistIDs = SplitArtistIds(item.ArtistIds);
                if (item.MissingInfo())
                {
                    var data = await SpotifyWorker.GetSongDataAsync(item.Id);
                    await DatabaseWorker.SetTrack(new Variables.Track()
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

                await DatabaseWorker.SetAlbum(new Variables.Album() {Id= albumID });
                foreach (string id in artistIDs)
                {
                    await DatabaseWorker.SetArtist(new Variables.Artist(){Id = id});
                }
            }
            foreach (var item in DatabaseWorker.GetAllAlbums())
            {
                var artistIDs = SplitIds(item.ArtistIDs, Variables.Seperator);
                if (item.MissingInfo())
                {
                    var data = await SpotifyWorker.GetAlbumDataAsync(item.Id);
                    await DatabaseWorker.SetAlbum(new Variables.Album()
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
                    await DatabaseWorker.SetArtist(new Variables.Artist(){Id = id});
                }
            }

            foreach (var item in DatabaseWorker.GetAllArtists())
            {
                if (item.MissingInfo())
                {
                    var data = await SpotifyWorker.GetArtistDataAsync(item.Id);
                    await DatabaseWorker.SetArtist(new Variables.Artist()
                    {
                        Id = item.Id,
                        Genres = data.Genres,
                        Name = data.Name,
                        ImageURL = data.ImageUrl
                    });
                }
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
            return SplitIds(ids, "::", Variables.Seperator);
        }
    }
}


```

## Models/DatabaseWorker.cs

```
/* File: DatabaseWorker.cs
 * Author: Glenn Sutherland, ChatGPT Codex
 * Description: This manages the sql commands and database for the software. 
*/
using Spotify_Playlist_Manager.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
namespace Spotify_Playlist_Manager.Models
{
    
    public static class DatabaseWorker
    {
        private static string _dbPath = Variables.DatabasePath;
        private static readonly SemaphoreSlim DbWriteLock = new(1, 1);

        public static void Init()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            cmd.ExecuteNonQuery();

            cmd.CommandText = """
                                  -- Settings table
                                  CREATE TABLE IF NOT EXISTS Settings (
                                      Key TEXT PRIMARY KEY,
                                      Value TEXT NOT NULL
                                  );

                                  -- Playlists table
                                  CREATE TABLE IF NOT EXISTS Playlists (
                                      Id TEXT PRIMARY KEY,               -- Spotify playlist ID
                                      Name TEXT,
                                      ImageURL TEXT,
                                      Description TEXT,
                                      SnapshotID TEXT,
                                      TrackIDs TEXT                      -- 'id1;;id2;;id3'
                                  );

                                  -- Albums table
                                  CREATE TABLE IF NOT EXISTS Albums (
                                      Id TEXT PRIMARY KEY,               -- Spotify album ID
                                      Name TEXT,
                                      ImageURL TEXT,
                                      ArtistIDs TEXT                     -- 'id1;;id2;;id3'
                                  );

                                  -- Tracks table (Spotify ID is anchor; SongID is internal secondary)
                                  CREATE TABLE IF NOT EXISTS Tracks (
                                      Id TEXT PRIMARY KEY,               -- Spotify track ID
                                      SongID TEXT,                       -- Carillon internal ID (CIID)
                                      Name TEXT,
                                      AlbumId TEXT,
                                      ArtistIds TEXT,
                                      DiscNumber INTEGER,
                                      DurationMs INTEGER,
                                      Explicit INTEGER,                  -- 0 = false, 1 = true
                                      PreviewUrl TEXT,
                                      TrackNumber INTEGER
                                  );

                                  -- Artists table
                                  CREATE TABLE IF NOT EXISTS Artists (
                                      Id TEXT PRIMARY KEY,               -- Spotify artist ID
                                      Name TEXT,
                                      ImageURL TEXT,
                                      Genres TEXT                        -- fixed typo: "Generes" → "Genres"
                                  );

                                  -- Similar table
                                  CREATE TABLE IF NOT EXISTS Similar (
                                      SongID TEXT,                       -- Spotify similar ID
                                      SongID2 TEXT                       -- removed trailing comma
                                  );
                              """;


            cmd.ExecuteNonQuery();
        }


        public static async Task SetSetting(string key, string value)
        {
            await DbWriteLock.WaitAsync();
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT OR REPLACE INTO Settings (Key, Value) VALUES ($key, $value);";
                cmd.Parameters.AddWithValue("$key", key);
                cmd.Parameters.AddWithValue("$value", value);
                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                DbWriteLock.Release();
            }
        }

        public static string? GetSetting(string key)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Value FROM Settings WHERE Key = $key;";
            cmd.Parameters.AddWithValue("$key", key);
            return cmd.ExecuteScalar() as string;
        }

        public static IEnumerable<(string key, string value)> GetAllSettings()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Key, Value FROM Settings;";

            using var reader = cmd.ExecuteReader();

            // Read each row and yield it as a tuple
            while (reader.Read())
            {
                string key = reader.GetString(0);   // column index 0 → Key
                string value = reader.GetString(1); // column index 1 → Value
                yield return (key, value);
            }
        }

        public static async Task SetPlaylist(Variables.PlayList playlist)
        {
            await DbWriteLock.WaitAsync();
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT OR REPLACE INTO Playlists (Id, Name, ImageURL, Description, SnapshotID, TrackIDs)
                                VALUES ($id, $name, $imageUrl, $description, $snapshotId, $trackIds);";

                cmd.Parameters.AddWithValue("$id", playlist.Id ?? string.Empty);
                cmd.Parameters.AddWithValue("$name", playlist.Name ?? string.Empty);
                cmd.Parameters.AddWithValue("$imageUrl", playlist.ImageURL ?? string.Empty);
                cmd.Parameters.AddWithValue("$description", playlist.Description ?? string.Empty);
                cmd.Parameters.AddWithValue("$snapshotId", playlist.SnapshotID ?? string.Empty);
                cmd.Parameters.AddWithValue("$trackIds", playlist.TrackIDs ?? string.Empty);

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                DbWriteLock.Release();
            }
        }

        public static Variables.PlayList? GetPlaylist(string id)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, ImageURL, Description, SnapshotID, TrackIDs FROM Playlists WHERE Id = $id LIMIT 1;";
            cmd.Parameters.AddWithValue("$id", id);

            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                return new Variables.PlayList
                {
                    Id = reader["Id"]?.ToString() ?? string.Empty,
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    ImageURL = reader["ImageURL"]?.ToString() ?? string.Empty,
                    Description = reader["Description"]?.ToString() ?? string.Empty,
                    SnapshotID = reader["SnapshotID"]?.ToString() ?? string.Empty,
                    TrackIDs = reader["TrackIDs"]?.ToString() ?? string.Empty
                };
            }

            return null;
        }

        public static IEnumerable<Variables.PlayList> GetAllPlaylists()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, ImageURL, Description, SnapshotID, TrackIDs FROM Playlists;";

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                yield return new Variables.PlayList
                {
                    Id = reader["Id"]?.ToString() ?? string.Empty,
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    ImageURL = reader["ImageURL"]?.ToString() ?? string.Empty,
                    Description = reader["Description"]?.ToString() ?? string.Empty,
                    SnapshotID = reader["SnapshotID"]?.ToString() ?? string.Empty,
                    TrackIDs = reader["TrackIDs"]?.ToString() ?? string.Empty
                };
            }
        }

        public static async Task SetAlbum(Variables.Album album)
        {
            await DbWriteLock.WaitAsync();
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT OR REPLACE INTO Albums (Id, Name, ImageURL, ArtistIDs)
                                VALUES ($id, $name, $imageUrl, $artistIds);";

                cmd.Parameters.AddWithValue("$id", album.Id ?? string.Empty);
                cmd.Parameters.AddWithValue("$name", album.Name ?? string.Empty);
                cmd.Parameters.AddWithValue("$imageUrl", album.ImageURL ?? string.Empty);
                cmd.Parameters.AddWithValue("$artistIds", album.ArtistIDs ?? string.Empty);

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                DbWriteLock.Release();
            }
        }

        public static Variables.Album? GetAlbum(string id)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, ImageURL, ArtistIDs FROM Albums WHERE Id = $id LIMIT 1;";
            cmd.Parameters.AddWithValue("$id", id);

            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                var albumId = reader["Id"]?.ToString() ?? string.Empty;

                return new Variables.Album
                {
                    Id = albumId,
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    ImageURL = reader["ImageURL"]?.ToString() ?? string.Empty,
                    ArtistIDs = reader["ArtistIDs"]?.ToString() ?? string.Empty,
                    TrackIDs = GetAlbumTrackIds(conn, albumId)
                };
            }

            return null;
        }

        public static IEnumerable<Variables.Album> GetAllAlbums()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, ImageURL, ArtistIDs FROM Albums;";

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var albumId = reader["Id"]?.ToString() ?? string.Empty;
                yield return new Variables.Album
                {
                    Id = albumId,
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    ImageURL = reader["ImageURL"]?.ToString() ?? string.Empty,
                    ArtistIDs = reader["ArtistIDs"]?.ToString() ?? string.Empty,
                    TrackIDs = GetAlbumTrackIds(conn, albumId)
                };
            }
        }

        private static string GetAlbumTrackIds(SqliteConnection connection, string albumId)
        {
            using var trackCmd = connection.CreateCommand();
            trackCmd.CommandText = "SELECT Id FROM Tracks WHERE AlbumId = $albumId;";
            trackCmd.Parameters.AddWithValue("$albumId", albumId);

            using var trackReader = trackCmd.ExecuteReader();
            List<string> trackIds = new();

            while (trackReader.Read())
            {
                var trackId = trackReader["Id"]?.ToString();
                if (!string.IsNullOrEmpty(trackId))
                {
                    trackIds.Add(trackId);
                }
            }

            return string.Join(Variables.Seperator, trackIds);
        }

        public static async Task SetTrack(Variables.Track track)
        {
            await DbWriteLock.WaitAsync();
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT OR REPLACE INTO Tracks (Id, SongID, Name, AlbumId, ArtistIds, DiscNumber, DurationMs, Explicit, PreviewUrl, TrackNumber)
                                VALUES ($id, $songId, $name, $albumId, $artistIds, $discNumber, $durationMs, $explicit, $previewUrl, $trackNumber);";

                cmd.Parameters.AddWithValue("$id", track.Id ?? string.Empty);
                cmd.Parameters.AddWithValue("$songId", track.SongID ?? string.Empty);
                cmd.Parameters.AddWithValue("$name", track.Name ?? string.Empty);
                cmd.Parameters.AddWithValue("$albumId", track.AlbumId ?? string.Empty);
                cmd.Parameters.AddWithValue("$artistIds", track.ArtistIds ?? string.Empty);
                cmd.Parameters.AddWithValue("$discNumber", track.DiscNumber);
                cmd.Parameters.AddWithValue("$durationMs", track.DurationMs);
                cmd.Parameters.AddWithValue("$explicit", track.Explicit ? 1 : 0);
                cmd.Parameters.AddWithValue("$previewUrl", track.PreviewUrl ?? string.Empty);
                cmd.Parameters.AddWithValue("$trackNumber", track.TrackNumber);

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                DbWriteLock.Release();
            }
        }

        public static Variables.Track GetTrack(string id)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();

            // Check if we're dealing with a Carillon internal SongID or a Spotify ID
            if (id.StartsWith(Variables.Identifier)) // e.g., "CIID"
            {
                cmd.CommandText = "SELECT * FROM Tracks WHERE SongID = @id LIMIT 1;";
            }
            else
            {
                cmd.CommandText = "SELECT * FROM Tracks WHERE Id = @id LIMIT 1;";
            }

            cmd.Parameters.AddWithValue("@id", id);

            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                // Safely convert columns to your Track object
                 return new Variables.Track() {
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    Id = reader["Id"]?.ToString() ?? string.Empty,
                    AlbumId = reader["AlbumId"]?.ToString() ?? string.Empty,
                    ArtistIds = reader["ArtistIds"]?.ToString() ?? string.Empty,
                    DiscNumber = reader["DiscNumber"] is DBNull ? 0 : Convert.ToInt32(reader["DiscNumber"]),
                    DurationMs = reader["DurationMs"] is DBNull ? 0 : Convert.ToInt32(reader["DurationMs"]),
                    Explicit = reader["Explicit"] is DBNull ? false : Convert.ToInt32(reader["Explicit"]) == 1,
                    PreviewUrl = reader["PreviewUrl"]?.ToString() ?? string.Empty,
                    TrackNumber = reader["TrackNumber"] is DBNull ? 0 : Convert.ToInt32(reader["TrackNumber"]),
                    SongID = reader["SongID"]?.ToString() ?? string.Empty
                };
            }

            return null; // No result found
        }

        public static IEnumerable<Variables.Track> GetAllTracks()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Tracks;";

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                yield return new Variables.Track() {
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    Id = reader["Id"]?.ToString() ?? string.Empty,
                    AlbumId = reader["AlbumId"]?.ToString() ?? string.Empty,
                    ArtistIds = reader["ArtistIds"]?.ToString() ?? string.Empty,
                    DiscNumber = reader["DiscNumber"] is DBNull ? 0 : Convert.ToInt32(reader["DiscNumber"]),
                    DurationMs = reader["DurationMs"] is DBNull ? 0 : Convert.ToInt32(reader["DurationMs"]),
                    Explicit = reader["Explicit"] is DBNull ? false : Convert.ToInt32(reader["Explicit"]) == 1,
                    PreviewUrl = reader["PreviewUrl"]?.ToString() ?? string.Empty,
                    TrackNumber = reader["TrackNumber"] is DBNull ? 0 : Convert.ToInt32(reader["TrackNumber"]),
                    SongID = reader["SongID"]?.ToString() ?? string.Empty
                };
            }
        }

        public static async Task SetArtist(Variables.Artist artist)
        {
            await DbWriteLock.WaitAsync();
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT OR REPLACE INTO Artists (Id, Name, ImageURL, Genres)
                                VALUES ($id, $name, $imageUrl, $genres);";

                cmd.Parameters.AddWithValue("$id", artist.Id ?? string.Empty);
                cmd.Parameters.AddWithValue("$name", artist.Name ?? string.Empty);
                cmd.Parameters.AddWithValue("$imageUrl", artist.ImageURL ?? string.Empty);
                cmd.Parameters.AddWithValue("$genres", artist.Genres ?? string.Empty);

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                DbWriteLock.Release();
            }
        }

        public static Variables.Artist? GetArtist(string id)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, ImageURL, Genres FROM Artists WHERE Id = $id LIMIT 1;";
            cmd.Parameters.AddWithValue("$id", id);

            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                return new Variables.Artist
                {
                    Id = reader["Id"]?.ToString() ?? string.Empty,
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    ImageURL = reader["ImageURL"]?.ToString() ?? string.Empty,
                    Genres = reader["Genres"]?.ToString() ?? string.Empty
                };
            }

            return null;
        }

        public static IEnumerable<Variables.Artist> GetAllArtists()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, ImageURL, Genres FROM Artists;";

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                yield return new Variables.Artist
                {
                    Id = reader["Id"]?.ToString() ?? string.Empty,
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    ImageURL = reader["ImageURL"]?.ToString() ?? string.Empty,
                    Genres = reader["Genres"]?.ToString() ?? string.Empty
                };
            }
        }

        public static async Task SetSimilar(string songId, string songId2)
        {
            await DbWriteLock.WaitAsync();
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO Similar (SongID, SongID2) VALUES ($songId, $songId2);";

                cmd.Parameters.AddWithValue("$songId", songId);
                cmd.Parameters.AddWithValue("$songId2", songId2);

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                DbWriteLock.Release();
            }
        }

        public static (string SongId, string SongId2)? GetSimilar(string songId, string songId2)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT SongID, SongID2 FROM Similar WHERE SongID = $songId AND SongID2 = $songId2 LIMIT 1;";
            cmd.Parameters.AddWithValue("$songId", songId);
            cmd.Parameters.AddWithValue("$songId2", songId2);

            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                return (
                    reader["SongID"]?.ToString() ?? string.Empty,
                    reader["SongID2"]?.ToString() ?? string.Empty
                );
            }

            return null;
        }

        public static IEnumerable<(string SongId, string SongId2)> GetAllSimilar()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT SongID, SongID2 FROM Similar;";

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                yield return (
                    reader["SongID"]?.ToString() ?? string.Empty,
                    reader["SongID2"]?.ToString() ?? string.Empty
                );
            }
        }


    }
}
```

## Models/SpotifyWorker.cs

```
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

        /// <summary>
        /// Returns a tuple of the most important song metadata for a given
        /// track. Tuples keep the public signature identical to the v1 worker so
        /// callers can continue unpacking values without refactoring.
        /// </summary>
        public static async Task<(string name, string id, string albumID, string artistIDs, int discnumber, int durrationms, bool Explicit, string previewURL, int tracknumber)> GetSongDataAsync(string id)
        {
            var spotify = await SpotifySession.Instance.GetClientAsync();
            FullTrack track = await spotify.Tracks.Get(id);
            string artistIDs = string.Join("::", track.Artists.Select(a => a.Id));
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

```

## Models/SpotifyWorker_Old.cs

```
/* File: SpotifyWorker_Old.cs
 * Author: Glenn Sutherland
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
            string artistIDs = string.Join("::", track.Artists.Select(a => a.Id));; 
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
```

## Models/Variables.cs

```
/* File: Variables.cs
 * Author: Glenn Sutherland, ChatGPT Codex
 * Description: Variables and Classes used by the program
 */
using System;
using System.IO;
using System.Linq;

namespace Spotify_Playlist_Manager.Models
{


    public static class Variables
    {
        // App Info
        public const string AppName = "SpotifyPlaylistManager";
        public static readonly string AppVersion = "0.0.0";

        // Base Directories
        public static readonly string AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);

        public static readonly string ConfigPath = Path.Combine(AppDataPath, "config.json");
        public static readonly string DatabasePath = Path.Combine(AppDataPath, "data.db");

        public static readonly string CachePath = Path.Combine(AppDataPath, "/cache");

        public static readonly string Seperator = ";;";
        public static readonly string Identifier = "CIID";
        public static readonly string IdentifierDelimiter = "___";
        public static readonly Random RNG = new Random();
        // Initialize any needed directories
        public static void Init()
        {
            Directory.CreateDirectory(AppDataPath);
            Directory.CreateDirectory(CachePath);
            File.Create(DatabasePath).Close();
            Console.WriteLine(DatabasePath);
        }

        public class PlayList
        {
            public string Name;
            public string ImageURL;
            public string Id;
            public string Description;
            public string SnapshotID;
            public string TrackIDs;

            /// <summary>
            /// Checks if essential data fields for the playlist are missing or invalid.
            /// </summary>
            /// <returns>True if any essential data is missing; otherwise, false.</returns>
            public bool MissingInfo()
            {
                if (string.IsNullOrEmpty(Name) ||
                    string.IsNullOrEmpty(Id) ||
                    string.IsNullOrEmpty(SnapshotID) ||
                    string.IsNullOrEmpty(TrackIDs))
                {
                    return true;
                }

                return false;
            }
        }
        public class Album
        {
            public string Name;
            public string ImageURL;
            public string Id;
            public string ArtistIDs;
            public string TrackIDs;

            /// <summary>
            /// Checks if essential data fields for the album are missing or invalid.
            /// </summary>
            /// <returns>True if any essential data is missing; otherwise, false.</returns>
            public bool MissingInfo()
            {
                if (string.IsNullOrEmpty(Name) ||
                    string.IsNullOrEmpty(Id) ||
                    string.IsNullOrEmpty(ArtistIDs) ||
                    string.IsNullOrEmpty(TrackIDs))
                {
                    return true;
                }

                return false;
            }
        }

        public class Track
        {
            public string Name;
            public string Id;
            public string AlbumId;
            public string ArtistIds;
            public int DiscNumber;
            public int DurationMs;
            public bool Explicit;
            public string PreviewUrl;
            public int TrackNumber;
            public string SongID;

            public Track()
            {
                
                    SongID = Variables.MakeId();
            }

            /// <summary>
            /// Checks if essential data fields for the track are missing or invalid.
            /// </summary>
            /// <returns>True if any essential data is missing; otherwise, false.</returns>
            public bool MissingInfo()
            {
                // Check for null or empty strings in essential fields
                if (string.IsNullOrEmpty(Name) ||
                    string.IsNullOrEmpty(Id) ||
                    string.IsNullOrEmpty(AlbumId) ||
                    string.IsNullOrEmpty(ArtistIds) ||
                    string.IsNullOrEmpty(SongID))
                {
                    return true;
                }

                // Check for invalid numeric values in essential fields
                if (DurationMs <= 0 ||
                    DiscNumber <= 0 ||
                    TrackNumber <= 0)
                {
                    return true;
                }

                // If all checks pass, the data is not considered 'missing'
                return false;
            }

            /// <summary>
            /// Legacy wrapper maintained for backwards compatibility.
            /// </summary>
            /// <returns>True if any essential data is missing; otherwise, false.</returns>
            public bool MissingData()
            {
                return MissingInfo();
            }
        }

        public class Artist
        {
            public string Name;
            public string ImageURL;
            public string Id;
            public string Genres;

            /// <summary>
            /// Checks if essential data fields for the artist are missing or invalid.
            /// </summary>
            /// <returns>True if any essential data is missing; otherwise, false.</returns>
            public bool MissingInfo()
            {
                if (string.IsNullOrEmpty(Name) ||
                    string.IsNullOrEmpty(Id))
                {
                    return true;
                }

                return false;
            }
        }
        public static class Settings
        {
            public static string SW_AccessToken = "SW_AccessToken";
            public static string SW_RefreshToken = "SW_RefreshToken";
            public static string SW_ClientToken = "SW_ClientToken";
            public static string SW_ClientSecret = "SW_ClientSecret";
        }

        public static string MakeId( string type = "SNG", int length = 30)
        {
            string AllowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            string songId = Variables.Identifier + Variables.IdentifierDelimiter + type +  Variables.IdentifierDelimiter;
            songId += new string(Enumerable.Range(0, length)
                .Select(_ => AllowedChars[RNG.Next(AllowedChars.Length)])
                .ToArray());
            return songId;
        }
    }

}
```

## Models/manual-test.cs

```
/* File: manual-test.cs
 * Author: Glenn Sutherland
 * Description: Manual testing for the Spotify Playlist Manager. This allows
 * for the modules to tested for before they are stringed together.
 */
using Spotify_Playlist_Manager.Models.txt;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Spotify_Playlist_Manager.Models;
using System.Collections.Concurrent;
using System.Threading;

public class TempProgram
{
    static async Task Main(string[] args)
    {
        Variables.Init();
        DatabaseWorker.Init();
        // --- Example Usage ---

        string myFile = "settings.txt";
        //old code
        /*
        FileHelper.ModifySpecificLine(myFile,4,"null"); //client token
        FileHelper.ModifySpecificLine(myFile,1,"null"); //client secret
        FileHelper.ModifySpecificLine(myFile,2,"null"); //token
        FileHelper.ModifySpecificLine(myFile,3,"null"); //refresh token
        */

        string clientID = FileHelper.ReadSpecificLine(myFile, 4);
        if (clientID == "" || clientID == null)
        {
            Console.WriteLine("Could not read settings.txt file for ClientID");
            clientID = Console.ReadLine();
        }

        string clientSecret = FileHelper.ReadSpecificLine(myFile, 1);
        ;

        if (clientSecret == "" || clientSecret == null)
        {
            Console.WriteLine("Could not read settings.txt file for ClientSecret");
            clientSecret = Console.ReadLine();

        }

        string token = "";
        string refreshToken = "";
        if (FileHelper.ReadSpecificLine(myFile, 2) != "null")
        {
            token = FileHelper.ReadSpecificLine(myFile, 2);
        }

        if (FileHelper.ReadSpecificLine(myFile, 3) != "null")
        {
            refreshToken = FileHelper.ReadSpecificLine(myFile, 3);
        }

        Console.WriteLine($"ClientID: {clientID}, ClientSecret: {clientSecret}");
        SpotifyWorker.Init(clientID, clientSecret,token, refreshToken);
        SpotifyWorker_Old.Init(clientID, clientSecret, token, refreshToken);
        var (at, rt) = await SpotifyWorker_Old.AuthenticateAsync();
        FileHelper.ModifySpecificLine(myFile, 4, clientID);
        FileHelper.ModifySpecificLine(myFile, 1, clientSecret);
        FileHelper.ModifySpecificLine(myFile, 2, at);
        FileHelper.ModifySpecificLine(myFile, 3, rt);
        Console.WriteLine("YO WE DONE with AUTHENTICATED!");
        string data = "data.txt";
        //Get the first playlist, its first song, that songs album and artist, and save the IDs.
        //uncomment this code to get new ids
        /*var ids = Getabitofdata().Result;
        Console.WriteLine(ids);
        string st = ids.playlistID + Variables.Seperator + ids.trackID + Variables.Seperator + ids.albumID +
                    Variables.Seperator + ids.artistID;
        Console.WriteLine(st);
        IEnumerable<string> list = new[] { st };
        FileHelper.CreateOrOverwriteFile(data, list);
        FileHelper.ModifySpecificLine(data, 1, st);*/
        DataCoordinator.Sync();

    }

    public static async Task<(string playlistID, string trackID, string albumID, string artistID)> Getabitofdata()
    {
        string playlistID = "";
        string trackID = "";
        string albumID = "";
        string artistID = "";
        await foreach (var playlist in SpotifyWorker_Old.GetUserPlaylistsAsync())
        {
            playlistID = playlist.Id;
            break;
        }
        trackID = SpotifyWorker_Old.GetPlaylistDataAsync(playlistID).Result.TrackIDs.Split(Variables.Seperator)[0];
        var data = SpotifyWorker_Old.GetSongDataAsync(trackID);
        albumID = data.Result.albumID;
        artistID = data.Result.artistIDs.Split(Variables.Seperator)[0];
        return  (playlistID, trackID, albumID, artistID);
    }
}
```

## Models/txt.cs

```
/* File: text.cs
 * Author: Gemmeni Pro 2.5
 * Description: This is an AI genereted debug module to quickly save data for testing.
 * It is not designed to be used in production. Nor should it be used as a permanent solution.
 */
using System;
using System.IO;        // Required for File operations
using System.Linq;      // Required for .ToList()
using System.Collections.Generic; // Required for List<T>

/// <summary>
/// A static utility class for performing common line-based file operations.
/// </summary>
namespace Spotify_Playlist_Manager.Models.txt
{
public static class FileHelper
{
    /// <summary>
    /// Appends a single line of text to the end of a file.
    /// A new line character is automatically added.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <param name="line">The line of text to add.</param>
    public static void AppendLine(string path, string line)
    {
        // Use AppendAllText to add to the file.
        // We add Environment.NewLine to ensure it works on all operating systems.
        File.AppendAllText(path, line + Environment.NewLine);
    }

    /// <summary>
    /// Reads a single line from a file.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <param name="lineNumber">The 1-based line number to read (e.g., 1, 2, 3).</param>
    /// <returns>The content of the line, or null if the file/line doesn't exist.</returns>
    public static string ReadSpecificLine(string path, int lineNumber)
    {
        if (!File.Exists(path))
        {
            // You might want to throw an exception here instead
            return null;
        }

        // Read all lines into an array
        string[] lines = File.ReadAllLines(path);

        // Convert 1-based line number to 0-based array index
        int lineIndex = lineNumber - 1;

        if (lineIndex >= 0 && lineIndex < lines.Length)
        {
            // Valid line number
            return lines[lineIndex];
        }
        
        // Line number is out of range
        return null;
    }

    /// <summary>
    /// Modifies a specific line in a file.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <param name="lineNumber">The 1-based line number to modify.</param>
    /// <param name="newText">The new text for the line.</param>
    /// <returns>True if modification was successful, false otherwise.</returns>
    public static bool ModifySpecificLine(string path, int lineNumber, string newText)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        // Read all lines into a List.
        List<string> lines = File.ReadAllLines(path).ToList();

        // Convert 1-based line number to 0-based list index
        int lineIndex = lineNumber - 1;

        if (lineIndex >= 0 && lineIndex < lines.Count)
        {
            // Modify the line in the list
            lines[lineIndex] = newText;

            // Write the entire modified list back to the file
            File.WriteAllLines(path, lines);
            return true;
        }
        
        // Line number was out of range
        return false;
    }

    /// <summary>
    /// Creates a new file (or overwrites an existing one) with the given lines.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <param name="lines">The content to write to the file.</param>
    public static void CreateOrOverwriteFile(string path, IEnumerable<string> lines)
    {
        File.WriteAllLines(path, lines);
    }

    /// <summary>
    /// Reads all lines from a file and returns them as a string array.
    /// Returns an empty array if the file doesn't exist.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <returns>A string array of all lines.</returns>
    public static string[] ReadAllLines(string path)
    {
        if (File.Exists(path))
        {
            return File.ReadAllLines(path);
        }
        
        return new string[0]; // Return an empty array
    }
}    
}

```

## Program.cs

```
﻿using Avalonia;
using System;

namespace Spotify_Playlist_Manager;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    //FIX LATER
    public static void nMain(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
```

## Spotify Playlist Manager.csproj

```
﻿<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <OutputType>WinExe</OutputType>
        <TargetFramework>net8.0</TargetFramework>
        <Nullable>enable</Nullable>
        <BuiltInComInteropSupport>true</BuiltInComInteropSupport>
        <ApplicationManifest>app.manifest</ApplicationManifest>
        <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
    </PropertyGroup>

    <ItemGroup>
        <AvaloniaResource Include="Assets\**" />
    </ItemGroup>

    <ItemGroup>
        <PackageReference Include="Avalonia" Version="11.3.6" />
        <PackageReference Include="Avalonia.Desktop" Version="11.3.6" />
        <PackageReference Include="Avalonia.Themes.Fluent" Version="11.3.6" />
        <PackageReference Include="Avalonia.Fonts.Inter" Version="11.3.6" />
        <!--Condition below is needed to remove Avalonia.Diagnostics package from build output in Release configuration.-->
        <PackageReference Include="Avalonia.Diagnostics" Version="11.3.6">
            <IncludeAssets Condition="'$(Configuration)' != 'Debug'">None</IncludeAssets>
            <PrivateAssets Condition="'$(Configuration)' != 'Debug'">All</PrivateAssets>
        </PackageReference>
        <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.1" />
        <PackageReference Include="Microsoft.Data.Sqlite" Version="9.0.10" />
        <PackageReference Include="SpotifyAPI.Web" Version="7.2.1" />
        <PackageReference Include="SpotifyAPI.Web.Auth" Version="7.2.1" />
        <PackageReference Include="SQLite" Version="3.13.0" />
    </ItemGroup>
</Project>

```

## Spotify Playlist Manager.sln

```
﻿
Microsoft Visual Studio Solution File, Format Version 12.00
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Spotify Playlist Manager", "Spotify Playlist Manager.csproj", "{6884876C-A9D2-46AA-BC24-351FE0F1D75C}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{6884876C-A9D2-46AA-BC24-351FE0F1D75C}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{6884876C-A9D2-46AA-BC24-351FE0F1D75C}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{6884876C-A9D2-46AA-BC24-351FE0F1D75C}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{6884876C-A9D2-46AA-BC24-351FE0F1D75C}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
EndGlobal

```

## ViewLocator.cs

```
using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Spotify_Playlist_Manager.ViewModels;

namespace Spotify_Playlist_Manager;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
```

## ViewModels/MainWindowViewModel.cs

```
﻿namespace Spotify_Playlist_Manager.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public string Greeting { get; } = "Welcome to Avalonia! Hello World! I made another edit!";
    
}
```

## ViewModels/ViewModelBase.cs

```
﻿using CommunityToolkit.Mvvm.ComponentModel;

namespace Spotify_Playlist_Manager.ViewModels;

public class ViewModelBase : ObservableObject
{
}
```

## Views/MainWindow.axaml

```
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:Spotify_Playlist_Manager.ViewModels"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d" d:DesignWidth="800" d:DesignHeight="450"
        x:Class="Spotify_Playlist_Manager.Views.MainWindow"
        x:DataType="vm:MainWindowViewModel"
        Icon="/Assets/avalonia-logo.ico"
        Title="Spotify_Playlist_Manager">

    <Design.DataContext>
        <!-- This only sets the DataContext for the previewer in an IDE,
             to set the actual DataContext for runtime, set the DataContext property in code (look at App.axaml.cs) -->
        <vm:MainWindowViewModel/>
    </Design.DataContext>

    <TextBlock Text="{Binding Greeting}" HorizontalAlignment="Center" VerticalAlignment="Center"/>

</Window>

```

## Views/MainWindow.axaml.cs

```
using Avalonia.Controls;

namespace Spotify_Playlist_Manager.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
```

