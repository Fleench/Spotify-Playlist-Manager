#include "DatabaseWorker.hpp"
#include "Helpers.hpp"
#include <sqlite3.h>
#include <iostream>

namespace SpotifyPlaylistManager {

std::mutex DatabaseWorker::dbMutex;

void DatabaseWorker::Init() {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    if (sqlite3_open(Variables::DatabasePath.c_str(), &db) != SQLITE_OK) {
        if (db) {
            std::cerr << "Can't open database: " << sqlite3_errmsg(db) << std::endl;
            sqlite3_close(db);
        } else {
            std::cerr << "Can't allocate database memory" << std::endl;
        }
        return;
    }

    const char* sql = 
        "PRAGMA journal_mode=WAL;"
        "CREATE TABLE IF NOT EXISTS Settings (Key TEXT PRIMARY KEY, Value TEXT NOT NULL);"
        "CREATE TABLE IF NOT EXISTS Playlists (Id TEXT PRIMARY KEY, Name TEXT, ImageURL TEXT, ImagePath TEXT, Description TEXT, SnapshotID TEXT, TrackIDs TEXT);"
        "CREATE TABLE IF NOT EXISTS Albums (Id TEXT PRIMARY KEY, Name TEXT, ImageURL TEXT, ImagePath TEXT, ArtistIDs TEXT, TrackIDs TEXT);"
        "CREATE TABLE IF NOT EXISTS Tracks (Id TEXT PRIMARY KEY, SongID TEXT, Name TEXT, AlbumId TEXT, ArtistIds TEXT, DiscNumber INTEGER, DurationMs INTEGER, Explicit INTEGER, PreviewUrl TEXT, TrackNumber INTEGER);"
        "CREATE TABLE IF NOT EXISTS Artists (Id TEXT PRIMARY KEY, Name TEXT, ImageURL TEXT, ImagePath TEXT, Genres TEXT);"
        "CREATE TABLE IF NOT EXISTS Similar (SongID TEXT, SongID2 TEXT, Type TEXT);"
        "CREATE TABLE IF NOT EXISTS MightBeSimilar (SongID TEXT, SongID2 TEXT);";

    char* errMsg = nullptr;
    if (sqlite3_exec(db, sql, nullptr, nullptr, &errMsg) != SQLITE_OK) {
        std::cerr << "SQL error: " << errMsg << std::endl;
        sqlite3_free(errMsg);
    }

    sqlite3_close(db);
}

// Settings
void DatabaseWorker::SetSetting(const std::string& key, const std::string& value) {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "INSERT OR REPLACE INTO Settings (Key, Value) VALUES (?, ?);";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    sqlite3_bind_text(stmt, 1, key.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 2, value.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_step(stmt);
    sqlite3_finalize(stmt);
    sqlite3_close(db);
}

std::optional<std::string> DatabaseWorker::GetSetting(const std::string& key) {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "SELECT Value FROM Settings WHERE Key = ?;";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    sqlite3_bind_text(stmt, 1, key.c_str(), -1, SQLITE_TRANSIENT);
    std::optional<std::string> result;
    if (sqlite3_step(stmt) == SQLITE_ROW) {
        const char* val = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 0));
        if (val) result = val;
    }
    sqlite3_finalize(stmt);
    sqlite3_close(db);
    return result;
}

void DatabaseWorker::RemoveSetting(const std::string& key) {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "DELETE FROM Settings WHERE Key = ?;";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    sqlite3_bind_text(stmt, 1, key.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_step(stmt);
    sqlite3_finalize(stmt);
    sqlite3_close(db);
}

std::vector<std::pair<std::string, std::string>> DatabaseWorker::GetAllSettings() {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "SELECT Key, Value FROM Settings;";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    std::vector<std::pair<std::string, std::string>> results;
    while (sqlite3_step(stmt) == SQLITE_ROW) {
        const char* k = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 0));
        const char* v = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 1));
        if (k && v) results.push_back({k, v});
    }
    sqlite3_finalize(stmt);
    sqlite3_close(db);
    return results;
}

// Playlists
void DatabaseWorker::SetPlaylist(const Variables::PlayList& playlist) {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "INSERT OR REPLACE INTO Playlists (Id, Name, ImageURL, ImagePath, Description, SnapshotID, TrackIDs) VALUES (?, ?, ?, ?, ?, ?, ?);";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    sqlite3_bind_text(stmt, 1, playlist.Id.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 2, playlist.Name.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 3, playlist.ImageURL.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 4, playlist.ImagePath.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 5, playlist.Description.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 6, playlist.SnapshotID.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 7, playlist.TrackIDs.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_step(stmt);
    sqlite3_finalize(stmt);
    sqlite3_close(db);
}

std::optional<Variables::PlayList> DatabaseWorker::GetPlaylist(const std::string& id) {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "SELECT Id, Name, ImageURL, ImagePath, Description, SnapshotID, TrackIDs FROM Playlists WHERE Id = ?;";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    sqlite3_bind_text(stmt, 1, id.c_str(), -1, SQLITE_TRANSIENT);
    std::optional<Variables::PlayList> result;
    if (sqlite3_step(stmt) == SQLITE_ROW) {
        Variables::PlayList obj;
        const char* c0 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 0)); if (c0) obj.Id = c0;
        const char* c1 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 1)); if (c1) obj.Name = c1;
        const char* c2 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 2)); if (c2) obj.ImageURL = c2;
        const char* c3 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 3)); if (c3) obj.ImagePath = c3;
        const char* c4 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 4)); if (c4) obj.Description = c4;
        const char* c5 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 5)); if (c5) obj.SnapshotID = c5;
        const char* c6 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 6)); if (c6) obj.TrackIDs = c6;
        result = obj;
    }
    sqlite3_finalize(stmt);
    sqlite3_close(db);
    return result;
}

void DatabaseWorker::RemovePlaylist(const std::string& id) {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "DELETE FROM Playlists WHERE Id = ?;";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    sqlite3_bind_text(stmt, 1, id.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_step(stmt);
    sqlite3_finalize(stmt);
    sqlite3_close(db);
}

std::vector<Variables::PlayList> DatabaseWorker::GetAllPlaylists() {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "SELECT Id, Name, ImageURL, ImagePath, Description, SnapshotID, TrackIDs FROM Playlists;";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    std::vector<Variables::PlayList> results;
    while (sqlite3_step(stmt) == SQLITE_ROW) {
        Variables::PlayList obj;
        const char* c0 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 0)); if (c0) obj.Id = c0;
        const char* c1 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 1)); if (c1) obj.Name = c1;
        const char* c2 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 2)); if (c2) obj.ImageURL = c2;
        const char* c3 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 3)); if (c3) obj.ImagePath = c3;
        const char* c4 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 4)); if (c4) obj.Description = c4;
        const char* c5 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 5)); if (c5) obj.SnapshotID = c5;
        const char* c6 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 6)); if (c6) obj.TrackIDs = c6;
        results.push_back(obj);
    }
    sqlite3_finalize(stmt);
    sqlite3_close(db);
    return results;
}

// Albums
void DatabaseWorker::SetAlbum(const Variables::Album& album) {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "INSERT OR REPLACE INTO Albums (Id, Name, ImageURL, ImagePath, ArtistIDs, TrackIDs) VALUES (?, ?, ?, ?, ?, ?);";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    sqlite3_bind_text(stmt, 1, album.Id.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 2, album.Name.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 3, album.ImageURL.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 4, album.ImagePath.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 5, album.ArtistIDs.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 6, album.TrackIDs.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_step(stmt);
    sqlite3_finalize(stmt);
    sqlite3_close(db);
}

std::optional<Variables::Album> DatabaseWorker::GetAlbum(const std::string& id) {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "SELECT Id, Name, ImageURL, ImagePath, ArtistIDs, TrackIDs FROM Albums WHERE Id = ?;";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    sqlite3_bind_text(stmt, 1, id.c_str(), -1, SQLITE_TRANSIENT);
    std::optional<Variables::Album> result;
    if (sqlite3_step(stmt) == SQLITE_ROW) {
        Variables::Album obj;
        const char* c0 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 0)); if (c0) obj.Id = c0;
        const char* c1 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 1)); if (c1) obj.Name = c1;
        const char* c2 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 2)); if (c2) obj.ImageURL = c2;
        const char* c3 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 3)); if (c3) obj.ImagePath = c3;
        const char* c4 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 4)); if (c4) obj.ArtistIDs = c4;
        const char* c5 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 5)); if (c5) obj.TrackIDs = c5;
        result = obj;
    }
    sqlite3_finalize(stmt);
    sqlite3_close(db);
    return result;
}

void DatabaseWorker::RemoveAlbum(const std::string& id) {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "DELETE FROM Albums WHERE Id = ?;";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    sqlite3_bind_text(stmt, 1, id.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_step(stmt);
    sqlite3_finalize(stmt);
    sqlite3_close(db);
}

std::vector<Variables::Album> DatabaseWorker::GetAllAlbums() {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "SELECT Id, Name, ImageURL, ImagePath, ArtistIDs, TrackIDs FROM Albums;";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    std::vector<Variables::Album> results;
    while (sqlite3_step(stmt) == SQLITE_ROW) {
        Variables::Album obj;
        const char* c0 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 0)); if (c0) obj.Id = c0;
        const char* c1 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 1)); if (c1) obj.Name = c1;
        const char* c2 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 2)); if (c2) obj.ImageURL = c2;
        const char* c3 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 3)); if (c3) obj.ImagePath = c3;
        const char* c4 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 4)); if (c4) obj.ArtistIDs = c4;
        const char* c5 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 5)); if (c5) obj.TrackIDs = c5;
        results.push_back(obj);
    }
    sqlite3_finalize(stmt);
    sqlite3_close(db);
    return results;
}

// Tracks
void DatabaseWorker::SetTrack(const Variables::Track& track) {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "INSERT OR REPLACE INTO Tracks (Id, SongID, Name, AlbumId, ArtistIds, DiscNumber, DurationMs, Explicit, PreviewUrl, TrackNumber) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?);";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    sqlite3_bind_text(stmt, 1, track.Id.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 2, track.SongID.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 3, track.Name.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 4, track.AlbumId.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 5, track.ArtistIds.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_int(stmt, 6, track.DiscNumber);
    sqlite3_bind_int(stmt, 7, track.DurationMs);
    sqlite3_bind_int(stmt, 8, track.Explicit ? 1 : 0);
    sqlite3_bind_text(stmt, 9, track.PreviewUrl.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_int(stmt, 10, track.TrackNumber);
    sqlite3_step(stmt);
    sqlite3_finalize(stmt);
    sqlite3_close(db);
}

void DatabaseWorker::EnsureTrackExists(const std::string& id) {
    if (id.empty()) {
        return;
    }

    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "INSERT OR IGNORE INTO Tracks (Id, SongID) VALUES (?, ?);";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    sqlite3_bind_text(stmt, 1, id.c_str(), -1, SQLITE_TRANSIENT);
    std::string songId = Variables::MakeId();
    sqlite3_bind_text(stmt, 2, songId.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_step(stmt);
    sqlite3_finalize(stmt);
    sqlite3_close(db);
}

std::optional<Variables::Track> DatabaseWorker::GetTrack(const std::string& id) {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "SELECT Id, SongID, Name, AlbumId, ArtistIds, DiscNumber, DurationMs, Explicit, PreviewUrl, TrackNumber FROM Tracks WHERE Id = ?;";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    sqlite3_bind_text(stmt, 1, id.c_str(), -1, SQLITE_TRANSIENT);
    std::optional<Variables::Track> result;
    if (sqlite3_step(stmt) == SQLITE_ROW) {
        Variables::Track obj;
        const char* c0 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 0)); if (c0) obj.Id = c0;
        const char* c1 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 1)); if (c1) obj.SongID = c1;
        const char* c2 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 2)); if (c2) obj.Name = c2;
        const char* c3 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 3)); if (c3) obj.AlbumId = c3;
        const char* c4 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 4)); if (c4) obj.ArtistIds = c4;
        obj.DiscNumber = sqlite3_column_int(stmt, 5);
        obj.DurationMs = sqlite3_column_int(stmt, 6);
        obj.Explicit = sqlite3_column_int(stmt, 7) == 1;
        const char* c8 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 8)); if (c8) obj.PreviewUrl = c8;
        obj.TrackNumber = sqlite3_column_int(stmt, 9);
        result = obj;
    }
    sqlite3_finalize(stmt);
    sqlite3_close(db);
    return result;
}

void DatabaseWorker::RemoveTrack(const std::string& id) {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "DELETE FROM Tracks WHERE Id = ?;";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    sqlite3_bind_text(stmt, 1, id.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_step(stmt);
    sqlite3_finalize(stmt);
    sqlite3_close(db);
}

std::vector<Variables::Track> DatabaseWorker::GetAllTracks() {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "SELECT Id, SongID, Name, AlbumId, ArtistIds, DiscNumber, DurationMs, Explicit, PreviewUrl, TrackNumber FROM Tracks;";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    std::vector<Variables::Track> results;
    while (sqlite3_step(stmt) == SQLITE_ROW) {
        Variables::Track obj;
        const char* c0 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 0)); if (c0) obj.Id = c0;
        const char* c1 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 1)); if (c1) obj.SongID = c1;
        const char* c2 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 2)); if (c2) obj.Name = c2;
        const char* c3 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 3)); if (c3) obj.AlbumId = c3;
        const char* c4 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 4)); if (c4) obj.ArtistIds = c4;
        obj.DiscNumber = sqlite3_column_int(stmt, 5);
        obj.DurationMs = sqlite3_column_int(stmt, 6);
        obj.Explicit = sqlite3_column_int(stmt, 7) == 1;
        const char* c8 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 8)); if (c8) obj.PreviewUrl = c8;
        obj.TrackNumber = sqlite3_column_int(stmt, 9);
        results.push_back(obj);
    }
    sqlite3_finalize(stmt);
    sqlite3_close(db);
    return results;
}

int DatabaseWorker::GetTrackCountBySongId(const std::string& songId) {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "SELECT COUNT(*) FROM Tracks WHERE SongID = ?;";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    sqlite3_bind_text(stmt, 1, songId.c_str(), -1, SQLITE_TRANSIENT);
    int count = 0;
    if (sqlite3_step(stmt) == SQLITE_ROW) {
        count = sqlite3_column_int(stmt, 0);
    }
    sqlite3_finalize(stmt);
    sqlite3_close(db);
    return count;
}

// Artists
void DatabaseWorker::SetArtist(const Variables::Artist& artist) {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "INSERT OR REPLACE INTO Artists (Id, Name, ImageURL, ImagePath, Genres) VALUES (?, ?, ?, ?, ?);";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    sqlite3_bind_text(stmt, 1, artist.Id.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 2, artist.Name.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 3, artist.ImageURL.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 4, artist.ImagePath.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 5, artist.Genres.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_step(stmt);
    sqlite3_finalize(stmt);
    sqlite3_close(db);
}

std::optional<Variables::Artist> DatabaseWorker::GetArtist(const std::string& id) {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "SELECT Id, Name, ImageURL, ImagePath, Genres FROM Artists WHERE Id = ?;";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    sqlite3_bind_text(stmt, 1, id.c_str(), -1, SQLITE_TRANSIENT);
    std::optional<Variables::Artist> result;
    if (sqlite3_step(stmt) == SQLITE_ROW) {
        Variables::Artist obj;
        const char* c0 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 0)); if (c0) obj.Id = c0;
        const char* c1 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 1)); if (c1) obj.Name = c1;
        const char* c2 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 2)); if (c2) obj.ImageURL = c2;
        const char* c3 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 3)); if (c3) obj.ImagePath = c3;
        const char* c4 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 4)); if (c4) obj.Genres = c4;
        result = obj;
    }
    sqlite3_finalize(stmt);
    sqlite3_close(db);
    return result;
}

void DatabaseWorker::RemoveArtist(const std::string& id) {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "DELETE FROM Artists WHERE Id = ?;";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    sqlite3_bind_text(stmt, 1, id.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_step(stmt);
    sqlite3_finalize(stmt);
    sqlite3_close(db);
}

std::vector<Variables::Artist> DatabaseWorker::GetAllArtists() {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "SELECT Id, Name, ImageURL, ImagePath, Genres FROM Artists;";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    std::vector<Variables::Artist> results;
    while (sqlite3_step(stmt) == SQLITE_ROW) {
        Variables::Artist obj;
        const char* c0 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 0)); if (c0) obj.Id = c0;
        const char* c1 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 1)); if (c1) obj.Name = c1;
        const char* c2 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 2)); if (c2) obj.ImageURL = c2;
        const char* c3 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 3)); if (c3) obj.ImagePath = c3;
        const char* c4 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 4)); if (c4) obj.Genres = c4;
        results.push_back(obj);
    }
    sqlite3_finalize(stmt);
    sqlite3_close(db);
    return results;
}

// Similar
void DatabaseWorker::SetSimilar(const std::string& songId1, const std::string& songId2, const std::string& type) {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "INSERT OR REPLACE INTO Similar (SongID, SongID2, Type) VALUES (?, ?, ?);";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    sqlite3_bind_text(stmt, 1, songId1.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 2, songId2.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 3, type.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_step(stmt);
    sqlite3_finalize(stmt);
    sqlite3_close(db);
}

void DatabaseWorker::RemoveSimilar(const std::string& songId1, const std::string& songId2, const std::string& type) {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "DELETE FROM Similar WHERE SongID = ? AND SongID2 = ? AND Type = ?;";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    sqlite3_bind_text(stmt, 1, songId1.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 2, songId2.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 3, type.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_step(stmt);
    sqlite3_finalize(stmt);
    sqlite3_close(db);
}

std::vector<std::tuple<std::string, std::string, std::string>> DatabaseWorker::GetAllSimilar() {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "SELECT SongID, SongID2, Type FROM Similar;";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    std::vector<std::tuple<std::string, std::string, std::string>> results;
    while (sqlite3_step(stmt) == SQLITE_ROW) {
        const char* c0 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 0));
        const char* c1 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 1));
        const char* c2 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 2));
        if (c0 && c1 && c2) results.push_back({c0, c1, c2});
    }
    sqlite3_finalize(stmt);
    sqlite3_close(db);
    return results;
}

// MightBeSimilar
void DatabaseWorker::SetMightBeSimilar(const std::string& songId1, const std::string& songId2) {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "INSERT OR REPLACE INTO MightBeSimilar (SongID, SongID2) VALUES (?, ?);";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    sqlite3_bind_text(stmt, 1, songId1.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 2, songId2.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_step(stmt);
    sqlite3_finalize(stmt);
    sqlite3_close(db);
}

void DatabaseWorker::RemoveMightBeSimilar(const std::string& songId1, const std::string& songId2) {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "DELETE FROM MightBeSimilar WHERE SongID = ? AND SongID2 = ?;";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    sqlite3_bind_text(stmt, 1, songId1.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 2, songId2.c_str(), -1, SQLITE_TRANSIENT);
    sqlite3_step(stmt);
    sqlite3_finalize(stmt);
    sqlite3_close(db);
}

std::vector<std::pair<std::string, std::string>> DatabaseWorker::GetAllMightBeSimilar() {
    std::lock_guard<std::mutex> lock(dbMutex);
    sqlite3* db;
    sqlite3_open(Variables::DatabasePath.c_str(), &db);
    sqlite3_stmt* stmt;
    const char* sql = "SELECT SongID, SongID2 FROM MightBeSimilar;";
    sqlite3_prepare_v2(db, sql, -1, &stmt, nullptr);
    std::vector<std::pair<std::string, std::string>> results;
    while (sqlite3_step(stmt) == SQLITE_ROW) {
        const char* c0 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 0));
        const char* c1 = reinterpret_cast<const char*>(sqlite3_column_text(stmt, 1));
        if (c0 && c1) results.push_back({c0, c1});
    }
    sqlite3_finalize(stmt);
    sqlite3_close(db);
    return results;
}

} // namespace SpotifyPlaylistManager
