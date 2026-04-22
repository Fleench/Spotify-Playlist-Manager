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

    /// <summary>
    /// Thin wrapper around the SQLite database used by the playlist manager.
    /// Every public method either reads from or writes to the local cache while
    /// ensuring that concurrent write operations are serialized through a
    /// <see cref="SemaphoreSlim"/>.
    /// </summary>
    public static class DatabaseWorker
    {
        private static string _dbPath = Variables.DatabasePath;
        private static readonly SemaphoreSlim DbWriteLock = new(1, 1);

        /// <summary>
        /// Initializes the SQLite schema and ensures columns added in later
        /// versions exist before any access occurs.
        /// </summary>
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
                                      ImagePath TEXT,
                                      Description TEXT,
                                      SnapshotID TEXT,
                                      TrackIDs TEXT                      -- 'id1;;id2;;id3'
                                  );

                                  -- Albums table
                                  CREATE TABLE IF NOT EXISTS Albums (
                                      Id TEXT PRIMARY KEY,               -- Spotify album ID
                                      Name TEXT,
                                      ImageURL TEXT,
                                      ImagePath TEXT,
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
                                      ImagePath TEXT,
                                      Genres TEXT                        -- fixed typo: "Generes" → "Genres"
                                  );

                                  -- Similar table
                                  CREATE TABLE IF NOT EXISTS Similar (
                                      SongID TEXT,                       -- Spotify similar ID
                                      SongID2 TEXT,                      -- Secondary similar ID
                                      Type TEXT                          -- Similarity classification
                                  );

                                  -- MightBeSimilar table
                                  CREATE TABLE IF NOT EXISTS MightBeSimilar (
                                      SongID TEXT,                       -- Primary track ID
                                      SongID2 TEXT                       -- Candidate similar track ID
                                  );
                              """;


            cmd.ExecuteNonQuery();

            EnsureColumnExists(conn, "Playlists", "ImagePath");
            EnsureColumnExists(conn, "Albums", "ImagePath");
            EnsureColumnExists(conn, "Artists", "ImagePath");
            EnsureColumnExists(conn, "Similar", "Type");
        }


        /// <summary>
        /// Inserts or updates a setting entry. Settings are lightweight key/value
        /// pairs used to store Spotify credentials and miscellaneous toggles.
        /// </summary>
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

        /// <summary>
        /// Deletes the specified setting key from the Settings table.
        /// </summary>
        public static async Task RemoveSetting(string key)
        {
            await DbWriteLock.WaitAsync();
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM Settings WHERE Key = $key;";
                cmd.Parameters.AddWithValue("$key", key);

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                DbWriteLock.Release();
            }
        }

        /// <summary>
        /// Retrieves a single setting value. Returns <c>null</c> when the key is
        /// not present.
        /// </summary>
        public static string? GetSetting(string key)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Value FROM Settings WHERE Key = $key;";
            cmd.Parameters.AddWithValue("$key", key);
            return cmd.ExecuteScalar() as string;
        }

        /// <summary>
        /// Enumerates every stored setting key/value pair.
        /// </summary>
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

        /// <summary>
        /// Adds or updates a playlist record. All string fields are normalized to
        /// empty strings to avoid <c>null</c> persistence issues.
        /// </summary>
        public static async Task SetPlaylist(Variables.PlayList playlist)
        {
            await DbWriteLock.WaitAsync();
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT OR REPLACE INTO Playlists (Id, Name, ImageURL, ImagePath, Description, SnapshotID, TrackIDs)
                                VALUES ($id, $name, $imageUrl, $imagePath, $description, $snapshotId, $trackIds);";

                cmd.Parameters.AddWithValue("$id", playlist.Id ?? string.Empty);
                cmd.Parameters.AddWithValue("$name", playlist.Name ?? string.Empty);
                cmd.Parameters.AddWithValue("$imageUrl", playlist.ImageURL ?? string.Empty);
                cmd.Parameters.AddWithValue("$imagePath", playlist.ImagePath ?? string.Empty);
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

        /// <summary>
        /// Removes a playlist and its metadata from the cache.
        /// </summary>
        public static async Task RemovePlaylist(string playlistId)
        {
            await DbWriteLock.WaitAsync();
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM Playlists WHERE Id = $id;";
                cmd.Parameters.AddWithValue("$id", playlistId);

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                DbWriteLock.Release();
            }
        }

        /// <summary>
        /// Fetches a playlist by Spotify identifier.
        /// </summary>
        public static Variables.PlayList? GetPlaylist(string id)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, ImageURL, ImagePath, Description, SnapshotID, TrackIDs FROM Playlists WHERE Id = $id LIMIT 1;";
            cmd.Parameters.AddWithValue("$id", id);

            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                return new Variables.PlayList
                {
                    Id = reader["Id"]?.ToString() ?? string.Empty,
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    ImageURL = reader["ImageURL"]?.ToString() ?? string.Empty,
                    ImagePath = reader["ImagePath"]?.ToString() ?? string.Empty,
                    Description = reader["Description"]?.ToString() ?? string.Empty,
                    SnapshotID = reader["SnapshotID"]?.ToString() ?? string.Empty,
                    TrackIDs = reader["TrackIDs"]?.ToString() ?? string.Empty
                };
            }

            return null;
        }

        /// <summary>
        /// Enumerates every playlist stored locally.
        /// </summary>
        public static IEnumerable<Variables.PlayList> GetAllPlaylists()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, ImageURL, ImagePath, Description, SnapshotID, TrackIDs FROM Playlists;";

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                yield return new Variables.PlayList
                {
                    Id = reader["Id"]?.ToString() ?? string.Empty,
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    ImageURL = reader["ImageURL"]?.ToString() ?? string.Empty,
                    ImagePath = reader["ImagePath"]?.ToString() ?? string.Empty,
                    Description = reader["Description"]?.ToString() ?? string.Empty,
                    SnapshotID = reader["SnapshotID"]?.ToString() ?? string.Empty,
                    TrackIDs = reader["TrackIDs"]?.ToString() ?? string.Empty
                };
            }
        }

        /// <summary>
        /// Adds or updates an album record in the Albums table.
        /// </summary>
        public static async Task SetAlbum(Variables.Album album)
        {
            await DbWriteLock.WaitAsync();
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT OR REPLACE INTO Albums (Id, Name, ImageURL, ImagePath, ArtistIDs)
                                VALUES ($id, $name, $imageUrl, $imagePath, $artistIds);";

                cmd.Parameters.AddWithValue("$id", album.Id ?? string.Empty);
                cmd.Parameters.AddWithValue("$name", album.Name ?? string.Empty);
                cmd.Parameters.AddWithValue("$imageUrl", album.ImageURL ?? string.Empty);
                cmd.Parameters.AddWithValue("$imagePath", album.ImagePath ?? string.Empty);
                cmd.Parameters.AddWithValue("$artistIds", album.ArtistIDs ?? string.Empty);

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                DbWriteLock.Release();
            }
        }

        /// <summary>
        /// Removes an album from the cache by identifier.
        /// </summary>
        public static async Task RemoveAlbum(string albumId)
        {
            await DbWriteLock.WaitAsync();
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM Albums WHERE Id = $id;";
                cmd.Parameters.AddWithValue("$id", albumId);

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                DbWriteLock.Release();
            }
        }

        /// <summary>
        /// Retrieves a single album along with its cached track ID list.
        /// </summary>
        public static Variables.Album? GetAlbum(string id)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, ImageURL, ImagePath, ArtistIDs FROM Albums WHERE Id = $id LIMIT 1;";
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
                    ImagePath = reader["ImagePath"]?.ToString() ?? string.Empty,
                    ArtistIDs = reader["ArtistIDs"]?.ToString() ?? string.Empty,
                    TrackIDs = GetAlbumTrackIds(conn, albumId)
                };
            }

            return null;
        }

        /// <summary>
        /// Enumerates all albums, supplementing each row with its track ID list.
        /// </summary>
        public static IEnumerable<Variables.Album> GetAllAlbums()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, ImageURL, ImagePath, ArtistIDs FROM Albums;";

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var albumId = reader["Id"]?.ToString() ?? string.Empty;
                yield return new Variables.Album
                {
                    Id = albumId,
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    ImageURL = reader["ImageURL"]?.ToString() ?? string.Empty,
                    ImagePath = reader["ImagePath"]?.ToString() ?? string.Empty,
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

        /// <summary>
        /// Adds or updates a track entry in the Tracks table.
        /// </summary>
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

        /// <summary>
        /// Removes a track entry by Spotify ID.
        /// </summary>
        public static async Task RemoveTrack(string trackId)
        {
            await DbWriteLock.WaitAsync();
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM Tracks WHERE Id = $id;";
                cmd.Parameters.AddWithValue("$id", trackId);

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                DbWriteLock.Release();
            }
        }

        /// <summary>
        /// Fetches a track by Spotify identifier or internal song identifier.
        /// </summary>
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

        /// <summary>
        /// Counts how many tracks share the supplied internal SongID.
        /// </summary>
        public static int GetTrackCountBySongId(string songId)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Tracks WHERE SongID = $songId;";
            cmd.Parameters.AddWithValue("$songId", songId);

            object? result = cmd.ExecuteScalar();

            return result switch
            {
                long longCount => (int)longCount,
                int intCount => intCount,
                _ => 0
            };
        }

        /// <summary>
        /// Enumerates every track stored in the Tracks table.
        /// </summary>
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

        /// <summary>
        /// Adds or updates an artist record in the Artists table.
        /// </summary>
        public static async Task SetArtist(Variables.Artist artist)
        {
            await DbWriteLock.WaitAsync();
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT OR REPLACE INTO Artists (Id, Name, ImageURL, ImagePath, Genres)
                                VALUES ($id, $name, $imageUrl, $imagePath, $genres);";

                cmd.Parameters.AddWithValue("$id", artist.Id ?? string.Empty);
                cmd.Parameters.AddWithValue("$name", artist.Name ?? string.Empty);
                cmd.Parameters.AddWithValue("$imageUrl", artist.ImageURL ?? string.Empty);
                cmd.Parameters.AddWithValue("$imagePath", artist.ImagePath ?? string.Empty);
                cmd.Parameters.AddWithValue("$genres", artist.Genres ?? string.Empty);

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                DbWriteLock.Release();
            }
        }

        /// <summary>
        /// Removes an artist entry by identifier.
        /// </summary>
        public static async Task RemoveArtist(string artistId)
        {
            await DbWriteLock.WaitAsync();
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM Artists WHERE Id = $id;";
                cmd.Parameters.AddWithValue("$id", artistId);

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                DbWriteLock.Release();
            }
        }

        /// <summary>
        /// Retrieves a specific artist record when present.
        /// </summary>
        public static Variables.Artist? GetArtist(string id)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, ImageURL, ImagePath, Genres FROM Artists WHERE Id = $id LIMIT 1;";
            cmd.Parameters.AddWithValue("$id", id);

            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                return new Variables.Artist
                {
                    Id = reader["Id"]?.ToString() ?? string.Empty,
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    ImageURL = reader["ImageURL"]?.ToString() ?? string.Empty,
                    ImagePath = reader["ImagePath"]?.ToString() ?? string.Empty,
                    Genres = reader["Genres"]?.ToString() ?? string.Empty
                };
            }

            return null;
        }

        /// <summary>
        /// Enumerates all cached artist records.
        /// </summary>
        public static IEnumerable<Variables.Artist> GetAllArtists()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, ImageURL, ImagePath, Genres FROM Artists;";

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                yield return new Variables.Artist
                {
                    Id = reader["Id"]?.ToString() ?? string.Empty,
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    ImageURL = reader["ImageURL"]?.ToString() ?? string.Empty,
                    ImagePath = reader["ImagePath"]?.ToString() ?? string.Empty,
                    Genres = reader["Genres"]?.ToString() ?? string.Empty
                };
            }
        }

        private static void EnsureColumnExists(SqliteConnection connection, string tableName, string columnName)
        {
            using var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = $"PRAGMA table_info({tableName});";

            using var reader = checkCmd.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader["name"]?.ToString(), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            try
            {
                using var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} TEXT;";
                alterCmd.ExecuteNonQuery();
            }
            catch (SqliteException ex)
            {
                Console.Error.WriteLine($"Unable to add column '{columnName}' to '{tableName}': {ex}");
            }
        }

        /// <summary>
        /// Records that two tracks are similar according to a specific strategy.
        /// </summary>
        public static async Task SetSimilar(string songId, string songId2, string type)
        {
            await DbWriteLock.WaitAsync();
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT OR REPLACE INTO Similar (SongID, SongID2, Type) VALUES ($songId, $songId2, $type);";

                cmd.Parameters.AddWithValue("$songId", songId);
                cmd.Parameters.AddWithValue("$songId2", songId2);
                cmd.Parameters.AddWithValue("$type", type);

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                DbWriteLock.Release();
            }
        }

        /// <summary>
        /// Deletes a previously recorded similarity relationship.
        /// </summary>
        public static async Task RemoveSimilar(string songId, string songId2, string type)
        {
            await DbWriteLock.WaitAsync();
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM Similar WHERE SongID = $songId AND SongID2 = $songId2 AND Type = $type;";
                cmd.Parameters.AddWithValue("$songId", songId);
                cmd.Parameters.AddWithValue("$songId2", songId2);
                cmd.Parameters.AddWithValue("$type", type);

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                DbWriteLock.Release();
            }
        }

        /// <summary>
        /// Retrieves a similarity relationship between two song IDs when stored.
        /// </summary>
        public static (string SongId, string SongId2, string Type)? GetSimilar(string songId, string songId2)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT SongID, SongID2, Type FROM Similar WHERE SongID = $songId AND SongID2 = $songId2 LIMIT 1;";
            cmd.Parameters.AddWithValue("$songId", songId);
            cmd.Parameters.AddWithValue("$songId2", songId2);

            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                return (
                    reader["SongID"]?.ToString() ?? string.Empty,
                    reader["SongID2"]?.ToString() ?? string.Empty,
                    reader["Type"]?.ToString() ?? string.Empty
                );
            }

            return null;
        }

        /// <summary>
        /// Enumerates all similarity relationships recorded in the database.
        /// </summary>
        public static IEnumerable<(string SongId, string SongId2, string Type)> GetAllSimilar()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT SongID, SongID2, Type FROM Similar;";

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                yield return (
                    reader["SongID"]?.ToString() ?? string.Empty,
                    reader["SongID2"]?.ToString() ?? string.Empty,
                    reader["Type"]?.ToString() ?? string.Empty
                );
            }
        }

        /// <summary>
        /// Marks two tracks as potential matches pending manual review.
        /// </summary>
        public static async Task SetMightBeSimilar(string songId, string songId2)
        {
            await DbWriteLock.WaitAsync();
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT OR REPLACE INTO MightBeSimilar (SongID, SongID2) VALUES ($songId, $songId2);";

                cmd.Parameters.AddWithValue("$songId", songId);
                cmd.Parameters.AddWithValue("$songId2", songId2);

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                DbWriteLock.Release();
            }
        }

        /// <summary>
        /// Removes a potential match entry once it has been resolved.
        /// </summary>
        public static async Task RemoveMightBeSimilar(string songId, string songId2)
        {
            await DbWriteLock.WaitAsync();
            try
            {
                using var conn = new SqliteConnection($"Data Source={_dbPath}");
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM MightBeSimilar WHERE SongID = $songId AND SongID2 = $songId2;";
                cmd.Parameters.AddWithValue("$songId", songId);
                cmd.Parameters.AddWithValue("$songId2", songId2);

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                DbWriteLock.Release();
            }
        }

        /// <summary>
        /// Retrieves a potential match pair when it exists.
        /// </summary>
        public static (string SongId, string SongId2)? GetMightBeSimilar(string songId, string songId2)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT SongID, SongID2 FROM MightBeSimilar WHERE SongID = $songId AND SongID2 = $songId2 LIMIT 1;";
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

        /// <summary>
        /// Enumerates all potential match pairs still awaiting user confirmation.
        /// </summary>
        public static IEnumerable<(string SongId, string SongId2)> GetAllMightBeSimilar()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT SongID, SongID2 FROM MightBeSimilar;";

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